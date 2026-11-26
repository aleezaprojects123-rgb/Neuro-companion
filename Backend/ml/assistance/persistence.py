"""
Persistence Repository for NeuroCompanion Phase 4.7 Assistance Runtime.

Manages auditable SQLite persistence for assistance runtime decisions.
Guarantees:
- Avoids storing raw audio or unnecessary sensitive patient data.
- Records selected strategy, policy status, rules applied, and Carl actions.
- Thread-safe connection handling using WAL mode.
- Fully separable from runtime execution (supports in-memory databases and no-op testing).
"""

import json
import logging
import os
import sqlite3
from contextlib import contextmanager
from typing import Any, Dict, List, Optional

from .runtime_schemas import AssistanceRuntimeResponse

logger = logging.getLogger(__name__)

# Default path matching the existing SQLite project database
_BACKEND_DIR = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DEFAULT_ASSISTANCE_DB_PATH = os.path.join(_BACKEND_DIR, "data", "neurocompanion.db")


class AssistanceDecisionRepository:
    """
    Modular repository for recording and querying assistance runtime decisions.
    """

    def __init__(self, db_path: Optional[str] = None):
        self.db_path = db_path or DEFAULT_ASSISTANCE_DB_PATH
        self._memory_conn = None
        if self.db_path == ":memory:":
            # Keep a persistent connection open for in-memory databases so state is retained across calls
            self._memory_conn = sqlite3.connect(":memory:", timeout=30.0)
            self._memory_conn.row_factory = sqlite3.Row
        else:
            os.makedirs(os.path.dirname(os.path.abspath(self.db_path)), exist_ok=True)
        self.init_table()

    @contextmanager
    def get_connection(self):
        """Context manager providing a SQLite connection with timeout and WAL mode."""
        if self._memory_conn is not None:
            try:
                yield self._memory_conn
                self._memory_conn.commit()
            except Exception:
                self._memory_conn.rollback()
                raise
        else:
            conn = sqlite3.connect(self.db_path, timeout=30.0)
            conn.row_factory = sqlite3.Row
            try:
                yield conn
                conn.commit()
            except Exception:
                conn.rollback()
                raise
            finally:
                conn.close()

    def init_table(self) -> None:
        """Initializes the assistance_runtime_decisions table if not present."""
        with self.get_connection() as conn:
            if self.db_path != ":memory:":
                conn.execute("PRAGMA journal_mode = WAL;")

            conn.execute("""
            CREATE TABLE IF NOT EXISTS assistance_runtime_decisions (
                decision_id TEXT PRIMARY KEY,
                request_id TEXT NOT NULL,
                patient_id TEXT NOT NULL,
                session_id TEXT NOT NULL,
                runtime_status TEXT NOT NULL,
                decision_status TEXT,
                selected_strategy_id TEXT,
                decision_reason TEXT NOT NULL,
                prediction_reference TEXT,
                policy_rules_applied TEXT NOT NULL,
                caregiver_control_applied INTEGER NOT NULL,
                patient_choice_required INTEGER NOT NULL,
                patient_choice_status TEXT NOT NULL,
                requires_carl_action INTEGER NOT NULL,
                carl_action TEXT,
                created_at TEXT NOT NULL
            );
            """)
            conn.execute("""
            CREATE INDEX IF NOT EXISTS idx_assistance_patient_created
            ON assistance_runtime_decisions (patient_id, created_at);
            """)

    def record_decision(self, response: AssistanceRuntimeResponse) -> None:
        """Persists an AssistanceRuntimeResponse into SQLite."""
        if not response.decision_id:
            # If no decision_id was generated (e.g. fatal pre-flight validation), skip persistence
            return

        with self.get_connection() as conn:
            conn.execute("""
            INSERT OR REPLACE INTO assistance_runtime_decisions (
                decision_id,
                request_id,
                patient_id,
                session_id,
                runtime_status,
                decision_status,
                selected_strategy_id,
                decision_reason,
                prediction_reference,
                policy_rules_applied,
                caregiver_control_applied,
                patient_choice_required,
                patient_choice_status,
                requires_carl_action,
                carl_action,
                created_at
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);
            """, (
                response.decision_id,
                response.request_id,
                response.patient_id,
                response.session_id,
                response.runtime_status.value,
                response.decision_status,
                response.selected_strategy_id,
                response.decision_reason,
                json.dumps(response.prediction_reference) if response.prediction_reference else None,
                json.dumps(response.policy_rules_applied),
                1 if response.caregiver_control_applied else 0,
                1 if response.patient_choice_required else 0,
                response.patient_choice_status,
                1 if response.requires_carl_action else 0,
                json.dumps(response.carl_action) if response.carl_action else None,
                response.created_at.isoformat(),
            ))

    def get_decision(self, decision_id: str) -> Optional[Dict[str, Any]]:
        """Retrieves a single decision record by its decision_id."""
        with self.get_connection() as conn:
            cursor = conn.execute(
                "SELECT * FROM assistance_runtime_decisions WHERE decision_id = ?;",
                (decision_id,)
            )
            row = cursor.fetchone()
            if not row:
                return None
            return self._row_to_dict(row)

    def list_decisions(
        self,
        patient_id: Optional[str] = None,
        session_id: Optional[str] = None,
        limit: int = 50
    ) -> List[Dict[str, Any]]:
        """Lists decisions filtered by patient_id and/or session_id, ordered by creation."""
        query = "SELECT * FROM assistance_runtime_decisions"
        params: List[Any] = []
        conditions: List[str] = []

        if patient_id:
            conditions.append("patient_id = ?")
            params.append(patient_id)
        if session_id:
            conditions.append("session_id = ?")
            params.append(session_id)

        if conditions:
            query += " WHERE " + " AND ".join(conditions)

        query += " ORDER BY created_at DESC LIMIT ?;"
        params.append(limit)

        with self.get_connection() as conn:
            cursor = conn.execute(query, params)
            return [self._row_to_dict(row) for row in cursor.fetchall()]

    @staticmethod
    def _row_to_dict(row: sqlite3.Row) -> Dict[str, Any]:
        """Converts a database row back to structured Python dictionary."""
        d = dict(row)
        if d.get("prediction_reference"):
            try:
                d["prediction_reference"] = json.loads(d["prediction_reference"])
            except Exception:
                pass
        if d.get("policy_rules_applied"):
            try:
                d["policy_rules_applied"] = json.loads(d["policy_rules_applied"])
            except Exception:
                d["policy_rules_applied"] = []
        if d.get("carl_action"):
            try:
                d["carl_action"] = json.loads(d["carl_action"])
            except Exception:
                pass
        d["caregiver_control_applied"] = bool(d["caregiver_control_applied"])
        d["patient_choice_required"] = bool(d["patient_choice_required"])
        d["requires_carl_action"] = bool(d["requires_carl_action"])
        return d
