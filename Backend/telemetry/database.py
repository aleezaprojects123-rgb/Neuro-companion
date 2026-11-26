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

import hashlib
import json
import os
import secrets
import sqlite3
import uuid
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


def hash_password(password: str, salt: Optional[str] = None) -> Tuple[str, str]:
    """Generates a secure PBKDF2-HMAC-SHA256 password hash with 100,000 iterations and random salt."""
    if not salt:
        salt = secrets.token_hex(16)
    pw_hash = hashlib.pbkdf2_hmac(
        "sha256", password.encode("utf-8"), salt.encode("utf-8"), 100000
    ).hex()
    return pw_hash, salt


def verify_password(password: str, stored_hash: str, salt: str) -> bool:
    """Verifies a password against the stored PBKDF2 hash in constant time."""
    pw_hash, _ = hash_password(password, salt)
    return secrets.compare_digest(pw_hash, stored_hash)


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

            # 6. User Accounts
            conn.execute("""
            CREATE TABLE IF NOT EXISTS users (
                user_id TEXT PRIMARY KEY,
                email TEXT UNIQUE NOT NULL,
                full_name TEXT NOT NULL,
                password_hash TEXT NOT NULL,
                salt TEXT NOT NULL,
                role TEXT NOT NULL DEFAULT 'caregiver',
                profile_picture_url TEXT,
                patient_association TEXT,
                accessibility_preferences_json TEXT NOT NULL DEFAULT '{}',
                created_at TEXT NOT NULL,
                last_login TEXT
            );
            """)
            conn.execute("""
            CREATE INDEX IF NOT EXISTS idx_users_email
            ON users(email);
            """)

            # 7. Authentication Tokens (Session Management)
            conn.execute("""
            CREATE TABLE IF NOT EXISTS auth_tokens (
                token TEXT PRIMARY KEY,
                user_id TEXT NOT NULL,
                created_at TEXT NOT NULL,
                expires_at TEXT NOT NULL,
                FOREIGN KEY (user_id) REFERENCES users(user_id)
            );
            """)
            conn.execute("""
            CREATE INDEX IF NOT EXISTS idx_auth_tokens_user
            ON auth_tokens(user_id);
            """)

            # 8. Password Reset Records
            conn.execute("""
            CREATE TABLE IF NOT EXISTS password_resets (
                reset_id TEXT PRIMARY KEY,
                email TEXT NOT NULL,
                reset_token TEXT NOT NULL,
                requested_at TEXT NOT NULL,
                status TEXT NOT NULL DEFAULT 'requested'
            );
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

    # -----------------------------------------------------------------
    # User Account & Authentication Operations
    # -----------------------------------------------------------------

    def create_user(
        self,
        email: str,
        full_name: str,
        password: str,
        role: str = "caregiver",
        profile_picture_url: Optional[str] = None,
        patient_association: Optional[str] = None,
        accessibility_preferences: Optional[Dict[str, Any]] = None,
    ) -> Dict[str, Any]:
        """Creates a new user account with secure PBKDF2 hashed password."""
        email_clean = email.strip().lower()
        with self.get_connection() as conn:
            existing = conn.execute(
                "SELECT user_id FROM users WHERE email = ?", (email_clean,)
            ).fetchone()
            if existing:
                raise ValueError(f"Email '{email_clean}' is already registered.")

            user_id = f"usr_{uuid.uuid4().hex[:12]}"
            pw_hash, salt = hash_password(password)
            now = datetime.now(timezone.utc).isoformat()
            prefs_json = json.dumps(accessibility_preferences or {})

            conn.execute(
                """
                INSERT INTO users (
                    user_id, email, full_name, password_hash, salt, role,
                    profile_picture_url, patient_association,
                    accessibility_preferences_json, created_at, last_login
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    user_id,
                    email_clean,
                    full_name.strip(),
                    pw_hash,
                    salt,
                    role.lower(),
                    profile_picture_url,
                    patient_association,
                    prefs_json,
                    now,
                    None,
                ),
            )
            return {
                "user_id": user_id,
                "email": email_clean,
                "full_name": full_name.strip(),
                "role": role.lower(),
                "profile_picture_url": profile_picture_url,
                "patient_association": patient_association,
                "accessibility_preferences": accessibility_preferences or {},
                "created_at": now,
                "last_login": None,
            }

    def authenticate_user(
        self, email: str, password: str
    ) -> Optional[Dict[str, Any]]:
        """Verifies email and password. Updates last_login and returns user profile dict or None."""
        email_clean = email.strip().lower()
        with self.get_connection() as conn:
            row = conn.execute(
                "SELECT * FROM users WHERE email = ?", (email_clean,)
            ).fetchone()
            if not row:
                return None

            if not verify_password(password, row["password_hash"], row["salt"]):
                return None

            now = datetime.now(timezone.utc).isoformat()
            conn.execute(
                "UPDATE users SET last_login = ? WHERE user_id = ?",
                (now, row["user_id"]),
            )

            return {
                "user_id": row["user_id"],
                "email": row["email"],
                "full_name": row["full_name"],
                "role": row["role"],
                "profile_picture_url": row["profile_picture_url"],
                "patient_association": row["patient_association"],
                "accessibility_preferences": json.loads(
                    row["accessibility_preferences_json"] or "{}"
                ),
                "created_at": row["created_at"],
                "last_login": now,
            }

    def create_session_token(self, user_id: str, days_valid: int = 7) -> str:
        """Generates a cryptographically secure session token."""
        token = secrets.token_urlsafe(32)
        now = datetime.now(timezone.utc)
        expires_at = now.timestamp() + (days_valid * 86400)
        expires_iso = datetime.fromtimestamp(expires_at, tz=timezone.utc).isoformat()

        with self.get_connection() as conn:
            conn.execute(
                """
                INSERT INTO auth_tokens (token, user_id, created_at, expires_at)
                VALUES (?, ?, ?, ?)
                """,
                (token, user_id, now.isoformat(), expires_iso),
            )
        return token

    def get_user_by_token(self, token: str) -> Optional[Dict[str, Any]]:
        """Retrieves active user profile by valid session token."""
        now_iso = datetime.now(timezone.utc).isoformat()
        with self.get_connection() as conn:
            row = conn.execute(
                """
                SELECT u.* FROM users u
                JOIN auth_tokens t ON u.user_id = t.user_id
                WHERE t.token = ? AND t.expires_at > ?
                """,
                (token, now_iso),
            ).fetchone()
            if not row:
                return None

            return {
                "user_id": row["user_id"],
                "email": row["email"],
                "full_name": row["full_name"],
                "role": row["role"],
                "profile_picture_url": row["profile_picture_url"],
                "patient_association": row["patient_association"],
                "accessibility_preferences": json.loads(
                    row["accessibility_preferences_json"] or "{}"
                ),
                "created_at": row["created_at"],
                "last_login": row["last_login"],
            }

    def get_user_by_id(self, user_id: str) -> Optional[Dict[str, Any]]:
        """Retrieves user profile by user_id."""
        with self.get_connection() as conn:
            row = conn.execute(
                "SELECT * FROM users WHERE user_id = ?", (user_id,)
            ).fetchone()
            if not row:
                return None
            return {
                "user_id": row["user_id"],
                "email": row["email"],
                "full_name": row["full_name"],
                "role": row["role"],
                "profile_picture_url": row["profile_picture_url"],
                "patient_association": row["patient_association"],
                "accessibility_preferences": json.loads(
                    row["accessibility_preferences_json"] or "{}"
                ),
                "created_at": row["created_at"],
                "last_login": row["last_login"],
            }

    def get_user_by_email(self, email: str) -> Optional[Dict[str, Any]]:
        """Retrieves user profile by email."""
        email_clean = email.strip().lower()
        with self.get_connection() as conn:
            row = conn.execute(
                "SELECT * FROM users WHERE email = ?", (email_clean,)
            ).fetchone()
            if not row:
                return None
            return {
                "user_id": row["user_id"],
                "email": row["email"],
                "full_name": row["full_name"],
                "role": row["role"],
                "profile_picture_url": row["profile_picture_url"],
                "patient_association": row["patient_association"],
                "accessibility_preferences": json.loads(
                    row["accessibility_preferences_json"] or "{}"
                ),
                "created_at": row["created_at"],
                "last_login": row["last_login"],
            }

    def update_user_profile(
        self,
        user_id: str,
        full_name: Optional[str] = None,
        profile_picture_url: Optional[str] = None,
        patient_association: Optional[str] = None,
        accessibility_preferences: Optional[Dict[str, Any]] = None,
    ) -> Optional[Dict[str, Any]]:
        """Updates user profile information."""
        user = self.get_user_by_id(user_id)
        if not user:
            return None

        new_name = full_name.strip() if full_name else user["full_name"]
        new_pic = profile_picture_url if profile_picture_url is not None else user["profile_picture_url"]
        new_assoc = patient_association if patient_association is not None else user["patient_association"]
        new_prefs = accessibility_preferences if accessibility_preferences is not None else user["accessibility_preferences"]

        with self.get_connection() as conn:
            conn.execute(
                """
                UPDATE users
                SET full_name = ?, profile_picture_url = ?, patient_association = ?, accessibility_preferences_json = ?
                WHERE user_id = ?
                """,
                (new_name, new_pic, new_assoc, json.dumps(new_prefs), user_id),
            )
        return self.get_user_by_id(user_id)

    def record_password_reset(self, email: str) -> str:
        """Records a password reset request without sending an unconfigured email."""
        reset_id = f"rst_{uuid.uuid4().hex[:12]}"
        reset_token = secrets.token_urlsafe(16)
        now_iso = datetime.now(timezone.utc).isoformat()
        with self.get_connection() as conn:
            conn.execute(
                """
                INSERT INTO password_resets (reset_id, email, reset_token, requested_at, status)
                VALUES (?, ?, ?, ?, 'requested')
                """,
                (reset_id, email.strip().lower(), reset_token, now_iso),
            )
        return reset_token


# Global database instance
db = Database()
