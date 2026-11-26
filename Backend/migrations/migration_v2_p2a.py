"""
NeuroCompanion - Phase 2A Migration v2 (Revision 6 Specification)
Migration Script: migrations/migration_v2_p2a.py

Transforms the legacy Phase 2A schema into the three-layer architecture:
- Canonical 'patients' master registry (174 distinct historical patients).
- 'session_records' table with deterministic UUIDv5 collision-resistant IDs for 150 unique (session, patient) contexts.
- Rebuilt 'interaction_events' with composite foreign key (session_id, patient_id) referencing session_records(session_id, patient_id).
- Preserves all 256 patient support profiles, 368 interaction events, 54 caregiver overrides, 18 interaction profiles, and 14 support recommendations.
- Isolated Layer 2B 'behavioral_interpretations' table.
- Extended Layer 3 assistance recommendations tracking granular interventions.
"""

import hashlib
import json
import os
import sqlite3
import uuid
from datetime import datetime, timezone
from typing import Dict, List, Optional, Tuple

# Deterministic namespace for NeuroCompanion session UUIDv5 generation
NEUROCOMPANION_SESSION_NAMESPACE = uuid.uuid5(uuid.NAMESPACE_URL, "https://neurocompanion.org/namespaces/sessions")


def get_canonical_session_uuid(old_session_id: str, patient_id: str) -> str:
    """
    Produces a deterministic 128-bit UUIDv5 identifier using unambiguous
    length-prefixed UTF-8 byte encoding of (old_session_id, patient_id).
    """
    old_s_bytes = old_session_id.encode("utf-8")
    p_bytes = patient_id.encode("utf-8")
    canonical_repr = f"len({len(old_s_bytes)}):{old_session_id}/len({len(p_bytes)}):{patient_id}"
    return str(uuid.uuid5(NEUROCOMPANION_SESSION_NAMESPACE, canonical_repr))


def run_migration(db_path: str, log_callback=print) -> Dict[str, any]:
    """
    Executes the controlled migration against the target SQLite database path.
    Preconditions:
    - Target database must be a separate disposable copy or validated target.
    - Foreign keys are toggled appropriately outside of transactions.
    """
    if not os.path.exists(db_path):
        raise FileNotFoundError(f"Target database not found: {db_path}")

    log_callback(f"Starting migration on: {db_path}")
    conn = sqlite3.connect(db_path)
    conn.row_factory = sqlite3.Row
    c = conn.cursor()

    # Pre-migration manifest collection
    c.execute("PRAGMA journal_mode")
    j_mode = c.fetchone()[0]
    log_callback(f"Journal mode: {j_mode}")

    # Step 1: Pre-migration data extraction & verification
    c.execute("SELECT COUNT(*) FROM patient_support_profiles")
    count_profiles_pre = c.fetchone()[0]
    c.execute("SELECT COUNT(*) FROM interaction_events")
    count_events_pre = c.fetchone()[0]
    c.execute("SELECT COUNT(*) FROM caregiver_overrides")
    count_overrides_pre = c.fetchone()[0]
    c.execute("SELECT COUNT(*) FROM interaction_profiles")
    count_int_profiles_pre = c.fetchone()[0]
    c.execute("SELECT COUNT(*) FROM support_recommendations")
    count_recommendations_pre = c.fetchone()[0]

    log_callback(f"Pre-migration row counts: profiles={count_profiles_pre}, events={count_events_pre}, overrides={count_overrides_pre}, int_profiles={count_int_profiles_pre}, recommendations={count_recommendations_pre}")

    # Build canonical master patients union
    # We query all 5 tables to identify every distinct patient_id and truthful historical first_seen_at
    patient_registry: Dict[str, Tuple[Optional[str], str]] = {}

    # 1. patient_support_profiles
    c.execute("SELECT patient_id, MIN(created_at) FROM patient_support_profiles WHERE patient_id IS NOT NULL AND TRIM(patient_id) != '' GROUP BY patient_id")
    for pid, ts in c.fetchall():
        clean_ts = ts.strip() if ts and ts.strip() else None
        patient_registry[pid] = (clean_ts, "onboarding_profile")

    # 2. interaction_events
    c.execute("SELECT patient_id, MIN(timestamp_utc) FROM interaction_events WHERE patient_id IS NOT NULL AND TRIM(patient_id) != '' GROUP BY patient_id")
    for pid, ts in c.fetchall():
        clean_ts = ts.strip() if ts and ts.strip() else None
        if pid not in patient_registry:
            patient_registry[pid] = (clean_ts, "interaction_event_telemetry")
        else:
            prev_ts, prev_src = patient_registry[pid]
            if clean_ts and (not prev_ts or clean_ts < prev_ts):
                patient_registry[pid] = (clean_ts, prev_src)

    # 3. caregiver_overrides
    c.execute("SELECT patient_id, MIN(timestamp_utc) FROM caregiver_overrides WHERE patient_id IS NOT NULL AND TRIM(patient_id) != '' GROUP BY patient_id")
    for pid, ts in c.fetchall():
        clean_ts = ts.strip() if ts and ts.strip() else None
        if pid not in patient_registry:
            patient_registry[pid] = (clean_ts, "caregiver_override")
        else:
            prev_ts, prev_src = patient_registry[pid]
            if clean_ts and (not prev_ts or clean_ts < prev_ts):
                patient_registry[pid] = (clean_ts, prev_src)

    # 4. interaction_profiles
    c.execute("SELECT patient_id, MIN(last_updated) FROM interaction_profiles WHERE patient_id IS NOT NULL AND TRIM(patient_id) != '' GROUP BY patient_id")
    for pid, ts in c.fetchall():
        clean_ts = ts.strip() if ts and ts.strip() else None
        if pid not in patient_registry:
            patient_registry[pid] = (clean_ts, "historical_migration_unknown")
        else:
            prev_ts, prev_src = patient_registry[pid]
            if clean_ts and (not prev_ts or clean_ts < prev_ts):
                patient_registry[pid] = (clean_ts, prev_src)

    # 5. support_recommendations
    c.execute("SELECT patient_id, MIN(timestamp_utc) FROM support_recommendations WHERE patient_id IS NOT NULL AND TRIM(patient_id) != '' GROUP BY patient_id")
    for pid, ts in c.fetchall():
        clean_ts = ts.strip() if ts and ts.strip() else None
        if pid not in patient_registry:
            patient_registry[pid] = (clean_ts, "historical_migration_unknown")
        else:
            prev_ts, prev_src = patient_registry[pid]
            if clean_ts and (not prev_ts or clean_ts < prev_ts):
                patient_registry[pid] = (clean_ts, prev_src)

    distinct_patient_count = len(patient_registry)
    log_callback(f"Master patients registry compiled: {distinct_patient_count} distinct patients")

    # Build historical session mapping
    c.execute("""
        SELECT session_id, patient_id, MIN(timestamp_utc) as started_at, MAX(timestamp_utc) as ended_at, COUNT(*) as ev_count
        FROM interaction_events
        GROUP BY session_id, patient_id
        ORDER BY session_id, patient_id
    """)
    session_patient_pairs = c.fetchall()
    log_callback(f"Distinct historical (session_id, patient_id) combinations: {len(session_patient_pairs)}")

    # Check mapping collision resistance
    session_mapping = {}
    used_migrated_ids = set()
    for row in session_patient_pairs:
        old_sid = row["session_id"]
        pid = row["patient_id"]
        migrated_id = get_canonical_session_uuid(old_sid, pid)
        if migrated_id in used_migrated_ids:
            raise ValueError(f"CRITICAL: Collision detected for UUIDv5 migrated_session_id {migrated_id}")
        used_migrated_ids.add(migrated_id)
        session_mapping[(old_sid, pid)] = {
            "migrated_session_id": migrated_id,
            "started_at": row["started_at"],
            "ended_at": row["ended_at"],
            "event_count": row["ev_count"]
        }

    migration_timestamp = datetime.now(timezone.utc).isoformat()

    # Step 2: Atomic Transactional Execution
    # Foreign keys must be OFF while tables are being dropped and recreated
    c.execute("PRAGMA foreign_keys = OFF;")
    c.execute("BEGIN IMMEDIATE TRANSACTION;")

    try:
        # A. Create patients table
        c.execute("""
            CREATE TABLE patients (
                patient_id TEXT PRIMARY KEY,
                first_seen_at TEXT,
                registration_source TEXT NOT NULL CHECK (
                    registration_source IN (
                        'onboarding_profile',
                        'interaction_event_telemetry',
                        'caregiver_override',
                        'historical_migration_unknown'
                    )
                ),
                migration_recorded_at TEXT NOT NULL
            );
        """)
        for pid, (first_seen, reg_source) in patient_registry.items():
            final_src = reg_source if first_seen is not None else "historical_migration_unknown"
            c.execute(
                "INSERT INTO patients (patient_id, first_seen_at, registration_source, migration_recorded_at) VALUES (?, ?, ?, ?)",
                (pid, first_seen, final_src, migration_timestamp)
            )

        # B. Create session_records table with UNIQUE(session_id, patient_id)
        c.execute("""
            CREATE TABLE session_records (
                session_id TEXT PRIMARY KEY,
                patient_id TEXT NOT NULL,
                legacy_session_id TEXT,
                started_at TEXT NOT NULL,
                ended_at TEXT,
                client_version TEXT NOT NULL,
                environmental_context_json TEXT NOT NULL DEFAULT '{}',
                lifecycle_status TEXT NOT NULL DEFAULT 'completed' CHECK (
                    lifecycle_status IN ('active', 'paused', 'completed', 'recovered_after_crash', 'stale_closed')
                ),
                completion_reason TEXT,
                FOREIGN KEY (patient_id) REFERENCES patients(patient_id) ON DELETE CASCADE,
                UNIQUE (session_id, patient_id)
            );
        """)
        c.execute("CREATE INDEX idx_sessions_patient ON session_records(patient_id, started_at DESC);")

        # C. Create session mapping audit table
        c.execute("""
            CREATE TABLE _session_migration_map (
                mapping_id INTEGER PRIMARY KEY AUTOINCREMENT,
                old_session_id TEXT NOT NULL,
                patient_id TEXT NOT NULL,
                migrated_session_id TEXT NOT NULL UNIQUE,
                event_count INTEGER NOT NULL,
                first_event_at TEXT NOT NULL,
                last_event_at TEXT NOT NULL,
                created_at TEXT NOT NULL,
                UNIQUE(old_session_id, patient_id)
            );
        """)
        for (old_sid, pid), meta in session_mapping.items():
            # Insert session record
            c.execute("""
                INSERT INTO session_records (
                    session_id, patient_id, legacy_session_id, started_at, ended_at,
                    client_version, environmental_context_json, lifecycle_status, completion_reason
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
            """, (
                meta["migrated_session_id"], pid, old_sid, meta["started_at"], meta["ended_at"],
                "historical_backfill_rev6", "{}", "completed", "historical_migration"
            ))

            # Insert mapping audit row
            c.execute("""
                INSERT INTO _session_migration_map (
                    old_session_id, patient_id, migrated_session_id, event_count, first_event_at, last_event_at, created_at
                ) VALUES (?, ?, ?, ?, ?, ?, ?)
            """, (
                old_sid, pid, meta["migrated_session_id"], meta["event_count"], meta["started_at"], meta["ended_at"], migration_timestamp
            ))

        # D. Rebuild interaction_events with composite foreign key & deterministic sequence numbers
        c.execute("""
            CREATE TABLE interaction_events_new (
                event_id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                patient_id TEXT NOT NULL,
                sequence_number INTEGER NOT NULL,
                timestamp_utc TEXT NOT NULL,
                therapy_area TEXT NOT NULL,
                activity_id TEXT NOT NULL,
                event_type TEXT NOT NULL,
                payload_json TEXT NOT NULL,
                metadata_json TEXT NOT NULL DEFAULT '{}',
                ingested_at TEXT NOT NULL,
                FOREIGN KEY (session_id, patient_id) REFERENCES session_records(session_id, patient_id) ON DELETE CASCADE,
                FOREIGN KEY (patient_id) REFERENCES patients(patient_id) ON DELETE CASCADE,
                UNIQUE (session_id, sequence_number)
            );
        """)

        # Fetch existing events and remap session_id to migrated_session_id
        # Sequence numbers are calculated chronologically per (old_session_id, patient_id) context
        # Deterministic tie-breaker: timestamp_utc ASC, event_id ASC
        c.execute("""
            SELECT event_id, session_id, patient_id, timestamp_utc, therapy_area, activity_id, event_type, payload_json, metadata_json, ingested_at
            FROM interaction_events
            ORDER BY session_id, patient_id, timestamp_utc ASC, event_id ASC
        """)
        raw_events = c.fetchall()

        curr_seq_map = {}
        for ev in raw_events:
            pair = (ev["session_id"], ev["patient_id"])
            migrated_sid = session_mapping[pair]["migrated_session_id"]
            curr_seq_map[migrated_sid] = curr_seq_map.get(migrated_sid, 0) + 1
            seq_num = curr_seq_map[migrated_sid]

            c.execute("""
                INSERT INTO interaction_events_new (
                    event_id, session_id, patient_id, sequence_number, timestamp_utc,
                    therapy_area, activity_id, event_type, payload_json, metadata_json, ingested_at
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """, (
                ev["event_id"], migrated_sid, ev["patient_id"], seq_num, ev["timestamp_utc"],
                ev["therapy_area"], ev["activity_id"], ev["event_type"], ev["payload_json"],
                ev["metadata_json"], ev["ingested_at"]
            ))

        c.execute("DROP TABLE interaction_events;")
        c.execute("ALTER TABLE interaction_events_new RENAME TO interaction_events;")
        c.execute("CREATE INDEX idx_events_session_seq ON interaction_events(session_id, sequence_number);")
        c.execute("CREATE INDEX idx_events_patient_time ON interaction_events(patient_id, timestamp_utc);")
        c.execute("CREATE INDEX idx_events_type ON interaction_events(event_type);")

        # E. Rebuild patient_support_profiles (Preserve AUTOINCREMENT and sequence)
        c.execute("""
            CREATE TABLE patient_support_profiles_new (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                patient_id TEXT NOT NULL,
                created_at TEXT NOT NULL,
                active_support_tier TEXT,
                carl_config_json TEXT NOT NULL,
                caregiver_preferences_json TEXT NOT NULL,
                onboarding_notes TEXT,
                is_active INTEGER NOT NULL DEFAULT 1,
                FOREIGN KEY (patient_id) REFERENCES patients(patient_id) ON DELETE CASCADE
            );
        """)
        c.execute("""
            INSERT INTO patient_support_profiles_new (
                id, patient_id, created_at, active_support_tier, carl_config_json,
                caregiver_preferences_json, onboarding_notes, is_active
            ) SELECT id, patient_id, created_at, active_support_tier, carl_config_json,
                     caregiver_preferences_json, onboarding_notes, is_active
              FROM patient_support_profiles;
        """)
        c.execute("DROP TABLE patient_support_profiles;")
        c.execute("ALTER TABLE patient_support_profiles_new RENAME TO patient_support_profiles;")
        c.execute("CREATE INDEX idx_support_profiles_patient ON patient_support_profiles(patient_id, is_active);")

        # F. Rebuild interaction_profiles (Add longitudinal_metrics_json)
        c.execute("""
            CREATE TABLE interaction_profiles_new (
                patient_id TEXT PRIMARY KEY,
                last_updated TEXT NOT NULL,
                total_sessions_recorded INTEGER NOT NULL DEFAULT 0,
                overall_completion_rate REAL,
                area_summaries_json TEXT NOT NULL,
                observed_assistance_json TEXT NOT NULL,
                longitudinal_metrics_json TEXT NOT NULL DEFAULT '{}',
                recent_trend_note TEXT,
                FOREIGN KEY (patient_id) REFERENCES patients(patient_id) ON DELETE CASCADE
            );
        """)
        c.execute("""
            INSERT INTO interaction_profiles_new (
                patient_id, last_updated, total_sessions_recorded, overall_completion_rate,
                area_summaries_json, observed_assistance_json, longitudinal_metrics_json, recent_trend_note
            ) SELECT patient_id, last_updated, total_sessions_recorded, overall_completion_rate,
                     area_summaries_json, observed_assistance_json, '{}', recent_trend_note
              FROM interaction_profiles;
        """)
        c.execute("DROP TABLE interaction_profiles;")
        c.execute("ALTER TABLE interaction_profiles_new RENAME TO interaction_profiles;")

        # G. Rebuild support_recommendations (Preserve historical tiers, add granular tracking)
        c.execute("""
            CREATE TABLE support_recommendations_new (
                recommendation_id TEXT PRIMARY KEY,
                patient_id TEXT NOT NULL,
                timestamp_utc TEXT NOT NULL,
                current_tier TEXT,
                recommended_tier TEXT NOT NULL,
                rationale TEXT NOT NULL,
                triggering_area TEXT,
                is_temporary_baseline INTEGER NOT NULL DEFAULT 1,
                status TEXT NOT NULL DEFAULT 'pending',
                suggested_assistance_type TEXT NOT NULL DEFAULT 'instructional_pacing',
                delivery_status TEXT NOT NULL DEFAULT 'not_delivered',
                patient_response TEXT,
                observed_outcome_json TEXT,
                FOREIGN KEY (patient_id) REFERENCES patients(patient_id) ON DELETE CASCADE
            );
        """)
        c.execute("""
            INSERT INTO support_recommendations_new (
                recommendation_id, patient_id, timestamp_utc, current_tier, recommended_tier,
                rationale, triggering_area, is_temporary_baseline, status,
                suggested_assistance_type, delivery_status, patient_response, observed_outcome_json
            ) SELECT recommendation_id, patient_id, timestamp_utc, current_tier, recommended_tier,
                     rationale, triggering_area, is_temporary_baseline, status,
                     'instructional_pacing', 'not_delivered', NULL, NULL
              FROM support_recommendations;
        """)
        c.execute("DROP TABLE support_recommendations;")
        c.execute("ALTER TABLE support_recommendations_new RENAME TO support_recommendations;")
        c.execute("CREATE INDEX idx_recommendations_patient ON support_recommendations(patient_id, timestamp_utc);")

        # H. Rebuild caregiver_overrides
        c.execute("""
            CREATE TABLE caregiver_overrides_new (
                override_id TEXT PRIMARY KEY,
                patient_id TEXT NOT NULL,
                timestamp_utc TEXT NOT NULL,
                selected_tier TEXT NOT NULL,
                lock_tier INTEGER NOT NULL DEFAULT 0,
                caregiver_note TEXT,
                FOREIGN KEY (patient_id) REFERENCES patients(patient_id) ON DELETE CASCADE
            );
        """)
        c.execute("""
            INSERT INTO caregiver_overrides_new (
                override_id, patient_id, timestamp_utc, selected_tier, lock_tier, caregiver_note
            ) SELECT override_id, patient_id, timestamp_utc, selected_tier, lock_tier, caregiver_note
              FROM caregiver_overrides;
        """)
        c.execute("DROP TABLE caregiver_overrides;")
        c.execute("ALTER TABLE caregiver_overrides_new RENAME TO caregiver_overrides;")
        c.execute("CREATE INDEX idx_overrides_patient ON caregiver_overrides(patient_id, timestamp_utc);")

        # I. Create behavioral_interpretations table (Layer 2B)
        c.execute("""
            CREATE TABLE behavioral_interpretations (
                interpretation_id TEXT PRIMARY KEY,
                patient_id TEXT NOT NULL,
                session_id TEXT,
                created_at TEXT NOT NULL,
                observation_summary TEXT NOT NULL,
                tentative_hypotheses_json TEXT NOT NULL,
                supporting_event_ids_json TEXT NOT NULL,
                confidence_level TEXT NOT NULL CHECK (confidence_level IN ('low', 'moderate', 'uncertain')),
                source TEXT NOT NULL,
                review_status TEXT NOT NULL DEFAULT 'provisional' CHECK (
                    review_status IN ('provisional', 'caregiver_acknowledged', 'caregiver_rejected', 'superseded')
                ),
                review_note TEXT,
                superseded_by TEXT,
                FOREIGN KEY (patient_id) REFERENCES patients(patient_id) ON DELETE CASCADE,
                FOREIGN KEY (session_id) REFERENCES session_records(session_id) ON DELETE SET NULL,
                FOREIGN KEY (superseded_by) REFERENCES behavioral_interpretations(interpretation_id) ON DELETE SET NULL
            );
        """)
        c.execute("CREATE INDEX idx_interpretations_patient ON behavioral_interpretations(patient_id, created_at);")

        # J. Pre-Commit Validation Checks (Inside Transaction)
        c.execute("SELECT COUNT(*) FROM patients")
        cnt_patients = c.fetchone()[0]
        c.execute("SELECT COUNT(*) FROM session_records")
        cnt_sessions = c.fetchone()[0]
        c.execute("SELECT COUNT(*) FROM interaction_events")
        cnt_events = c.fetchone()[0]
        c.execute("SELECT COUNT(*) FROM patient_support_profiles")
        cnt_profiles = c.fetchone()[0]
        c.execute("SELECT COUNT(*) FROM caregiver_overrides")
        cnt_overrides = c.fetchone()[0]
        c.execute("SELECT COUNT(*) FROM interaction_profiles")
        cnt_int_profiles = c.fetchone()[0]
        c.execute("SELECT COUNT(*) FROM support_recommendations")
        cnt_recommendations = c.fetchone()[0]

        if cnt_patients != 174:
            raise ValueError(f"Pre-commit check failed: Expected 174 patients, got {cnt_patients}")
        if cnt_sessions != 150:
            raise ValueError(f"Pre-commit check failed: Expected 150 sessions, got {cnt_sessions}")
        if cnt_events != count_events_pre:
            raise ValueError(f"Pre-commit check failed: Events count mismatch: pre={count_events_pre}, post={cnt_events}")
        if cnt_profiles != count_profiles_pre:
            raise ValueError(f"Pre-commit check failed: Profiles count mismatch: pre={count_profiles_pre}, post={cnt_profiles}")
        if cnt_overrides != count_overrides_pre:
            raise ValueError(f"Pre-commit check failed: Overrides count mismatch: pre={count_overrides_pre}, post={cnt_overrides}")
        if cnt_int_profiles != count_int_profiles_pre:
            raise ValueError(f"Pre-commit check failed: Int profiles mismatch: pre={count_int_profiles_pre}, post={cnt_int_profiles}")
        if cnt_recommendations != count_recommendations_pre:
            raise ValueError(f"Pre-commit check failed: Recommendations mismatch: pre={count_recommendations_pre}, post={cnt_recommendations}")

        # Commit transaction
        conn.commit()
        log_callback("Transaction committed successfully.")

    except Exception as e:
        conn.rollback()
        conn.close()
        raise RuntimeError(f"Migration transaction aborted and rolled back: {e}") from e

    # Step 3: Post-Commit Referential & Physical Integrity Check
    c.execute("PRAGMA foreign_keys = ON;")
    c.execute("PRAGMA foreign_key_check;")
    fk_violations = c.fetchall()
    if fk_violations:
        conn.close()
        raise RuntimeError(f"Post-commit foreign_key_check FAILED: {fk_violations}")

    c.execute("PRAGMA integrity_check;")
    integ = c.fetchone()[0]
    if integ != "ok":
        conn.close()
        raise RuntimeError(f"Post-commit integrity_check FAILED: {integ}")

    conn.close()
    log_callback("Post-commit PRAGMA foreign_key_check and integrity_check passed cleanly.")

    return {
        "status": "success",
        "patients_count": cnt_patients,
        "sessions_count": cnt_sessions,
        "events_count": cnt_events,
        "profiles_count": cnt_profiles,
        "overrides_count": cnt_overrides,
        "interaction_profiles_count": cnt_int_profiles,
        "recommendations_count": cnt_recommendations,
        "migration_recorded_at": migration_timestamp
    }


if __name__ == "__main__":
    import sys
    target = sys.argv[1] if len(sys.argv) > 1 else None
    if not target:
        print("Usage: python migration_v2_p2a.py <path_to_disposable_db>")
        sys.exit(1)
    res = run_migration(target)
    print("MIGRATION COMPLETED:", json.dumps(res, indent=2))
