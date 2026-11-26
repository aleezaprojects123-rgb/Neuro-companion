"""
Phase 4.6 — Safe Assistance Policy Tests
Tests the full decision flow:
    InteractionTelemetryContext
        -> AssistancePredictionEngine (Phase 4.5)
        -> AssistancePolicyInterface  (Phase 4.5)
        -> SafeAssistancePolicy       (Phase 4.6)
        -> PolicyDecision

Coverage:
    - Normal recommendation approval
    - No additional assistance (silent autonomy)
    - Caregiver override and lock precedence
    - Patient rejection (dismissal)
    - Patient acceptance / help request
    - Patient-initiated break (optional, not forced)
    - Rate limiting / cooldown suppression
    - Repeated prompt suppression
    - Missing / insufficient interaction data
    - CDR independence (no CDR-to-strategy mapping)
    - PolicyDecision output contract completeness
    - Audit trail (policy_rules_applied)
"""

import unittest
from datetime import datetime, timezone

from ml.assistance.policy_decision import DecisionStatus, PatientChoiceStatus, PolicyDecision
from ml.assistance.safe_policy import (
    RULE_BREAK_OPTIONAL,
    RULE_CAREGIVER_LOCK,
    RULE_CDR_PROHIBITION,
    RULE_COOLDOWN,
    RULE_DATA_SUFFICIENCY,
    RULE_NO_FORCED_ESCALATION,
    RULE_PATIENT_BREAK_REQUEST,
    RULE_PATIENT_DISMISSAL,
    RULE_PATIENT_HELP_REQUEST,
    SafeAssistancePolicy,
)
from ml.assistance.schemas import InteractionTelemetryContext, StrategyID


def _ctx(**kwargs) -> InteractionTelemetryContext:
    """Helper: build a valid base context, overriding with kwargs."""
    defaults = dict(
        session_id="sess-test-001",
        patient_id="patient-test-001",
        task_name="MemoryMatchTask",
        step_error_count=0,
        consecutive_error_count=0,
        target_miss_count=0,
        session_duration_minutes=5.0,
        patient_requested_help=False,
        patient_requested_repeat=False,
        patient_requested_break=False,
        patient_dismissed_prompt=False,
        caregiver_locked_strategy=None,
    )
    defaults.update(kwargs)
    return InteractionTelemetryContext(**defaults)


class TestPolicyDecisionContract(unittest.TestCase):
    """PolicyDecision output contract must always be fully populated."""

    def setUp(self):
        self.policy = SafeAssistancePolicy()

    def test_decision_has_all_required_fields(self):
        ctx = _ctx()
        d = self.policy.decide(ctx, seconds_since_last_prompt=60.0)
        self.assertIsInstance(d, PolicyDecision)
        # decision_id must be a non-empty UUID string
        self.assertIsInstance(d.decision_id, str)
        self.assertGreater(len(d.decision_id), 0)
        # patient_id and session_id must be populated
        self.assertEqual(d.patient_id, "patient-test-001")
        self.assertEqual(d.session_id, "sess-test-001")
        # created_at must be a timezone-aware datetime
        self.assertIsInstance(d.created_at, datetime)
        self.assertIsNotNone(d.created_at.tzinfo)
        # decision_status must be a DecisionStatus value
        self.assertIsInstance(d.decision_status, DecisionStatus)
        # policy_rules_applied must be a list
        self.assertIsInstance(d.policy_rules_applied, list)
        self.assertGreater(len(d.policy_rules_applied), 0)

    def test_to_dict_is_json_serialisable(self):
        import json
        ctx = _ctx()
        d = self.policy.decide(ctx, seconds_since_last_prompt=60.0)
        d_dict = d.to_dict()
        # Must not raise
        serialised = json.dumps(d_dict)
        self.assertIn("decision_id", serialised)
        self.assertIn("decision_status", serialised)
        self.assertIn("policy_rules_applied", serialised)

    def test_cdr_rule_always_in_audit_trail(self):
        ctx = _ctx()
        d = self.policy.decide(ctx, seconds_since_last_prompt=60.0)
        self.assertIn(RULE_CDR_PROHIBITION, d.policy_rules_applied)

    def test_data_sufficiency_rule_always_in_audit_trail(self):
        ctx = _ctx()
        d = self.policy.decide(ctx, seconds_since_last_prompt=60.0)
        self.assertIn(RULE_DATA_SUFFICIENCY, d.policy_rules_applied)


class TestNormalApproval(unittest.TestCase):
    """When conditions trigger a recommendation and cooldown has passed, it should be approved."""

    def setUp(self):
        self.policy = SafeAssistancePolicy()

    def test_idle_latency_triggers_approved_strategy(self):
        # High idle latency should trigger AS01 or similar
        ctx = _ctx(idle_duration_ms=2000.0, target_miss_count=1)
        d = self.policy.decide(ctx, seconds_since_last_prompt=60.0)
        # Either APPROVED or PATIENT_HELP_REQUEST; strategy must not be None
        self.assertIn(d.decision_status, [DecisionStatus.APPROVED, DecisionStatus.PATIENT_HELP_REQUEST])
        self.assertIsNotNone(d.selected_strategy_id)

    def test_approved_decision_has_prediction_reference(self):
        ctx = _ctx(idle_duration_ms=2000.0, target_miss_count=2)
        d = self.policy.decide(ctx, seconds_since_last_prompt=60.0)
        if d.decision_status == DecisionStatus.APPROVED:
            self.assertIsNotNone(d.prediction_reference)

    def test_approved_strategy_is_valid_strategy_id(self):
        ctx = _ctx(idle_duration_ms=2000.0, target_miss_count=1)
        d = self.policy.decide(ctx, seconds_since_last_prompt=60.0)
        if d.selected_strategy_id is not None:
            valid_ids = {s.value for s in StrategyID}
            self.assertIn(d.selected_strategy_id, valid_ids)


class TestNoAdditionalAssistance(unittest.TestCase):
    """When patient is performing well and cooldown is satisfied, silence is a valid outcome."""

    def setUp(self):
        self.policy = SafeAssistancePolicy()

    def test_clean_session_returns_no_additional_assistance(self):
        ctx = _ctx(
            step_error_count=0,
            target_miss_count=0,
            idle_duration_ms=200.0,
            session_duration_minutes=5.0,
        )
        d = self.policy.decide(ctx, seconds_since_last_prompt=60.0)
        # Should be NO_ADDITIONAL_ASSISTANCE — patient is doing fine
        self.assertEqual(d.decision_status, DecisionStatus.NO_ADDITIONAL_ASSISTANCE)
        self.assertIsNone(d.selected_strategy_id)
        self.assertFalse(d.caregiver_control_applied)

    def test_no_additional_assistance_has_reason(self):
        ctx = _ctx(step_error_count=0, target_miss_count=0)
        d = self.policy.decide(ctx, seconds_since_last_prompt=60.0)
        if d.decision_status == DecisionStatus.NO_ADDITIONAL_ASSISTANCE:
            self.assertIsInstance(d.decision_reason, str)
            self.assertGreater(len(d.decision_reason), 0)


class TestCaregiverControl(unittest.TestCase):
    """Caregiver locks and overrides must take absolute precedence over predictions."""

    def setUp(self):
        self.policy = SafeAssistancePolicy()

    def test_caregiver_lock_produces_caregiver_override_status(self):
        ctx = _ctx(caregiver_locked_strategy=StrategyID.AS03_STEP_GUIDANCE.value)
        d = self.policy.decide(ctx, seconds_since_last_prompt=60.0)
        self.assertEqual(d.decision_status, DecisionStatus.CAREGIVER_OVERRIDE)
        self.assertTrue(d.caregiver_control_applied)

    def test_caregiver_lock_selects_locked_strategy(self):
        locked = StrategyID.AS06_SLOWER_SPEECH.value
        ctx = _ctx(caregiver_locked_strategy=locked)
        d = self.policy.decide(ctx, seconds_since_last_prompt=60.0)
        self.assertEqual(d.selected_strategy_id, locked)

    def test_caregiver_lock_overrides_even_in_cooldown(self):
        ctx = _ctx(caregiver_locked_strategy=StrategyID.AS01_REPEAT.value)
        # Even inside cooldown window, caregiver lock takes precedence
        d = self.policy.decide(ctx, seconds_since_last_prompt=5.0)
        self.assertEqual(d.decision_status, DecisionStatus.CAREGIVER_OVERRIDE)
        self.assertIn(RULE_CAREGIVER_LOCK, d.policy_rules_applied)

    def test_caregiver_lock_overrides_patient_dismissal(self):
        ctx = _ctx(
            caregiver_locked_strategy=StrategyID.AS07_RESPONSE_TIME.value,
            patient_dismissed_prompt=True,
        )
        d = self.policy.decide(ctx, seconds_since_last_prompt=60.0)
        self.assertEqual(d.decision_status, DecisionStatus.CAREGIVER_OVERRIDE)


class TestPatientAgency(unittest.TestCase):
    """Patient choices must be respected immediately and absolutely."""

    def setUp(self):
        self.policy = SafeAssistancePolicy()

    def test_patient_help_request_is_honoured(self):
        ctx = _ctx(patient_requested_help=True)
        d = self.policy.decide(ctx, seconds_since_last_prompt=5.0)
        # Even inside cooldown, explicit help request is served
        self.assertEqual(d.decision_status, DecisionStatus.PATIENT_HELP_REQUEST)
        self.assertIsNotNone(d.selected_strategy_id)
        self.assertIn(RULE_PATIENT_HELP_REQUEST, d.policy_rules_applied)

    def test_patient_repeat_request_is_honoured(self):
        ctx = _ctx(patient_requested_repeat=True)
        d = self.policy.decide(ctx, seconds_since_last_prompt=5.0)
        self.assertEqual(d.decision_status, DecisionStatus.PATIENT_HELP_REQUEST)
        self.assertIsNotNone(d.selected_strategy_id)

    def test_patient_dismissal_suppresses_all_spontaneous_assistance(self):
        ctx = _ctx(
            patient_dismissed_prompt=True,
            idle_duration_ms=5000.0,
            target_miss_count=3,
        )
        d = self.policy.decide(ctx, seconds_since_last_prompt=60.0)
        self.assertEqual(d.decision_status, DecisionStatus.SUPPRESSED_BY_PATIENT)
        self.assertIsNone(d.selected_strategy_id)
        self.assertIn(RULE_PATIENT_DISMISSAL, d.policy_rules_applied)

    def test_patient_choice_pending_when_strategy_requires_consent(self):
        # AS08 always requires patient choice
        ctx = _ctx(patient_requested_break=True)
        d = self.policy.decide(ctx, seconds_since_last_prompt=60.0)
        if d.selected_strategy_id == StrategyID.AS08_BREAK_PAUSE.value:
            self.assertTrue(d.patient_choice_required)
            self.assertEqual(d.patient_choice_status, PatientChoiceStatus.PENDING)

    def test_non_choice_strategy_has_not_required_status(self):
        ctx = _ctx(patient_requested_help=True, target_miss_count=0)
        d = self.policy.decide(ctx, seconds_since_last_prompt=60.0)
        # AS01 Repeat does not require patient choice
        if d.selected_strategy_id == StrategyID.AS01_REPEAT.value:
            self.assertFalse(d.patient_choice_required)
            self.assertEqual(d.patient_choice_status, PatientChoiceStatus.NOT_REQUIRED)


class TestAS08BreakBehaviour(unittest.TestCase):
    """AS08 Break must always be optional and patient-controlled."""

    def setUp(self):
        self.policy = SafeAssistancePolicy()

    def test_patient_break_request_is_honoured_as_patient_help_request(self):
        ctx = _ctx(patient_requested_break=True)
        d = self.policy.decide(ctx, seconds_since_last_prompt=60.0)
        self.assertEqual(d.decision_status, DecisionStatus.PATIENT_HELP_REQUEST)
        self.assertEqual(d.selected_strategy_id, StrategyID.AS08_BREAK_PAUSE.value)

    def test_as08_always_requires_patient_choice(self):
        ctx = _ctx(patient_requested_break=True)
        d = self.policy.decide(ctx, seconds_since_last_prompt=60.0)
        if d.selected_strategy_id == StrategyID.AS08_BREAK_PAUSE.value:
            self.assertTrue(d.patient_choice_required)

    def test_as08_recommendation_includes_break_optional_rule(self):
        ctx = _ctx(patient_requested_break=True)
        d = self.policy.decide(ctx, seconds_since_last_prompt=60.0)
        if d.selected_strategy_id == StrategyID.AS08_BREAK_PAUSE.value:
            self.assertIn(RULE_BREAK_OPTIONAL, d.policy_rules_applied)

    def test_long_session_may_trigger_break_as_approved_optional(self):
        ctx = _ctx(session_duration_minutes=25.0, consecutive_error_count=0)
        d = self.policy.decide(ctx, seconds_since_last_prompt=60.0)
        if d.selected_strategy_id == StrategyID.AS08_BREAK_PAUSE.value:
            # Must be APPROVED (suggested), not forced
            self.assertIn(d.decision_status, [DecisionStatus.APPROVED, DecisionStatus.PATIENT_HELP_REQUEST])
            # patient_choice_required must be True
            self.assertTrue(d.patient_choice_required)


class TestRateLimiting(unittest.TestCase):
    """Cooldown suppresses spontaneous prompts to prevent cognitive overload."""

    def setUp(self):
        self.policy = SafeAssistancePolicy()

    def test_cooldown_suppresses_spontaneous_prompt(self):
        # Trigger conditions present but inside cooldown window
        ctx = _ctx(idle_duration_ms=2000.0, target_miss_count=2)
        d = self.policy.decide(ctx, seconds_since_last_prompt=10.0)
        self.assertEqual(d.decision_status, DecisionStatus.SUPPRESSED_COOLDOWN)
        self.assertIsNone(d.selected_strategy_id)
        self.assertIn(RULE_COOLDOWN, d.policy_rules_applied)

    def test_cooldown_does_not_suppress_patient_help_request(self):
        ctx = _ctx(patient_requested_help=True)
        d = self.policy.decide(ctx, seconds_since_last_prompt=5.0)
        # Patient help request bypasses cooldown
        self.assertNotEqual(d.decision_status, DecisionStatus.SUPPRESSED_COOLDOWN)
        self.assertEqual(d.decision_status, DecisionStatus.PATIENT_HELP_REQUEST)

    def test_cooldown_does_not_suppress_caregiver_lock(self):
        ctx = _ctx(caregiver_locked_strategy=StrategyID.AS04_VISUAL_HINT.value)
        d = self.policy.decide(ctx, seconds_since_last_prompt=0.0)
        self.assertNotEqual(d.decision_status, DecisionStatus.SUPPRESSED_COOLDOWN)
        self.assertEqual(d.decision_status, DecisionStatus.CAREGIVER_OVERRIDE)

    def test_after_cooldown_expires_prompt_allowed(self):
        ctx = _ctx(idle_duration_ms=2000.0, target_miss_count=1)
        d = self.policy.decide(ctx, seconds_since_last_prompt=60.0)
        # With cooldown expired, spontaneous prompt should not be suppressed
        self.assertNotEqual(d.decision_status, DecisionStatus.SUPPRESSED_COOLDOWN)


class TestInsufficientData(unittest.TestCase):
    """Missing mandatory identifiers must result in INSUFFICIENT_DATA, not automatic assistance."""

    def setUp(self):
        self.policy = SafeAssistancePolicy()

    def test_missing_patient_id_returns_insufficient_data(self):
        ctx = InteractionTelemetryContext(session_id="sess-001", patient_id="")
        d = self.policy.decide(ctx)
        self.assertEqual(d.decision_status, DecisionStatus.INSUFFICIENT_DATA)
        self.assertIsNone(d.selected_strategy_id)

    def test_missing_session_id_returns_insufficient_data(self):
        ctx = InteractionTelemetryContext(session_id="   ", patient_id="patient-001")
        d = self.policy.decide(ctx)
        self.assertEqual(d.decision_status, DecisionStatus.INSUFFICIENT_DATA)

    def test_missing_optional_fields_do_not_trigger_assistance(self):
        # Only mandatory identifiers present; all optional telemetry absent (defaults)
        ctx = InteractionTelemetryContext(
            session_id="sess-001",
            patient_id="patient-001",
        )
        d = self.policy.decide(ctx, seconds_since_last_prompt=60.0)
        # Must not be INSUFFICIENT_DATA — optional fields being absent is fine
        self.assertNotEqual(d.decision_status, DecisionStatus.INSUFFICIENT_DATA)
        # With no telemetry signals, should return NO_ADDITIONAL_ASSISTANCE
        self.assertEqual(d.decision_status, DecisionStatus.NO_ADDITIONAL_ASSISTANCE)

    def test_zero_errors_alone_does_not_trigger_assistance(self):
        ctx = _ctx(step_error_count=0, target_miss_count=0, idle_duration_ms=None)
        d = self.policy.decide(ctx, seconds_since_last_prompt=60.0)
        # Zero errors with no other signals = no additional assistance
        self.assertEqual(d.decision_status, DecisionStatus.NO_ADDITIONAL_ASSISTANCE)


class TestCDRIndependence(unittest.TestCase):
    """CDR scores must never influence any aspect of the assistance decision."""

    def setUp(self):
        self.policy = SafeAssistancePolicy()

    def test_cdr_score_does_not_change_decision(self):
        ctx = _ctx()
        d_without_cdr = self.policy.decide(ctx, seconds_since_last_prompt=60.0)
        d_with_cdr_mild = self.policy.decide(ctx, seconds_since_last_prompt=60.0, cdr_score=0.5)
        d_with_cdr_severe = self.policy.decide(ctx, seconds_since_last_prompt=60.0, cdr_score=3.0)
        # Decision status must be identical regardless of CDR value
        self.assertEqual(d_without_cdr.decision_status, d_with_cdr_mild.decision_status)
        self.assertEqual(d_without_cdr.decision_status, d_with_cdr_severe.decision_status)

    def test_cdr_rule_in_audit_trail_always(self):
        ctx = _ctx()
        d = self.policy.decide(ctx, cdr_score=1.0)
        self.assertIn(RULE_CDR_PROHIBITION, d.policy_rules_applied)

    def test_cdr_does_not_appear_in_strategy_id(self):
        ctx = _ctx()
        d = self.policy.decide(ctx, seconds_since_last_prompt=60.0, cdr_score=2.0)
        # Verify no CDR-based strategy string leaked into decision
        if d.selected_strategy_id is not None:
            self.assertNotIn("CDR", d.selected_strategy_id.upper())

    def test_decision_reason_does_not_reference_cdr(self):
        ctx = _ctx()
        d = self.policy.decide(ctx, seconds_since_last_prompt=60.0, cdr_score=1.5)
        self.assertNotIn("CDR", d.decision_reason.upper())


class TestNoAutomaticEscalation(unittest.TestCase):
    """Errors alone must not auto-escalate to demonstration or higher tiers."""

    def setUp(self):
        self.policy = SafeAssistancePolicy()

    def test_error_count_alone_does_not_force_demonstration(self):
        # Many consecutive errors — but no other triggers (no failed cues, idle not extreme).
        # AS05 Demonstration must NOT be automatically forced regardless of error count.
        # The safety property is: whatever decision is taken, AS05_DEMONSTRATION is not
        # the selected strategy unless failed_cue_count and idle_duration_ms also qualify.
        ctx = _ctx(
            step_error_count=5,
            consecutive_error_count=5,
            target_miss_count=0,
            idle_duration_ms=200.0,
            session_duration_minutes=5.0,
        )
        d = self.policy.decide(ctx, seconds_since_last_prompt=60.0)
        # Core safety assertion: errors alone must NOT auto-select demonstration
        self.assertNotEqual(
            d.selected_strategy_id,
            StrategyID.AS05_DEMONSTRATION.value,
            "AS05 Demonstration must not be automatically selected on error count alone "
            "without also meeting failed_cue_count and idle_duration_ms thresholds."
        )

    def test_slow_response_alone_does_not_trigger_assistance(self):
        # Slow response with zero errors should not force any strategy
        ctx = _ctx(
            idle_duration_ms=800.0,   # below AS01 threshold of 1200ms
            step_error_count=0,
            target_miss_count=0,
        )
        d = self.policy.decide(ctx, seconds_since_last_prompt=60.0)
        self.assertEqual(d.decision_status, DecisionStatus.NO_ADDITIONAL_ASSISTANCE)


if __name__ == "__main__":
    unittest.main()
