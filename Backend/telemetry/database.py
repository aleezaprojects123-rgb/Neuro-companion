"""
NeuroCompanion Database Module.
Manages persistent SQLite storage for Unity Interaction-Based Adaptive Intelligence.

Tables:
- patient_support_profiles: Historical & active support configurations (onboarding without MMSE)
- interaction_events: Raw deduplicated telemetry events emitted from Unity
- interaction_profiles: Aggregated longitudinal interaction summaries
- support_recommendations: Proposed adjustments to support tiers
- caregiver_overrides: Explicit caregiver decisions superseding automated suggestions
"""

import json
import os
import sqlite3
from contextlib import contextmanager
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple

from telemetry.schemas import (
    TherapyArea,
    EventType,
    SupportTier,
    AssistanceSource,
    CompletionStatus,
    InteractionPayload,
    InteractionEvent,
    CarlConfiguration,
    CaregiverPreferences,
    InitialSupportProfile,
    AreaSummary,
    InteractionProfile,
    SupportRecommendation,
    CaregiverOverride,
)

DEFAULT_DB_PATH = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "data", "neurocompanion.db"
)


def _utc_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


class Database:
    """Thread-safe SQLite database manager for NeuroCompanion telemetry."""

    def __init__(self, db_path: Optional[str] = None):
        self.db_path = db_path or DEFAULT_DB_PATH
        # Ensure parent directory exists
        os.makedirs(os.path.dirname(os.path.abspath(self.db_path)), exist_ok=True)
        self.init_db()

    @contextmanager
    def get_connection(self):
        """Context manager providing a SQLite connection with foreign keys and WAL mode."""
        conn = sqlite3.connect(self.db_path, timeout=30.0)
        conn.row_factory = sqlite3.Row
        conn.execute("PRAGMA foreign_keys = ON;")
        try:
            yield conn
            conn.commit()
        except Exception:
            conn.rollback()
            raise
        finally:
            conn.close()

    def _run_migrations(self, conn: sqlite3.Connection):
        """
        Backward-compatible migration:
        If patient_support_profiles or support_recommendations were previously created with
        NOT NULL on active_support_tier / current_tier, recreate them safely preserving data.
        """
        # 1. Check patient_support_profiles.active_support_tier nullability
        table_info = conn.execute("PRAGMA table_info(patient_support_profiles);").fetchall()
        tier_col = next((col for col in table_info if col["name"] == "active_support_tier"), None)
        if tier_col and tier_col["notnull"] == 1:
            conn.execute("""
                CREATE TABLE patient_support_profiles_new (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    patient_id TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    active_support_tier TEXT,
                    carl_config_json TEXT NOT NULL,
                    caregiver_preferences_json TEXT NOT NULL,
                    onboarding_notes TEXT,
                    is_active INTEGER NOT NULL DEFAULT 1
                );
            """)
            conn.execute("""
                INSERT INTO patient_support_profiles_new (
                    id, patient_id, created_at, active_support_tier,
                    carl_config_json, caregiver_preferences_json,
                    onboarding_notes, is_active
                ) SELECT id, patient_id, created_at, active_support_tier,
                         carl_config_json, caregiver_preferences_json,
                         onboarding_notes, is_active
                  FROM patient_support_profiles;
            """)
            conn.execute("DROP TABLE patient_support_profiles;")
            conn.execute("ALTER TABLE patient_support_profiles_new RENAME TO patient_support_profiles;")
            conn.execute("""
                CREATE INDEX IF NOT EXISTS idx_support_profiles_patient
                ON patient_support_profiles(patient_id, is_active);
            """)

        # 2. Check support_recommendations.current_tier nullability
        rec_info = conn.execute("PRAGMA table_info(support_recommendations);").fetchall()
        cur_tier_col = next((col for col in rec_info if col["name"] == "current_tier"), None)
        if cur_tier_col and cur_tier_col["notnull"] == 1:
            conn.execute("""
                CREATE TABLE support_recommendations_new (
                    recommendation_id TEXT PRIMARY KEY,
                    patient_id TEXT NOT NULL,
                    timestamp_utc TEXT NOT NULL,
                    current_tier TEXT,
                    recommended_tier TEXT NOT NULL,
                    rationale TEXT NOT NULL,
                    triggering_area TEXT,
                    is_temporary_baseline INTEGER NOT NULL DEFAULT 1,
                    status TEXT NOT NULL DEFAULT 'pending'
                );
            """)
            conn.execute("""
                INSERT INTO support_recommendations_new (
                    recommendation_id, patient_id, timestamp_utc,
                    current_tier, recommended_tier, rationale,
                    triggering_area, is_temporary_baseline, status
                ) SELECT recommendation_id, patient_id, timestamp_utc,
                         current_tier, recommended_tier, rationale,
                         triggering_area, is_temporary_baseline, status
                  FROM support_recommendations;
            """)
            conn.execute("DROP TABLE support_recommendations;")
            conn.execute("ALTER TABLE support_recommendations_new RENAME TO support_recommendations;")
            conn.execute("""
                CREATE INDEX IF NOT EXISTS idx_recommendations_patient
                ON support_recommendations(patient_id, timestamp_utc);
            """)

    def init_db(self):
        """Initialize schema and tables if they do not exist, and run backward-compatible migrations."""
        with self.get_connection() as conn:
            conn.execute("PRAGMA journal_mode = WAL;")

            # 1. Patient Support Profiles (Maintains version history)
            # active_support_tier is nullable so initial profiles can exist without an assigned tier
            conn.execute("""
            CREATE TABLE IF NOT EXISTS patient_support_profiles (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                patient_id TEXT NOT NULL,
                created_at TEXT NOT NULL,
                active_support_tier TEXT,
                carl_config_json TEXT NOT NULL,
                caregiver_preferences_json TEXT NOT NULL,
                onboarding_notes TEXT,
                is_active INTEGER NOT NULL DEFAULT 1
            );
            """)
            conn.execute("""
            CREATE INDEX IF NOT EXISTS idx_support_profiles_patient
            ON patient_support_profiles(patient_id, is_active);
            """)

            # 2. Interaction Events (Raw, deduplicated by event_id)
            conn.execute("""
            CREATE TABLE IF NOT EXISTS interaction_events (
                event_id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                patient_id TEXT NOT NULL,
                timestamp_utc TEXT NOT NULL,
                therapy_area TEXT NOT NULL,
                activity_id TEXT NOT NULL,
                event_type TEXT NOT NULL,
                payload_json TEXT NOT NULL,
                metadata_json TEXT NOT NULL,
                ingested_at TEXT NOT NULL
            );
            """)
            conn.execute("""
            CREATE INDEX IF NOT EXISTS idx_events_patient_session
            ON interaction_events(patient_id, session_id, timestamp_utc);
            """)
            conn.execute("""
            CREATE INDEX IF NOT EXISTS idx_events_area_activity
            ON interaction_events(therapy_area, activity_id);
            """)

            # 3. Aggregated Interaction Profiles
            conn.execute("""
            CREATE TABLE IF NOT EXISTS interaction_profiles (
                patient_id TEXT PRIMARY KEY,
                last_updated TEXT NOT NULL,
                total_sessions_recorded INTEGER NOT NULL DEFAULT 0,
                overall_completion_rate REAL,
                area_summaries_json TEXT NOT NULL,
                observed_assistance_json TEXT NOT NULL,
                recent_trend_note TEXT
            );
            """)

            # 4. Support Recommendations
            # current_tier is nullable so recommendations can be made from initial (unassigned) configuration
            conn.execute("""
            CREATE TABLE IF NOT EXISTS support_recommendations (
                recommendation_id TEXT PRIMARY KEY,
                patient_id TEXT NOT NULL,
                timestamp_utc TEXT NOT NULL,
                current_tier TEXT,
                recommended_tier TEXT NOT NULL,
                rationale TEXT NOT NULL,
                triggering_area TEXT,
                is_temporary_baseline INTEGER NOT NULL DEFAULT 1,
                status TEXT NOT NULL DEFAULT 'pending' -- 'pending', 'approved', 'rejected', 'superseded'
            );
            """)
            conn.execute("""
            CREATE INDEX IF NOT EXISTS idx_recommendations_patient
            ON support_recommendations(patient_id, timestamp_utc);
            """)

            # 5. Caregiver Overrides (Authoritative decisions)
            conn.execute("""
            CREATE TABLE IF NOT EXISTS caregiver_overrides (
                override_id TEXT PRIMARY KEY,
                patient_id TEXT NOT NULL,
                timestamp_utc TEXT NOT NULL,
                selected_tier TEXT NOT NULL,
                lock_tier INTEGER NOT NULL DEFAULT 0,
                caregiver_note TEXT
            );
            """)
            conn.execute("""
            CREATE INDEX IF NOT EXISTS idx_overrides_patient
            ON caregiver_overrides(patient_id, timestamp_utc);
            """)

            # Run backward-compatible schema migrations on existing database tables
            self._run_migrations(conn)

    # -----------------------------------------------------------------
    # Support Profile Operations
    # -----------------------------------------------------------------

    def save_support_profile(self, profile: InitialSupportProfile) -> None:
        """
        Store a support profile. Deactivates previous active profile to maintain
        complete audit history without silently overwriting.
        """
        with self.get_connection() as conn:
            # Mark previous records as inactive
            conn.execute(
                "UPDATE patient_support_profiles SET is_active = 0 WHERE patient_id = ?",
                (profile.patient_id,)
            )
            # Insert new active record
            tier_val = profile.active_support_tier.value if profile.active_support_tier else None
            conn.execute("""
                INSERT INTO patient_support_profiles (
                    patient_id, created_at, active_support_tier,
                    carl_config_json, caregiver_preferences_json,
                    onboarding_notes, is_active
                ) VALUES (?, ?, ?, ?, ?, ?, 1)
            """, (
                profile.patient_id,
                profile.created_at.isoformat(),
                tier_val,
                profile.carl_config.model_dump_json(),
                profile.caregiver_preferences.model_dump_json(),
                profile.onboarding_notes
            ))

    def get_support_profile(self, patient_id: str) -> Optional[InitialSupportProfile]:
        """Retrieve the currently active support profile for a patient."""
        with self.get_connection() as conn:
            row = conn.execute("""
                SELECT * FROM patient_support_profiles
                WHERE patient_id = ? AND is_active = 1
                ORDER BY id DESC LIMIT 1
            """, (patient_id,)).fetchone()

            if not row:
                return None

            tier_val = SupportTier(row["active_support_tier"]) if row["active_support_tier"] else None
            return InitialSupportProfile(
                patient_id=row["patient_id"],
                created_at=datetime.fromisoformat(row["created_at"]),
                active_support_tier=tier_val,
                carl_config=CarlConfiguration.model_validate_json(row["carl_config_json"]),
                caregiver_preferences=CaregiverPreferences.model_validate_json(row["caregiver_preferences_json"]),
                onboarding_notes=row["onboarding_notes"]
            )

    # -----------------------------------------------------------------
    # Telemetry Event Operations
    # -----------------------------------------------------------------

    def insert_events(self, events: List[InteractionEvent]) -> Tuple[int, int]:
        """
        Insert a batch of interaction events.
        Deduplicates on event_id: existing event IDs are safely skipped.
        Returns (inserted_count, skipped_count).
        """
        inserted = 0
        skipped = 0
        now_iso = _utc_iso()

        with self.get_connection() as conn:
            for event in events:
                try:
                    conn.execute("""
                        INSERT INTO interaction_events (
                            event_id, session_id, patient_id, timestamp_utc,
                            therapy_area, activity_id, event_type,
                            payload_json, metadata_json, ingested_at
                        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                    """, (
                        event.event_id,
                        event.session_id,
                        event.patient_id,
                        event.timestamp_utc.isoformat(),
                        event.therapy_area.value,
                        event.activity_id,
                        event.event_type.value,
                        event.payload.model_dump_json(),
                        json.dumps(event.metadata),
                        now_iso
                    ))
                    inserted += 1
                except sqlite3.IntegrityError:
                    # Duplicate event_id detected
                    skipped += 1

        return inserted, skipped

    def get_events_for_patient(
        self, patient_id: str, limit: int = 1000, offset: int = 0
    ) -> List[InteractionEvent]:
        """Retrieve historical interaction events for a patient chronologically."""
        events = []
        with self.get_connection() as conn:
            rows = conn.execute("""
                SELECT * FROM interaction_events
                WHERE patient_id = ?
                ORDER BY timestamp_utc ASC
                LIMIT ? OFFSET ?
            """, (patient_id, limit, offset)).fetchall()

            for r in rows:
                events.append(InteractionEvent(
                    event_id=r["event_id"],
                    session_id=r["session_id"],
                    patient_id=r["patient_id"],
                    timestamp_utc=datetime.fromisoformat(r["timestamp_utc"]),
                    therapy_area=TherapyArea(r["therapy_area"]),
                    activity_id=r["activity_id"],
                    event_type=EventType(r["event_type"]),
                    payload=InteractionPayload.model_validate_json(r["payload_json"]),
                    metadata=json.loads(r["metadata_json"])
                ))
        return events

    # -----------------------------------------------------------------
    # Interaction Profile Operations
    # -----------------------------------------------------------------

    def save_interaction_profile(self, profile: InteractionProfile) -> None:
        """Upsert aggregated interaction profile for a patient."""
        area_summaries_dict = {
            area.value: summary.model_dump()
            for area, summary in profile.area_summaries.items()
        }
        observed_assistance_dict = {
            src.value: count
            for src, count in profile.observed_assistance_frequency.items()
        }

        with self.get_connection() as conn:
            conn.execute("""
                INSERT INTO interaction_profiles (
                    patient_id, last_updated, total_sessions_recorded,
                    overall_completion_rate, area_summaries_json,
                    observed_assistance_json, recent_trend_note
                ) VALUES (?, ?, ?, ?, ?, ?, ?)
                ON CONFLICT(patient_id) DO UPDATE SET
                    last_updated = excluded.last_updated,
                    total_sessions_recorded = excluded.total_sessions_recorded,
                    overall_completion_rate = excluded.overall_completion_rate,
                    area_summaries_json = excluded.area_summaries_json,
                    observed_assistance_json = excluded.observed_assistance_json,
                    recent_trend_note = excluded.recent_trend_note
            """, (
                profile.patient_id,
                profile.last_updated.isoformat(),
                profile.total_sessions_recorded,
                profile.overall_completion_rate,
                json.dumps(area_summaries_dict),
                json.dumps(observed_assistance_dict),
                profile.recent_trend_note
            ))

    def get_interaction_profile(self, patient_id: str) -> Optional[InteractionProfile]:
        """Retrieve aggregated interaction profile for a patient."""
        with self.get_connection() as conn:
            row = conn.execute(
                "SELECT * FROM interaction_profiles WHERE patient_id = ?",
                (patient_id,)
            ).fetchone()

            if not row:
                return None

            area_raw = json.loads(row["area_summaries_json"])
            area_summaries = {
                TherapyArea(k): AreaSummary(**v) for k, v in area_raw.items()
            }

            assistance_raw = json.loads(row["observed_assistance_json"])
            observed_assistance = {
                AssistanceSource(k): v for k, v in assistance_raw.items()
            }

            return InteractionProfile(
                patient_id=row["patient_id"],
                last_updated=datetime.fromisoformat(row["last_updated"]),
                total_sessions_recorded=row["total_sessions_recorded"],
                overall_completion_rate=row["overall_completion_rate"],
                area_summaries=area_summaries,
                observed_assistance_frequency=observed_assistance,
                recent_trend_note=row["recent_trend_note"]
            )

    # -----------------------------------------------------------------
    # Support Recommendations Operations
    # -----------------------------------------------------------------

    def save_recommendation(self, rec: SupportRecommendation) -> None:
        """Store a support adaptation recommendation."""
        with self.get_connection() as conn:
            tier_val = rec.current_tier.value if rec.current_tier else None
            conn.execute("""
                INSERT INTO support_recommendations (
                    recommendation_id, patient_id, timestamp_utc,
                    current_tier, recommended_tier, rationale,
                    triggering_area, is_temporary_baseline, status
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, 'pending')
            """, (
                rec.recommendation_id,
                rec.patient_id,
                rec.timestamp_utc.isoformat(),
                tier_val,
                rec.recommended_tier.value,
                rec.rationale,
                rec.triggering_area.value if rec.triggering_area else None,
                1 if rec.is_temporary_baseline else 0
            ))

    def get_recommendations_for_patient(
        self, patient_id: str, limit: int = 50
    ) -> List[SupportRecommendation]:
        """Retrieve historical recommendations for a patient."""
        recs = []
        with self.get_connection() as conn:
            rows = conn.execute("""
                SELECT * FROM support_recommendations
                WHERE patient_id = ?
                ORDER BY timestamp_utc DESC
                LIMIT ?
            """, (patient_id, limit)).fetchall()

            for r in rows:
                c_tier = SupportTier(r["current_tier"]) if r["current_tier"] else None
                recs.append(SupportRecommendation(
                    recommendation_id=r["recommendation_id"],
                    patient_id=r["patient_id"],
                    timestamp_utc=datetime.fromisoformat(r["timestamp_utc"]),
                    current_tier=c_tier,
                    recommended_tier=SupportTier(r["recommended_tier"]),
                    rationale=r["rationale"],
                    triggering_area=TherapyArea(r["triggering_area"]) if r["triggering_area"] else None,
                    is_temporary_baseline=bool(r["is_temporary_baseline"])
                ))
        return recs

    # -----------------------------------------------------------------
    # Caregiver Overrides Operations
    # -----------------------------------------------------------------

    def save_caregiver_override(self, override: CaregiverOverride) -> None:
        """
        Record an authoritative caregiver override.
        Updates the active patient support profile to the chosen tier.
        """
        # 1. Insert override record in its own transaction
        with self.get_connection() as conn:
            conn.execute("""
                INSERT INTO caregiver_overrides (
                    override_id, patient_id, timestamp_utc,
                    selected_tier, lock_tier, caregiver_note
                ) VALUES (?, ?, ?, ?, ?, ?)
            """, (
                override.override_id,
                override.patient_id,
                override.timestamp_utc.isoformat(),
                override.selected_tier.value,
                1 if override.lock_tier else 0,
                override.caregiver_note
            ))

        # 2. Update active profile tier in a separate transaction to avoid nested connection lock
        active_profile = self.get_support_profile(override.patient_id)
        if active_profile:
            active_profile.active_support_tier = override.selected_tier
            active_profile.caregiver_preferences.allow_adaptive_recommendations = not override.lock_tier
            self.save_support_profile(active_profile)
        else:
            new_profile = InitialSupportProfile(
                patient_id=override.patient_id,
                active_support_tier=override.selected_tier,
                caregiver_preferences=CaregiverPreferences(
                    allow_adaptive_recommendations=not override.lock_tier
                ),
                onboarding_notes=f"Created via caregiver override: {override.caregiver_note or 'No notes'}"
            )
            self.save_support_profile(new_profile)

    def get_overrides_for_patient(
        self, patient_id: str, limit: int = 50
    ) -> List[CaregiverOverride]:
        """Retrieve historical caregiver overrides for a patient."""
        overrides = []
        with self.get_connection() as conn:
            rows = conn.execute("""
                SELECT * FROM caregiver_overrides
                WHERE patient_id = ?
                ORDER BY timestamp_utc DESC
                LIMIT ?
            """, (patient_id, limit)).fetchall()

            for r in rows:
                overrides.append(CaregiverOverride(
                    override_id=r["override_id"],
                    patient_id=r["patient_id"],
                    timestamp_utc=datetime.fromisoformat(r["timestamp_utc"]),
                    selected_tier=SupportTier(r["selected_tier"]),
                    lock_tier=bool(r["lock_tier"]),
                    caregiver_note=r["caregiver_note"]
                ))
        return overrides


# Global database instance
db = Database()
