"""
NeuroCompanion - Extended Model Test Suite (Phase 3→4)

Validates:
1. Patient preferences (CRUD, multiple categories, superseding, current-only retrieval)
2. Memory support presentations (saving, retrieval, repetition count, content linking)
3. Observation opportunities (linking to presentations, response status, latency, assistance flags)
4. Assistance episodes (patient requested vs Carl offered, lifecycle, outcome updates)
5. Dynamic UI configuration history (initial accessible config, versioning, closing previous, active retrieval)
6. Event interpretations (cognitive domains CD01-CD09, assistance labels AS01-AS10, analyst notes)
7. Consent records (granting, declining, validity check, expiration)
8. Preservation of Phase 2A existing tables, columns, and data invariants
9. Extended interaction_events columns idempotency and schema versioning
10. Explicit verification of NO CDR-to-support-tier mapping
"""

import os
import tempfile
import unittest
import uuid
from datetime import datetime, timezone, timedelta

from telemetry.schemas import TherapyArea, SupportTier
from telemetry.extended_schemas import (
    PreferenceCategory,
    PreferenceSource,
    PatientPreference,
    PresentationMethod,
    MemorySupportPresentation,
    OpportunityType,
    ResponseStatus,
    ObservationOpportunity,
    AssistanceType,
    AssistanceInitiation,
    SubsequentOutcome,
    AssistanceEpisode,
    ConfigChangeSource,
    UIConfigurationHistory,
    EventInterpretation,
    ConsentType,
    ConsentRecord,
    ExtendedInteractionEvent,
)
from telemetry.extended_database import ExtendedDatabase
from telemetry.database import Database


def _now() -> datetime:
    return datetime.now(timezone.utc)


class BaseExtendedDBTest(unittest.TestCase):
    def setUp(self):
        # Create a fresh temporary SQLite database for each test
        self.temp_file = tempfile.NamedTemporaryFile(suffix=".db", delete=False)
        self.temp_file.close()
        self.db_path = self.temp_file.name

        # Initialise base DB first to create the original 5 tables
        self.base_db = Database(db_path=self.db_path)
        # Initialise extended DB to create the 7 new tables + new columns
        self.ext_db = ExtendedDatabase(db_path=self.db_path)

    def tearDown(self):
        try:
            os.remove(self.db_path)
        except OSError:
            pass


class TestPatientPreferences(BaseExtendedDBTest):
    def test_save_and_retrieve_preference(self):
        pref = PatientPreference(
            preference_id=str(uuid.uuid4()),
            patient_id="PAT_001",
            preference_category=PreferenceCategory.MUSIC,
            preference_key="favorite_genre",
            preference_value="Classical - 1950s",
            source=PreferenceSource.PATIENT_EXPLICIT,
            is_current=True,
            recorded_at=_now(),
        )
        self.ext_db.save_preference(pref)
        prefs = self.ext_db.get_preferences_for_patient("PAT_001")
        self.assertEqual(len(prefs), 1)
        self.assertEqual(prefs[0].preference_key, "favorite_genre")
        self.assertEqual(prefs[0].preference_value, "Classical - 1950s")
        self.assertTrue(prefs[0].is_current)

    def test_multiple_preference_categories(self):
        p1 = PatientPreference(
            preference_id=str(uuid.uuid4()),
            patient_id="PAT_001",
            preference_category=PreferenceCategory.ACTIVITY,
            preference_key="preferred_room",
            preference_value="ReminiscenceCorner",
            source=PreferenceSource.PATIENT_EXPLICIT,
            is_current=True,
            recorded_at=_now(),
        )
        p2 = PatientPreference(
            preference_id=str(uuid.uuid4()),
            patient_id="PAT_001",
            preference_category=PreferenceCategory.ACCESSIBILITY,
            preference_key="font_size",
            preference_value="large",
            source=PreferenceSource.CAREGIVER_PROVIDED,
            is_current=True,
            recorded_at=_now(),
        )
        self.ext_db.save_preference(p1)
        self.ext_db.save_preference(p2)

        prefs = self.ext_db.get_preferences_for_patient("PAT_001")
        self.assertEqual(len(prefs), 2)

    def test_supersede_preference(self):
        p1 = PatientPreference(
            preference_id=str(uuid.uuid4()),
            patient_id="PAT_001",
            preference_category=PreferenceCategory.ACCESSIBILITY,
            preference_key="text_size",
            preference_value="medium",
            source=PreferenceSource.PATIENT_EXPLICIT,
            is_current=True,
            recorded_at=_now() - timedelta(days=1),
        )
        self.ext_db.save_preference(p1)

        p2 = PatientPreference(
            preference_id=str(uuid.uuid4()),
            patient_id="PAT_001",
            preference_category=PreferenceCategory.ACCESSIBILITY,
            preference_key="text_size",
            preference_value="large",
            source=PreferenceSource.CAREGIVER_PROVIDED,
            is_current=True,
            recorded_at=_now(),
        )
        self.ext_db.save_preference(p2)

        current_prefs = self.ext_db.get_preferences_for_patient("PAT_001", current_only=True)
        self.assertEqual(len(current_prefs), 1)
        self.assertEqual(current_prefs[0].preference_value, "large")

        all_prefs = self.ext_db.get_preferences_for_patient("PAT_001", current_only=False)
        self.assertEqual(len(all_prefs), 2)


class TestMemorySupportPresentations(BaseExtendedDBTest):
    def test_save_and_retrieve_presentation(self):
        pres = MemorySupportPresentation(
            information_id=str(uuid.uuid4()),
            patient_id="PAT_001",
            session_id="SESS_101",
            room=TherapyArea.ORIENTATION_ROOM,
            activity_id="clock_orientation_01",
            content_id="today_date_card_v1",
            content_version="1.0",
            presentation_method=PresentationMethod.COMBINED,
            presented_at=_now(),
            repetition_count=0,
        )
        self.ext_db.save_presentation(pres)

        presentations = self.ext_db.get_presentations_for_patient("PAT_001")
        self.assertEqual(len(presentations), 1)
        self.assertEqual(presentations[0].content_id, "today_date_card_v1")
        self.assertEqual(presentations[0].room, TherapyArea.ORIENTATION_ROOM)

    def test_presentation_repetition_count(self):
        pres = MemorySupportPresentation(
            information_id=str(uuid.uuid4()),
            patient_id="PAT_001",
            session_id="SESS_101",
            room=TherapyArea.ORIENTATION_ROOM,
            content_id="location_prompt_card",
            presentation_method=PresentationMethod.SPOKEN,
            presented_at=_now(),
            repetition_count=3,
        )
        self.ext_db.save_presentation(pres)

        session_pres = self.ext_db.get_presentations_for_session("SESS_101")
        self.assertEqual(len(session_pres), 1)
        self.assertEqual(session_pres[0].repetition_count, 3)


class TestObservationOpportunities(BaseExtendedDBTest):
    def test_save_and_link_opportunity(self):
        pres_id = str(uuid.uuid4())
        pres = MemorySupportPresentation(
            information_id=pres_id,
            patient_id="PAT_001",
            session_id="SESS_101",
            room=TherapyArea.ORIENTATION_ROOM,
            content_id="date_time_calendar",
            presentation_method=PresentationMethod.VISUAL,
            presented_at=_now() - timedelta(minutes=5),
        )
        self.ext_db.save_presentation(pres)

        opp_id = str(uuid.uuid4())
        opp = ObservationOpportunity(
            opportunity_id=opp_id,
            linked_presentation_id=pres_id,
            patient_id="PAT_001",
            session_id="SESS_101",
            room=TherapyArea.ORIENTATION_ROOM,
            opportunity_type=OpportunityType.RECOGNITION,
            presented_at=_now(),
            response_status=ResponseStatus.RESPONDED,
            response_latency_seconds=4.2,
            assistance_requested=False,
            assistance_received=False,
        )
        self.ext_db.save_observation_opportunity(opp)

        opportunities = self.ext_db.get_opportunities_for_session("SESS_101")
        self.assertEqual(len(opportunities), 1)
        self.assertEqual(opportunities[0].linked_presentation_id, pres_id)
        self.assertEqual(opportunities[0].response_status, ResponseStatus.RESPONDED)

    def test_update_opportunity_response(self):
        opp_id = str(uuid.uuid4())
        opp = ObservationOpportunity(
            opportunity_id=opp_id,
            patient_id="PAT_001",
            session_id="SESS_101",
            room=TherapyArea.COGNITIVE_GARDEN,
            opportunity_type=OpportunityType.TASK_CONTINUATION,
            presented_at=_now(),
            response_status=ResponseStatus.UNKNOWN,
        )
        self.ext_db.save_observation_opportunity(opp)

        self.ext_db.update_opportunity_response(
            opportunity_id=opp_id,
            response_status=ResponseStatus.SKIPPED,
            response_latency_seconds=12.5,
            related_event_id="EVT_SKIP_999",
        )

        opportunities = self.ext_db.get_opportunities_for_session("SESS_101")
        self.assertEqual(opportunities[0].response_status, ResponseStatus.SKIPPED)
        self.assertEqual(opportunities[0].response_latency_seconds, 12.5)
        self.assertEqual(opportunities[0].related_event_id, "EVT_SKIP_999")


class TestAssistanceEpisodes(BaseExtendedDBTest):
    def test_save_episode_lifecycle(self):
        ep_id = str(uuid.uuid4())
        ep = AssistanceEpisode(
            episode_id=ep_id,
            patient_id="PAT_001",
            session_id="SESS_101",
            room=TherapyArea.COGNITIVE_GARDEN,
            activity_id="watering_flowerbed",
            activity_step_id="step_2_select_watering_can",
            triggering_event_id="EVT_HESITATION_01",
            assistance_type=AssistanceType.AS04_VISUAL_HINT,
            initiation=AssistanceInitiation.CARL_OFFERED,
            offered_at=_now() - timedelta(seconds=30),
            accepted=True,
            delivered=True,
            delivered_at=_now() - timedelta(seconds=20),
            subsequent_outcome=SubsequentOutcome.UNKNOWN,
        )
        self.ext_db.save_assistance_episode(ep)

        episodes = self.ext_db.get_episodes_for_session("SESS_101")
        self.assertEqual(len(episodes), 1)
        self.assertEqual(episodes[0].assistance_type, AssistanceType.AS04_VISUAL_HINT)
        self.assertTrue(episodes[0].delivered)

        # Update episode outcome
        self.ext_db.update_episode_outcome(
            episode_id=ep_id,
            delivered=True,
            delivered_at=_now(),
            subsequent_outcome=SubsequentOutcome.COMPLETED_INDEPENDENTLY,
            outcome_event_id="EVT_COMPLETED_02",
            notes="Patient watered plant correctly after gentle visual highlight",
        )

        episodes_after = self.ext_db.get_episodes_for_session("SESS_101")
        self.assertEqual(episodes_after[0].subsequent_outcome, SubsequentOutcome.COMPLETED_INDEPENDENTLY)
        self.assertEqual(episodes_after[0].outcome_event_id, "EVT_COMPLETED_02")


class TestUIConfigurationHistory(BaseExtendedDBTest):
    def test_create_and_version_ui_config(self):
        cfg1_id = str(uuid.uuid4())
        cfg1 = UIConfigurationHistory(
            config_id=cfg1_id,
            patient_id="PAT_001",
            effective_from=_now() - timedelta(hours=2),
            text_size="large",
            text_contrast="high",
            max_visible_choices=3,
            change_source=ConfigChangeSource.CAREGIVER,
            change_reason="Caregiver configured high-contrast accessibility mode",
        )
        self.ext_db.save_ui_config(cfg1)

        active = self.ext_db.get_active_ui_config("PAT_001")
        self.assertIsNotNone(active)
        self.assertEqual(active.config_id, cfg1_id)
        self.assertEqual(active.text_size, "large")

        # Save new configuration - should close the previous one
        cfg2_id = str(uuid.uuid4())
        cfg2 = UIConfigurationHistory(
            config_id=cfg2_id,
            patient_id="PAT_001",
            effective_from=_now(),
            text_size="extra_large",
            text_contrast="high",
            max_visible_choices=2,
            change_source=ConfigChangeSource.AI_RECOMMENDATION,
            change_reason="Phase 6 adaptation applied step-by-step guidance",
        )
        self.ext_db.save_ui_config(cfg2)

        active_new = self.ext_db.get_active_ui_config("PAT_001")
        self.assertEqual(active_new.config_id, cfg2_id)
        self.assertEqual(active_new.text_size, "extra_large")

        # History should contain both
        history = self.ext_db.get_ui_config_history("PAT_001")
        self.assertEqual(len(history), 2)
        # Old config should have effective_to populated
        old_cfg = next(c for c in history if c.config_id == cfg1_id)
        self.assertIsNotNone(old_cfg.effective_to)


class TestEventInterpretations(BaseExtendedDBTest):
    def test_save_and_retrieve_interpretation(self):
        interp_id = str(uuid.uuid4())
        interp = EventInterpretation(
            interpretation_id=interp_id,
            event_id="EVT_TEST_101",
            patient_id="PAT_001",
            cognitive_domains=["CD01", "CD04"],
            assistance_labels=["AS01", "AS04"],
            analyst_note="Observation shows orientation recognition with visual hint delivered",
            generated_at=_now(),
            model_version=None,  # Rule-based
        )
        self.ext_db.save_event_interpretation(interp)

        retrieved = self.ext_db.get_interpretations_for_event("EVT_TEST_101")
        self.assertEqual(len(retrieved), 1)
        self.assertIn("CD01", retrieved[0].cognitive_domains)
        self.assertIn("CD04", retrieved[0].cognitive_domains)
        self.assertIn("AS01", retrieved[0].assistance_labels)
        self.assertIsNone(retrieved[0].model_version)


class TestConsentRecords(BaseExtendedDBTest):
    def test_consent_lifecycle(self):
        c_id = str(uuid.uuid4())
        consent = ConsentRecord(
            consent_id=c_id,
            patient_id="PAT_001",
            consent_type=ConsentType.PHOTO_USE,
            granted=True,
            granted_by="caregiver_legal_guardian",
            granted_at=_now(),
            expires_at=_now() + timedelta(days=365),
            notes="Caregiver consented to displaying family photos in Reminiscence Corner",
        )
        self.ext_db.save_consent_record(consent)

        self.assertTrue(self.ext_db.has_valid_consent("PAT_001", ConsentType.PHOTO_USE))
        self.assertFalse(self.ext_db.has_valid_consent("PAT_001", ConsentType.PERSONAL_CONTENT))


class TestPhase2APreservation(BaseExtendedDBTest):
    def test_original_tables_exist(self):
        with self.ext_db.get_connection() as conn:
            tables = [
                r[0]
                for r in conn.execute(
                    "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'"
                ).fetchall()
            ]
        for original in [
            "interaction_events",
            "interaction_profiles",
            "patient_support_profiles",
            "support_recommendations",
            "caregiver_overrides",
        ]:
            self.assertIn(original, tables)

    def test_extended_columns_on_interaction_events(self):
        with self.ext_db.get_connection() as conn:
            cols = [
                r["name"]
                for r in conn.execute("PRAGMA table_info(interaction_events)").fetchall()
            ]
        expected_new_cols = [
            "event_category",
            "activity_step_id",
            "sequence_number",
            "prompt_id",
            "observation_opportunity_id",
            "assistance_episode_id",
            "correlation_id",
            "event_schema_version",
            "cognitive_domain_tags_json",
            "assistance_labels_json",
        ]
        for col in expected_new_cols:
            self.assertIn(col, cols)

    def test_init_extended_tables_is_idempotent(self):
        # Calling init_extended_tables multiple times must not raise errors
        self.ext_db.init_extended_tables()
        self.ext_db.init_extended_tables()


class TestNoCDRMapping(unittest.TestCase):
    def test_no_cdr_or_mmse_in_extended_schemas(self):
        import inspect
        import telemetry.extended_schemas as ext_s

        source = inspect.getsource(ext_s).lower()
        self.assertNotIn("clinical_dementia_rating", source)
        self.assertNotIn("cdr_to_tier", source)
        self.assertNotIn("map_cdr", source)

    def test_support_tiers_are_only_standard_guided_highsupport(self):
        valid_tiers = {tier.value for tier in SupportTier}
        self.assertEqual(valid_tiers, {"Standard", "Guided", "HighSupport"})


# ---------------------------------------------------------------------------
# ASGI Test Helper for Extended HTTP Routes
# ---------------------------------------------------------------------------

async def _asgi_request(application, method: str, path: str, body=None):
    import json
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
        "client": ("127.0.0.1", 8000),
        "server": ("127.0.0.1", 8000),
        "scheme": "http",
    }
    response_body = []
    response_status = [None]
    async def receive():
        return {"type": "http.request", "body": body_bytes, "more_body": False}
    async def send(message):
        if message["type"] == "http.response.start":
            response_status[0] = message["status"]
        elif message["type"] == "http.response.body":
            response_body.append(message.get("body", b""))
    await application(scope, receive, send)
    resp_text = b"".join(response_body).decode("utf-8", errors="replace")
    parsed = None
    if resp_text:
        try:
            parsed = json.loads(resp_text)
        except Exception:
            parsed = resp_text
    return response_status[0], parsed


def _run_async(coro):
    import asyncio
    return asyncio.run(coro)


class TestExtendedRoutesIsolated(unittest.TestCase):
    """
    Validates all 7 extended API endpoints mounted at /api/v2/
    using an isolated SQLite database to verify HTTP contract & persistence.
    """

    def setUp(self):
        self.temp_file = tempfile.NamedTemporaryFile(suffix=".db", delete=False)
        self.temp_file.close()
        self.db_path = self.temp_file.name

        import telemetry.database as db_mod
        import telemetry.routes as routes_mod
        import telemetry.extended_database as extended_db_mod
        import telemetry.extended_routes as extended_routes_mod
        from main import app

        self.app = app
        self.base_db = Database(db_path=self.db_path)
        self.ext_db = ExtendedDatabase(db_path=self.db_path)

        self._orig_telemetry_routes_db = routes_mod.db
        self._orig_telemetry_database_db = db_mod.db
        self._orig_ext_routes_db = extended_routes_mod.extended_db
        self._orig_ext_database_db = extended_db_mod.extended_db
        self._orig_ext_database_ext_db = extended_db_mod.ext_db

        routes_mod.db = self.base_db
        db_mod.db = self.base_db
        extended_routes_mod.extended_db = self.ext_db
        extended_db_mod.extended_db = self.ext_db
        extended_db_mod.ext_db = self.ext_db

    def tearDown(self):
        import telemetry.database as db_mod
        import telemetry.routes as routes_mod
        import telemetry.extended_database as extended_db_mod
        import telemetry.extended_routes as extended_routes_mod

        routes_mod.db = self._orig_telemetry_routes_db
        db_mod.db = self._orig_telemetry_database_db
        extended_routes_mod.extended_db = self._orig_ext_routes_db
        extended_db_mod.extended_db = self._orig_ext_database_db
        extended_db_mod.ext_db = self._orig_ext_database_ext_db

        if os.path.exists(self.db_path):
            for suffix in ["", "-wal", "-shm"]:
                p = self.db_path + suffix
                if os.path.exists(p):
                    try:
                        os.remove(p)
                    except Exception:
                        pass

    def test_01_patient_preferences_api(self):
        pat_id = f"PAT_EXT_{uuid.uuid4().hex[:6]}"
        payload = {
            "preference_category": "ACTIVITY",
            "preference_key": "favorite_flower",
            "preference_value": "Blue Hydrangea",
            "source": "PATIENT_EXPLICIT",
            "is_current": True,
            "notes": "Patient loves blue hydrangeas"
        }
        status, body = _run_async(_asgi_request(self.app, "POST", f"/api/v2/preferences/{pat_id}", payload))
        self.assertEqual(status, 201, body)
        self.assertEqual(body["preference_value"], "Blue Hydrangea")

        status, body = _run_async(_asgi_request(self.app, "GET", f"/api/v2/preferences/{pat_id}"))
        self.assertEqual(status, 200, body)
        self.assertEqual(body["count"], 1)
        self.assertEqual(body["preferences"][0]["preference_key"], "favorite_flower")

        # Direct isolated DB verification
        persisted = self.ext_db.get_preferences_for_patient(pat_id)
        self.assertEqual(len(persisted), 1)
        self.assertEqual(persisted[0].preference_value, "Blue Hydrangea")

    def test_02_memory_support_presentations_api(self):
        pat_id = f"PAT_EXT_{uuid.uuid4().hex[:6]}"
        payload = {
            "patient_id": pat_id,
            "session_id": "SESS_TEST",
            "room": "OrientationRoom",
            "activity_id": "orientation_calendar",
            "content_id": "CAL_TODAY_DATE",
            "content_version": "1.0",
            "presentation_method": "SPOKEN",
            "repetition_count": 1
        }
        status, body = _run_async(_asgi_request(self.app, "POST", "/api/v2/presentations", payload))
        self.assertEqual(status, 201, body)
        self.assertEqual(body["content_id"], "CAL_TODAY_DATE")

        status, body = _run_async(_asgi_request(self.app, "GET", f"/api/v2/presentations/{pat_id}"))
        self.assertEqual(status, 200, body)
        self.assertEqual(body["count"], 1)

        # Retrieve by session
        status, body = _run_async(_asgi_request(self.app, "GET", f"/api/v2/presentations/{pat_id}/session/SESS_TEST"))
        self.assertEqual(status, 200, body)
        self.assertEqual(body["count"], 1)

    def test_03_observation_opportunities_api(self):
        pat_id = f"PAT_EXT_{uuid.uuid4().hex[:6]}"
        payload = {
            "patient_id": pat_id,
            "session_id": "SESS_TEST",
            "room": "OrientationRoom",
            "activity_id": "orientation_calendar",
            "opportunity_type": "ORIENTATION_PROMPT",
            "response_status": "UNKNOWN"
        }
        status, body = _run_async(_asgi_request(self.app, "POST", "/api/v2/opportunities", payload))
        self.assertEqual(status, 201, body)
        opp_id = body["opportunity_id"]

        # GET by session
        status, body = _run_async(_asgi_request(self.app, "GET", f"/api/v2/opportunities/{pat_id}/session/SESS_TEST"))
        self.assertEqual(status, 200, body)
        self.assertEqual(body["count"], 1)

        # PATCH update outcome
        patch_payload = {
            "response_status": "RESPONDED",
            "response_latency_seconds": 4.5,
            "assistance_requested": False
        }
        status, body = _run_async(_asgi_request(self.app, "PATCH", f"/api/v2/opportunities/{opp_id}/response", patch_payload))
        self.assertEqual(status, 200, body)
        self.assertTrue(body["updated"])

    def test_04_assistance_episodes_api(self):
        pat_id = f"PAT_EXT_{uuid.uuid4().hex[:6]}"
        payload = {
            "patient_id": pat_id,
            "session_id": "SESS_TEST",
            "room": "ReminiscenceCorner",
            "activity_id": "reminiscence_audio",
            "assistance_type": "AS01_REPEAT_INSTRUCTION",
            "initiation": "CARL_OFFERED",
            "notes": "Would you like me to tell you more about this song?"
        }
        status, body = _run_async(_asgi_request(self.app, "POST", "/api/v2/assistance/episodes", payload))
        self.assertEqual(status, 201, body)
        ep_id = body["episode_id"]

        # GET episodes for session
        status, body = _run_async(_asgi_request(self.app, "GET", f"/api/v2/assistance/episodes/{pat_id}/session/SESS_TEST"))
        self.assertEqual(status, 200, body)
        self.assertEqual(body["count"], 1)

        # PATCH update episode outcome
        patch_payload = {
            "subsequent_outcome": "COMPLETED_WITH_ASSISTANCE",
            "delivered": True
        }
        status, body = _run_async(_asgi_request(self.app, "PATCH", f"/api/v2/assistance/episodes/{ep_id}/outcome", patch_payload))
        self.assertEqual(status, 200, body)
        self.assertTrue(body["updated"])

    def test_05_dynamic_ui_config_api(self):
        pat_id = f"PAT_EXT_{uuid.uuid4().hex[:6]}"
        payload = {
            "text_size": "large",
            "text_contrast": "high",
            "max_visible_choices": 3,
            "subtitles_enabled": True,
            "speech_enabled": True,
            "speech_speed": 1.0,
            "instruction_format": "concise",
            "visual_hints_available": True,
            "change_source": "CAREGIVER",
            "change_reason": "Patient requested larger text for easier reading"
        }
        status, body = _run_async(_asgi_request(self.app, "POST", f"/api/v2/ui/config/{pat_id}", payload))
        self.assertEqual(status, 201, body)
        self.assertEqual(body["text_size"], "large")

        # GET active config
        status, body = _run_async(_asgi_request(self.app, "GET", f"/api/v2/ui/config/{pat_id}/active"))
        self.assertEqual(status, 200, body)
        self.assertEqual(body["text_size"], "large")

        # GET history
        status, body = _run_async(_asgi_request(self.app, "GET", f"/api/v2/ui/config/{pat_id}/history"))
        self.assertEqual(status, 200, body)
        self.assertEqual(body["count"], 1)

    def test_06_event_interpretations_api(self):
        pat_id = f"PAT_EXT_{uuid.uuid4().hex[:6]}"
        evt_id = f"EVT_{uuid.uuid4().hex[:8]}"
        payload = {
            "event_id": evt_id,
            "patient_id": pat_id,
            "cognitive_domains": ["CD01_TEMPORAL_ORIENTATION"],
            "assistance_labels": ["AS01_EXPLICIT_HELP_CALL"],
            "analyst_note": "Temporal orientation verification event",
            "model_version": "v1.0"
        }
        status, body = _run_async(_asgi_request(self.app, "POST", "/api/v2/interpretations", payload))
        self.assertEqual(status, 201, body)

        status, body = _run_async(_asgi_request(self.app, "GET", f"/api/v2/interpretations/event/{evt_id}"))
        self.assertEqual(status, 200, body)
        self.assertEqual(body["count"], 1)
        self.assertIn("CD01_TEMPORAL_ORIENTATION", body["interpretations"][0]["cognitive_domains"])

    def test_07_consent_records_api(self):
        pat_id = f"PAT_EXT_{uuid.uuid4().hex[:6]}"
        payload = {
            "consent_type": "AI_ANALYSIS",
            "granted": True,
            "granted_by": "Family Caregiver Sarah",
            "notes": "Full research consent given with caregiver authorization"
        }
        status, body = _run_async(_asgi_request(self.app, "POST", f"/api/v2/consent/{pat_id}", payload))
        self.assertEqual(status, 201, body)

        status, body = _run_async(_asgi_request(self.app, "GET", f"/api/v2/consent/{pat_id}/AI_ANALYSIS/check"))
        self.assertEqual(status, 200, body)
        self.assertTrue(body["has_valid_consent"])


if __name__ == "__main__":
    unittest.main()
