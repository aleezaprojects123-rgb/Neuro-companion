"""
NeuroCompanion — Step 3.1 Backend Integration Audit
====================================================
Full HTTP-level integration tests using the pure ASGI async pattern.
(httpx / starlette.testclient are NOT available in this venv.)

Tests covered
─────────────
 1  POST /telemetry/events          → 201, correct inserted/skipped counts
 2  POST /telemetry/events          → 201, duplicate event_ids skipped
 3  POST /telemetry/events          → 422, invalid schema (empty event_id)
 4  POST /telemetry/events          → 400, empty list body
 5  POST /profiles/support          → 201, without MMSE field
 6  GET  /profiles/support/{id}     → 200, existing patient
 7  GET  /profiles/support/{id}     → 404, unknown patient
 8  GET  /profiles/interaction/{id} → 200, computed from ingested events
 9  GET  /profiles/interaction/{id} → 404, patient with no events
10  GET  /profiles/recommendations  → 200, may be empty list
11  POST /profiles/override         → 200, tier updated correctly
12  POST /predict (Phase 1)         → 200, regression guard

Persistence tests
─────────────────
13  Events survive DB reconnection
14  Support profile is_active flag survives restart
15  Duplicate event_id does not duplicate on second insert
16  Caregiver lock flag persists across reconnection

Interaction aggregation unit test
──────────────────────────────────
17  compute_interaction_profile_from_events produces correct counts
"""

import asyncio
import json
import os
import sys
import tempfile
import unittest
from datetime import datetime, timezone

# ── Path setup ────────────────────────────────────────────────────────────────
BACKEND_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if BACKEND_DIR not in sys.path:
    sys.path.insert(0, BACKEND_DIR)

# ── App import (triggers DB init on global `db`) ──────────────────────────────
from main import app  # noqa: E402  (must come after sys.path setup)
from telemetry.database import Database
from telemetry.schemas import (
    AssistanceSource,
    CompletionStatus,
    EventType,
    InteractionEvent,
    InteractionPayload,
    SupportTier,
    TherapyArea,
)
from telemetry.aggregation import compute_interaction_profile_from_events


# ══════════════════════════════════════════════════════════════════════════════
# ASGI helper — works without httpx
# ══════════════════════════════════════════════════════════════════════════════

async def _trigger_lifespan_startup(application):
    """
    Send lifespan.startup to a Starlette/FastAPI ASGI app so that the
    @asynccontextmanager lifespan (which loads the ML pipeline) executes
    before the first HTTP request is processed.
    """
    startup_complete = asyncio.Event()
    shutdown_complete = asyncio.Event()

    startup_exc: list = []
    messages_to_app: asyncio.Queue = asyncio.Queue()
    messages_from_app: asyncio.Queue = asyncio.Queue()

    async def receive():
        return await messages_to_app.get()

    async def send(message):
        await messages_from_app.put(message)

    # Kick off the lifespan task
    lifespan_task = asyncio.ensure_future(
        application({"type": "lifespan", "asgi": {"version": "3.0"}}, receive, send)
    )

    # Send startup
    await messages_to_app.put({"type": "lifespan.startup"})

    # Wait for startup.complete or startup.failed
    while True:
        msg = await messages_from_app.get()
        if msg["type"] == "lifespan.startup.complete":
            break
        elif msg["type"] == "lifespan.startup.failed":
            startup_exc.append(msg.get("message", "startup failed"))
            break

    if startup_exc:
        raise RuntimeError(f"Lifespan startup failed: {startup_exc[0]}")

    return lifespan_task, messages_to_app, messages_from_app


async def _asgi_request(
    application,
    method: str,
    path: str,
    body=None,
    headers: dict | None = None,
):
    """
    Send a single HTTP request to a Starlette/FastAPI ASGI app and return
    (status_code: int, body: dict | str).
    """
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
    """Run a coroutine synchronously (Python 3.10+ compatible)."""
    return asyncio.run(coro)

# ══════════════════════════════════════════════════════════════════════════════
# Reusable event factories
# ══════════════════════════════════════════════════════════════════════════════

PATIENT = "P_AUDIT_001"
SESSION = "S_AUDIT_001"


def _make_event_payload(
    event_id: str,
    event_type: str = "hint_requested",
    therapy_area: str = "OrientationRoom",
    activity_id: str = "ACT_001",
    patient_id: str = PATIENT,
    session_id: str = SESSION,
):
    return {
        "event_id": event_id,
        "session_id": session_id,
        "patient_id": patient_id,
        "timestamp_utc": datetime.now(timezone.utc).isoformat(),
        "therapy_area": therapy_area,
        "activity_id": activity_id,
        "event_type": event_type,
        "payload": {
            "completion_status": None,
            "latency_seconds": 2.5,
            "attempt_number": 1,
            "hint_level": 1,
            "assistance_source": "user_requested",
        },
    }


# ══════════════════════════════════════════════════════════════════════════════
# Test class
# ══════════════════════════════════════════════════════════════════════════════

class TestStep31Audit(unittest.TestCase):
    """
    HTTP-level integration tests via pure ASGI (no httpx).
    The app's lifespan is triggered in setUpClass so the OASIS pipeline
    is loaded before any HTTP requests are issued.
    """

    _lifespan_ctx = None

    @classmethod
    def setUpClass(cls):
        """Trigger the FastAPI lifespan (loads the OASIS pipeline)."""
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

    # ── 1. Telemetry ingestion — basic ────────────────────────────────────────
    def test_01_ingest_events_success(self):
        events = [
            _make_event_payload("EVT_A1_01", event_type="hint_requested"),
            _make_event_payload("EVT_A1_02", event_type="activity_completed"),
        ]
        status, body = run(_asgi_request(app, "POST", "/telemetry/events", events))
        self.assertEqual(status, 201, body)
        self.assertEqual(body["received_count"], 2)
        self.assertGreaterEqual(body["inserted_count"], 0)
        self.assertEqual(
            body["inserted_count"] + body["skipped_duplicates_count"],
            2
        )

    # ── 2. Duplicate event IDs are skipped ───────────────────────────────────
    def test_02_duplicate_events_skipped(self):
        # First insert
        events = [_make_event_payload("EVT_DUP_01")]
        run(_asgi_request(app, "POST", "/telemetry/events", events))
        # Second insert — same event_id
        status, body = run(_asgi_request(app, "POST", "/telemetry/events", events))
        self.assertEqual(status, 201, body)
        self.assertEqual(body["skipped_duplicates_count"], 1)
        self.assertEqual(body["inserted_count"], 0)

    # ── 3. Invalid schema → 422 ───────────────────────────────────────────────
    def test_03_invalid_event_422(self):
        bad = [{"event_id": "", "session_id": "S1"}]  # missing required fields
        status, body = run(_asgi_request(app, "POST", "/telemetry/events", bad))
        self.assertEqual(status, 422, body)

    # ── 4. Empty list → 400 ───────────────────────────────────────────────────
    def test_04_empty_event_list_400(self):
        status, body = run(_asgi_request(app, "POST", "/telemetry/events", []))
        self.assertEqual(status, 400, body)

    # ── 5. Create support profile WITHOUT MMSE ────────────────────────────────
    def test_05_create_support_profile_no_mmse(self):
        profile = {
            "patient_id": PATIENT,
            "active_support_tier": "Standard",   # exact enum value
            "carl_config": {
                "instruction_style": "concise",
                "allow_auto_hints": True,
                "hesitation_threshold_seconds": 30.0,
                "demonstrations_enabled": False,
                "speech_rate_multiplier": 1.0,
            },
            "caregiver_preferences": {
                "allow_adaptive_recommendations": True,
                "notify_on_tier_change": True,
            },
            "onboarding_notes": "Audit test — no MMSE provided",
        }
        status, body = run(_asgi_request(app, "POST", "/profiles/support", profile))
        self.assertEqual(status, 201, body)
        self.assertEqual(body["profile"]["patient_id"], PATIENT)
        # MMSE must not appear as a required or returned field
        self.assertNotIn("mmse_score", body["profile"])

    # ── 6. Retrieve existing support profile → 200 ───────────────────────────
    def test_06_get_support_profile_existing(self):
        # Ensure profile exists first (idempotent re-create with minimal valid body)
        run(_asgi_request(app, "POST", "/profiles/support", {
            "patient_id": PATIENT,
            "active_support_tier": "Standard",
        }))
        status, body = run(_asgi_request(app, "GET", f"/profiles/support/{PATIENT}"))
        self.assertEqual(status, 200, body)
        self.assertEqual(body["profile"]["patient_id"], PATIENT)

    # ── 7. Retrieve unknown patient support profile → 404 ────────────────────
    def test_07_get_support_profile_missing(self):
        status, body = run(_asgi_request(app, "GET", "/profiles/support/P_DOES_NOT_EXIST"))
        self.assertEqual(status, 404, body)

    # ── 8. Interaction profile computed from events → 200 ────────────────────
    def test_08_interaction_profile_computed_from_events(self):
        # Seed deterministic events for this patient in a unique session
        patient = "P_AUDIT_INTERACT"
        session = "S_INTER_01"
        events = [
            _make_event_payload("EVT_INT_01", "hint_requested",       patient_id=patient, session_id=session),
            _make_event_payload("EVT_INT_02", "activity_completed",   patient_id=patient, session_id=session),
            _make_event_payload("EVT_INT_03", "instruction_repeated", patient_id=patient, session_id=session),
            _make_event_payload("EVT_INT_04", "activity_skipped",     patient_id=patient, session_id=session, activity_id="ACT_002"),
        ]
        s, b = run(_asgi_request(app, "POST", "/telemetry/events", events))
        self.assertEqual(s, 201, b)

        status, body = run(_asgi_request(app, "GET", f"/profiles/interaction/{patient}"))
        self.assertEqual(status, 200, body)
        p = body["profile"]
        self.assertEqual(p["patient_id"], patient)
        self.assertEqual(p["total_sessions_recorded"], 1)
        # Area summaries must be present
        self.assertIn("area_summaries", p)
        # OrientationRoom should have hint, repeat and skip counts
        or_summary = p["area_summaries"].get("OrientationRoom", {})
        self.assertGreaterEqual(or_summary.get("hints_requested_total", 0), 1)
        self.assertGreaterEqual(or_summary.get("instructions_repeated_total", 0), 1)
        self.assertGreaterEqual(or_summary.get("skips_total", 0), 1)

    # ── 9. Interaction profile for patient with no events → 404 ──────────────
    def test_09_interaction_profile_no_events(self):
        status, body = run(_asgi_request(app, "GET", "/profiles/interaction/P_NO_EVENTS_EVER"))
        self.assertEqual(status, 404, body)

    # ── 10. Recommendations list (may be empty) → 200 ────────────────────────
    def test_10_recommendations_list(self):
        status, body = run(_asgi_request(app, "GET", f"/profiles/recommendations/{PATIENT}"))
        self.assertEqual(status, 200, body)
        self.assertIn("recommendations", body)
        self.assertIsInstance(body["recommendations"], list)

    # ── 11. Caregiver override updates tier ───────────────────────────────────
    def test_11_caregiver_override(self):
        # Ensure the patient has a profile first
        run(_asgi_request(app, "POST", "/profiles/support", {
            "patient_id": PATIENT,
            "active_support_tier": "Standard",
        }))
        import uuid
        override = {
            "override_id": f"OVR_AUDIT_{uuid.uuid4().hex[:8]}",  # unique per run
            "patient_id": PATIENT,
            "timestamp_utc": datetime.now(timezone.utc).isoformat(),
            "selected_tier": "Guided",    # exact enum value
            "lock_tier": True,
            "caregiver_note": "Audit override test",
        }
        status, body = run(_asgi_request(app, "POST", "/profiles/override", override))
        self.assertEqual(status, 200, body)
        self.assertEqual(body["active_tier"], "Guided")
        self.assertTrue(body["lock_tier"])

        # Verify the profile actually reflects the new tier
        s2, b2 = run(_asgi_request(app, "GET", f"/profiles/support/{PATIENT}"))
        self.assertEqual(s2, 200, b2)
        self.assertEqual(b2["profile"]["active_support_tier"], "Guided")

    # ── 12. Phase 1 regression — POST /predict ────────────────────────────────
    def test_12_phase1_predict_regression(self):
        payload = {"Age": 74, "M/F": "F", "Educ": 2, "MMSE": 22}
        status, body = run(_asgi_request(app, "POST", "/predict", payload))
        self.assertEqual(status, 200, body)
        self.assertIn("predicted_class", body)
        self.assertIn("model_version", body)
        self.assertEqual(body["model_version"], "oasis-practical-v1")


# ══════════════════════════════════════════════════════════════════════════════
# Persistence tests (use a fresh temp DB instance)
# ══════════════════════════════════════════════════════════════════════════════

class TestPersistence(unittest.TestCase):

    def setUp(self):
        self.tmp = tempfile.NamedTemporaryFile(suffix=".db", delete=False)
        self.db_path = self.tmp.name
        self.tmp.close()

    def tearDown(self):
        try:
            os.unlink(self.db_path)
        except FileNotFoundError:
            pass

    def _make_event(self, eid="EVT_PERSIST_01") -> InteractionEvent:
        return InteractionEvent(
            event_id=eid,
            session_id="S_PERSIST",
            patient_id="P_PERSIST",
            timestamp_utc=datetime.now(timezone.utc),
            therapy_area=TherapyArea.ORIENTATION_ROOM,
            activity_id="ACT_PERSIST",
            event_type=EventType.HINT_REQUESTED,
            payload=InteractionPayload(
                latency_seconds=1.0,
                assistance_source=AssistanceSource.USER_REQUESTED,
            ),
        )

    # ── 13. Events survive DB reconnection ───────────────────────────────────
    def test_13_events_persist_across_reconnect(self):
        db1 = Database(self.db_path)
        inserted, _ = db1.insert_events([self._make_event("EVT_P_13")])
        self.assertEqual(inserted, 1)
        del db1  # close first instance

        db2 = Database(self.db_path)
        events = db2.get_events_for_patient("P_PERSIST")
        self.assertEqual(len(events), 1)
        self.assertEqual(events[0].event_id, "EVT_P_13")

    # ── 14. Support profile is_active persists across restart ────────────────
    def test_14_support_profile_persists(self):
        from telemetry.schemas import InitialSupportProfile, SupportTier

        db1 = Database(self.db_path)
        profile = InitialSupportProfile(
            patient_id="P_PERSIST",
            active_support_tier=SupportTier.HIGH_SUPPORT,
        )
        db1.save_support_profile(profile)
        del db1

        db2 = Database(self.db_path)
        retrieved = db2.get_support_profile("P_PERSIST")
        self.assertIsNotNone(retrieved)
        self.assertEqual(retrieved.active_support_tier, SupportTier.HIGH_SUPPORT)

    # ── 15. Duplicate event_id does not create duplicate record ──────────────
    def test_15_duplicate_event_no_double_insert(self):
        db1 = Database(self.db_path)
        evt = self._make_event("EVT_P_15")
        i1, s1 = db1.insert_events([evt])
        i2, s2 = db1.insert_events([evt])  # second insert same ID
        self.assertEqual(i1, 1)
        self.assertEqual(s1, 0)
        self.assertEqual(i2, 0)
        self.assertEqual(s2, 1)
        all_events = db1.get_events_for_patient("P_PERSIST")
        self.assertEqual(len(all_events), 1)

    # ── 16. Caregiver lock persists across reconnect ──────────────────────────
    def test_16_caregiver_lock_persists(self):
        from telemetry.schemas import CaregiverOverride, SupportTier

        db1 = Database(self.db_path)
        # Create initial profile first
        from telemetry.schemas import InitialSupportProfile
        db1.save_support_profile(InitialSupportProfile(patient_id="P_PERSIST"))
        override = CaregiverOverride(
            override_id="OVR_P_16",
            patient_id="P_PERSIST",
            selected_tier=SupportTier.GUIDED,
            lock_tier=True,
            caregiver_note="Persistence test",
        )
        db1.save_caregiver_override(override)
        del db1

        db2 = Database(self.db_path)
        profile = db2.get_support_profile("P_PERSIST")
        self.assertIsNotNone(profile)
        self.assertEqual(profile.active_support_tier, SupportTier.GUIDED)
        # lock is reflected in allow_adaptive_recommendations being False
        self.assertFalse(profile.caregiver_preferences.allow_adaptive_recommendations)


# ══════════════════════════════════════════════════════════════════════════════
# Aggregation unit test (pure Python, no HTTP layer)
# ══════════════════════════════════════════════════════════════════════════════

class TestAggregation(unittest.TestCase):

    def _make_ie(self, eid, etype, area=TherapyArea.ORIENTATION_ROOM, aid="ACT_1") -> InteractionEvent:
        return InteractionEvent(
            event_id=eid,
            session_id="S_AGG_01",
            patient_id="P_AGG",
            timestamp_utc=datetime.now(timezone.utc),
            therapy_area=area,
            activity_id=aid,
            event_type=etype,
            payload=InteractionPayload(latency_seconds=1.5),
        )

    # ── 17. Aggregation produces correct metric counts ────────────────────────
    def test_17_aggregation_correct_counts(self):
        events = [
            self._make_ie("E1", EventType.HINT_REQUESTED),
            self._make_ie("E2", EventType.HINT_REQUESTED),
            self._make_ie("E3", EventType.INSTRUCTION_REPEATED),
            self._make_ie("E4", EventType.ACTIVITY_COMPLETED),
            self._make_ie("E5", EventType.ACTIVITY_SKIPPED, aid="ACT_2"),
            self._make_ie("E6", EventType.HINT_REQUESTED,
                          area=TherapyArea.REMINISCENCE_CORNER, aid="ACT_3"),
        ]
        profile = compute_interaction_profile_from_events("P_AGG", events)

        self.assertEqual(profile.patient_id, "P_AGG")
        self.assertEqual(profile.total_sessions_recorded, 1)

        or_sum = profile.area_summaries[TherapyArea.ORIENTATION_ROOM]
        self.assertEqual(or_sum.hints_requested_total, 2)
        self.assertEqual(or_sum.instructions_repeated_total, 1)
        self.assertEqual(or_sum.skips_total, 1)
        self.assertEqual(or_sum.activities_completed, 1)

        rc_sum = profile.area_summaries[TherapyArea.REMINISCENCE_CORNER]
        self.assertEqual(rc_sum.hints_requested_total, 1)

        # Overall completion: 1 completed out of 3 unique (session:activity) keys
        # ACT_1 in OR, ACT_2 in OR, ACT_3 in RC → 3 attempted; 1 completed
        self.assertEqual(profile.overall_completion_rate, round(1 / 3, 4))

    def test_17b_aggregation_empty_events(self):
        profile = compute_interaction_profile_from_events("P_EMPTY", [])
        self.assertEqual(profile.total_sessions_recorded, 0)
        self.assertIsNone(profile.overall_completion_rate)


if __name__ == "__main__":
    unittest.main(verbosity=2)
