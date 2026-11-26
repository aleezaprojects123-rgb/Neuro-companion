"""
Automated Test Suite for NeuroCompanion Phase 4.5
AS01-AS10 AI Assistance Prediction Subsystem.
Verifies all ten strategy predictors, missing telemetry handling, model signal integration,
robust fallbacks, patient agency, caregiver overrides, and prohibition of CDR tiering.
"""

import os
import sys
import tempfile
import unittest

# Ensure Backend/ root is in sys.path
CURRENT_DIR = os.path.dirname(os.path.abspath(__file__))
BACKEND_ROOT = os.path.abspath(os.path.join(CURRENT_DIR, ".."))
if BACKEND_ROOT not in sys.path:
    sys.path.insert(0, BACKEND_ROOT)

from ml.assistance import (
    AssistancePredictionEngine,
    AssistancePolicyInterface,
    InteractionTelemetryContext,
    AssistancePredictionRecord,
    PredictionBatchResult,
    ModelSignalProvider,
    RuntimeFeatureExtractor,
    StrategyID,
    RecommendationType,
    AssistanceConfig,
    DEFAULT_CONFIG
)


class TestStrategyPredictionOutputs(unittest.TestCase):
    """Verifies that all ten assistance strategies produce structured, valid outputs."""

    def setUp(self):
        self.engine = AssistancePredictionEngine()

    def test_predict_all_returns_all_ten_strategies(self):
        ctx = InteractionTelemetryContext(
            session_id="S100",
            patient_id="P100",
            task_name="StoryQuizGame",
            task_domain="cognitive_selection",
            response_latency_ms=1100.0,
            idle_duration_ms=1300.0,
            target_miss_count=1,
            step_error_count=0,
            candidate_prompt_text="Please choose the green flower from the shelf."
        )
        batch = self.engine.predict_all(ctx)
        self.assertIsInstance(batch, PredictionBatchResult)
        self.assertEqual(len(batch.recommendations), 10)

        expected_ids = [
            "AS01_REPEAT_INSTRUCTION",
            "AS02_SIMPLIFY_INSTRUCTION",
            "AS03_STEP_BY_STEP",
            "AS04_VISUAL_HINT",
            "AS05_DEMONSTRATION",
            "AS06_SLOWER_SPEECH",
            "AS07_ADDITIONAL_RESPONSE_TIME",
            "AS08_BREAK_PAUSE",
            "AS09_VERBAL_EXPLANATION",
            "AS10_INDEPENDENT_COMPLETION"
        ]
        for sid in expected_ids:
            self.assertIn(sid, batch.recommendations)
            rec = batch.recommendations[sid]
            self.assertIsInstance(rec, AssistancePredictionRecord)
            self.assertEqual(rec.strategy_id, sid)
            self.assertIsInstance(rec.is_recommended, bool)
            self.assertIsInstance(rec.recommendation, str)
            self.assertGreater(len(rec.recommendation), 0)
            self.assertIsInstance(rec.evidence_features_used, dict)
            self.assertIsInstance(rec.limitations, str)
            self.assertGreater(len(rec.limitations), 0)

    def test_predict_individual_strategy(self):
        ctx = InteractionTelemetryContext(
            session_id="S101",
            patient_id="P101",
            task_name="CompleteGame",
            task_domain="cognitive_selection",
            candidate_prompt_text="This is a very long, overly intricate, highly complex sentence that should trigger sentence shortening."
        )
        rec = self.engine.predict_strategy(StrategyID.AS02_SIMPLIFY.value, ctx)
        self.assertEqual(rec.strategy_id, StrategyID.AS02_SIMPLIFY.value)
        self.assertTrue(rec.is_recommended)
        self.assertIn("word count limit", rec.recommendation)

    def test_predict_unknown_strategy_raises_key_error(self):
        ctx = InteractionTelemetryContext()
        with self.assertRaises(KeyError):
            self.engine.predict_strategy("AS99_NON_EXISTENT", ctx)


class TestMissingAndIncompleteTelemetry(unittest.TestCase):
    """Verifies that the subsystem gracefully tolerates missing, None, or malformed telemetry."""

    def setUp(self):
        self.engine = AssistancePredictionEngine()

    def test_completely_empty_context(self):
        empty_ctx = InteractionTelemetryContext()
        batch = self.engine.predict_all(empty_ctx)
        self.assertEqual(len(batch.recommendations), 10)
        # Default behavior on empty: AS10 Independent Completion recommended
        self.assertTrue(batch.recommendations[StrategyID.AS10_INDEPENDENT.value].is_recommended)

    def test_dictionary_with_missing_and_none_fields(self):
        raw_dict = {
            "session_id": "RAW_1",
            "response_latency_ms": None,
            "idle_duration_ms": None,
            "candidate_prompt_text": None,
            "target_miss_count": -5  # Negative count should be clipped to 0
        }
        batch = self.engine.predict_all(raw_dict)
        self.assertEqual(len(batch.recommendations), 10)
        as01 = batch.recommendations[StrategyID.AS01_REPEAT.value]
        self.assertEqual(as01.evidence_features_used["target_miss_count"], 0)


class TestModelArtifactIntegrationAndFallbacks(unittest.TestCase):
    """Verifies integration with trained Phase 4.4 models and robust fallbacks when missing."""

    def test_live_artifact_loading(self):
        provider = ModelSignalProvider(BACKEND_ROOT)
        self.assertIsNotNone(provider.m6_model)
        self.assertIsNotNone(provider.m3_model)
        self.assertIsNotNone(provider.m1_model)
        self.assertIsNotNone(provider.m4_priors)

        # Test M6 dialogue quality evaluation
        m6_sig = provider.get_m6_dialogue_quality("We are right here with you. Take all the time you need.")
        self.assertFalse(m6_sig["is_fallback"])
        self.assertIn("predicted_empathy", m6_sig)
        self.assertGreater(m6_sig["predicted_empathy"], 3.0)

        # Test M3 task error risk signal
        m3_sig = provider.get_m3_error_risk("StoryQuizGame", 10.0)
        self.assertFalse(m3_sig["is_fallback"])
        self.assertIn("error_probability", m3_sig)
        self.assertGreaterEqual(m3_sig["error_probability"], 0.0)

        # Test M1 pacing reference
        m1_motor = provider.get_m1_pacing_reference("motor_tapping")
        self.assertFalse(m1_motor["is_fallback"])
        self.assertAlmostEqual(m1_motor["p95_ms"], 1031.27, places=1)

        m1_cog = provider.get_m1_pacing_reference("cognitive_selection")
        self.assertFalse(m1_cog["is_fallback"])
        self.assertAlmostEqual(m1_cog["p95_ms"], 1379.91, places=1)

    def test_missing_artifacts_fallback_gracefully(self):
        with tempfile.TemporaryDirectory() as empty_dir:
            provider = ModelSignalProvider(backend_root=empty_dir)
            self.assertIsNone(provider.m6_model)
            self.assertIsNone(provider.m3_model)
            self.assertIsNone(provider.m1_model)

            # M6 Fallback
            m6_fb = provider.get_m6_dialogue_quality("Take your time and breathe.")
            self.assertTrue(m6_fb["is_fallback"])
            self.assertIn("predicted_empathy", m6_fb)

            # M3 Fallback
            m3_fb = provider.get_m3_error_risk("StoryQuizGame", 10.0)
            self.assertTrue(m3_fb["is_fallback"])
            self.assertEqual(m3_fb["error_probability"], 0.22)

            # M1 Fallback
            m1_fb = provider.get_m1_pacing_reference("motor_tapping")
            self.assertTrue(m1_fb["is_fallback"])
            self.assertAlmostEqual(m1_fb["p95_ms"], 1031.27, places=1)


class TestHonestMethodClassification(unittest.TestCase):
    """Verifies that genuine ML, statistical references, and deterministic rules are labeled honestly."""

    def setUp(self):
        self.engine = AssistancePredictionEngine()

    def test_genuine_ml_flag_only_on_m6_and_m3(self):
        ctx = InteractionTelemetryContext(
            task_name="StoryQuizGame",
            candidate_prompt_text="I understand this is difficult. Let us do it together."
        )
        batch = self.engine.predict_all(ctx)

        # AS09 (M6 supporting signal) and AS10 (M3 supporting signal) are PROVISIONAL —
        # neither M6 nor M3 is a trained assistance-benefit predictor.
        # is_genuine_ml must be False for both after Phase 4.5 classification correction.
        self.assertFalse(
            batch.recommendations[StrategyID.AS09_VERBAL_EXPLANATION.value].is_genuine_ml,
            "AS09 must NOT claim is_genuine_ml=True: M6 is a text-quality scorer, "
            "not a trained AS09 assistance-benefit predictor."
        )
        self.assertFalse(
            batch.recommendations[StrategyID.AS10_INDEPENDENT.value].is_genuine_ml,
            "AS10 must NOT claim is_genuine_ml=True: M3 is an error-risk classifier, "
            "not a trained AS10 autonomy predictor."
        )

        # All other strategies must also not claim genuine ML
        non_ml_ids = [
            StrategyID.AS01_REPEAT.value,
            StrategyID.AS02_SIMPLIFY.value,
            StrategyID.AS03_STEP_GUIDANCE.value,
            StrategyID.AS04_VISUAL_HINT.value,
            StrategyID.AS05_DEMONSTRATION.value,
            StrategyID.AS06_SLOWER_SPEECH.value,
            StrategyID.AS07_RESPONSE_TIME.value,
            StrategyID.AS08_BREAK_PAUSE.value
        ]
        for sid in non_ml_ids:
            rec = batch.recommendations[sid]
            self.assertFalse(rec.is_genuine_ml, f"Strategy {sid} must not claim to be genuine ML.")

    def test_uncalibrated_confidence_scores_are_none(self):
        ctx = InteractionTelemetryContext(
            task_name="StoryQuizGame",
            step_error_count=0,
            idle_duration_ms=100.0,
            target_miss_count=0
        )
        batch = self.engine.predict_all(ctx)
        # When AS01, AS04, AS05, AS08 are not recommended, confidence is None
        self.assertIsNone(batch.recommendations[StrategyID.AS01_REPEAT.value].confidence_score)
        self.assertIsNone(batch.recommendations[StrategyID.AS04_VISUAL_HINT.value].confidence_score)
        self.assertIsNone(batch.recommendations[StrategyID.AS05_DEMONSTRATION.value].confidence_score)
        self.assertIsNone(batch.recommendations[StrategyID.AS08_BREAK_PAUSE.value].confidence_score)


class TestPatientSafetyAndPolicyInterface(unittest.TestCase):
    """Verifies clinical safety rules, caregiver overrides, patient agency, and prohibition of CDR tiering."""

    def setUp(self):
        self.engine = AssistancePredictionEngine()
        self.policy = AssistancePolicyInterface()

    def test_caregiver_lock_takes_absolute_priority(self):
        ctx = InteractionTelemetryContext(
            caregiver_locked_strategy=StrategyID.AS05_DEMONSTRATION.value,
            step_error_count=0  # Even with 0 errors, caregiver lock overrides
        )
        batch = self.engine.predict_all(ctx)
        decision = self.policy.evaluate_policy(batch, ctx)

        self.assertEqual(decision["approved_strategy"], StrategyID.AS05_DEMONSTRATION.value)
        self.assertEqual(decision["action_type"], "CAREGIVER_LOCK")
        self.assertTrue(decision["caregiver_override_applied"])

    def test_patient_explicit_request_honored_immediately(self):
        ctx = InteractionTelemetryContext(
            patient_requested_repeat=True
        )
        batch = self.engine.predict_all(ctx)
        decision = self.policy.evaluate_policy(batch, ctx)

        self.assertEqual(decision["approved_strategy"], StrategyID.AS01_REPEAT.value)
        self.assertEqual(decision["action_type"], "OFFER_PROMPT")
        self.assertTrue(decision["patient_dismissible"])

    def test_patient_dismissal_suppresses_cues(self):
        ctx = InteractionTelemetryContext(
            target_miss_count=3,  # Would normally trigger AS04 Visual Hint
            patient_dismissed_prompt=True
        )
        batch = self.engine.predict_all(ctx)
        decision = self.policy.evaluate_policy(batch, ctx)

        self.assertIsNone(decision["approved_strategy"])
        self.assertEqual(decision["action_type"], "SUPPRESS_DISMISSED")

    def test_rate_limiting_cooldown_suppresses_spontaneous_prompts(self):
        ctx = InteractionTelemetryContext(
            target_miss_count=3  # Would normally trigger visual hint
        )
        batch = self.engine.predict_all(ctx)
        # Only 10s elapsed since last prompt (< 45s cooldown)
        decision = self.policy.evaluate_policy(batch, ctx, seconds_since_last_prompt=10.0)

        self.assertIsNone(decision["approved_strategy"])
        self.assertEqual(decision["action_type"], "SUPPRESS_COOLDOWN")

    def test_as08_break_is_optional_and_never_locks_out(self):
        ctx = InteractionTelemetryContext(
            session_duration_minutes=25.0,
            consecutive_error_count=5
        )
        batch = self.engine.predict_all(ctx)
        decision = self.policy.evaluate_policy(batch, ctx)

        self.assertEqual(decision["approved_strategy"], StrategyID.AS08_BREAK_PAUSE.value)
        self.assertEqual(decision["action_type"], "OFFER_BREAK_OPTIONAL")
        self.assertTrue(decision["patient_dismissible"])
        self.assertIn("non-blocking 'Keep Playing'", decision["policy_rationale"])

    def test_no_automatic_forced_escalation_to_demonstration(self):
        # A simple error burst without prior cues must NOT jump straight to Demonstration (AS05)
        ctx = InteractionTelemetryContext(
            step_error_count=2,
            prior_strategy_offered=None  # Lower tiers not yet attempted
        )
        batch = self.engine.predict_all(ctx)
        decision = self.policy.evaluate_policy(batch, ctx)

        self.assertNotEqual(decision["approved_strategy"], StrategyID.AS05_DEMONSTRATION.value)

    def test_prohibition_of_cdr_category_tiering(self):
        ctx = InteractionTelemetryContext(
            task_name="StoryQuizGame",
            step_error_count=0
        )
        batch = self.engine.predict_all(ctx)

        # Passing CDR 0.5, 1.0, 2.0 must have zero effect on selected strategy
        dec_none = self.policy.evaluate_policy(batch, ctx, cdr_score=None)
        dec_mild = self.policy.evaluate_policy(batch, ctx, cdr_score=1.0)
        dec_mod = self.policy.evaluate_policy(batch, ctx, cdr_score=2.0)

        self.assertEqual(dec_none["approved_strategy"], dec_mild["approved_strategy"])
        self.assertEqual(dec_mild["approved_strategy"], dec_mod["approved_strategy"])
        self.assertTrue(dec_mild["cdr_tiering_prohibited"])


class TestEngineOutputConsistency(unittest.TestCase):
    """Verifies that engine produces deterministic, consistent outputs across repeated invocations."""

    def test_deterministic_consistency(self):
        engine = AssistancePredictionEngine()
        ctx = InteractionTelemetryContext(
            session_id="S_DET",
            patient_id="P_DET",
            task_name="StoryQuizGame",
            step_error_count=1,
            idle_duration_ms=1300.0,
            candidate_prompt_text="Select the correct story card."
        )

        res1 = engine.predict_all(ctx)
        res2 = engine.predict_all(ctx)

        for sid in engine.list_supported_strategies():
            r1 = res1.recommendations[sid]
            r2 = res2.recommendations[sid]
            self.assertEqual(r1.is_recommended, r2.is_recommended)
            self.assertEqual(r1.recommendation, r2.recommendation)
            self.assertEqual(r1.confidence_score, r2.confidence_score)
            self.assertEqual(r1.is_genuine_ml, r2.is_genuine_ml)


if __name__ == "__main__":
    unittest.main()
