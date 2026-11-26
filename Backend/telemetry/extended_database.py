"""
NeuroCompanion - Extended Database Module (Phase 3→4)
Manages the six new Phase 3/4 tables and extends the interaction_events table
with optional annotation columns via idempotent ALTER TABLE statements.

IMPORTANT: Do NOT modify database.py. This class works alongside it.
"""

import json
import os
import sqlite3
from contextlib import contextmanager
from datetime import datetime, timezone
from typing import Any, Dict, List, Optional, Tuple

from telemetry.database import DEFAULT_DB_PATH
from telemetry.schemas import TherapyArea
from telemetry.extended_schemas import (
    PatientPreference,
    PreferenceCategory,
    PreferenceSource,
    MemorySupportPresentation,
    PresentationMethod,
    ObservationOpportunity,
    OpportunityType,
    ResponseStatus,
    AssistanceEpisode,
    AssistanceType,
    AssistanceInitiation,
    SubsequentOutcome,
    UIConfigurationHistory,
    ConfigChangeSource,
    EventInterpretation,
    ConsentRecord,
    ConsentType,
)


def _utc_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


class ExtendedDatabase:
    """
    Thread-safe SQLite database manager for NeuroCompanion Phase 3/4 tables.
    Uses the same database file and connection pattern as Database in database.py.
    """

    def __init__(self, db_path: Optional[str] = None):
        self.db_path = db_path or DEFAULT_DB_PATH
        os.makedirs(os.path.dirname(os.path.abspath(self.db_path)), exist_ok=True)
        self.init_extended_tables()

    # -----------------------------------------------------------------
    # Connection context manager
    # -----------------------------------------------------------------

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

    # -----------------------------------------------------------------
    # Schema initialisation
    # -----------------------------------------------------------------

    def init_extended_tables(self) -> None:
        """
        Creates the six new Phase 3/4 tables (IF NOT EXISTS) and adds optional
        annotation columns to interaction_events via idempotent ALTER TABLE.
        """
        with self.get_connection() as conn:
            conn.execute("PRAGMA journal_mode = WAL;")

            # ----------------------------------------------------------
            # 1. patient_preferences
            # ----------------------------------------------------------
            conn.execute("""
            CREATE TABLE IF NOT EXISTS patient_preferences (
                preference_id TEXT PRIMARY KEY,
                patient_id TEXT NOT NULL,
                preference_category TEXT NOT NULL,
                preference_key TEXT NOT NULL,
                preference_value TEXT NOT NULL,
                source TEXT NOT NULL,
                is_current INTEGER NOT NULL DEFAULT 1,
                recorded_at TEXT NOT NULL,
                superseded_at TEXT,
                notes TEXT
            );
            """)
            conn.execute("""
            CREATE INDEX IF NOT EXISTS idx_preferences_patient_current
            ON patient_preferences(patient_id, is_current);
            """)
            conn.execute("""
            CREATE INDEX IF NOT EXISTS idx_preferences_patient_category
            ON patient_preferences(patient_id, preference_category);
            """)

            # ----------------------------------------------------------
            # 2. memory_support_presentations
            # ----------------------------------------------------------
            conn.execute("""
            CREATE TABLE IF NOT EXISTS memory_support_presentations (
                information_id TEXT PRIMARY KEY,
                patient_id TEXT NOT NULL,
                session_id TEXT NOT NULL,
                room TEXT NOT NULL,
                activity_id TEXT,
                content_id TEXT NOT NULL,
                content_version TEXT NOT NULL DEFAULT '1.0',
                presentation_method TEXT NOT NULL,
                presented_at TEXT NOT NULL,
                repetition_count INTEGER NOT NULL DEFAULT 0,
                triggering_event_id TEXT
            );
            """)
            conn.execute("""
            CREATE INDEX IF NOT EXISTS idx_presentations_patient
            ON memory_support_presentations(patient_id, presented_at);
            """)
            conn.execute("""
            CREATE INDEX IF NOT EXISTS idx_presentations_session
            ON memory_support_presentations(session_id);
            """)

            # ----------------------------------------------------------
            # 3. observation_opportunities
            # ----------------------------------------------------------
            conn.execute("""
            CREATE TABLE IF NOT EXISTS observation_opportunities (
                opportunity_id TEXT PRIMARY KEY,
                linked_presentation_id TEXT,
                patient_id TEXT NOT NULL,
                session_id TEXT NOT NULL,
                room TEXT NOT NULL,
                activity_id TEXT,
                opportunity_type TEXT NOT NULL,
                presented_at TEXT NOT NULL,
                response_status TEXT NOT NULL DEFAULT 'UNKNOWN',
                response_latency_seconds REAL,
                assistance_requested INTEGER NOT NULL DEFAULT 0,
                assistance_received INTEGER NOT NULL DEFAULT 0,
                related_event_id TEXT
            );
            """)
            conn.execute("""
            CREATE INDEX IF NOT EXISTS idx_opportunities_session
            ON observation_opportunities(session_id, presented_at);
            """)
            conn.execute("""
            CREATE INDEX IF NOT EXISTS idx_opportunities_patient
            ON observation_opportunities(patient_id, presented_at);
            """)

            # ----------------------------------------------------------
            # 4. assistance_episodes
            # ----------------------------------------------------------
            conn.execute("""
            CREATE TABLE IF NOT EXISTS assistance_episodes (
                episode_id TEXT PRIMARY KEY,
                patient_id TEXT NOT NULL,
                session_id TEXT NOT NULL,
                room TEXT NOT NULL,
                activity_id TEXT,
                activity_step_id TEXT,
                triggering_event_id TEXT,
                assistance_type TEXT NOT NULL,
                initiation TEXT NOT NULL,
                offered_at TEXT NOT NULL,
                accepted INTEGER,
                delivered INTEGER NOT NULL DEFAULT 0,
                delivered_at TEXT,
                subsequent_outcome TEXT NOT NULL DEFAULT 'UNKNOWN',
                outcome_event_id TEXT,
                notes TEXT
            );
            """)
            conn.execute("""
            CREATE INDEX IF NOT EXISTS idx_episodes_session
            ON assistance_episodes(session_id, offered_at);
            """)
            conn.execute("""
            CREATE INDEX IF NOT EXISTS idx_episodes_patient
            ON assistance_episodes(patient_id, offered_at);
            """)

            # ----------------------------------------------------------
            # 5. ui_configuration_history
            # ----------------------------------------------------------
            conn.execute("""
            CREATE TABLE IF NOT EXISTS ui_configuration_history (
                config_id TEXT PRIMARY KEY,
                patient_id TEXT NOT NULL,
                effective_from TEXT NOT NULL,
                effective_to TEXT,
                text_size TEXT NOT NULL DEFAULT 'medium',
                text_contrast TEXT NOT NULL DEFAULT 'standard',
                max_visible_choices INTEGER NOT NULL DEFAULT 4,
                subtitles_enabled INTEGER NOT NULL DEFAULT 1,
                speech_enabled INTEGER NOT NULL DEFAULT 1,
                speech_speed REAL NOT NULL DEFAULT 1.0,
                instruction_format TEXT NOT NULL DEFAULT 'concise',
                visual_hints_available INTEGER NOT NULL DEFAULT 1,
                demonstrations_available INTEGER NOT NULL DEFAULT 0,
                step_by_step_guidance INTEGER NOT NULL DEFAULT 0,
                activity_complexity TEXT NOT NULL DEFAULT 'standard',
                navigation_assistance INTEGER NOT NULL DEFAULT 1,
                change_source TEXT NOT NULL,
                change_reason TEXT,
                related_recommendation_id TEXT,
                related_assistance_episode_id TEXT
            );
            """)
            conn.execute("""
            CREATE INDEX IF NOT EXISTS idx_ui_config_patient_effective
            ON ui_configuration_history(patient_id, effective_from DESC);
            """)
            conn.execute("""
            CREATE INDEX IF NOT EXISTS idx_ui_config_patient_active
            ON ui_configuration_history(patient_id, effective_to)
            WHERE effective_to IS NULL;
            """)

            # ----------------------------------------------------------
            # 6. event_interpretations
            # ----------------------------------------------------------
            conn.execute("""
            CREATE TABLE IF NOT EXISTS event_interpretations (
                interpretation_id TEXT PRIMARY KEY,
                event_id TEXT NOT NULL,
                patient_id TEXT NOT NULL,
                cognitive_domains_json TEXT NOT NULL DEFAULT '[]',
                assistance_labels_json TEXT NOT NULL DEFAULT '[]',
                analyst_note TEXT,
                generated_at TEXT NOT NULL,
                model_version TEXT
            );
            """)
            conn.execute("""
            CREATE INDEX IF NOT EXISTS idx_interpretations_event
            ON event_interpretations(event_id);
            """)
            conn.execute("""
            CREATE INDEX IF NOT EXISTS idx_interpretations_patient
            ON event_interpretations(patient_id, generated_at);
            """)

            # ----------------------------------------------------------
            # 7. consent_records
            # ----------------------------------------------------------
            conn.execute("""
            CREATE TABLE IF NOT EXISTS consent_records (
                consent_id TEXT PRIMARY KEY,
                patient_id TEXT NOT NULL,
                consent_type TEXT NOT NULL,
                granted INTEGER NOT NULL,
                granted_by TEXT NOT NULL,
                granted_at TEXT NOT NULL,
                expires_at TEXT,
                notes TEXT
            );
            """)
            conn.execute("""
            CREATE INDEX IF NOT EXISTS idx_consent_patient_type
            ON consent_records(patient_id, consent_type, granted_at DESC);
            """)

        # ------------------------------------------------------------------
        # Extend interaction_events with optional Phase 3/4 columns.
        # Each ALTER TABLE is wrapped in its own connection + try/except so
        # that an OperationalError for an already-existing column is silently
        # ignored while other errors propagate normally.
        # ------------------------------------------------------------------
        new_columns: List[Tuple[str, str]] = [
            ("event_category",               "TEXT"),
            ("activity_step_id",             "TEXT"),
            ("sequence_number",              "INTEGER"),
            ("prompt_id",                    "TEXT"),
            ("observation_opportunity_id",   "TEXT"),
            ("assistance_episode_id",        "TEXT"),
            ("correlation_id",               "TEXT"),
            ("event_schema_version",         "TEXT DEFAULT '1.0'"),
            ("cognitive_domain_tags_json",   "TEXT"),
            ("assistance_labels_json",       "TEXT"),
        ]
        for col_name, col_def in new_columns:
            try:
                with self.get_connection() as conn:
                    conn.execute(
                        f"ALTER TABLE interaction_events ADD COLUMN {col_name} {col_def};"
                    )
            except sqlite3.OperationalError:
                # Column already exists — this is expected on subsequent starts.
                pass

    # =================================================================
    # A. Patient Preferences
    # =================================================================

    def save_preference(self, pref: PatientPreference) -> None:
        """
        Store a patient preference. If is_current=True, supersede all previous
        active preferences with the same patient_id + preference_key combination.
        """
        now_iso = _utc_iso()
        with self.get_connection() as conn:
            if pref.is_current:
                # Mark same key's existing current records as superseded
                conn.execute(
                    """
                    UPDATE patient_preferences
                    SET is_current = 0, superseded_at = ?
                    WHERE patient_id = ? AND preference_key = ? AND is_current = 1
                    """,
                    (now_iso, pref.patient_id, pref.preference_key),
                )
            conn.execute(
                """
                INSERT INTO patient_preferences (
                    preference_id, patient_id, preference_category,
                    preference_key, preference_value, source,
                    is_current, recorded_at, superseded_at, notes
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    pref.preference_id,
                    pref.patient_id,
                    pref.preference_category.value,
                    pref.preference_key,
                    pref.preference_value,
                    pref.source.value,
                    1 if pref.is_current else 0,
                    pref.recorded_at.isoformat(),
                    pref.superseded_at.isoformat() if pref.superseded_at else None,
                    pref.notes,
                ),
            )

    def get_preferences_for_patient(
        self, patient_id: str, current_only: bool = True
    ) -> List[PatientPreference]:
        """Retrieve preferences for a patient. By default returns only current ones."""
        with self.get_connection() as conn:
            if current_only:
                rows = conn.execute(
                    "SELECT * FROM patient_preferences WHERE patient_id = ? AND is_current = 1 "
                    "ORDER BY recorded_at DESC",
                    (patient_id,),
                ).fetchall()
            else:
                rows = conn.execute(
                    "SELECT * FROM patient_preferences WHERE patient_id = ? ORDER BY recorded_at DESC",
                    (patient_id,),
                ).fetchall()
        return [self._row_to_preference(r) for r in rows]

    @staticmethod
    def _row_to_preference(r: sqlite3.Row) -> PatientPreference:
        return PatientPreference(
            preference_id=r["preference_id"],
            patient_id=r["patient_id"],
            preference_category=PreferenceCategory(r["preference_category"]),
            preference_key=r["preference_key"],
            preference_value=r["preference_value"],
            source=PreferenceSource(r["source"]),
            is_current=bool(r["is_current"]),
            recorded_at=datetime.fromisoformat(r["recorded_at"]),
            superseded_at=datetime.fromisoformat(r["superseded_at"]) if r["superseded_at"] else None,
            notes=r["notes"],
        )

    # =================================================================
    # B. Memory Support Presentations
    # =================================================================

    def save_presentation(self, pres: MemorySupportPresentation) -> None:
        """Store a memory support presentation record."""
        with self.get_connection() as conn:
            conn.execute(
                """
                INSERT INTO memory_support_presentations (
                    information_id, patient_id, session_id, room,
                    activity_id, content_id, content_version,
                    presentation_method, presented_at,
                    repetition_count, triggering_event_id
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    pres.information_id,
                    pres.patient_id,
                    pres.session_id,
                    pres.room.value,
                    pres.activity_id,
                    pres.content_id,
                    pres.content_version,
                    pres.presentation_method.value,
                    pres.presented_at.isoformat(),
                    pres.repetition_count,
                    pres.triggering_event_id,
                ),
            )

    def get_presentations_for_patient(
        self, patient_id: str, limit: int = 500
    ) -> List[MemorySupportPresentation]:
        """Retrieve memory support presentations for a patient, most recent first."""
        with self.get_connection() as conn:
            rows = conn.execute(
                "SELECT * FROM memory_support_presentations WHERE patient_id = ? "
                "ORDER BY presented_at DESC LIMIT ?",
                (patient_id, limit),
            ).fetchall()
        return [self._row_to_presentation(r) for r in rows]

    def get_presentations_for_session(
        self, session_id: str
    ) -> List[MemorySupportPresentation]:
        """Retrieve all memory support presentations within a session."""
        with self.get_connection() as conn:
            rows = conn.execute(
                "SELECT * FROM memory_support_presentations WHERE session_id = ? "
                "ORDER BY presented_at ASC",
                (session_id,),
            ).fetchall()
        return [self._row_to_presentation(r) for r in rows]

    @staticmethod
    def _row_to_presentation(r: sqlite3.Row) -> MemorySupportPresentation:
        return MemorySupportPresentation(
            information_id=r["information_id"],
            patient_id=r["patient_id"],
            session_id=r["session_id"],
            room=TherapyArea(r["room"]),
            activity_id=r["activity_id"],
            content_id=r["content_id"],
            content_version=r["content_version"],
            presentation_method=PresentationMethod(r["presentation_method"]),
            presented_at=datetime.fromisoformat(r["presented_at"]),
            repetition_count=r["repetition_count"],
            triggering_event_id=r["triggering_event_id"],
        )

    # =================================================================
    # C. Observation Opportunities
    # =================================================================

    def save_observation_opportunity(self, opp: ObservationOpportunity) -> None:
        """Store an observation opportunity record."""
        with self.get_connection() as conn:
            conn.execute(
                """
                INSERT INTO observation_opportunities (
                    opportunity_id, linked_presentation_id, patient_id,
                    session_id, room, activity_id, opportunity_type,
                    presented_at, response_status, response_latency_seconds,
                    assistance_requested, assistance_received, related_event_id
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    opp.opportunity_id,
                    opp.linked_presentation_id,
                    opp.patient_id,
                    opp.session_id,
                    opp.room.value,
                    opp.activity_id,
                    opp.opportunity_type.value,
                    opp.presented_at.isoformat(),
                    opp.response_status.value,
                    opp.response_latency_seconds,
                    1 if opp.assistance_requested else 0,
                    1 if opp.assistance_received else 0,
                    opp.related_event_id,
                ),
            )

    def get_opportunities_for_session(
        self, session_id: str
    ) -> List[ObservationOpportunity]:
        """Retrieve all observation opportunities within a session."""
        with self.get_connection() as conn:
            rows = conn.execute(
                "SELECT * FROM observation_opportunities WHERE session_id = ? "
                "ORDER BY presented_at ASC",
                (session_id,),
            ).fetchall()
        return [self._row_to_opportunity(r) for r in rows]

    def update_opportunity_response(
        self,
        opportunity_id: str,
        response_status: ResponseStatus,
        response_latency_seconds: Optional[float] = None,
        related_event_id: Optional[str] = None,
        assistance_requested: Optional[bool] = None,
        assistance_received: Optional[bool] = None,
    ) -> None:
        """Update response status and latency on an existing observation opportunity."""
        with self.get_connection() as conn:
            conn.execute(
                """
                UPDATE observation_opportunities
                SET response_status = ?,
                    response_latency_seconds = COALESCE(?, response_latency_seconds),
                    related_event_id = COALESCE(?, related_event_id),
                    assistance_requested = CASE WHEN ? IS NOT NULL THEN ? ELSE assistance_requested END,
                    assistance_received = CASE WHEN ? IS NOT NULL THEN ? ELSE assistance_received END
                WHERE opportunity_id = ?
                """,
                (
                    response_status.value,
                    response_latency_seconds,
                    related_event_id,
                    None if assistance_requested is None else (1 if assistance_requested else 0),
                    None if assistance_requested is None else (1 if assistance_requested else 0),
                    None if assistance_received is None else (1 if assistance_received else 0),
                    None if assistance_received is None else (1 if assistance_received else 0),
                    opportunity_id,
                ),
            )

    @staticmethod
    def _row_to_opportunity(r: sqlite3.Row) -> ObservationOpportunity:
        return ObservationOpportunity(
            opportunity_id=r["opportunity_id"],
            linked_presentation_id=r["linked_presentation_id"],
            patient_id=r["patient_id"],
            session_id=r["session_id"],
            room=TherapyArea(r["room"]),
            activity_id=r["activity_id"],
            opportunity_type=OpportunityType(r["opportunity_type"]),
            presented_at=datetime.fromisoformat(r["presented_at"]),
            response_status=ResponseStatus(r["response_status"]),
            response_latency_seconds=r["response_latency_seconds"],
            assistance_requested=bool(r["assistance_requested"]),
            assistance_received=bool(r["assistance_received"]),
            related_event_id=r["related_event_id"],
        )

    # =================================================================
    # D. Assistance Episodes
    # =================================================================

    def save_assistance_episode(self, episode: AssistanceEpisode) -> None:
        """Store a new assistance episode."""
        with self.get_connection() as conn:
            conn.execute(
                """
                INSERT INTO assistance_episodes (
                    episode_id, patient_id, session_id, room,
                    activity_id, activity_step_id, triggering_event_id,
                    assistance_type, initiation, offered_at,
                    accepted, delivered, delivered_at,
                    subsequent_outcome, outcome_event_id, notes
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    episode.episode_id,
                    episode.patient_id,
                    episode.session_id,
                    episode.room.value,
                    episode.activity_id,
                    episode.activity_step_id,
                    episode.triggering_event_id,
                    episode.assistance_type.value,
                    episode.initiation.value,
                    episode.offered_at.isoformat(),
                    None if episode.accepted is None else (1 if episode.accepted else 0),
                    1 if episode.delivered else 0,
                    episode.delivered_at.isoformat() if episode.delivered_at else None,
                    episode.subsequent_outcome.value,
                    episode.outcome_event_id,
                    episode.notes,
                ),
            )

    def get_episodes_for_session(self, session_id: str) -> List[AssistanceEpisode]:
        """Retrieve all assistance episodes within a session."""
        with self.get_connection() as conn:
            rows = conn.execute(
                "SELECT * FROM assistance_episodes WHERE session_id = ? "
                "ORDER BY offered_at ASC",
                (session_id,),
            ).fetchall()
        return [self._row_to_episode(r) for r in rows]

    def get_episodes_for_patient(
        self, patient_id: str, limit: int = 500
    ) -> List[AssistanceEpisode]:
        """Retrieve assistance episodes for a patient, most recent first."""
        with self.get_connection() as conn:
            rows = conn.execute(
                "SELECT * FROM assistance_episodes WHERE patient_id = ? "
                "ORDER BY offered_at DESC LIMIT ?",
                (patient_id, limit),
            ).fetchall()
        return [self._row_to_episode(r) for r in rows]

    def update_episode_outcome(
        self,
        episode_id: str,
        delivered: bool,
        delivered_at: Optional[datetime],
        subsequent_outcome: SubsequentOutcome,
        outcome_event_id: Optional[str] = None,
        accepted: Optional[bool] = None,
        notes: Optional[str] = None,
    ) -> None:
        """Update delivery and outcome fields on an existing assistance episode."""
        with self.get_connection() as conn:
            conn.execute(
                """
                UPDATE assistance_episodes
                SET delivered = ?,
                    delivered_at = ?,
                    subsequent_outcome = ?,
                    outcome_event_id = COALESCE(?, outcome_event_id),
                    accepted = COALESCE(?, accepted),
                    notes = COALESCE(?, notes)
                WHERE episode_id = ?
                """,
                (
                    1 if delivered else 0,
                    delivered_at.isoformat() if delivered_at else None,
                    subsequent_outcome.value,
                    outcome_event_id,
                    None if accepted is None else (1 if accepted else 0),
                    notes,
                    episode_id,
                ),
            )

    @staticmethod
    def _row_to_episode(r: sqlite3.Row) -> AssistanceEpisode:
        return AssistanceEpisode(
            episode_id=r["episode_id"],
            patient_id=r["patient_id"],
            session_id=r["session_id"],
            room=TherapyArea(r["room"]),
            activity_id=r["activity_id"],
            activity_step_id=r["activity_step_id"],
            triggering_event_id=r["triggering_event_id"],
            assistance_type=AssistanceType(r["assistance_type"]),
            initiation=AssistanceInitiation(r["initiation"]),
            offered_at=datetime.fromisoformat(r["offered_at"]),
            accepted=None if r["accepted"] is None else bool(r["accepted"]),
            delivered=bool(r["delivered"]),
            delivered_at=datetime.fromisoformat(r["delivered_at"]) if r["delivered_at"] else None,
            subsequent_outcome=SubsequentOutcome(r["subsequent_outcome"]),
            outcome_event_id=r["outcome_event_id"],
            notes=r["notes"],
        )

    # =================================================================
    # E. UI Configuration History
    # =================================================================

    def save_ui_config(self, config: UIConfigurationHistory) -> None:
        """
        Store a UI configuration snapshot. If effective_to is None, closes out
        any currently active (effective_to IS NULL) config for the patient first.
        """
        with self.get_connection() as conn:
            if config.effective_to is None:
                # Close the previous active config
                conn.execute(
                    """
                    UPDATE ui_configuration_history
                    SET effective_to = ?
                    WHERE patient_id = ? AND effective_to IS NULL
                    """,
                    (config.effective_from.isoformat(), config.patient_id),
                )
            conn.execute(
                """
                INSERT INTO ui_configuration_history (
                    config_id, patient_id, effective_from, effective_to,
                    text_size, text_contrast, max_visible_choices,
                    subtitles_enabled, speech_enabled, speech_speed,
                    instruction_format, visual_hints_available,
                    demonstrations_available, step_by_step_guidance,
                    activity_complexity, navigation_assistance,
                    change_source, change_reason,
                    related_recommendation_id, related_assistance_episode_id
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    config.config_id,
                    config.patient_id,
                    config.effective_from.isoformat(),
                    config.effective_to.isoformat() if config.effective_to else None,
                    config.text_size,
                    config.text_contrast,
                    config.max_visible_choices,
                    1 if config.subtitles_enabled else 0,
                    1 if config.speech_enabled else 0,
                    config.speech_speed,
                    config.instruction_format,
                    1 if config.visual_hints_available else 0,
                    1 if config.demonstrations_available else 0,
                    1 if config.step_by_step_guidance else 0,
                    config.activity_complexity,
                    1 if config.navigation_assistance else 0,
                    config.change_source.value,
                    config.change_reason,
                    config.related_recommendation_id,
                    config.related_assistance_episode_id,
                ),
            )

    def get_active_ui_config(self, patient_id: str) -> Optional[UIConfigurationHistory]:
        """Retrieve the currently active UI configuration for a patient."""
        with self.get_connection() as conn:
            row = conn.execute(
                "SELECT * FROM ui_configuration_history "
                "WHERE patient_id = ? AND effective_to IS NULL "
                "ORDER BY effective_from DESC LIMIT 1",
                (patient_id,),
            ).fetchone()
        return self._row_to_ui_config(row) if row else None

    def get_ui_config_history(
        self, patient_id: str, limit: int = 50
    ) -> List[UIConfigurationHistory]:
        """Retrieve the full UI configuration history for a patient, most recent first."""
        with self.get_connection() as conn:
            rows = conn.execute(
                "SELECT * FROM ui_configuration_history WHERE patient_id = ? "
                "ORDER BY effective_from DESC LIMIT ?",
                (patient_id, limit),
            ).fetchall()
        return [self._row_to_ui_config(r) for r in rows]

    @staticmethod
    def _row_to_ui_config(r: sqlite3.Row) -> UIConfigurationHistory:
        return UIConfigurationHistory(
            config_id=r["config_id"],
            patient_id=r["patient_id"],
            effective_from=datetime.fromisoformat(r["effective_from"]),
            effective_to=datetime.fromisoformat(r["effective_to"]) if r["effective_to"] else None,
            text_size=r["text_size"],
            text_contrast=r["text_contrast"],
            max_visible_choices=r["max_visible_choices"],
            subtitles_enabled=bool(r["subtitles_enabled"]),
            speech_enabled=bool(r["speech_enabled"]),
            speech_speed=r["speech_speed"],
            instruction_format=r["instruction_format"],
            visual_hints_available=bool(r["visual_hints_available"]),
            demonstrations_available=bool(r["demonstrations_available"]),
            step_by_step_guidance=bool(r["step_by_step_guidance"]),
            activity_complexity=r["activity_complexity"],
            navigation_assistance=bool(r["navigation_assistance"]),
            change_source=ConfigChangeSource(r["change_source"]),
            change_reason=r["change_reason"],
            related_recommendation_id=r["related_recommendation_id"],
            related_assistance_episode_id=r["related_assistance_episode_id"],
        )

    # =================================================================
    # F. Event Interpretations
    # =================================================================

    def save_event_interpretation(self, interp: EventInterpretation) -> None:
        """Store a semantic interpretation annotation for an interaction event."""
        with self.get_connection() as conn:
            conn.execute(
                """
                INSERT INTO event_interpretations (
                    interpretation_id, event_id, patient_id,
                    cognitive_domains_json, assistance_labels_json,
                    analyst_note, generated_at, model_version
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    interp.interpretation_id,
                    interp.event_id,
                    interp.patient_id,
                    json.dumps(interp.cognitive_domains),
                    json.dumps(interp.assistance_labels),
                    interp.analyst_note,
                    interp.generated_at.isoformat(),
                    interp.model_version,
                ),
            )

    def get_interpretations_for_event(
        self, event_id: str
    ) -> List[EventInterpretation]:
        """Retrieve all interpretations for a specific interaction event."""
        with self.get_connection() as conn:
            rows = conn.execute(
                "SELECT * FROM event_interpretations WHERE event_id = ? "
                "ORDER BY generated_at DESC",
                (event_id,),
            ).fetchall()
        return [self._row_to_interpretation(r) for r in rows]

    @staticmethod
    def _row_to_interpretation(r: sqlite3.Row) -> EventInterpretation:
        return EventInterpretation(
            interpretation_id=r["interpretation_id"],
            event_id=r["event_id"],
            patient_id=r["patient_id"],
            cognitive_domains=json.loads(r["cognitive_domains_json"]),
            assistance_labels=json.loads(r["assistance_labels_json"]),
            analyst_note=r["analyst_note"],
            generated_at=datetime.fromisoformat(r["generated_at"]),
            model_version=r["model_version"],
        )

    # =================================================================
    # G. Consent Records
    # =================================================================

    def save_consent_record(self, record: ConsentRecord) -> None:
        """Store a consent decision. Existing records are never modified."""
        with self.get_connection() as conn:
            conn.execute(
                """
                INSERT INTO consent_records (
                    consent_id, patient_id, consent_type, granted,
                    granted_by, granted_at, expires_at, notes
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    record.consent_id,
                    record.patient_id,
                    record.consent_type.value,
                    1 if record.granted else 0,
                    record.granted_by,
                    record.granted_at.isoformat(),
                    record.expires_at.isoformat() if record.expires_at else None,
                    record.notes,
                ),
            )

    def get_consent_records(
        self, patient_id: str, consent_type: Optional[ConsentType] = None
    ) -> List[ConsentRecord]:
        """
        Retrieve consent records for a patient.
        Optionally filter to a specific consent type. Returns most recent first.
        """
        with self.get_connection() as conn:
            if consent_type is not None:
                rows = conn.execute(
                    "SELECT * FROM consent_records WHERE patient_id = ? AND consent_type = ? "
                    "ORDER BY granted_at DESC",
                    (patient_id, consent_type.value),
                ).fetchall()
            else:
                rows = conn.execute(
                    "SELECT * FROM consent_records WHERE patient_id = ? "
                    "ORDER BY granted_at DESC",
                    (patient_id,),
                ).fetchall()
        return [self._row_to_consent(r) for r in rows]

    def check_consent(
        self, patient_id: str, consent_type: ConsentType
    ) -> bool:
        """
        Returns True if the patient currently has valid (non-expired) consent
        of the given type. Uses the most recently recorded consent decision.
        If no record exists, returns False.
        """
        now_iso = _utc_iso()
        with self.get_connection() as conn:
            row = conn.execute(
                """
                SELECT granted, expires_at
                FROM consent_records
                WHERE patient_id = ? AND consent_type = ?
                ORDER BY granted_at DESC
                LIMIT 1
                """,
                (patient_id, consent_type.value),
            ).fetchone()

        if row is None:
            return False
        if not bool(row["granted"]):
            return False
        if row["expires_at"] is not None and row["expires_at"] < now_iso:
            return False
        return True

    has_valid_consent = check_consent

    @staticmethod
    def _row_to_consent(r: sqlite3.Row) -> ConsentRecord:
        return ConsentRecord(
            consent_id=r["consent_id"],
            patient_id=r["patient_id"],
            consent_type=ConsentType(r["consent_type"]),
            granted=bool(r["granted"]),
            granted_by=r["granted_by"],
            granted_at=datetime.fromisoformat(r["granted_at"]),
            expires_at=datetime.fromisoformat(r["expires_at"]) if r["expires_at"] else None,
            notes=r["notes"],
        )


# Global extended database instance
extended_db = ExtendedDatabase()
ext_db = extended_db
