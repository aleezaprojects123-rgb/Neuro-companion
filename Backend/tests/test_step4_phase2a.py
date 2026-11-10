"""
NeuroCompanion — Step 4 (Phase 2A) Test Suite
=============================================
Comprehensive tests covering:
 1. Automatic initial configuration for new patients without MMSE.
 2. Automatic configuration without caregiver tier selection.
 3. Pathway A (MMSE available) & Pathway B (missing MMSE) both using the same accessible initial config.
 4. Immediate Carl assistance functionality (on-demand guidance without ML delay).
 5. Explicit interaction assistance requests (hints, repeats, demonstrations, skips).
 6. Temporary baseline adaptation recommendation generation (clearly flagged temporary baseline).
 7. Caregiver override and locked override protection (recommendations silenced when locked).
 8. Strict decoupling: No automatic mapping from OASIS CDR prediction to assistance tier.
 9. Existing telemetry API regression.
10. Existing Phase 1 /predict regression.
11. Database persistence and backward compatibility.
"""

import asyncio
import json
import os
import sys
import tempfile
import unittest
import uuid
from datetime import datetime, timezone

BACKEND_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if BACKEND_DIR not in sys.path:
    sys.path.insert(0, BACKEND_DIR)

from main import app
from telemetry.database import Database
from telemetry.baseline_adaptation import (
    AssistanceActionRequest,
    generate_immediate_assistance,
    evaluate_baseline_recommendation,
)
from telemetry.schemas import (
    TherapyArea,
    EventType,
    SupportTier,
    AssistanceSource,
    InteractionEvent,
    InteractionPayload,
    CarlConfiguration,
    CaregiverPreferences,
    InitialSupportProfile,
    InteractionProfile,
    AreaSummary,
    CaregiverOverride,
)


async def _asgi_request(
    application,
    method: str,
    path: str,
    body=None,
    headers: dict | None = None,
):
    body_bytes = b""
    if body is not None:
        if isinstance(body, (dict, list)):
            body_bytes = json.dumps(body).encode()
        elif isinstance(body, str):
            body_bytes = body.encode()
        else:
            body_bytes = body

    default_headers = {
        "content-type": "application/json",
        "content-length": str(len(body_bytes)),
    }
    if headers:
        default_headers.update(headers)

    raw_headers = [
        (k.lower().encode(), v.encode()) for k, v in default_headers.items()
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


def run(coro):
    return asyncio.run(coro)


class TestStep4Phase2A(unittest.TestCase):
    _lifespan_ctx = None

    @classmethod
    def setUpClass(cls):
        async def _startup():
            ctx = app.router.lifespan_context(app)
            await ctx.__aenter__()
            return ctx
        cls._lifespan_ctx = asyncio.run(_startup())

    @classmethod
    def tearDownClass(cls):
        if cls._lifespan_ctx is not None:
            async def _shutdown():
                await cls._lifespan_ctx.__aexit__(None, None, None)
            try:
                asyncio.run(_shutdown())
            except Exception:
                pass

    # 1. New patient configuration without MMSE
    def test_01_onboard_new_patient_without_mmse(self):
        pat_id = f"P_STEP4_NOMMSE_{uuid.uuid4().hex[:6]}"
        payload = {"patient_id": pat_id}
        status, body = run(_asgi_request(app, "POST", "/profiles/onboard", payload))
        self.assertEqual(status, 201, body)
        self.assertEqual(body["status"], "success")
        prof = body["profile"]
        self.assertEqual(prof["patient_id"], pat_id)
        # Architecture requirement: Initial configuration does NOT assign a support tier
        self.assertIsNone(prof["active_support_tier"])
        # MMSE is not present as a field in the profile
        self.assertNotIn("mmse", prof)
        self.assertNotIn("mmse_score", prof)

    # 2. New patient configuration without caregiver tier selection
    def test_02_onboard_without_caregiver_tier_selection(self):
        pat_id = f"P_STEP4_NOTIER_{uuid.uuid4().hex[:6]}"
        # Caregiver provides preferences but NO tier choice
        payload = {
            "patient_id": pat_id,
            "caregiver_preferences": {
                "preferred_therapy_areas": ["OrientationRoom", "CognitiveGarden"],
                "max_session_duration_minutes": 20,
                "allow_adaptive_recommendations": True,
            },
            "onboarding_notes": "Needs large font if possible",
        }
        status, body = run(_asgi_request(app, "POST", "/profiles/onboard", payload))
        self.assertEqual(status, 201, body)
        prof = body["profile"]
        # Architecture requirement: active_support_tier is None (no hidden Standard default)
        self.assertIsNone(prof["active_support_tier"])
        self.assertEqual(prof["carl_config"]["instruction_style"], "concise")
        self.assertTrue(prof["carl_config"]["allow_auto_hints"])
        self.assertEqual(prof["onboarding_notes"], "Needs large font if possible")

    # 3. Both MMSE pathways converge to same initial accessible support config
    def test_03_both_pathways_converge_same_initial_config(self):
        # Pathway A: Assessor runs OASIS predict first
        oasis_payload = {"Age": 78, "M/F": "M", "Educ": 3, "MMSE": 24}
        oasis_status, oasis_body = run(_asgi_request(app, "POST", "/predict", oasis_payload))
        self.assertEqual(oasis_status, 200, oasis_body)
        self.assertIn("predicted_class", oasis_body)

        pat_a = f"P_PATHWAY_A_{uuid.uuid4().hex[:6]}"
        status_a, body_a = run(_asgi_request(app, "POST", "/profiles/onboard", {"patient_id": pat_a}))
        self.assertEqual(status_a, 201)

        # Pathway B: No MMSE, straight to onboarding
        pat_b = f"P_PATHWAY_B_{uuid.uuid4().hex[:6]}"
        status_b, body_b = run(_asgi_request(app, "POST", "/profiles/onboard", {"patient_id": pat_b}))
        self.assertEqual(status_b, 201)

        # Both have identical accessible initial companion setup
        cfg_a = body_a["profile"]["carl_config"]
        cfg_b = body_b["profile"]["carl_config"]
        self.assertEqual(cfg_a["instruction_style"], cfg_b["instruction_style"])
        self.assertEqual(cfg_a["allow_auto_hints"], cfg_b["allow_auto_hints"])
        self.assertEqual(cfg_a["hesitation_threshold_seconds"], cfg_b["hesitation_threshold_seconds"])
        self.assertEqual(body_a["profile"]["active_support_tier"], body_b["profile"]["active_support_tier"])

    # 4. Immediate Carl assistance functionality (on-demand actions)
    def test_04_immediate_assistance_actions(self):
        actions = [
            ("repeat_instruction", "repeating"),
            ("step_by_step_breakdown", "guiding_step"),
            ("visual_demonstration", "demonstrating"),
            ("show_hint", "hinting"),
            ("slow_pacing", "relaxed_pace"),
            ("pause_or_skip", "offering_break"),
        ]
        for action, expected_state in actions:
            req = {
                "patient_id": "P_IMM_TEST",
                "session_id": "S_IMM_01",
                "therapy_area": "OrientationRoom",
                "activity_id": "daily_clock",
                "action_type": action,
                "step_instruction": "Look at the big clock hand.",
            }
            status, body = run(_asgi_request(app, "POST", "/carl/assistance", req))
            self.assertEqual(status, 200, body)
            self.assertEqual(body["action_type"], action)
            self.assertEqual(body["carl_state"], expected_state)
            self.assertTrue(body["allow_skip"])
            self.assertIsNotNone(body["spoken_prompt"])

    # 5. Explicit interaction requests (hints, repeats, demonstrations, skips)
    def test_05_explicit_interaction_telemetry_ingestion(self):
        pat_id = f"P_EXP_{uuid.uuid4().hex[:6]}"
        sess_id = f"S_EXP_{uuid.uuid4().hex[:6]}"
        events = [
            {
                "event_id": f"evt_{uuid.uuid4().hex[:8]}",
                "session_id": sess_id,
                "patient_id": pat_id,
                "timestamp_utc": datetime.now(timezone.utc).isoformat(),
                "therapy_area": "OrientationRoom",
                "activity_id": "calendar_task",
                "event_type": "hint_requested",
                "payload": {"assistance_source": "user_requested"},
            },
            {
                "event_id": f"evt_{uuid.uuid4().hex[:8]}",
                "session_id": sess_id,
                "patient_id": pat_id,
                "timestamp_utc": datetime.now(timezone.utc).isoformat(),
                "therapy_area": "OrientationRoom",
                "activity_id": "calendar_task",
                "event_type": "instruction_repeated",
                "payload": {"assistance_source": "user_requested"},
            },
            {
                "event_id": f"evt_{uuid.uuid4().hex[:8]}",
                "session_id": sess_id,
                "patient_id": pat_id,
                "timestamp_utc": datetime.now(timezone.utc).isoformat(),
                "therapy_area": "CognitiveGarden",
                "activity_id": "flower_sorting",
                "event_type": "demonstration_requested",
                "payload": {"assistance_source": "user_requested"},
            },
            {
                "event_id": f"evt_{uuid.uuid4().hex[:8]}",
                "session_id": sess_id,
                "patient_id": pat_id,
                "timestamp_utc": datetime.now(timezone.utc).isoformat(),
                "therapy_area": "ReminiscenceCorner",
                "activity_id": "photo_album",
                "event_type": "activity_skipped",
                "payload": {"assistance_source": "user_requested"},
            },
        ]
        status, body = run(_asgi_request(app, "POST", "/telemetry/events", events))
        self.assertEqual(status, 201, body)
        self.assertEqual(body["inserted_count"], 4)

        # Retrieve profile and verify the aggregation recorded demos and skips
        s_prof, b_prof = run(_asgi_request(app, "GET", f"/profiles/interaction/{pat_id}"))
        self.assertEqual(s_prof, 200, b_prof)
        garden_summary = b_prof["profile"]["area_summaries"]["CognitiveGarden"]
        self.assertEqual(garden_summary["demonstrations_requested_total"], 1)

    # 6. Temporary recommendation generation (transparent baseline heuristic)
    def test_06_temporary_baseline_recommendation_generation(self):
        pat_id = f"P_REC_{uuid.uuid4().hex[:6]}"
        # 1. Onboard patient
        run(_asgi_request(app, "POST", "/profiles/onboard", {"patient_id": pat_id}))

        # 2. Ingest events over 2 separate sessions with frequent hint & repeat requests (>= 5 total)
        events = []
        for s_idx in [1, 2]:
            sess_id = f"S_REC_{s_idx}"
            for i in range(3):
                events.append({
                    "event_id": f"evt_rec_{s_idx}_{i}_{uuid.uuid4().hex[:6]}",
                    "session_id": sess_id,
                    "patient_id": pat_id,
                    "timestamp_utc": datetime.now(timezone.utc).isoformat(),
                    "therapy_area": "OrientationRoom",
                    "activity_id": f"task_{i}",
                    "event_type": "hint_requested" if i % 2 == 0 else "instruction_repeated",
                    "payload": {"assistance_source": "user_requested"},
                })
        run(_asgi_request(app, "POST", "/telemetry/events", events))

        # 3. Retrieve recommendations
        status, body = run(_asgi_request(app, "GET", f"/profiles/recommendations/{pat_id}"))
        self.assertEqual(status, 200, body)
        self.assertGreaterEqual(body["count"], 1)
        rec = body["recommendations"][0]
        # Current tier was unassigned (None) on initial onboarding
        self.assertIsNone(rec["current_tier"])
        self.assertEqual(rec["recommended_tier"], "Guided")
        self.assertTrue(rec["is_temporary_baseline"])
        self.assertIn("Temporary baseline heuristic", rec["rationale"])

    # 7. Caregiver override and locked override protection
    def test_07_caregiver_override_and_locked_protection(self):
        pat_id = f"P_LOCK_{uuid.uuid4().hex[:6]}"
        run(_asgi_request(app, "POST", "/profiles/onboard", {"patient_id": pat_id}))

        # Caregiver overrides and locks at Standard
        override = {
            "override_id": f"ovr_{uuid.uuid4().hex[:8]}",
            "patient_id": pat_id,
            "timestamp_utc": datetime.now(timezone.utc).isoformat(),
            "selected_tier": "Standard",
            "lock_tier": True,
            "caregiver_note": "Caregiver explicitly locked tier to Standard.",
        }
        s_ovr, b_ovr = run(_asgi_request(app, "POST", "/profiles/override", override))
        self.assertEqual(s_ovr, 200, b_ovr)
        self.assertTrue(b_ovr["lock_tier"])

        # Even if patient frequently requests hints across 3 sessions
        events = []
        for s_idx in [1, 2, 3]:
            sess_id = f"S_LOCK_{s_idx}"
            for i in range(3):
                events.append({
                    "event_id": f"evt_lock_{s_idx}_{i}_{uuid.uuid4().hex[:6]}",
                    "session_id": sess_id,
                    "patient_id": pat_id,
                    "timestamp_utc": datetime.now(timezone.utc).isoformat(),
                    "therapy_area": "OrientationRoom",
                    "activity_id": f"task_{i}",
                    "event_type": "hint_requested",
                    "payload": {"assistance_source": "user_requested"},
                })
        run(_asgi_request(app, "POST", "/telemetry/events", events))

        # Because caregiver locked the tier, evaluate_baseline_recommendation returns None
        status, body = run(_asgi_request(app, "GET", f"/profiles/recommendations/{pat_id}"))
        self.assertEqual(status, 200, body)
        self.assertEqual(body["count"], 0)

    # 8. Strict decoupling: No automatic mapping from OASIS CDR prediction to assistance tier
    def test_08_no_direct_oasis_to_assistance_tier_mapping(self):
        # A severe OASIS CDR prediction (e.g. CDR 1+) must NOT alter active support profile
        oasis_payload = {"Age": 82, "M/F": "M", "Educ": 1, "MMSE": 14}
        oasis_status, oasis_body = run(_asgi_request(app, "POST", "/predict", oasis_payload))
        self.assertEqual(oasis_status, 200)
        self.assertEqual(oasis_body["predicted_class"], 2)  # CDR 1+

        # Onboard patient
        pat_id = f"P_OASIS_DECOUPLED_{uuid.uuid4().hex[:6]}"
        s_onboard, b_onboard = run(_asgi_request(app, "POST", "/profiles/onboard", {"patient_id": pat_id}))
        self.assertEqual(s_onboard, 201)

        # The support tier is unassigned upon onboarding (None), NEVER automatically set to HighSupport
        self.assertIsNone(b_onboard["profile"]["active_support_tier"])

    # 9. Existing telemetry API regression
    def test_09_telemetry_api_regression(self):
        pat_id = f"P_REGR_{uuid.uuid4().hex[:6]}"
        run(_asgi_request(app, "POST", "/profiles/onboard", {"patient_id": pat_id}))
        s_prof, b_prof = run(_asgi_request(app, "GET", f"/profiles/support/{pat_id}"))
        self.assertEqual(s_prof, 200)
        self.assertEqual(b_prof["profile"]["patient_id"], pat_id)

    # 10. Existing Phase 1 /predict regression
    def test_10_phase1_predict_regression(self):
        payload = {"Age": 74, "M/F": "F", "Educ": 2, "MMSE": 22}
        status, body = run(_asgi_request(app, "POST", "/predict", payload))
        self.assertEqual(status, 200, body)
        self.assertIn("predicted_class", body)
        self.assertEqual(body["model_version"], "oasis-practical-v1")

    # 11. Database persistence and backward compatibility
    def test_11_database_persistence_and_backward_compatibility(self):
        tmp = tempfile.NamedTemporaryFile(suffix=".db", delete=False)
        db_path = tmp.name
        tmp.close()
        try:
            db_inst = Database(db_path)
            # Create a profile without assigned tier (active_support_tier=None)
            p = InitialSupportProfile(
                patient_id="P_PERSIST_TEST",
                active_support_tier=None
            )
            db_inst.save_support_profile(p)
            del db_inst

            # Reopen DB
            db_inst2 = Database(db_path)
            retrieved = db_inst2.get_support_profile("P_PERSIST_TEST")
            self.assertIsNotNone(retrieved)
            self.assertEqual(retrieved.patient_id, "P_PERSIST_TEST")
            self.assertIsNone(retrieved.active_support_tier)
        finally:
            try:
                os.unlink(db_path)
            except Exception:
                pass

    # 12. Architecture Correction Audit: Ambiguous evidence returns no recommendation
    def test_12_ambiguous_evidence_returns_no_recommendation(self):
        pat_id = f"P_AMBIG_{uuid.uuid4().hex[:6]}"
        run(_asgi_request(app, "POST", "/profiles/onboard", {"patient_id": pat_id}))

        # Ingest only 1 or 2 scattered hints (below threshold of 5)
        events = [
            {
                "event_id": f"evt_amb_{uuid.uuid4().hex[:8]}",
                "session_id": "S_AMB_01",
                "patient_id": pat_id,
                "timestamp_utc": datetime.now(timezone.utc).isoformat(),
                "therapy_area": "OrientationRoom",
                "activity_id": "clock_task",
                "event_type": "hint_requested",
                "payload": {"assistance_source": "user_requested"},
            },
            {
                "event_id": f"evt_amb_{uuid.uuid4().hex[:8]}",
                "session_id": "S_AMB_02",
                "patient_id": pat_id,
                "timestamp_utc": datetime.now(timezone.utc).isoformat(),
                "therapy_area": "OrientationRoom",
                "activity_id": "clock_task",
                "event_type": "instruction_repeated",
                "payload": {"assistance_source": "user_requested"},
            },
        ]
        run(_asgi_request(app, "POST", "/telemetry/events", events))

        # Ambiguous/insufficient requests must return count = 0
        status, body = run(_asgi_request(app, "GET", f"/profiles/recommendations/{pat_id}"))
        self.assertEqual(status, 200, body)
        self.assertEqual(body["count"], 0)

    # 13. Architecture Correction Audit: Skipping alone does NOT trigger High Support
    def test_13_skipping_alone_does_not_trigger_high_support(self):
        pat_id = f"P_SKIP_TEST_{uuid.uuid4().hex[:6]}"
        # Start patient on Guided tier via caregiver override
        run(_asgi_request(app, "POST", "/profiles/onboard", {"patient_id": pat_id}))
        override = {
            "override_id": f"ovr_{uuid.uuid4().hex[:8]}",
            "patient_id": pat_id,
            "timestamp_utc": datetime.now(timezone.utc).isoformat(),
            "selected_tier": "Guided",
            "lock_tier": False,
        }
        run(_asgi_request(app, "POST", "/profiles/override", override))

        # Patient skips 6 activities across 2 sessions, but makes NO demonstration requests
        events = []
        for s_idx in [1, 2]:
            sess_id = f"S_SKIP_{s_idx}"
            for i in range(3):
                events.append({
                    "event_id": f"evt_skp_{s_idx}_{i}_{uuid.uuid4().hex[:6]}",
                    "session_id": sess_id,
                    "patient_id": pat_id,
                    "timestamp_utc": datetime.now(timezone.utc).isoformat(),
                    "therapy_area": "OrientationRoom",
                    "activity_id": f"skip_task_{i}",
                    "event_type": "activity_skipped",
                    "payload": {"assistance_source": "user_requested"},
                })
        run(_asgi_request(app, "POST", "/telemetry/events", events))

        # Skipping alone MUST NOT trigger High Support
        status, body = run(_asgi_request(app, "GET", f"/profiles/recommendations/{pat_id}"))
        self.assertEqual(status, 200, body)
        # Verify no HighSupport recommendation was generated solely from skips
        for rec in body.get("recommendations", []):
            self.assertNotEqual(rec["recommended_tier"], "HighSupport")

    # 14. Architecture Correction Audit: Zero assistance requests does NOT automatically reduce support
    def test_14_zero_assistance_requests_does_not_auto_reduce_support(self):
        pat_id = f"P_ZERO_ASSIST_{uuid.uuid4().hex[:6]}"
        # Patient on Guided tier
        run(_asgi_request(app, "POST", "/profiles/onboard", {"patient_id": pat_id}))
        override = {
            "override_id": f"ovr_{uuid.uuid4().hex[:8]}",
            "patient_id": pat_id,
            "timestamp_utc": datetime.now(timezone.utc).isoformat(),
            "selected_tier": "Guided",
            "lock_tier": False,
        }
        run(_asgi_request(app, "POST", "/profiles/override", override))

        # Complete activities across 4 sessions with ZERO assistance requests
        events = []
        for s_idx in range(1, 5):
            sess_id = f"S_ZERO_{s_idx}"
            events.append({
                "event_id": f"evt_zero_{s_idx}_{uuid.uuid4().hex[:6]}",
                "session_id": sess_id,
                "patient_id": pat_id,
                "timestamp_utc": datetime.now(timezone.utc).isoformat(),
                "therapy_area": "OrientationRoom",
                "activity_id": "easy_task",
                "event_type": "activity_completed",
                "payload": {"completion_status": "independent"},
            })
        run(_asgi_request(app, "POST", "/telemetry/events", events))

        # Zero assistance requests must NOT automatically reduce support tier
        status, body = run(_asgi_request(app, "GET", f"/profiles/recommendations/{pat_id}"))
        self.assertEqual(status, 200, body)
        self.assertEqual(body["count"], 0)


if __name__ == "__main__":
    unittest.main(verbosity=2)
