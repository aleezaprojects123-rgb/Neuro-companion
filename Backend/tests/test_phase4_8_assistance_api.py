"""
Phase 4.8 — Assistance API Integration Tests.

Validates the FastAPI endpoint:
    POST /api/v1/assistance/evaluate

Covers the 14 required test areas:
 1. Valid assistance request.
 2. Correct runtime invocation.
 3. Valid structured response.
 4. Missing patient/session identifiers (HTTP 422).
 5. Invalid request data (HTTP 422).
 6. No additional assistance (NO_ADDITIONAL_ASSISTANCE).
 7. Suppressed recommendation (cooldown & patient dismissal).
 8. Caregiver control (CAREGIVER_CONTROLLED).
 9. Patient-initiated assistance (PATIENT_INITIATED).
10. Optional AS08 break (OPTIONAL_BREAK with no lockout).
11. Runtime error handling (graceful ERROR response, no stack trace, no fake actions).
12. Router registration in FastAPI application.
13. Existing API routes remain functional (/health, root, etc.).
14. No unauthorized executable Carl actions when policy does not permit.
"""

import asyncio
import json
import os
import sys
import unittest
from pathlib import Path
from unittest.mock import MagicMock

BACKEND_DIR = str(Path(__file__).resolve().parent.parent)
if BACKEND_DIR not in sys.path:
    sys.path.insert(0, BACKEND_DIR)

from main import app
from api.routes.assistance import get_assistance_runtime
from ml.assistance.schemas import StrategyID
from ml.assistance.carl_action import CarlActionType


# ---------------------------------------------------------------------------
# ASGI Test Helper (matching existing test infrastructure in test_p2a30_api_contract.py)
# ---------------------------------------------------------------------------

async def _asgi_request(application, method: str, path: str, body=None):
    """Sends a raw ASGI HTTP request and returns (status_code, parsed_body)."""
    body_bytes = b""
    if body is not None:
        body_bytes = json.dumps(body).encode() if isinstance(body, (dict, list)) else body
    raw_headers = [
        (b"content-type", b"application/json"),
        (b"content-length", str(len(body_bytes)).encode()),
    ]
    scope = {
        "type": "http",
        "asgi": {"version": "3.0"},
        "http_version": "1.1",
        "method": method.upper(),
        "path": path,
        "raw_path": path.encode(),
        "query_string": b"",
        "headers": raw_headers,
        "server": ("127.0.0.1", 8000),
    }
    response_status: list[int] = []
    response_body_parts: list[bytes] = []
    body_sent = False

    async def receive():
        nonlocal body_sent
        if not body_sent:
            body_sent = True
            return {"type": "http.request", "body": body_bytes, "more_body": False}
        await asyncio.sleep(9999)

    async def send(message):
        if message["type"] == "http.response.start":
            response_status.append(message["status"])
        elif message["type"] == "http.response.body":
            response_body_parts.append(message.get("body", b""))

    await application(scope, receive, send)
    raw = b"".join(response_body_parts)
    try:
        parsed = json.loads(raw)
    except Exception:
        parsed = raw.decode(errors="replace")
    return response_status[0] if response_status else 0, parsed


def _post(path: str, body: dict):
    """Synchronous helper for POST requests."""
    return asyncio.run(_asgi_request(app, "POST", path, body))


def _get(path: str):
    """Synchronous helper for GET requests."""
    return asyncio.run(_asgi_request(app, "GET", path))


class TestPhase48AssistanceAPI(unittest.TestCase):
    """Test suite covering all 14 Phase 4.8 verification items."""

    def tearDown(self):
        # Reset any dependency overrides after each test
        app.dependency_overrides.clear()

    # -----------------------------------------------------------------------
    # 1. Valid assistance request
    # -----------------------------------------------------------------------
    def test_01_valid_assistance_request(self):
        payload = {
            "patient_id": "patient-api-001",
            "session_id": "session-api-001",
            "task_name": "OrientationQuiz",
            "idle_duration_ms": 1800.0,
            "target_miss_count": 1,
            "seconds_since_last_prompt": 60.0,
        }
        status, body = _post("/api/v1/assistance/evaluate", payload)
        self.assertEqual(status, 200, f"Expected 200 OK, got {status}: {body}")
        self.assertIsInstance(body, dict)

    # -----------------------------------------------------------------------
    # 2. Correct runtime invocation
    # -----------------------------------------------------------------------
    def test_02_correct_runtime_invocation(self):
        payload = {
            "patient_id": "patient-api-002",
            "session_id": "session-api-002",
            "task_name": "CognitiveGarden",
            "request_id": "req-custom-uuid-1234",
            "seconds_since_last_prompt": 60.0,
        }
        status, body = _post("/api/v1/assistance/evaluate", payload)
        self.assertEqual(status, 200)
        self.assertEqual(body["request_id"], "req-custom-uuid-1234")
        self.assertEqual(body["patient_id"], "patient-api-002")
        self.assertEqual(body["session_id"], "session-api-002")
        self.assertIsNotNone(body["decision_id"])

    # -----------------------------------------------------------------------
    # 3. Valid structured response
    # -----------------------------------------------------------------------
    def test_03_valid_structured_response(self):
        payload = {
            "patient_id": "patient-api-003",
            "session_id": "session-api-003",
            "task_name": "ReminiscenceCorner",
            "idle_duration_ms": 1500.0,
            "target_miss_count": 1,
            "seconds_since_last_prompt": 60.0,
        }
        status, body = _post("/api/v1/assistance/evaluate", payload)
        self.assertEqual(status, 200)

        required_fields = [
            "request_id", "patient_id", "session_id", "runtime_status",
            "decision_id", "decision_status", "selected_strategy_id",
            "decision_reason", "prediction_reference", "policy_rules_applied",
            "caregiver_control_applied", "patient_choice_required",
            "patient_choice_status", "requires_carl_action", "carl_action",
            "created_at"
        ]
        for field in required_fields:
            self.assertIn(field, body, f"Response missing mandatory field: {field}")

        self.assertIsInstance(body["policy_rules_applied"], list)
        self.assertIsInstance(body["requires_carl_action"], bool)

    # -----------------------------------------------------------------------
    # 4. Missing patient/session identifiers
    # -----------------------------------------------------------------------
    def test_04a_missing_patient_id_returns_422(self):
        payload = {
            "session_id": "session-api-004",
            "task_name": "OrientationQuiz",
        }
        status, body = _post("/api/v1/assistance/evaluate", payload)
        self.assertEqual(status, 422, f"Expected 422 for missing patient_id, got {status}: {body}")

    def test_04b_empty_patient_id_returns_422(self):
        payload = {
            "patient_id": "   ",
            "session_id": "session-api-004",
            "task_name": "OrientationQuiz",
        }
        status, body = _post("/api/v1/assistance/evaluate", payload)
        self.assertEqual(status, 422, f"Expected 422 for blank patient_id, got {status}: {body}")

    def test_04c_missing_session_id_returns_422(self):
        payload = {
            "patient_id": "patient-api-004",
            "task_name": "OrientationQuiz",
        }
        status, body = _post("/api/v1/assistance/evaluate", payload)
        self.assertEqual(status, 422, f"Expected 422 for missing session_id, got {status}: {body}")

    # -----------------------------------------------------------------------
    # 5. Invalid request data
    # -----------------------------------------------------------------------
    def test_05a_invalid_latency_type_returns_422(self):
        payload = {
            "patient_id": "patient-api-005",
            "session_id": "session-api-005",
            "response_latency_ms": "not-a-number",
        }
        status, body = _post("/api/v1/assistance/evaluate", payload)
        self.assertEqual(status, 422, f"Expected 422 for string latency, got {status}: {body}")

    def test_05b_negative_step_index_returns_422(self):
        payload = {
            "patient_id": "patient-api-005",
            "session_id": "session-api-005",
            "step_index": -1,
        }
        status, body = _post("/api/v1/assistance/evaluate", payload)
        self.assertEqual(status, 422, f"Expected 422 for negative step_index, got {status}: {body}")

    # -----------------------------------------------------------------------
    # 6. No additional assistance
    # -----------------------------------------------------------------------
    def test_06_no_additional_assistance(self):
        payload = {
            "patient_id": "patient-api-006",
            "session_id": "session-api-006",
            "task_name": "MemoryTask",
            "step_error_count": 0,
            "target_miss_count": 0,
            "idle_duration_ms": 100.0,
            "seconds_since_last_prompt": 60.0,
        }
        status, body = _post("/api/v1/assistance/evaluate", payload)
        self.assertEqual(status, 200)
        self.assertEqual(body["runtime_status"], "NO_ADDITIONAL_ASSISTANCE")
        self.assertFalse(body["requires_carl_action"])
        self.assertIsNone(body["carl_action"])
        self.assertIsNone(body["selected_strategy_id"])

    # -----------------------------------------------------------------------
    # 7. Suppressed recommendation
    # -----------------------------------------------------------------------
    def test_07a_cooldown_suppression(self):
        payload = {
            "patient_id": "patient-api-007",
            "session_id": "session-api-007",
            "idle_duration_ms": 2500.0,
            "target_miss_count": 2,
            "seconds_since_last_prompt": 10.0,  # inside 45s cooldown
        }
        status, body = _post("/api/v1/assistance/evaluate", payload)
        self.assertEqual(status, 200)
        self.assertEqual(body["runtime_status"], "SUPPRESSED")
        self.assertEqual(body["decision_status"], "SUPPRESSED_COOLDOWN")
        self.assertFalse(body["requires_carl_action"])
        self.assertIsNone(body["carl_action"])

    def test_07b_patient_dismissal_suppression(self):
        payload = {
            "patient_id": "patient-api-007",
            "session_id": "session-api-007",
            "idle_duration_ms": 3000.0,
            "patient_dismissed_prompt": True,
            "seconds_since_last_prompt": 60.0,
        }
        status, body = _post("/api/v1/assistance/evaluate", payload)
        self.assertEqual(status, 200)
        self.assertEqual(body["runtime_status"], "SUPPRESSED")
        self.assertEqual(body["decision_status"], "SUPPRESSED_BY_PATIENT")
        self.assertFalse(body["requires_carl_action"])
        self.assertIsNone(body["carl_action"])

    # -----------------------------------------------------------------------
    # 8. Caregiver control
    # -----------------------------------------------------------------------
    def test_08_caregiver_control(self):
        payload = {
            "patient_id": "patient-api-008",
            "session_id": "session-api-008",
            "caregiver_locked_strategy": StrategyID.AS06_SLOWER_SPEECH.value,
            "seconds_since_last_prompt": 5.0,  # Even within cooldown
            "patient_dismissed_prompt": True,   # Even if dismissed
        }
        status, body = _post("/api/v1/assistance/evaluate", payload)
        self.assertEqual(status, 200)
        self.assertEqual(body["runtime_status"], "CAREGIVER_CONTROLLED")
        self.assertEqual(body["decision_status"], "CAREGIVER_OVERRIDE")
        self.assertTrue(body["caregiver_control_applied"])
        self.assertEqual(body["selected_strategy_id"], StrategyID.AS06_SLOWER_SPEECH.value)
        self.assertTrue(body["requires_carl_action"])
        self.assertIsNotNone(body["carl_action"])
        self.assertEqual(body["carl_action"]["action_type"], CarlActionType.SLOW_SPEECH.value)

    # -----------------------------------------------------------------------
    # 9. Patient-initiated assistance
    # -----------------------------------------------------------------------
    def test_09a_patient_requested_help(self):
        payload = {
            "patient_id": "patient-api-009",
            "session_id": "session-api-009",
            "patient_requested_help": True,
            "target_miss_count": 1,
            "seconds_since_last_prompt": 5.0,  # Bypasses cooldown
        }
        status, body = _post("/api/v1/assistance/evaluate", payload)
        self.assertEqual(status, 200)
        self.assertEqual(body["runtime_status"], "PATIENT_INITIATED")
        self.assertEqual(body["decision_status"], "PATIENT_HELP_REQUEST")
        self.assertTrue(body["requires_carl_action"])
        self.assertIsNotNone(body["carl_action"])

    def test_09b_patient_requested_repeat(self):
        payload = {
            "patient_id": "patient-api-009",
            "session_id": "session-api-009",
            "patient_requested_repeat": True,
            "seconds_since_last_prompt": 2.0,
        }
        status, body = _post("/api/v1/assistance/evaluate", payload)
        self.assertEqual(status, 200)
        self.assertEqual(body["runtime_status"], "PATIENT_INITIATED")
        self.assertTrue(body["requires_carl_action"])
        self.assertEqual(body["carl_action"]["action_type"], CarlActionType.REPEAT_CURRENT_INSTRUCTION.value)

    # -----------------------------------------------------------------------
    # 10. Optional AS08 break
    # -----------------------------------------------------------------------
    def test_10_optional_as08_break(self):
        payload = {
            "patient_id": "patient-api-010",
            "session_id": "session-api-010",
            "patient_requested_break": True,
            "seconds_since_last_prompt": 60.0,
        }
        status, body = _post("/api/v1/assistance/evaluate", payload)
        self.assertEqual(status, 200)
        self.assertEqual(body["runtime_status"], "OPTIONAL_BREAK")
        self.assertEqual(body["selected_strategy_id"], StrategyID.AS08_BREAK_PAUSE.value)
        self.assertTrue(body["requires_carl_action"])
        self.assertTrue(body["patient_choice_required"])

        carl_action = body["carl_action"]
        self.assertIsNotNone(carl_action)
        self.assertEqual(carl_action["action_type"], CarlActionType.OFFER_OPTIONAL_BREAK.value)
        self.assertTrue(carl_action["is_optional"])
        self.assertFalse(carl_action["parameters"].get("force_lockout", False))
        self.assertTrue(carl_action["parameters"].get("allow_keep_playing", False))

    # -----------------------------------------------------------------------
    # 11. Runtime error handling
    # -----------------------------------------------------------------------
    def test_11_runtime_error_handling(self):
        mock_runtime = MagicMock()
        mock_runtime.process.side_effect = RuntimeError("Simulated internal DB failure")

        app.dependency_overrides[get_assistance_runtime] = lambda: mock_runtime

        payload = {
            "patient_id": "patient-api-011",
            "session_id": "session-api-011",
        }
        status, body = _post("/api/v1/assistance/evaluate", payload)
        self.assertEqual(status, 200)
        self.assertEqual(body["runtime_status"], "ERROR")
        self.assertFalse(body["requires_carl_action"])
        self.assertIsNone(body["carl_action"])
        self.assertIn("failed safely", body["decision_reason"])

    # -----------------------------------------------------------------------
    # 12. Router registration
    # -----------------------------------------------------------------------
    def test_12_router_registration(self):
        openapi_schema = app.openapi()
        paths = openapi_schema.get("paths", {})
        self.assertIn("/api/v1/assistance/evaluate", paths)
        self.assertIn("post", paths["/api/v1/assistance/evaluate"])

    # -----------------------------------------------------------------------
    # 13. Existing API routes remain functional
    # -----------------------------------------------------------------------
    def test_13a_health_endpoint_still_functional(self):
        status, body = _get("/api/v1/health")
        self.assertEqual(status, 200)
        self.assertEqual(body.get("status"), "ok")

    def test_13b_root_endpoint_still_functional(self):
        status, body = _get("/")
        self.assertEqual(status, 200)
        self.assertEqual(body.get("project"), "NeuroCompanion")

    # -----------------------------------------------------------------------
    # 14. No unauthorized executable Carl actions
    # -----------------------------------------------------------------------
    def test_14_no_unauthorized_executable_carl_actions(self):
        # Clean session -> NO_ADDITIONAL_ASSISTANCE -> must have no carl_action
        payload_clean = {
            "patient_id": "p-14",
            "session_id": "s-14",
            "step_error_count": 0,
            "target_miss_count": 0,
            "idle_duration_ms": 100.0,
        }
        _, body_clean = _post("/api/v1/assistance/evaluate", payload_clean)
        if body_clean["runtime_status"] in ["NO_ADDITIONAL_ASSISTANCE", "SUPPRESSED", "INSUFFICIENT_DATA", "ERROR"]:
            self.assertFalse(body_clean["requires_carl_action"])
            self.assertIsNone(body_clean["carl_action"])

        # Cooldown session -> SUPPRESSED -> must have no carl_action
        payload_cooldown = {
            "patient_id": "p-14",
            "session_id": "s-14",
            "idle_duration_ms": 2000.0,
            "seconds_since_last_prompt": 5.0,
        }
        _, body_cd = _post("/api/v1/assistance/evaluate", payload_cooldown)
        self.assertEqual(body_cd["runtime_status"], "SUPPRESSED")
        self.assertFalse(body_cd["requires_carl_action"])
        self.assertIsNone(body_cd["carl_action"])


if __name__ == "__main__":
    unittest.main()
