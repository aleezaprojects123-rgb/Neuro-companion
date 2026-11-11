"""
NeuroCompanion — P2A-30 API Contract Freeze Test Suite
=======================================================
Verifies:
 1.  All versioned /api/v1/ routes respond correctly.
 2.  All legacy routes (no prefix) remain functional.
 3.  Both onboarding pathways (MMSE-available + no-MMSE) work via /api/v1/.
 4.  Initial onboarding NEVER silently assigns a support tier (active_support_tier = None).
 5.  Telemetry is persisted and deduplicated via /api/v1/telemetry/events.
 6.  Interaction profiles aggregate correctly via /api/v1/profiles/interaction/.
 7.  Immediate assistance (/api/v1/carl/assistance) is separate from recommendations.
 8.  Caregiver override and lock via /api/v1/profiles/override.
 9.  Phase 1 /predict is unchanged (legacy and versioned).
10.  Health endpoint reports model loaded status.
11.  API version constants are correct in main module.
12.  Error responses match contract specification.
"""

import asyncio
import json
import sys
import uuid
from datetime import datetime, timezone
from pathlib import Path

import unittest

BACKEND_DIR = str(Path(__file__).resolve().parent.parent)
if BACKEND_DIR not in sys.path:
    sys.path.insert(0, BACKEND_DIR)

from main import app, API_VERSION, API_PREFIX  # noqa: E402


# ---------------------------------------------------------------------------
# Shared ASGI test helper (identical to existing test infrastructure)
# ---------------------------------------------------------------------------

async def _asgi_request(application, method: str, path: str, body=None):
    """Send a raw ASGI HTTP request and return (status_code, parsed_body)."""
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


def run(coro):
    return asyncio.run(coro)


# ---------------------------------------------------------------------------
# Test suite
# ---------------------------------------------------------------------------

class TestP2A30APIContractFreeze(unittest.TestCase):
    """P2A-30: Full API contract freeze verification."""

    _lifespan_ctx = None

    @classmethod
    def setUpClass(cls):
        """Start FastAPI lifespan (loads ML model) once for the whole suite."""
        async def _startup():
            ctx = app.router.lifespan_context(app)
            await ctx.__aenter__()
            return ctx
        cls._lifespan_ctx = asyncio.run(_startup())

    @classmethod
    def tearDownClass(cls):
        """Shut down FastAPI lifespan cleanly."""
        if cls._lifespan_ctx is not None:
            async def _shutdown():
                await cls._lifespan_ctx.__aexit__(None, None, None)
            try:
                asyncio.run(_shutdown())
            except Exception:
                pass

    # ------------------------------------------------------------------
    # 1. API version constants
    # ------------------------------------------------------------------

    def test_01_api_version_constants(self):
        """API_VERSION is 'v1' and API_PREFIX is '/api/v1'."""
        self.assertEqual(API_VERSION, "v1")
        self.assertEqual(API_PREFIX, "/api/v1")

    # ------------------------------------------------------------------
    # 2. Health endpoint — legacy and versioned
    # ------------------------------------------------------------------

    def test_02a_health_legacy(self):
        """GET /health — legacy route returns 200 with model_loaded."""
        status, body = run(_asgi_request(app, "GET", "/health"))
        self.assertEqual(status, 200, body)
        self.assertEqual(body["status"], "ok")
        self.assertIn("model_loaded", body)
        self.assertIn("model_version", body)

    def test_02b_health_versioned(self):
        """GET /api/v1/health — versioned route returns identical response."""
        s_legacy, b_legacy = run(_asgi_request(app, "GET", "/health"))
        s_v1, b_v1 = run(_asgi_request(app, "GET", "/api/v1/health"))
        self.assertEqual(s_v1, 200, b_v1)
        self.assertEqual(b_v1["status"], b_legacy["status"])
        self.assertEqual(b_v1["model_loaded"], b_legacy["model_loaded"])
        self.assertEqual(b_v1["model_version"], b_legacy["model_version"])

    def test_02c_health_model_loaded(self):
        """model_loaded is True after lifespan startup."""
        status, body = run(_asgi_request(app, "GET", "/api/v1/health"))
        self.assertTrue(body["model_loaded"], "Model must be loaded after lifespan startup")

    # ------------------------------------------------------------------
    # 3. Phase 1 /predict — legacy and versioned unchanged
    # ------------------------------------------------------------------

    def test_03a_predict_legacy_unchanged(self):
        """POST /predict — legacy route returns predicted_class, label, probabilities."""
        payload = {"Age": 74.0, "M/F": "F", "Educ": 2.0, "MMSE": 22.0}
        status, body = run(_asgi_request(app, "POST", "/predict", payload))
        self.assertEqual(status, 200, body)
        self.assertIn("predicted_class", body)
        self.assertIn("predicted_label", body)
        self.assertIn("class_probabilities", body)
        self.assertEqual(body["model_version"], "oasis-practical-v1")
        self.assertIn("disclaimer", body)

    def test_03b_predict_versioned(self):
        """POST /api/v1/predict — versioned route returns identical response."""
        payload = {"Age": 74.0, "M/F": "F", "Educ": 2.0, "MMSE": 22.0}
        s_leg, b_leg = run(_asgi_request(app, "POST", "/predict", payload))
        s_v1, b_v1 = run(_asgi_request(app, "POST", "/api/v1/predict", payload))
        self.assertEqual(s_v1, 200, b_v1)
        self.assertEqual(b_v1["predicted_class"], b_leg["predicted_class"])
        self.assertEqual(b_v1["model_version"], b_leg["model_version"])
        self.assertAlmostEqual(
            b_v1["class_probabilities"]["class_0"],
            b_leg["class_probabilities"]["class_0"],
            places=4,
        )

    def test_03c_predict_no_tier_mapping(self):
        """Prediction result must NOT contain any support tier field."""
        payload = {"Age": 82.0, "M/F": "M", "Educ": 1.0, "MMSE": 14.0}
        _, body = run(_asgi_request(app, "POST", "/api/v1/predict", payload))
        # Predicted class 2 (CDR 1+) — confirm no tier is in the response
        self.assertEqual(body["predicted_class"], 2)
        for forbidden in ("support_tier", "active_support_tier", "tier", "HighSupport"):
            self.assertNotIn(forbidden, body, f"Predict response must not contain '{forbidden}'")

    def test_03d_predict_invalid_input_422(self):
        """POST /api/v1/predict with out-of-range MMSE returns 422."""
        payload = {"Age": 74.0, "M/F": "F", "Educ": 2.0, "MMSE": 99.0}  # MMSE > 30
        status, body = run(_asgi_request(app, "POST", "/api/v1/predict", payload))
        self.assertEqual(status, 422, body)

    # ------------------------------------------------------------------
    # 4. Onboarding — versioned and legacy, both pathways
    # ------------------------------------------------------------------

    def test_04a_onboard_no_mmse_pathway_versioned(self):
        """POST /api/v1/profiles/onboard — no-MMSE pathway creates profile with tier=None."""
        pat_id = f"P_V1_NOMMSE_{uuid.uuid4().hex[:6]}"
        payload = {"patient_id": pat_id}
        status, body = run(_asgi_request(app, "POST", "/api/v1/profiles/onboard", payload))
        self.assertEqual(status, 201, body)
        self.assertEqual(body["status"], "success")
        prof = body["profile"]
        self.assertEqual(prof["patient_id"], pat_id)
        # Contract guarantee: active_support_tier MUST be null on initial onboard
        self.assertIsNone(prof["active_support_tier"],
                          "active_support_tier MUST be null after initial onboarding")

    def test_04b_onboard_no_mmse_pathway_legacy(self):
        """POST /profiles/onboard — legacy route still works, tier=None."""
        pat_id = f"P_LEG_NOMMSE_{uuid.uuid4().hex[:6]}"
        status, body = run(_asgi_request(app, "POST", "/profiles/onboard", {"patient_id": pat_id}))
        self.assertEqual(status, 201, body)
        self.assertIsNone(body["profile"]["active_support_tier"])

    def test_04c_onboard_mmse_available_pathway(self):
        """MMSE-available pathway: predict first, then onboard — tier still None after onboard."""
        # Step 1: Assessor runs OASIS predict (MMSE available)
        oasis_status, oasis_body = run(_asgi_request(
            app, "POST", "/api/v1/predict",
            {"Age": 78.0, "M/F": "M", "Educ": 3.0, "MMSE": 24.0}
        ))
        self.assertEqual(oasis_status, 200, oasis_body)
        self.assertIn("predicted_class", oasis_body)
        # Step 2: Onboard — NEVER pass predict result to onboard; tier must still be None
        pat_id = f"P_V1_MMSE_{uuid.uuid4().hex[:6]}"
        s_on, b_on = run(_asgi_request(app, "POST", "/api/v1/profiles/onboard",
                                        {"patient_id": pat_id}))
        self.assertEqual(s_on, 201, b_on)
        self.assertIsNone(b_on["profile"]["active_support_tier"],
                          "Onboarding must not auto-assign tier even when MMSE is available")

    def test_04d_onboard_with_caregiver_prefs_versioned(self):
        """POST /api/v1/profiles/onboard with caregiver_preferences persists preferences."""
        pat_id = f"P_V1_PREFS_{uuid.uuid4().hex[:6]}"
        payload = {
            "patient_id": pat_id,
            "caregiver_preferences": {
                "preferred_therapy_areas": ["CognitiveGarden"],
                "max_session_duration_minutes": 20,
                "allow_adaptive_recommendations": True,
            },
            "onboarding_notes": "Uses hearing aid",
        }
        status, body = run(_asgi_request(app, "POST", "/api/v1/profiles/onboard", payload))
        self.assertEqual(status, 201, body)
        self.assertEqual(body["profile"]["onboarding_notes"], "Uses hearing aid")
        self.assertIsNone(body["profile"]["active_support_tier"])

    def test_04e_onboard_accessible_carl_defaults(self):
        """Initial Carl config has concise instructions and auto-hints enabled."""
        pat_id = f"P_V1_CARL_{uuid.uuid4().hex[:6]}"
        status, body = run(_asgi_request(app, "POST", "/api/v1/profiles/onboard",
                                          {"patient_id": pat_id}))
        self.assertEqual(status, 201, body)
        carl = body["profile"]["carl_config"]
        self.assertEqual(carl["instruction_style"], "concise")
        self.assertTrue(carl["allow_auto_hints"])
        self.assertGreater(carl["hesitation_threshold_seconds"], 0)

    # ------------------------------------------------------------------
    # 5. Telemetry — versioned and legacy, persistence and deduplication
    # ------------------------------------------------------------------

    def _make_event(self, patient_id: str, session_id: str, area: str = "OrientationRoom",
                    event_type: str = "hint_requested") -> dict:
        return {
            "event_id": f"evt_{uuid.uuid4().hex}",
            "session_id": session_id,
            "patient_id": patient_id,
            "timestamp_utc": datetime.now(timezone.utc).isoformat(),
            "therapy_area": area,
            "activity_id": "contract_test_activity",
            "event_type": event_type,
            "payload": {"assistance_source": "user_requested"},
        }

    def test_05a_telemetry_ingest_versioned(self):
        """POST /api/v1/telemetry/events — events are accepted and persisted."""
        pat_id = f"P_TEL_V1_{uuid.uuid4().hex[:6]}"
        events = [self._make_event(pat_id, "S1") for _ in range(3)]
        status, body = run(_asgi_request(app, "POST", "/api/v1/telemetry/events", events))
        self.assertEqual(status, 201, body)
        self.assertEqual(body["received_count"], 3)
        self.assertEqual(body["inserted_count"], 3)
        self.assertEqual(body["skipped_duplicates_count"], 0)

    def test_05b_telemetry_ingest_legacy(self):
        """POST /telemetry/events — legacy route still works."""
        pat_id = f"P_TEL_LEG_{uuid.uuid4().hex[:6]}"
        events = [self._make_event(pat_id, "S1")]
        status, body = run(_asgi_request(app, "POST", "/telemetry/events", events))
        self.assertEqual(status, 201, body)
        self.assertEqual(body["inserted_count"], 1)

    def test_05c_telemetry_deduplication(self):
        """Duplicate event_id values are skipped safely (idempotent)."""
        pat_id = f"P_TEL_DEDUP_{uuid.uuid4().hex[:6]}"
        event = self._make_event(pat_id, "S1")
        # First insertion
        s1, b1 = run(_asgi_request(app, "POST", "/api/v1/telemetry/events", [event]))
        self.assertEqual(b1["inserted_count"], 1)
        # Second insertion — same event_id
        s2, b2 = run(_asgi_request(app, "POST", "/api/v1/telemetry/events", [event]))
        self.assertEqual(b2["inserted_count"], 0)
        self.assertEqual(b2["skipped_duplicates_count"], 1)

    def test_05d_telemetry_empty_array_400(self):
        """POST /api/v1/telemetry/events with empty array returns 400."""
        status, body = run(_asgi_request(app, "POST", "/api/v1/telemetry/events", []))
        self.assertEqual(status, 400, body)

    # ------------------------------------------------------------------
    # 6. Interaction profile aggregation — versioned
    # ------------------------------------------------------------------

    def test_06a_interaction_profile_aggregates_correctly(self):
        """GET /api/v1/profiles/interaction/{patient_id} aggregates area metrics."""
        pat_id = f"P_INTER_{uuid.uuid4().hex[:6]}"
        sess = f"S_INTER_{uuid.uuid4().hex[:6]}"
        events = [
            self._make_event(pat_id, sess, "OrientationRoom", "hint_requested"),
            self._make_event(pat_id, sess, "OrientationRoom", "instruction_repeated"),
            self._make_event(pat_id, sess, "CognitiveGarden", "demonstration_requested"),
            self._make_event(pat_id, sess, "ReminiscenceCorner", "activity_completed"),
        ]
        run(_asgi_request(app, "POST", "/api/v1/telemetry/events", events))

        status, body = run(_asgi_request(app, "GET", f"/api/v1/profiles/interaction/{pat_id}"))
        self.assertEqual(status, 200, body)
        self.assertEqual(body["status"], "success")
        prof = body["profile"]
        self.assertEqual(prof["patient_id"], pat_id)
        self.assertIn("area_summaries", prof)
        self.assertIn("OrientationRoom", prof["area_summaries"])
        self.assertIn("CognitiveGarden", prof["area_summaries"])
        self.assertIn("ReminiscenceCorner", prof["area_summaries"])
        # Verify counts
        self.assertEqual(prof["area_summaries"]["OrientationRoom"]["hints_requested_total"], 1)
        self.assertEqual(prof["area_summaries"]["OrientationRoom"]["instructions_repeated_total"], 1)
        self.assertEqual(prof["area_summaries"]["CognitiveGarden"]["demonstrations_requested_total"], 1)

    def test_06b_interaction_profile_legacy(self):
        """GET /profiles/interaction/{patient_id} — legacy route works."""
        pat_id = f"P_INTER_LEG_{uuid.uuid4().hex[:6]}"
        events = [self._make_event(pat_id, "SL")]
        run(_asgi_request(app, "POST", "/telemetry/events", events))
        status, body = run(_asgi_request(app, "GET", f"/profiles/interaction/{pat_id}"))
        self.assertEqual(status, 200, body)

    def test_06c_interaction_profile_404_no_events(self):
        """GET /api/v1/profiles/interaction for unknown patient returns 404."""
        status, body = run(_asgi_request(app, "GET",
                                          f"/api/v1/profiles/interaction/P_NONEXISTENT_{uuid.uuid4().hex[:6]}"))
        self.assertEqual(status, 404, body)

    # ------------------------------------------------------------------
    # 7. Immediate Carl assistance — versioned and legacy, decoupled from recommendations
    # ------------------------------------------------------------------

    def test_07a_carl_assistance_all_action_types_versioned(self):
        """POST /api/v1/carl/assistance — all 6 action types return correct carl_state."""
        actions = [
            ("repeat_instruction", "repeating"),
            ("step_by_step_breakdown", "guiding_step"),
            ("visual_demonstration", "demonstrating"),
            ("show_hint", "hinting"),
            ("slow_pacing", "relaxed_pace"),
            ("pause_or_skip", "offering_break"),
        ]
        for action_type, expected_state in actions:
            with self.subTest(action_type=action_type):
                req = {
                    "patient_id": "P_CARL_V1",
                    "session_id": "S_CARL_V1",
                    "therapy_area": "OrientationRoom",
                    "activity_id": "contract_test",
                    "action_type": action_type,
                    "step_instruction": "Look at the clock hand.",
                }
                status, body = run(_asgi_request(app, "POST", "/api/v1/carl/assistance", req))
                self.assertEqual(status, 200, body)
                self.assertEqual(body["action_type"], action_type)
                self.assertEqual(body["carl_state"], expected_state)
                self.assertTrue(body["allow_skip"])
                self.assertIsNotNone(body["spoken_prompt"])
                self.assertGreater(len(body["spoken_prompt"]), 0)

    def test_07b_carl_assistance_legacy(self):
        """POST /carl/assistance — legacy route works."""
        req = {
            "patient_id": "P_CARL_LEG",
            "session_id": "S_CARL_LEG",
            "therapy_area": "CognitiveGarden",
            "activity_id": "legacy_test",
            "action_type": "show_hint",
        }
        status, body = run(_asgi_request(app, "POST", "/carl/assistance", req))
        self.assertEqual(status, 200, body)
        self.assertEqual(body["carl_state"], "hinting")

    def test_07c_carl_assistance_unknown_action_returns_reassurance(self):
        """POST /api/v1/carl/assistance with unknown action_type returns general_reassurance."""
        req = {
            "patient_id": "P_CARL_UNK",
            "session_id": "S_CARL_UNK",
            "therapy_area": "ReminiscenceCorner",
            "activity_id": "unk_test",
            "action_type": "do_something_unknown",
        }
        status, body = run(_asgi_request(app, "POST", "/api/v1/carl/assistance", req))
        self.assertEqual(status, 200, body)
        self.assertEqual(body["action_type"], "general_reassurance")
        self.assertEqual(body["carl_state"], "comforting")

    def test_07d_carl_assistance_separate_from_recommendations(self):
        """Carl assistance endpoint does NOT create a persistent recommendation."""
        pat_id = f"P_CARL_SEP_{uuid.uuid4().hex[:6]}"
        run(_asgi_request(app, "POST", "/api/v1/profiles/onboard", {"patient_id": pat_id}))
        # Fire 10 assistance requests
        for _ in range(10):
            run(_asgi_request(app, "POST", "/api/v1/carl/assistance", {
                "patient_id": pat_id,
                "session_id": "S_SEP",
                "therapy_area": "OrientationRoom",
                "activity_id": "sep_test",
                "action_type": "show_hint",
            }))
        # Recommendations endpoint must still return 0 (no telemetry events were ingested)
        status, body = run(_asgi_request(app, "GET", f"/api/v1/profiles/recommendations/{pat_id}"))
        self.assertEqual(status, 200, body)
        self.assertEqual(body["count"], 0,
                         "Carl assistance calls must NOT create support recommendations")

    # ------------------------------------------------------------------
    # 8. Caregiver override and lock — versioned and legacy
    # ------------------------------------------------------------------

    def _make_override(self, patient_id: str, tier: str, lock: bool = False) -> dict:
        return {
            "override_id": f"ovr_{uuid.uuid4().hex}",
            "patient_id": patient_id,
            "timestamp_utc": datetime.now(timezone.utc).isoformat(),
            "selected_tier": tier,
            "lock_tier": lock,
            "caregiver_note": f"Contract test: set {tier}, lock={lock}",
        }

    def test_08a_caregiver_override_versioned(self):
        """POST /api/v1/profiles/override — sets tier, returns active_tier."""
        pat_id = f"P_OVR_V1_{uuid.uuid4().hex[:6]}"
        run(_asgi_request(app, "POST", "/api/v1/profiles/onboard", {"patient_id": pat_id}))
        ovr = self._make_override(pat_id, "Guided", lock=False)
        status, body = run(_asgi_request(app, "POST", "/api/v1/profiles/override", ovr))
        self.assertEqual(status, 200, body)
        self.assertEqual(body["active_tier"], "Guided")
        self.assertFalse(body["lock_tier"])

    def test_08b_caregiver_override_legacy(self):
        """POST /profiles/override — legacy route still works."""
        pat_id = f"P_OVR_LEG_{uuid.uuid4().hex[:6]}"
        run(_asgi_request(app, "POST", "/profiles/onboard", {"patient_id": pat_id}))
        ovr = self._make_override(pat_id, "Standard")
        status, body = run(_asgi_request(app, "POST", "/profiles/override", ovr))
        self.assertEqual(status, 200, body)
        self.assertEqual(body["active_tier"], "Standard")

    def test_08c_caregiver_lock_suppresses_recommendations(self):
        """lock_tier=True silences all future recommendations."""
        pat_id = f"P_LOCK_V1_{uuid.uuid4().hex[:6]}"
        run(_asgi_request(app, "POST", "/api/v1/profiles/onboard", {"patient_id": pat_id}))
        # Lock at Standard
        ovr = self._make_override(pat_id, "Standard", lock=True)
        s_ovr, b_ovr = run(_asgi_request(app, "POST", "/api/v1/profiles/override", ovr))
        self.assertTrue(b_ovr["lock_tier"])
        # Ingest many hint events (more than the threshold of 5 across 2 sessions)
        events = []
        for s_idx in [1, 2]:
            for i in range(4):
                events.append(self._make_event(pat_id, f"S_LOCK_{s_idx}",
                                               "OrientationRoom", "hint_requested"))
        run(_asgi_request(app, "POST", "/api/v1/telemetry/events", events))
        # Recommendations must return 0 because tier is locked
        status, body = run(_asgi_request(app, "GET",
                                          f"/api/v1/profiles/recommendations/{pat_id}"))
        self.assertEqual(status, 200, body)
        self.assertEqual(body["count"], 0, "Locked tier must suppress all recommendations")

    def test_08d_support_profile_get_versioned(self):
        """GET /api/v1/profiles/support/{patient_id} — returns active profile."""
        pat_id = f"P_GETPROF_V1_{uuid.uuid4().hex[:6]}"
        run(_asgi_request(app, "POST", "/api/v1/profiles/onboard", {"patient_id": pat_id}))
        status, body = run(_asgi_request(app, "GET", f"/api/v1/profiles/support/{pat_id}"))
        self.assertEqual(status, 200, body)
        self.assertEqual(body["profile"]["patient_id"], pat_id)

    def test_08e_support_profile_get_legacy(self):
        """GET /profiles/support/{patient_id} — legacy route works."""
        pat_id = f"P_GETPROF_LEG_{uuid.uuid4().hex[:6]}"
        run(_asgi_request(app, "POST", "/profiles/onboard", {"patient_id": pat_id}))
        status, body = run(_asgi_request(app, "GET", f"/profiles/support/{pat_id}"))
        self.assertEqual(status, 200, body)

    def test_08f_support_profile_404_unknown(self):
        """GET /api/v1/profiles/support for unknown patient returns 404."""
        status, _ = run(_asgi_request(app, "GET",
                                       f"/api/v1/profiles/support/P_GHOST_{uuid.uuid4().hex[:6]}"))
        self.assertEqual(status, 404)

    # ------------------------------------------------------------------
    # 9. Recommendations — versioned and legacy
    # ------------------------------------------------------------------

    def test_09a_recommendations_versioned_sufficient_evidence(self):
        """GET /api/v1/profiles/recommendations — returns Guided when >=5 explicit requests."""
        pat_id = f"P_RECV1_{uuid.uuid4().hex[:6]}"
        run(_asgi_request(app, "POST", "/api/v1/profiles/onboard", {"patient_id": pat_id}))
        events = []
        for s_idx in [1, 2]:
            for i in range(3):
                events.append(self._make_event(pat_id, f"S_REC_{s_idx}",
                                               "OrientationRoom",
                                               "hint_requested" if i < 2 else "instruction_repeated"))
        run(_asgi_request(app, "POST", "/api/v1/telemetry/events", events))
        status, body = run(_asgi_request(app, "GET", f"/api/v1/profiles/recommendations/{pat_id}"))
        self.assertEqual(status, 200, body)
        self.assertGreaterEqual(body["count"], 1)
        rec = body["recommendations"][0]
        self.assertIsNone(rec["current_tier"])
        self.assertEqual(rec["recommended_tier"], "Guided")
        self.assertTrue(rec["is_temporary_baseline"])

    def test_09b_recommendations_legacy_route(self):
        """GET /profiles/recommendations — legacy route works."""
        pat_id = f"P_RECLEG_{uuid.uuid4().hex[:6]}"
        run(_asgi_request(app, "POST", "/profiles/onboard", {"patient_id": pat_id}))
        status, body = run(_asgi_request(app, "GET", f"/profiles/recommendations/{pat_id}"))
        self.assertEqual(status, 200, body)
        self.assertIn("recommendations", body)

    def test_09c_recommendations_insufficient_evidence_returns_zero(self):
        """GET /api/v1/profiles/recommendations — returns count=0 for insufficient evidence."""
        pat_id = f"P_INSUF_{uuid.uuid4().hex[:6]}"
        run(_asgi_request(app, "POST", "/api/v1/profiles/onboard", {"patient_id": pat_id}))
        # Only 2 hints total (< 5 threshold)
        events = [
            self._make_event(pat_id, "S1", "OrientationRoom", "hint_requested"),
            self._make_event(pat_id, "S2", "OrientationRoom", "hint_requested"),
        ]
        run(_asgi_request(app, "POST", "/api/v1/telemetry/events", events))
        status, body = run(_asgi_request(app, "GET", f"/api/v1/profiles/recommendations/{pat_id}"))
        self.assertEqual(status, 200, body)
        self.assertEqual(body["count"], 0)

    # ------------------------------------------------------------------
    # 10. Lifespan and shutdown clean (no crash on teardown)
    # ------------------------------------------------------------------

    def test_10_lifespan_model_is_loaded(self):
        """After lifespan startup, is_model_loaded() returns True."""
        from model.predict import is_model_loaded
        self.assertTrue(is_model_loaded(), "Model must be loaded after lifespan startup")

    # ------------------------------------------------------------------
    # 11. Edge cases and error contract
    # ------------------------------------------------------------------

    def test_11a_onboard_empty_patient_id_422(self):
        """POST /api/v1/profiles/onboard with empty patient_id returns 422."""
        status, body = run(_asgi_request(app, "POST", "/api/v1/profiles/onboard",
                                          {"patient_id": ""}))
        self.assertEqual(status, 422, body)

    def test_11b_predict_missing_field_422(self):
        """POST /api/v1/predict missing MMSE returns 422."""
        status, body = run(_asgi_request(app, "POST", "/api/v1/predict",
                                          {"Age": 74.0, "M/F": "F", "Educ": 2.0}))
        self.assertEqual(status, 422, body)

    def test_11c_predict_invalid_sex_422(self):
        """POST /api/v1/predict with invalid M/F value returns 422."""
        status, body = run(_asgi_request(app, "POST", "/api/v1/predict",
                                          {"Age": 74.0, "M/F": "X", "Educ": 2.0, "MMSE": 22.0}))
        self.assertEqual(status, 422, body)

    def test_11d_telemetry_invalid_event_type_422(self):
        """POST /api/v1/telemetry/events with invalid event_type returns 422."""
        event = self._make_event("P_INVALID", "S1")
        event["event_type"] = "not_a_real_event"
        status, body = run(_asgi_request(app, "POST", "/api/v1/telemetry/events", [event]))
        self.assertEqual(status, 422, body)

    def test_11e_telemetry_invalid_therapy_area_422(self):
        """POST /api/v1/telemetry/events with invalid therapy_area returns 422."""
        event = self._make_event("P_INVALID", "S1")
        event["therapy_area"] = "MysteryRoom"
        status, body = run(_asgi_request(app, "POST", "/api/v1/telemetry/events", [event]))
        self.assertEqual(status, 422, body)


if __name__ == "__main__":
    unittest.main(verbosity=2)
