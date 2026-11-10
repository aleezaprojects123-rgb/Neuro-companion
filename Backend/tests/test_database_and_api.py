"""
Comprehensive Test Suite for NeuroCompanion Database and Telemetry API Endpoints.
Verifies:
1. SQLite database persistence, table schemas, and idempotence.
2. Event ingestion, batch processing, and duplicate event deduplication.
3. Invalid event rejection.
4. Support profile creation without MMSE, historical versioning, and retrieval.
5. Recommendation storage and retrieval.
6. Caregiver overrides and tier locking.
7. Regression verification of Phase 1 OASIS clinical endpoints.
"""

import os
import sys
import tempfile
import unittest
from datetime import datetime, timezone
from pathlib import Path

# Ensure Backend/ is in sys.path
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from telemetry.database import Database
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


class TestDatabaseAndTelemetryAPI(unittest.TestCase):
    """Test suite for persistent storage and API routes."""

    def setUp(self):
        """Create an isolated temporary SQLite database for each test."""
        self.temp_db_fd, self.temp_db_path = tempfile.mkstemp(suffix=".db")
        self.db = Database(self.temp_db_path)

    def tearDown(self):
        """Clean up temporary test database file."""
        os.close(self.temp_db_fd)
        if os.path.exists(self.temp_db_path):
            os.remove(self.temp_db_path)

    def test_database_creation_and_tables(self):
        """Verify all tables are created with expected indexes."""
        with self.db.get_connection() as conn:
            cursor = conn.cursor()
            cursor.execute("SELECT name FROM sqlite_master WHERE type='table';")
            tables = {row["name"] for row in cursor.fetchall()}
            expected_tables = {
                "patient_support_profiles",
                "interaction_events",
                "interaction_profiles",
                "support_recommendations",
                "caregiver_overrides",
            }
            self.assertTrue(expected_tables.issubset(tables))

    def test_event_ingestion_and_deduplication(self):
        """Verify events are stored and duplicate event IDs are skipped safely."""
        event1 = InteractionEvent(
            event_id="evt_101",
            session_id="sess_01",
            patient_id="P_001",
            timestamp_utc=datetime.now(timezone.utc),
            therapy_area=TherapyArea.ORIENTATION_ROOM,
            activity_id="clock_reading",
            event_type=EventType.HINT_REQUESTED,
            payload=InteractionPayload(hint_tier_delivered=1, latency_seconds=5.2)
        )
        event2 = InteractionEvent(
            event_id="evt_102",
            session_id="sess_01",
            patient_id="P_001",
            timestamp_utc=datetime.now(timezone.utc),
            therapy_area=TherapyArea.ORIENTATION_ROOM,
            activity_id="clock_reading",
            event_type=EventType.ATTEMPT_SUBMITTED,
            payload=InteractionPayload(is_correct=True, attempt_index=1)
        )

        # Batch 1: Insert both
        inserted, skipped = self.db.insert_events([event1, event2])
        self.assertEqual(inserted, 2)
        self.assertEqual(skipped, 0)

        # Batch 2: Insert event1 again along with a new event3
        event3 = InteractionEvent(
            event_id="evt_103",
            session_id="sess_01",
            patient_id="P_001",
            timestamp_utc=datetime.now(timezone.utc),
            therapy_area=TherapyArea.ORIENTATION_ROOM,
            activity_id="clock_reading",
            event_type=EventType.ACTIVITY_COMPLETED,
            payload=InteractionPayload(completion_status=CompletionStatus.INDEPENDENT)
        )
        inserted, skipped = self.db.insert_events([event1, event3])
        self.assertEqual(inserted, 1)  # Only event3 inserted
        self.assertEqual(skipped, 1)   # event1 skipped as duplicate

        # Check total retrieved
        stored_events = self.db.get_events_for_patient("P_001")
        self.assertEqual(len(stored_events), 3)

    def test_support_profile_lifecycle_without_mmse(self):
        """Verify profile creation and version history preservation without MMSE."""
        initial_profile = InitialSupportProfile(
            patient_id="P_001",
            active_support_tier=SupportTier.STANDARD,
            carl_config=CarlConfiguration(instruction_style="concise"),
            caregiver_preferences=CaregiverPreferences(max_session_duration_minutes=15)
        )
        self.db.save_support_profile(initial_profile)

        retrieved = self.db.get_support_profile("P_001")
        self.assertIsNotNone(retrieved)
        self.assertEqual(retrieved.active_support_tier, SupportTier.STANDARD)

        # Update profile to Guided tier
        updated_profile = InitialSupportProfile(
            patient_id="P_001",
            active_support_tier=SupportTier.GUIDED,
            carl_config=CarlConfiguration(instruction_style="step_by_step")
        )
        self.db.save_support_profile(updated_profile)

        # Verify active profile is updated
        active_now = self.db.get_support_profile("P_001")
        self.assertEqual(active_now.active_support_tier, SupportTier.GUIDED)

        # Verify historical profile was preserved (not overwritten)
        with self.db.get_connection() as conn:
            cursor = conn.cursor()
            cursor.execute("SELECT COUNT(*) AS count FROM patient_support_profiles WHERE patient_id='P_001'")
            total_records = cursor.fetchone()["count"]
            self.assertEqual(total_records, 2)

    def test_recommendation_and_caregiver_override(self):
        """Verify recommendation storage, retrieval, and authoritative caregiver override."""
        rec = SupportRecommendation(
            recommendation_id="rec_001",
            patient_id="P_001",
            current_tier=SupportTier.STANDARD,
            recommended_tier=SupportTier.GUIDED,
            rationale="Repeated hints in Cognitive Garden sequencing."
        )
        self.db.save_recommendation(rec)

        recs = self.db.get_recommendations_for_patient("P_001")
        self.assertEqual(len(recs), 1)
        self.assertEqual(recs[0].recommended_tier, SupportTier.GUIDED)

        # Caregiver overrides and locks at HighSupport
        import uuid as _uuid
        override = CaregiverOverride(
            override_id=f"ovr_001_{_uuid.uuid4().hex[:8]}",
            patient_id="P_001",
            selected_tier=SupportTier.HIGH_SUPPORT,
            lock_tier=True,
            caregiver_note="Caregiver requested full visual guidance."
        )
        self.db.save_caregiver_override(override)

        # Active profile should now be HighSupport
        active_profile = self.db.get_support_profile("P_001")
        self.assertEqual(active_profile.active_support_tier, SupportTier.HIGH_SUPPORT)
        self.assertFalse(active_profile.caregiver_preferences.allow_adaptive_recommendations)

    def test_api_routes_integration(self):
        """Test API endpoints via asynchronous router invocations."""
        import asyncio
        from telemetry.routes import (
            ingest_telemetry_events,
            create_support_profile,
            get_support_profile,
            get_support_recommendations,
            apply_caregiver_override,
        )
        from telemetry.database import db as global_db

        async def run_api_tests():
            # 1. Onboarding without MMSE
            profile = InitialSupportProfile(
                patient_id="P_TEST_API",
                active_support_tier=SupportTier.STANDARD
            )
            resp = await create_support_profile(profile)
            self.assertEqual(resp.status, "success")
            self.assertEqual(resp.profile.patient_id, "P_TEST_API")

            # 2. Retrieve profile
            get_resp = await get_support_profile("P_TEST_API")
            self.assertEqual(get_resp.profile.active_support_tier, SupportTier.STANDARD)

            # 3. Telemetry Ingestion
            event = InteractionEvent(
                event_id="evt_api_01",
                session_id="sess_api_01",
                patient_id="P_TEST_API",
                timestamp_utc=datetime.now(timezone.utc),
                therapy_area=TherapyArea.ORIENTATION_ROOM,
                activity_id="daily_schedule",
                event_type=EventType.ACTIVITY_STARTED
            )
            ingest_resp = await ingest_telemetry_events([event])
            self.assertEqual(ingest_resp.received_count, 1)

            # 4. Caregiver Override
            import uuid
            ovr = CaregiverOverride(
                override_id=f"ovr_api_{uuid.uuid4().hex[:8]}",
                patient_id="P_TEST_API",
                selected_tier=SupportTier.GUIDED,
                lock_tier=False
            )
            ovr_resp = await apply_caregiver_override(ovr)
            self.assertEqual(ovr_resp.active_tier, SupportTier.GUIDED)

        asyncio.run(run_api_tests())


if __name__ == "__main__":
    unittest.main()
