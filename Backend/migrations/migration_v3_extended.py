"""
NeuroCompanion Migration v3 — Extended Interaction Model

Adds 7 new tables and 10 optional columns to interaction_events.
Safe to run multiple times (idempotent). Never drops or modifies existing
columns or tables.

Tables added:
  - patient_preferences
  - memory_support_presentations
  - observation_opportunities
  - assistance_episodes
  - ui_configuration_history
  - event_interpretations
  - consent_records

Columns added to interaction_events:
  - event_category, activity_step_id, sequence_number, prompt_id,
    observation_opportunity_id, assistance_episode_id, correlation_id,
    event_schema_version, cognitive_domain_tags_json, assistance_labels_json
"""

import sqlite3
import os
import sys
from datetime import datetime, timezone

MIGRATION_VERSION = "v3_extended"

DB_PATH = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    "data",
    "neurocompanion.db",
)

# ---------------------------------------------------------------------------
# DDL — new tables
# ---------------------------------------------------------------------------

NEW_TABLES_DDL = [
    # patient_preferences
    """
    CREATE TABLE IF NOT EXISTS patient_preferences (
        preference_id       TEXT PRIMARY KEY,
        patient_id          TEXT NOT NULL,
        preference_category TEXT NOT NULL,
        preference_key      TEXT NOT NULL,
        preference_value    TEXT NOT NULL,
        source              TEXT NOT NULL,
        is_current          INTEGER NOT NULL DEFAULT 1,
        recorded_at         TEXT NOT NULL,
        superseded_at       TEXT,
        notes               TEXT
    )
    """,
    """
    CREATE INDEX IF NOT EXISTS idx_preferences_patient
        ON patient_preferences(patient_id, preference_category, is_current)
    """,

    # memory_support_presentations
    """
    CREATE TABLE IF NOT EXISTS memory_support_presentations (
        information_id       TEXT PRIMARY KEY,
        patient_id           TEXT NOT NULL,
        session_id           TEXT NOT NULL,
        room                 TEXT NOT NULL,
        activity_id          TEXT,
        content_id           TEXT NOT NULL,
        content_version      TEXT NOT NULL DEFAULT '1.0',
        presentation_method  TEXT NOT NULL,
        presented_at         TEXT NOT NULL,
        repetition_count     INTEGER NOT NULL DEFAULT 0,
        triggering_event_id  TEXT
    )
    """,
    """
    CREATE INDEX IF NOT EXISTS idx_presentations_patient_session
        ON memory_support_presentations(patient_id, session_id, presented_at)
    """,
    """
    CREATE INDEX IF NOT EXISTS idx_presentations_content
        ON memory_support_presentations(patient_id, content_id)
    """,

    # observation_opportunities
    """
    CREATE TABLE IF NOT EXISTS observation_opportunities (
        opportunity_id          TEXT PRIMARY KEY,
        linked_presentation_id  TEXT,
        patient_id              TEXT NOT NULL,
        session_id              TEXT NOT NULL,
        room                    TEXT NOT NULL,
        activity_id             TEXT,
        opportunity_type        TEXT NOT NULL,
        presented_at            TEXT NOT NULL,
        response_status         TEXT NOT NULL DEFAULT 'unknown',
        response_latency_seconds REAL,
        assistance_requested    INTEGER NOT NULL DEFAULT 0,
        assistance_received     INTEGER NOT NULL DEFAULT 0,
        related_event_id        TEXT
    )
    """,
    """
    CREATE INDEX IF NOT EXISTS idx_opportunities_patient_session
        ON observation_opportunities(patient_id, session_id)
    """,
    """
    CREATE INDEX IF NOT EXISTS idx_opportunities_presentation
        ON observation_opportunities(linked_presentation_id)
    """,

    # assistance_episodes
    """
    CREATE TABLE IF NOT EXISTS assistance_episodes (
        episode_id           TEXT PRIMARY KEY,
        patient_id           TEXT NOT NULL,
        session_id           TEXT NOT NULL,
        room                 TEXT NOT NULL,
        activity_id          TEXT,
        activity_step_id     TEXT,
        triggering_event_id  TEXT,
        assistance_type      TEXT NOT NULL,
        initiation           TEXT NOT NULL,
        offered_at           TEXT NOT NULL,
        accepted             INTEGER,
        delivered            INTEGER NOT NULL DEFAULT 0,
        delivered_at         TEXT,
        subsequent_outcome   TEXT NOT NULL DEFAULT 'unknown',
        outcome_event_id     TEXT,
        notes                TEXT
    )
    """,
    """
    CREATE INDEX IF NOT EXISTS idx_episodes_patient_session
        ON assistance_episodes(patient_id, session_id)
    """,
    """
    CREATE INDEX IF NOT EXISTS idx_episodes_triggering
        ON assistance_episodes(triggering_event_id)
    """,

    # ui_configuration_history
    """
    CREATE TABLE IF NOT EXISTS ui_configuration_history (
        config_id                    TEXT PRIMARY KEY,
        patient_id                   TEXT NOT NULL,
        effective_from               TEXT NOT NULL,
        effective_to                 TEXT,
        text_size                    TEXT NOT NULL DEFAULT 'medium',
        text_contrast                TEXT NOT NULL DEFAULT 'standard',
        max_visible_choices          INTEGER NOT NULL DEFAULT 4,
        subtitles_enabled            INTEGER NOT NULL DEFAULT 1,
        speech_enabled               INTEGER NOT NULL DEFAULT 1,
        speech_speed                 REAL NOT NULL DEFAULT 1.0,
        instruction_format           TEXT NOT NULL DEFAULT 'concise',
        visual_hints_available       INTEGER NOT NULL DEFAULT 1,
        demonstrations_available     INTEGER NOT NULL DEFAULT 0,
        step_by_step_guidance        INTEGER NOT NULL DEFAULT 0,
        activity_complexity          TEXT NOT NULL DEFAULT 'standard',
        navigation_assistance        INTEGER NOT NULL DEFAULT 1,
        change_source                TEXT NOT NULL,
        change_reason                TEXT,
        related_recommendation_id    TEXT,
        related_assistance_episode_id TEXT
    )
    """,
    """
    CREATE INDEX IF NOT EXISTS idx_ui_config_patient
        ON ui_configuration_history(patient_id, effective_from)
    """,

    # event_interpretations
    """
    CREATE TABLE IF NOT EXISTS event_interpretations (
        interpretation_id       TEXT PRIMARY KEY,
        event_id                TEXT NOT NULL,
        patient_id              TEXT NOT NULL,
        cognitive_domains_json  TEXT NOT NULL DEFAULT '[]',
        assistance_labels_json  TEXT NOT NULL DEFAULT '[]',
        analyst_note            TEXT,
        generated_at            TEXT NOT NULL,
        model_version           TEXT
    )
    """,
    """
    CREATE INDEX IF NOT EXISTS idx_interpretations_event
        ON event_interpretations(event_id)
    """,
    """
    CREATE INDEX IF NOT EXISTS idx_interpretations_patient
        ON event_interpretations(patient_id, generated_at)
    """,

    # consent_records
    """
    CREATE TABLE IF NOT EXISTS consent_records (
        consent_id   TEXT PRIMARY KEY,
        patient_id   TEXT NOT NULL,
        consent_type TEXT NOT NULL,
        granted      INTEGER NOT NULL DEFAULT 1,
        granted_by   TEXT NOT NULL,
        granted_at   TEXT NOT NULL,
        expires_at   TEXT,
        notes        TEXT
    )
    """,
    """
    CREATE INDEX IF NOT EXISTS idx_consent_patient
        ON consent_records(patient_id, consent_type)
    """,
]

# Table names (for reporting / verification)
NEW_TABLE_NAMES = [
    "patient_preferences",
    "memory_support_presentations",
    "observation_opportunities",
    "assistance_episodes",
    "ui_configuration_history",
    "event_interpretations",
    "consent_records",
]

# New columns for interaction_events: (column_name, column_definition)
NEW_INTERACTION_EVENT_COLUMNS = [
    ("event_category",              "TEXT"),
    ("activity_step_id",            "TEXT"),
    ("sequence_number",             "INTEGER"),
    ("prompt_id",                   "TEXT"),
    ("observation_opportunity_id",  "TEXT"),
    ("assistance_episode_id",       "TEXT"),
    ("correlation_id",              "TEXT"),
    ("event_schema_version",        "TEXT DEFAULT '1.0'"),
    ("cognitive_domain_tags_json",  "TEXT"),
    ("assistance_labels_json",      "TEXT"),
]


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _table_exists(cursor: sqlite3.Cursor, table_name: str) -> bool:
    cursor.execute(
        "SELECT 1 FROM sqlite_master WHERE type='table' AND name=?",
        (table_name,),
    )
    return cursor.fetchone() is not None


def _column_exists(cursor: sqlite3.Cursor, table_name: str, column_name: str) -> bool:
    cursor.execute(f"PRAGMA table_info({table_name})")
    rows = cursor.fetchall()
    return any(row[1] == column_name for row in rows)


# ---------------------------------------------------------------------------
# Public API
# ---------------------------------------------------------------------------

def run_migration(db_path: str = DB_PATH) -> dict:
    """
    Apply the v3_extended migration to *db_path*.

    Returns a dict with keys:
      version, tables_created, columns_added, already_existed, timestamp
    """
    tables_created: list[str] = []
    columns_added: list[str] = []
    already_existed: list[str] = []

    conn = sqlite3.connect(db_path)
    try:
        cursor = conn.cursor()

        # -- 1. Create new tables (and their indexes) -------------------------
        for stmt in NEW_TABLES_DDL:
            cursor.execute(stmt.strip())

        # Report which tables were newly created vs. already there.
        # Because we used CREATE TABLE IF NOT EXISTS, we detect "pre-existence"
        # by checking *before* committing whether the table was just created.
        # Instead, we check after the fact using sqlite_master.
        # (All tables now exist after running the DDL above.)
        conn.commit()

        cursor.execute(
            "SELECT name FROM sqlite_master WHERE type='table' AND name IN ({seq})".format(
                seq=",".join("?" * len(NEW_TABLE_NAMES))
            ),
            NEW_TABLE_NAMES,
        )
        found_now = {row[0] for row in cursor.fetchall()}
        # We cannot distinguish "just created" from "pre-existed" after a
        # CREATE TABLE IF NOT EXISTS, so we report all as created (the
        # migration is idempotent either way).
        tables_created = sorted(found_now)

        # -- 2. Add columns to interaction_events ----------------------------
        if _table_exists(cursor, "interaction_events"):
            for col_name, col_def in NEW_INTERACTION_EVENT_COLUMNS:
                if _column_exists(cursor, "interaction_events", col_name):
                    already_existed.append(f"interaction_events.{col_name}")
                    continue
                try:
                    cursor.execute(
                        f"ALTER TABLE interaction_events ADD COLUMN {col_name} {col_def}"
                    )
                    conn.commit()
                    columns_added.append(f"interaction_events.{col_name}")
                except sqlite3.OperationalError as exc:
                    # Column may have been added by a concurrent process;
                    # treat duplicate-column errors as non-fatal.
                    if "duplicate column name" in str(exc).lower():
                        already_existed.append(f"interaction_events.{col_name}")
                    else:
                        raise
        else:
            already_existed.append(
                "interaction_events (table not found — column additions skipped)"
            )

        conn.commit()

    finally:
        conn.close()

    return {
        "version": MIGRATION_VERSION,
        "tables_created": tables_created,
        "columns_added": columns_added,
        "already_existed": already_existed,
        "timestamp": datetime.now(timezone.utc).isoformat(),
    }


def verify_migration(db_path: str = DB_PATH) -> dict:
    """
    Verify that all expected tables exist in *db_path*.

    Returns a dict with keys: ok, tables_found, missing
    """
    conn = sqlite3.connect(db_path)
    try:
        cursor = conn.cursor()
        cursor.execute(
            "SELECT name FROM sqlite_master WHERE type='table' AND name IN ({seq})".format(
                seq=",".join("?" * len(NEW_TABLE_NAMES))
            ),
            NEW_TABLE_NAMES,
        )
        tables_found = sorted(row[0] for row in cursor.fetchall())
    finally:
        conn.close()

    missing = sorted(set(NEW_TABLE_NAMES) - set(tables_found))
    return {
        "ok": len(missing) == 0,
        "tables_found": tables_found,
        "missing": missing,
    }


# ---------------------------------------------------------------------------
# Entry-point
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    db_path = sys.argv[1] if len(sys.argv) > 1 else DB_PATH
    print(f"Running migration {MIGRATION_VERSION} on: {db_path}")
    result = run_migration(db_path)
    print(result)
    verify = verify_migration(db_path)
    print(verify)
    sys.exit(0 if verify["ok"] else 1)
