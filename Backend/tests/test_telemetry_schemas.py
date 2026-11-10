"""
Unit and Integration Tests for NeuroCompanion Telemetry Schemas.
Tests validation of InteractionEvent, InitialSupportProfile, InteractionProfile,
SupportRecommendation, and CaregiverOverride.
"""

import sys
import unittest
from datetime import datetime, timezone
from pathlib import Path

# Ensure Backend/ is in sys.path
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

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


class TestInteractionTelemetrySchemas(unittest.TestCase):
    """Test suite verifying telemetry schema integrity and edge case handling."""

    def test_interaction_event_valid(self):
        """Verify standard valid interaction event instantiation across therapy areas."""
        for area in [TherapyArea.ORIENTATION_ROOM, TherapyArea.REMINISCENCE_CORNER, TherapyArea.COGNITIVE_GARDEN]:
            event = InteractionEvent(
                event_id="evt_001",
                session_id="sess_101",
                patient_id="P_042",
                timestamp_utc=datetime.now(timezone.utc),
                therapy_area=area,
                activity_id="clock_orientation",
                event_type=EventType.HINT_REQUESTED,
                payload=InteractionPayload(
                    attempt_index=1,
                    hint_tier_delivered=1,
                    assistance_source=AssistanceSource.USER_REQUESTED,
                    latency_seconds=4.5
                )
            )
            self.assertEqual(event.patient_id, "P_042")
            self.assertEqual(event.therapy_area, area)
            self.assertEqual(event.payload.hint_tier_delivered, 1)

    def test_interaction_event_empty_payload(self):
        """Verify interaction events safely accept default/empty payloads."""
        event = InteractionEvent(
            event_id="evt_002",
            session_id="sess_102",
            patient_id="P_042",
            timestamp_utc=datetime.now(timezone.utc),
            therapy_area=TherapyArea.REMINISCENCE_CORNER,
            activity_id="photo_reminiscence",
            event_type=EventType.ACTIVITY_STARTED
        )
        self.assertIsNone(event.payload.attempt_index)
        self.assertIsNone(event.payload.is_correct)
        self.assertIsNone(event.payload.latency_seconds)

    def test_interaction_event_rejection_of_empty_ids(self):
        """Verify empty string or whitespace IDs are strictly rejected."""
        with self.assertRaises(ValueError):
            InteractionEvent(
                event_id="   ",
                session_id="sess_101",
                patient_id="P_042",
                timestamp_utc=datetime.now(timezone.utc),
                therapy_area=TherapyArea.COGNITIVE_GARDEN,
                activity_id="flower_sequence",
                event_type=EventType.ATTEMPT_SUBMITTED
            )

    def test_initial_support_profile_without_mmse(self):
        """Verify InitialSupportProfile instantiates cleanly without requiring MMSE."""
        profile = InitialSupportProfile(
            patient_id="P_042",
            active_support_tier=SupportTier.GUIDED,
            carl_config=CarlConfiguration(
                instruction_style="step_by_step",
                hesitation_threshold_seconds=20.0
            ),
            caregiver_preferences=CaregiverPreferences(
                max_session_duration_minutes=20,
                allow_adaptive_recommendations=True
            ),
            onboarding_notes="Caregiver requested gentle visual target highlights"
        )
        self.assertEqual(profile.patient_id, "P_042")
        self.assertEqual(profile.active_support_tier, SupportTier.GUIDED)
        self.assertEqual(profile.carl_config.hesitation_threshold_seconds, 20.0)

    def test_interaction_profile_longitudinal_summary(self):
        """Verify InteractionProfile tracks area summaries and assistance frequencies."""
        profile = InteractionProfile(
            patient_id="P_042",
            total_sessions_recorded=5,
            overall_completion_rate=0.85,
            area_summaries={
                TherapyArea.ORIENTATION_ROOM: AreaSummary(
                    activities_attempted=10,
                    activities_completed=9,
                    hints_requested_total=2,
                    average_response_latency_seconds=6.2
                ),
                TherapyArea.REMINISCENCE_CORNER: AreaSummary(
                    activities_attempted=8,
                    activities_completed=8,
                    hints_requested_total=0
                ),
                TherapyArea.COGNITIVE_GARDEN: AreaSummary(
                    activities_attempted=12,
                    activities_completed=9,
                    hints_requested_total=5,
                    skips_total=1
                )
            },
            observed_assistance_frequency={
                AssistanceSource.USER_REQUESTED: 4,
                AssistanceSource.CARL_AUTO_PROMPTED: 3,
                AssistanceSource.CAREGIVER_INITIATED: 0
            },
            recent_trend_note="Independent in Reminiscence; benefits from hints in Garden sequencing"
        )
        self.assertEqual(profile.total_sessions_recorded, 5)
        self.assertEqual(profile.area_summaries[TherapyArea.ORIENTATION_ROOM].activities_completed, 9)
        self.assertEqual(profile.observed_assistance_frequency[AssistanceSource.USER_REQUESTED], 4)

    def test_support_recommendation_and_caregiver_override(self):
        """Verify support recommendation and explicit caregiver override governance."""
        recommendation = SupportRecommendation(
            recommendation_id="rec_001",
            patient_id="P_042",
            current_tier=SupportTier.STANDARD,
            recommended_tier=SupportTier.GUIDED,
            rationale="Clock and calendar activities in Orientation Room showed repeated hint requests across 3 sessions.",
            triggering_area=TherapyArea.ORIENTATION_ROOM,
            is_temporary_baseline=True
        )
        self.assertTrue(recommendation.is_temporary_baseline)
        self.assertEqual(recommendation.recommended_tier, SupportTier.GUIDED)

        # Caregiver overrides and chooses High Support instead
        override = CaregiverOverride(
            override_id="ovr_001",
            patient_id="P_042",
            selected_tier=SupportTier.HIGH_SUPPORT,
            lock_tier=True,
            caregiver_note="Caregiver preferred High Support demonstrations for this week"
        )
        self.assertEqual(override.selected_tier, SupportTier.HIGH_SUPPORT)
        self.assertTrue(override.lock_tier)


if __name__ == "__main__":
    unittest.main()
