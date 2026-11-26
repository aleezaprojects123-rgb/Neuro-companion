"""
Phase 4.7 — Assistance Runtime Integration Tests.

Validates the operational backend assistance runtime connecting:
    Interaction Telemetry
    -> Feature Extraction
    -> AS01-AS10 Prediction Engine (Phase 4.5)
    -> Safe Assistance Policy (Phase 4.6)
    -> Carl Action Adapter
    -> Structured Response & Traceable Persistence

Test Coverage (12 required categories):
1. Valid telemetry reaching the prediction engine.
2. Prediction output reaching the policy.
3. Policy decision correctly converted into a Carl action.
4. No additional assistance produces no executable action.
5. Suppressed assistance produces no executable action.
6. Patient-initiated assistance is handled correctly.
7. Caregiver control is respected.
8. AS08 generates only an optional break offer.
9. Missing or invalid telemetry is handled safely.
10. Runtime errors do not create fake approvals.
11. Decision metadata and prediction references are preserved.
12. Persistence integration (in-memory SQLite and separable operation).
"""

import json
import unittest
from unittest.mock import MagicMock

from ml.assistance.carl_action import CarlAction, CarlActionAdapter, CarlActionType
from ml.assistance.persistence import AssistanceDecisionRepository
from ml.assistance.policy_decision import DecisionStatus, PatientChoiceStatus, PolicyDecision
from ml.assistance.runtime import AssistanceRuntime
from ml.assistance.runtime_schemas import (
    AssistanceRuntimeRequest,
    AssistanceRuntimeResponse,
    RuntimeStatus,
)
from ml.assistance.schemas import InteractionTelemetryContext, StrategyID


def _ctx(**kwargs) -> InteractionTelemetryContext:
    """Helper to build a valid baseline telemetry context."""
    defaults = dict(
        session_id="session-p47-001",
        patient_id="patient-p47-001",
        task_name="OrientationRoomQuiz",
        step_index=1,
        step_total_steps=3,
        response_latency_ms=1000.0,
        idle_duration_ms=200.0,
        target_miss_count=0,
        step_error_count=0,
        consecutive_error_count=0,
        session_duration_minutes=5.0,
        patient_requested_help=False,
        patient_requested_repeat=False,
        patient_requested_break=False,
        patient_dismissed_prompt=False,
        caregiver_locked_strategy=None,
    )
    defaults.update(kwargs)
    return InteractionTelemetryContext(**defaults)


class Test1TelemetryToEngine(unittest.TestCase):
    """1. Valid telemetry reaching the prediction engine."""

    def setUp(self):
        self.runtime = AssistanceRuntime()

    def test_typed_context_reaches_engine_and_processes(self):
        ctx = _ctx(idle_duration_ms=1500.0, target_miss_count=1)
        resp = self.runtime.process(ctx, seconds_since_last_prompt=60.0)
        self.assertIsInstance(resp, AssistanceRuntimeResponse)
        self.assertEqual(resp.patient_id, "patient-p47-001")
        self.assertEqual(resp.session_id, "session-p47-001")

    def test_raw_dict_input_coerced_and_processed(self):
        raw_dict = {
            "session_id": "sess-raw-123",
            "patient_id": "patient-raw-456",
            "task_name": "CognitiveGarden",
            "idle_duration_ms": 1400.0,
            "target_miss_count": 1,
        }
        resp = self.runtime.process(raw_dict, seconds_since_last_prompt=60.0)
        self.assertEqual(resp.patient_id, "patient-raw-456")
        self.assertEqual(resp.session_id, "sess-raw-123")
        self.assertIn(resp.runtime_status, [RuntimeStatus.APPROVED, RuntimeStatus.NO_ADDITIONAL_ASSISTANCE])


class Test2PredictionToPolicy(unittest.TestCase):
    """2. Prediction output reaching the policy."""

    def setUp(self):
        self.runtime = AssistanceRuntime()

    def test_prediction_output_reaches_policy_and_applies_rules(self):
        ctx = _ctx(idle_duration_ms=1800.0, target_miss_count=1)
        resp = self.runtime.process(ctx, seconds_since_last_prompt=60.0)
        self.assertIsInstance(resp.policy_rules_applied, list)
        self.assertIn("CDR_PROHIBITION", resp.policy_rules_applied)
        self.assertIn("DATA_SUFFICIENCY_CHECK", resp.policy_rules_applied)
        self.assertIsNotNone(resp.decision_id)


class Test3PolicyToCarlAction(unittest.TestCase):
    """3. Policy decision correctly converted into a Carl action."""

    def setUp(self):
        self.adapter = CarlActionAdapter()

    def test_approved_as01_converts_to_repeat_action(self):
        decision = PolicyDecision(
            patient_id="p1",
            session_id="s1",
            task_name="TaskA",
            selected_strategy_id=StrategyID.AS01_REPEAT.value,
            decision_status=DecisionStatus.APPROVED,
            decision_reason="Idle latency exceeded threshold",
            prediction_reference={"evidence_features_used": {"idle_ms": 1500.0}},
            policy_rules_applied=["CANDIDATE_STRATEGY_SELECTION"],
            caregiver_control_applied=False,
            patient_choice_required=False,
            patient_choice_status=PatientChoiceStatus.NOT_REQUIRED,
        )
        action = self.adapter.adapt(decision)
        self.assertIsNotNone(action)
        self.assertEqual(action.action_type, CarlActionType.REPEAT_CURRENT_INSTRUCTION)
        self.assertEqual(action.strategy_id, StrategyID.AS01_REPEAT.value)
        self.assertFalse(action.is_optional)
        self.assertIn("repeat_mode", action.parameters)

    def test_all_ten_strategies_map_to_valid_action_types(self):
        for sid in StrategyID:
            decision = PolicyDecision(
                patient_id="p1",
                session_id="s1",
                task_name="TaskX",
                selected_strategy_id=sid.value,
                decision_status=DecisionStatus.APPROVED,
                decision_reason="Testing mapping",
                prediction_reference={},
                policy_rules_applied=[],
                caregiver_control_applied=False,
                patient_choice_required=False,
                patient_choice_status=PatientChoiceStatus.NOT_REQUIRED,
            )
            action = self.adapter.adapt(decision)
            self.assertIsNotNone(action, f"Strategy {sid} failed to adapt to CarlAction")
            self.assertIsInstance(action.action_type, CarlActionType)


class Test4NoAdditionalAssistance(unittest.TestCase):
    """4. No additional assistance produces no executable action."""

    def setUp(self):
        self.runtime = AssistanceRuntime()

    def test_autonomous_completion_produces_no_action(self):
        ctx = _ctx(step_error_count=0, target_miss_count=0, idle_duration_ms=100.0)
        resp = self.runtime.process(ctx, seconds_since_last_prompt=60.0)
        self.assertEqual(resp.runtime_status, RuntimeStatus.NO_ADDITIONAL_ASSISTANCE)
        self.assertFalse(resp.requires_carl_action)
        self.assertIsNone(resp.carl_action)
        self.assertIsNone(resp.selected_strategy_id)


class Test5SuppressedAssistance(unittest.TestCase):
    """5. Suppressed assistance produces no executable action."""

    def setUp(self):
        self.runtime = AssistanceRuntime()

    def test_cooldown_suppression_produces_no_action(self):
        # High idle would normally trigger AS01, but cooldown is active (10s < 45s)
        ctx = _ctx(idle_duration_ms=2500.0, target_miss_count=2)
        resp = self.runtime.process(ctx, seconds_since_last_prompt=10.0)
        self.assertEqual(resp.runtime_status, RuntimeStatus.SUPPRESSED)
        self.assertEqual(resp.decision_status, DecisionStatus.SUPPRESSED_COOLDOWN.value)
        self.assertFalse(resp.requires_carl_action)
        self.assertIsNone(resp.carl_action)

    def test_patient_dismissal_suppression_produces_no_action(self):
        ctx = _ctx(patient_dismissed_prompt=True, idle_duration_ms=3000.0)
        resp = self.runtime.process(ctx, seconds_since_last_prompt=60.0)
        self.assertEqual(resp.runtime_status, RuntimeStatus.SUPPRESSED)
        self.assertEqual(resp.decision_status, DecisionStatus.SUPPRESSED_BY_PATIENT.value)
        self.assertFalse(resp.requires_carl_action)
        self.assertIsNone(resp.carl_action)


class Test6PatientInitiatedAssistance(unittest.TestCase):
    """6. Patient-initiated assistance is handled correctly."""

    def setUp(self):
        self.runtime = AssistanceRuntime()

    def test_patient_help_request_bypasses_cooldown_and_generates_action(self):
        ctx = _ctx(patient_requested_help=True, target_miss_count=1)
        resp = self.runtime.process(ctx, seconds_since_last_prompt=5.0)  # within cooldown
        self.assertEqual(resp.runtime_status, RuntimeStatus.PATIENT_INITIATED)
        self.assertTrue(resp.requires_carl_action)
        self.assertIsNotNone(resp.carl_action)
        self.assertEqual(resp.carl_action["action_type"], CarlActionType.SHOW_VISUAL_HINT.value)

    def test_patient_repeat_request_triggers_repeat_action(self):
        ctx = _ctx(patient_requested_repeat=True)
        resp = self.runtime.process(ctx, seconds_since_last_prompt=2.0)
        self.assertEqual(resp.runtime_status, RuntimeStatus.PATIENT_INITIATED)
        self.assertTrue(resp.requires_carl_action)
        self.assertEqual(resp.carl_action["action_type"], CarlActionType.REPEAT_CURRENT_INSTRUCTION.value)


class Test7CaregiverControl(unittest.TestCase):
    """7. Caregiver control is respected."""

    def setUp(self):
        self.runtime = AssistanceRuntime()

    def test_caregiver_lock_takes_absolute_precedence(self):
        ctx = _ctx(
            caregiver_locked_strategy=StrategyID.AS06_SLOWER_SPEECH.value,
            patient_dismissed_prompt=True,  # Even if dismissed
        )
        resp = self.runtime.process(ctx, seconds_since_last_prompt=5.0)  # Even in cooldown
        self.assertEqual(resp.runtime_status, RuntimeStatus.CAREGIVER_CONTROLLED)
        self.assertTrue(resp.caregiver_control_applied)
        self.assertEqual(resp.selected_strategy_id, StrategyID.AS06_SLOWER_SPEECH.value)
        self.assertTrue(resp.requires_carl_action)
        self.assertEqual(resp.carl_action["action_type"], CarlActionType.SLOW_SPEECH.value)


class Test8AS08OptionalBreak(unittest.TestCase):
    """8. AS08 generates only an optional break offer."""

    def setUp(self):
        self.runtime = AssistanceRuntime()

    def test_as08_break_offer_is_optional_and_never_locked(self):
        ctx = _ctx(patient_requested_break=True)
        resp = self.runtime.process(ctx, seconds_since_last_prompt=60.0)
        self.assertEqual(resp.runtime_status, RuntimeStatus.OPTIONAL_BREAK)
        self.assertTrue(resp.requires_carl_action)
        self.assertTrue(resp.patient_choice_required)
        self.assertIsNotNone(resp.carl_action)
        self.assertEqual(resp.carl_action["action_type"], CarlActionType.OFFER_OPTIONAL_BREAK.value)
        self.assertTrue(resp.carl_action["is_optional"])
        self.assertFalse(resp.carl_action["parameters"].get("force_lockout", False))
        self.assertTrue(resp.carl_action["parameters"].get("allow_keep_playing", False))


class Test9MissingAndInvalidTelemetry(unittest.TestCase):
    """9. Missing or invalid telemetry is handled safely."""

    def setUp(self):
        self.runtime = AssistanceRuntime()

    def test_missing_patient_id_returns_insufficient_data(self):
        raw = {"session_id": "sess-1", "patient_id": ""}
        resp = self.runtime.process(raw)
        self.assertEqual(resp.runtime_status, RuntimeStatus.INSUFFICIENT_DATA)
        self.assertFalse(resp.requires_carl_action)
        self.assertIsNone(resp.carl_action)

    def test_missing_session_id_returns_insufficient_data(self):
        raw = {"session_id": "   ", "patient_id": "patient-1"}
        resp = self.runtime.process(raw)
        self.assertEqual(resp.runtime_status, RuntimeStatus.INSUFFICIENT_DATA)
        self.assertFalse(resp.requires_carl_action)

    def test_none_context_returns_insufficient_data(self):
        resp = self.runtime.process(None)
        self.assertEqual(resp.runtime_status, RuntimeStatus.INSUFFICIENT_DATA)
        self.assertFalse(resp.requires_carl_action)

    def test_invalid_type_returns_insufficient_data(self):
        resp = self.runtime.process(12345)
        self.assertEqual(resp.runtime_status, RuntimeStatus.INSUFFICIENT_DATA)
        self.assertFalse(resp.requires_carl_action)

    def test_missing_numeric_telemetry_does_not_invent_difficulty(self):
        # Empty numeric fields should not force high-tier assistance
        ctx = InteractionTelemetryContext(
            session_id="sess-sparse",
            patient_id="patient-sparse",
        )
        resp = self.runtime.process(ctx, seconds_since_last_prompt=60.0)
        self.assertEqual(resp.runtime_status, RuntimeStatus.NO_ADDITIONAL_ASSISTANCE)
        self.assertFalse(resp.requires_carl_action)


class Test10RuntimeErrorResilience(unittest.TestCase):
    """10. Runtime errors do not create fake approvals."""

    def test_policy_exception_handled_gracefully(self):
        mock_policy = MagicMock()
        mock_policy.decide.side_effect = RuntimeError("Database timeout during policy check")
        runtime = AssistanceRuntime(policy=mock_policy)

        ctx = _ctx()
        resp = runtime.process(ctx)
        self.assertEqual(resp.runtime_status, RuntimeStatus.ERROR)
        self.assertFalse(resp.requires_carl_action)
        self.assertIsNone(resp.carl_action)
        self.assertIsNone(resp.selected_strategy_id)
        self.assertIn("failure", resp.decision_reason.lower())


class Test11DecisionMetadataPreservation(unittest.TestCase):
    """11. Decision metadata and prediction references are preserved."""

    def setUp(self):
        self.runtime = AssistanceRuntime()

    def test_metadata_fields_fully_populated(self):
        ctx = _ctx(idle_duration_ms=1800.0, target_miss_count=1)
        req = AssistanceRuntimeRequest(
            context=ctx,
            seconds_since_last_prompt=60.0,
            metadata={"client_fps": 60, "scene": "Garden"},
        )
        resp = self.runtime.process(req)
        self.assertEqual(resp.request_id, req.request_id)
        self.assertIsNotNone(resp.decision_id)
        self.assertIsNotNone(resp.created_at)
        self.assertIsInstance(resp.policy_rules_applied, list)
        self.assertIsInstance(resp.decision_reason, str)

    def test_to_dict_serializes_cleanly(self):
        ctx = _ctx(idle_duration_ms=1800.0, target_miss_count=1)
        resp = self.runtime.process(ctx)
        d = resp.to_dict()
        serialized = json.dumps(d)
        self.assertIn("request_id", serialized)
        self.assertIn("runtime_status", serialized)
        self.assertIn("decision_status", serialized)


class Test12PersistenceIntegration(unittest.TestCase):
    """12. Persistence integration if implemented."""

    def setUp(self):
        # Use isolated in-memory SQLite database for test purity
        self.repo = AssistanceDecisionRepository(db_path=":memory:")
        self.runtime = AssistanceRuntime(repository=self.repo)

    def test_runtime_decision_persisted_and_retrievable(self):
        ctx = _ctx(idle_duration_ms=2000.0, target_miss_count=1)
        resp = self.runtime.process(ctx, seconds_since_last_prompt=60.0)
        self.assertIsNotNone(resp.decision_id)

        # Retrieve from database
        stored = self.repo.get_decision(resp.decision_id)
        self.assertIsNotNone(stored)
        self.assertEqual(stored["decision_id"], resp.decision_id)
        self.assertEqual(stored["patient_id"], "patient-p47-001")
        self.assertEqual(stored["session_id"], "session-p47-001")
        self.assertEqual(stored["runtime_status"], resp.runtime_status.value)
        self.assertEqual(stored["selected_strategy_id"], resp.selected_strategy_id)
        self.assertIsInstance(stored["policy_rules_applied"], list)

    def test_listing_decisions_by_patient_id(self):
        ctx1 = _ctx(patient_id="patient-list-01", session_id="s1")
        ctx2 = _ctx(patient_id="patient-list-01", session_id="s2")
        ctx3 = _ctx(patient_id="patient-list-02", session_id="s3")

        self.runtime.process(ctx1)
        self.runtime.process(ctx2)
        self.runtime.process(ctx3)

        records_p1 = self.repo.list_decisions(patient_id="patient-list-01")
        self.assertEqual(len(records_p1), 2)
        records_p2 = self.repo.list_decisions(patient_id="patient-list-02")
        self.assertEqual(len(records_p2), 1)

    def test_runtime_operates_cleanly_without_repository(self):
        runtime_no_repo = AssistanceRuntime(repository=None)
        ctx = _ctx()
        resp = runtime_no_repo.process(ctx)
        self.assertIsInstance(resp, AssistanceRuntimeResponse)


if __name__ == "__main__":
    unittest.main()
