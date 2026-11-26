"""
AS01-AS10 Strategy Predictor Implementations for NeuroCompanion Phase 4.5.
Defines individual strategy predictors with honest method classification:
- Genuine ML Supporting Signal: M6 dialogue quality used by AS09 (text empathy/actionability scorer ONLY —
  NOT a trained predictor of whether Verbal Explanation will benefit the patient).
- Genuine ML Supporting Signal: M3 error-risk used by AS10 (task error-occurrence classifier ONLY —
  NOT a trained independent-completion predictor).
- Statistical Reference: M1 Log-Normal patience bound (AS07)
- Provisional / Deterministic Logic: AS01, AS02, AS03, AS04, AS05, AS06, AS08, AS09, AS10

NOTE: AS09 and AS10 are PROVISIONAL recommendations. M6 and M3 contribute supporting signals
only; no model in Phase 4 was trained to predict assistance benefit or patient autonomy directly.
"""

import abc
from typing import Dict, Any, Optional
from datetime import datetime, timezone
import numpy as np

from .schemas import (
    StrategyID,
    RecommendationType,
    InteractionTelemetryContext,
    AssistancePredictionRecord
)
from .config import AssistanceConfig, DEFAULT_CONFIG
from .model_signals import ModelSignalProvider
from .feature_extractor import RuntimeFeatureExtractor


class BaseStrategyPredictor(abc.ABC):
    """Abstract base class for all ten assistance strategy predictors."""

    def __init__(
        self,
        strategy_id: StrategyID,
        strategy_name: str,
        config: Optional[AssistanceConfig] = None,
        model_signals: Optional[ModelSignalProvider] = None,
        feature_extractor: Optional[RuntimeFeatureExtractor] = None
    ):
        self.strategy_id = strategy_id
        self.strategy_name = strategy_name
        self.config = config or DEFAULT_CONFIG
        self.signals = model_signals or ModelSignalProvider()
        self.extractor = feature_extractor or RuntimeFeatureExtractor(self.signals)

    @abc.abstractmethod
    def predict(self, context: InteractionTelemetryContext) -> AssistancePredictionRecord:
        """Generates a structured prediction record based on runtime context."""
        pass


# ==============================================================================
# AS01: Repeat Instruction
# ==============================================================================

class AS01RepeatPredictor(BaseStrategyPredictor):
    """
    AS01: Repeat Instruction.
    Re-voices current step instruction when hesitation or attentional lapse occurs.
    Method: Deterministic Behavioral Rule supported by M3 task-error-risk prior.
    """

    def __init__(self, **kwargs):
        super().__init__(StrategyID.AS01_REPEAT, "Repeat Instruction", **kwargs)

    def predict(self, context: InteractionTelemetryContext) -> AssistancePredictionRecord:
        m3_risk = self.signals.get_m3_error_risk(context.task_name, (context.response_latency_ms or 0.0) / 1000.0)
        idle_ms = context.idle_duration_ms or 0.0

        is_explicit = context.patient_requested_repeat or (context.patient_requested_help and context.step_error_count == 0)
        is_latency_trigger = bool(idle_ms >= self.config.repeat_idle_latency_ms_threshold and context.target_miss_count == 0)
        is_miss_trigger = bool(context.target_miss_count >= self.config.repeat_target_miss_threshold and context.step_error_count == 0)

        is_rec = bool(is_explicit or is_latency_trigger or is_miss_trigger)

        confidence = None
        if is_explicit:
            confidence = 0.85
            rec_text = "Patient explicitly requested instruction repeat."
        elif is_latency_trigger:
            confidence = 0.60
            rec_text = f"Idle latency ({idle_ms:.0f}ms) exceeded threshold ({self.config.repeat_idle_latency_ms_threshold}ms)."
        elif is_miss_trigger:
            confidence = 0.55
            rec_text = f"Target miss ({context.target_miss_count}) observed without prior error."
        else:
            rec_text = "Instruction repeat not indicated at this time."

        return AssistancePredictionRecord(
            strategy_id=self.strategy_id.value,
            strategy_name=self.strategy_name,
            is_recommended=is_rec,
            recommendation=rec_text,
            recommendation_type=RecommendationType.DETERMINISTIC_RECOMMENDATION.value,
            evidence_features_used={
                "idle_duration_ms": context.idle_duration_ms,
                "target_miss_count": context.target_miss_count,
                "patient_requested_repeat": context.patient_requested_repeat,
                "patient_requested_help": context.patient_requested_help,
                "m3_supporting_error_probability": m3_risk["error_probability"]
            },
            model_method_source="rule_as01_latency_and_miss_threshold",
            is_genuine_ml=False,
            confidence_score=confidence,
            limitations="Deterministic behavioral rule. PREPARE acoustic dataset lacks repeat labels; based on client inactivity thresholds.",
            requires_patient_choice=True,
            requires_policy_approval=True
        )


# ==============================================================================
# AS02: Simplify Instruction
# ==============================================================================

class AS02SimplifyPredictor(BaseStrategyPredictor):
    """
    AS02: Simplify Instruction.
    Evaluates candidate prompt readability and recommends sentence shortening / vocabulary simplification.
    Method: Deterministic Formulaic Linguistic Analysis (Flesch-Kincaid Grade Level & Word Count).
    """

    def __init__(self, **kwargs):
        super().__init__(StrategyID.AS02_SIMPLIFY, "Simplify Instruction", **kwargs)

    def predict(self, context: InteractionTelemetryContext) -> AssistancePredictionRecord:
        ling = self.extractor.extract_linguistic_features(context.candidate_prompt_text)

        fkgl_high = ling["has_text"] and (ling["fkgl_grade_level"] > self.config.simplify_fkgl_grade_threshold)
        word_count_high = ling["has_text"] and (ling["word_count"] > self.config.simplify_word_count_threshold)
        repeated_hesitation = bool(context.prior_strategy_offered == StrategyID.AS01_REPEAT.value and context.prior_strategy_success is False)

        is_rec = bool(fkgl_high or word_count_high or repeated_hesitation)

        confidence = None
        if fkgl_high and word_count_high:
            confidence = 0.90
            rec_text = f"Candidate text exceeds 6th grade level (FKGL={ling['fkgl_grade_level']}) and word count limit ({ling['word_count']} words)."
        elif fkgl_high:
            confidence = 0.80
            rec_text = f"Candidate text exceeds 6th grade level (FKGL={ling['fkgl_grade_level']}). Simplification recommended."
        elif word_count_high:
            confidence = 0.75
            rec_text = f"Sentence length ({ling['word_count']} words) exceeds threshold ({self.config.simplify_word_count_threshold} words)."
        elif repeated_hesitation:
            confidence = 0.65
            rec_text = "Prior verbal repeat failed to elicit interaction. Sentence simplification indicated."
        else:
            rec_text = "Instruction text complexity is within accessible limits."

        return AssistancePredictionRecord(
            strategy_id=self.strategy_id.value,
            strategy_name=self.strategy_name,
            is_recommended=is_rec,
            recommendation=rec_text,
            recommendation_type=RecommendationType.DETERMINISTIC_RECOMMENDATION.value,
            evidence_features_used={
                "has_candidate_text": ling["has_text"],
                "fkgl_grade_level": ling["fkgl_grade_level"],
                "word_count": ling["word_count"],
                "prior_strategy_offered": context.prior_strategy_offered,
                "prior_strategy_success": context.prior_strategy_success
            },
            model_method_source="formula_flesch_kincaid_grade_level_v1",
            is_genuine_ml=False,
            confidence_score=confidence,
            limitations="Deterministic linguistic formula calculation. Does not predict individual patient cognitive comprehension.",
            requires_patient_choice=False,
            requires_policy_approval=True
        )


# ==============================================================================
# AS03: Step-by-Step Guidance
# ==============================================================================

class AS03StepGuidancePredictor(BaseStrategyPredictor):
    """
    AS03: Step-by-Step Guidance.
    Decomposes multi-step tasks into discrete atomic sub-actions.
    Method: Hierarchical Task State-Machine combined with M4 Historical Task-Difficulty Prior.
    """

    def __init__(self, **kwargs):
        super().__init__(StrategyID.AS03_STEP_GUIDANCE, "Step-by-Step Guidance", **kwargs)

    def predict(self, context: InteractionTelemetryContext) -> AssistancePredictionRecord:
        m4_prior = self.signals.get_m4_task_prior()
        is_multi_step = bool(context.step_total_steps >= self.config.step_guidance_min_task_steps)
        has_step_errors = bool(context.step_error_count >= self.config.step_guidance_step_error_threshold)

        is_rec = bool(is_multi_step and (has_step_errors or context.patient_requested_help))

        confidence = None
        if is_rec:
            confidence = 0.70
            rec_text = f"Multi-step task ({context.step_index}/{context.step_total_steps}) with errors/help request. Sequential sub-step guidance recommended."
        else:
            rec_text = "Step-by-step decomposition not required for current single-action step."

        return AssistancePredictionRecord(
            strategy_id=self.strategy_id.value,
            strategy_name=self.strategy_name,
            is_recommended=is_rec,
            recommendation=rec_text,
            recommendation_type=RecommendationType.DETERMINISTIC_RECOMMENDATION.value,
            evidence_features_used={
                "step_index": context.step_index,
                "step_total_steps": context.step_total_steps,
                "step_error_count": context.step_error_count,
                "patient_requested_help": context.patient_requested_help,
                "m4_overall_independence_rate": m4_prior["overall_independence_rate"]
            },
            model_method_source="state_machine_and_m4_casas_prior",
            is_genuine_ml=False,
            confidence_score=confidence,
            limitations="Deterministic task graph logic supported by CASAS domestic activity baseline prior. Not an interactive ML model.",
            requires_patient_choice=True,
            requires_policy_approval=True
        )


# ==============================================================================
# AS04: Visual Hint
# ==============================================================================

class AS04VisualHintPredictor(BaseStrategyPredictor):
    """
    AS04: Visual Hint.
    Highlights target 3D objects with pulsing outline or arrow pointer.
    Method: Deterministic Spatial Drift Rule supported by M3 Hurdle Error Risk Model.
    """

    def __init__(self, **kwargs):
        super().__init__(StrategyID.AS04_VISUAL_HINT, "Visual Hint", **kwargs)

    def predict(self, context: InteractionTelemetryContext) -> AssistancePredictionRecord:
        m3_risk = self.signals.get_m3_error_risk(context.task_name, (context.response_latency_ms or 0.0) / 1000.0)
        idle_ms = context.idle_duration_ms or 0.0

        spatial_miss_trigger = bool(context.target_miss_count >= self.config.visual_hint_target_miss_threshold)
        m3_risk_trigger = bool(m3_risk["error_probability"] >= self.config.visual_hint_m3_error_risk_cutoff and idle_ms >= self.config.visual_hint_idle_latency_ms)
        repeated_click_trigger = bool(context.repeated_click_count >= 3 and context.step_error_count > 0)

        is_rec = bool(spatial_miss_trigger or m3_risk_trigger or repeated_click_trigger)

        confidence = None
        if spatial_miss_trigger:
            confidence = 0.80
            rec_text = f"Spatial target misses ({context.target_miss_count}) indicate localization difficulty. Visual cue recommended."
        elif m3_risk_trigger:
            confidence = round(float(m3_risk["error_probability"]), 3)
            rec_text = f"High predicted error risk ({m3_risk['error_probability']:.2f}) with latency ({idle_ms:.0f}ms). Visual cue recommended."
        elif repeated_click_trigger:
            confidence = 0.70
            rec_text = f"Repeated rapid clicks ({context.repeated_click_count}) on incorrect target. Visual target highlight recommended."
        else:
            rec_text = "Visual hint not indicated."

        return AssistancePredictionRecord(
            strategy_id=self.strategy_id.value,
            strategy_name=self.strategy_name,
            is_recommended=is_rec,
            recommendation=rec_text,
            recommendation_type=RecommendationType.DETERMINISTIC_RECOMMENDATION.value,
            evidence_features_used={
                "target_miss_count": context.target_miss_count,
                "repeated_click_count": context.repeated_click_count,
                "idle_duration_ms": context.idle_duration_ms,
                "m3_supporting_error_probability": m3_risk["error_probability"]
            },
            model_method_source="rule_spatial_drift_and_m3_hurdle_prior",
            is_genuine_ml=False,
            confidence_score=confidence,
            limitations="Spatial miss rule supported by M3 error-risk prior. M3 trained on healthy older adults, not dementia visual search.",
            requires_patient_choice=True,
            requires_policy_approval=True
        )


# ==============================================================================
# AS05: Demonstration
# ==============================================================================

class AS05DemonstrationPredictor(BaseStrategyPredictor):
    """
    AS05: Demonstration.
    Animates physical demonstration of interaction in 3D.
    Method: Multi-Tier Escalation Fallback Policy.
    """

    def __init__(self, **kwargs):
        super().__init__(StrategyID.AS05_DEMONSTRATION, "Demonstration", **kwargs)

    def predict(self, context: InteractionTelemetryContext) -> AssistancePredictionRecord:
        idle_ms = context.idle_duration_ms or 0.0

        caregiver_override = bool(context.caregiver_locked_strategy == self.strategy_id.value)
        escalation_triggered = bool(
            context.step_error_count >= self.config.demonstration_error_threshold
            and idle_ms >= self.config.demonstration_idle_latency_ms
            and context.prior_strategy_offered in [StrategyID.AS01_REPEAT.value, StrategyID.AS04_VISUAL_HINT.value]
            and context.prior_strategy_success is False
        )

        is_rec = bool(caregiver_override or escalation_triggered)

        confidence = None
        if caregiver_override:
            confidence = 0.95
            rec_text = "Caregiver portal configuration specifies active Demonstration Mode."
        elif escalation_triggered:
            confidence = 0.70
            rec_text = f"Prior cues (AS01/AS04) unresolved with >= {self.config.demonstration_error_threshold} errors. Escalating to Demonstration."
        else:
            rec_text = "Demonstration not indicated. Lower-tier prompts must be exhausted first."

        return AssistancePredictionRecord(
            strategy_id=self.strategy_id.value,
            strategy_name=self.strategy_name,
            is_recommended=is_rec,
            recommendation=rec_text,
            recommendation_type=RecommendationType.DETERMINISTIC_RECOMMENDATION.value,
            evidence_features_used={
                "caregiver_locked_strategy": context.caregiver_locked_strategy,
                "step_error_count": context.step_error_count,
                "idle_duration_ms": context.idle_duration_ms,
                "prior_strategy_offered": context.prior_strategy_offered,
                "prior_strategy_success": context.prior_strategy_success
            },
            model_method_source="escalation_fallback_policy_v1",
            is_genuine_ml=False,
            confidence_score=confidence,
            limitations="High-assistance intervention. Deterministic multi-tier escalation rule. Zero ML training labels exist.",
            requires_patient_choice=True,
            requires_policy_approval=True
        )


# ==============================================================================
# AS06: Slower Speech
# ==============================================================================

class AS06SlowerSpeechPredictor(BaseStrategyPredictor):
    """
    AS06: Slower Speech.
    Adjusts text-to-speech speech rate multiplier (e.g. 0.85x or 0.75x).
    Method: Caregiver Portal Configuration & Patient Speed Setting Lookup.
    """

    def __init__(self, **kwargs):
        super().__init__(StrategyID.AS06_SLOWER_SPEECH, "Slower Speech", **kwargs)

    def predict(self, context: InteractionTelemetryContext) -> AssistancePredictionRecord:
        cg_rate = context.caregiver_speech_rate_setting
        is_rec = bool(cg_rate is not None and cg_rate < 1.0)

        target_rate = cg_rate if is_rec else self.config.speech_rate_default
        confidence = 1.0 if is_rec else None
        rec_text = f"Speech rate set to {target_rate}x based on caregiver preference." if is_rec else "Normal speech cadence (1.0x)."

        return AssistancePredictionRecord(
            strategy_id=self.strategy_id.value,
            strategy_name=self.strategy_name,
            is_recommended=is_rec,
            recommendation=rec_text,
            recommendation_type=RecommendationType.DETERMINISTIC_RECOMMENDATION.value,
            evidence_features_used={
                "caregiver_speech_rate_setting": cg_rate,
                "target_speech_rate": target_rate
            },
            model_method_source="caregiver_configuration_lookup",
            is_genuine_ml=False,
            confidence_score=confidence,
            limitations="Deterministic setting lookup. MDVR-KCL acoustic dataset provides descriptive reference only; cannot diagnose auditory processing speed.",
            requires_patient_choice=False,
            requires_policy_approval=True
        )


# ==============================================================================
# AS07: Additional Response Time
# ==============================================================================

class AS07ResponseTimePredictor(BaseStrategyPredictor):
    """
    AS07: Additional Response Time.
    Computes patience window before prompting using M1 Log-Normal latency distributions.
    Method: Parametric Statistical Reference Estimator.
    """

    def __init__(self, **kwargs):
        super().__init__(StrategyID.AS07_RESPONSE_TIME, "Additional Response Time", **kwargs)

    def predict(self, context: InteractionTelemetryContext) -> AssistancePredictionRecord:
        pacing = self.extractor.extract_pacing_features(context)
        ref = self.signals.get_m1_pacing_reference(context.task_domain)

        # Dynamic patience bound = P95 + safety buffer
        patience_window_ms = round(ref["p95_ms"] + self.config.pacing_safety_buffer_ms, 2)
        latency = context.response_latency_ms or 0.0
        within_patience = bool(latency <= patience_window_ms)

        rec_text = (
            f"Active domain '{ref['domain']}': Population 95th percentile is {ref['p95_ms']:.1f}ms. "
            f"Recommended patience window: {patience_window_ms:.1f}ms before offering prompts."
        )

        return AssistancePredictionRecord(
            strategy_id=self.strategy_id.value,
            strategy_name=self.strategy_name,
            is_recommended=within_patience,
            recommendation=rec_text,
            recommendation_type=RecommendationType.STATISTICAL_REFERENCE.value,
            evidence_features_used={
                "task_domain": context.task_domain,
                "observed_latency_ms": context.response_latency_ms,
                "reference_domain": ref["domain"],
                "m1_lognormal_p95_ms": ref["p95_ms"],
                "calculated_patience_window_ms": patience_window_ms,
                "sample_size": ref["sample_size"]
            },
            model_method_source=ref["source"],
            is_genuine_ml=False,
            confidence_score=0.95,  # Statistical coverage bound
            limitations="Statistical population percentile reference. FORBIDDEN to enforce as an individual patient timeout or premature task abort.",
            requires_patient_choice=False,
            requires_policy_approval=True
        )


# ==============================================================================
# AS08: Break / Pause
# ==============================================================================

class AS08BreakPausePredictor(BaseStrategyPredictor):
    """
    AS08: Break / Pause.
    Suggests optional rest pause or gentle session exit.
    Method: Deterministic Temporal Duration and Error-Burst Policy.
    """

    def __init__(self, **kwargs):
        super().__init__(StrategyID.AS08_BREAK_PAUSE, "Break / Pause", **kwargs)

    def predict(self, context: InteractionTelemetryContext) -> AssistancePredictionRecord:
        dur = context.session_duration_minutes
        consec_errors = context.consecutive_error_count

        time_trigger = bool(dur >= self.config.break_continuous_minutes_threshold)
        burst_trigger = bool(consec_errors >= self.config.break_consecutive_error_burst_threshold)
        explicit_trigger = context.patient_requested_break

        is_rec = bool(time_trigger or burst_trigger or explicit_trigger)

        confidence = None
        if explicit_trigger:
            confidence = 1.0
            rec_text = "Patient explicitly requested a break."
        elif time_trigger and burst_trigger:
            confidence = 0.90
            rec_text = f"Continuous play ({dur:.1f} min >= {self.config.break_continuous_minutes_threshold} min) and error burst ({consec_errors} errors). Gentle break suggested."
        elif time_trigger:
            confidence = 0.85
            rec_text = f"Continuous session ({dur:.1f} min) reached 20-minute threshold. Optional rest pause suggested."
        elif burst_trigger:
            confidence = 0.75
            rec_text = f"Consecutive errors ({consec_errors}) indicate fatigue or frustration. Optional pause suggested."
        else:
            rec_text = "Session duration and error rate within normal limits. Break not required."

        return AssistancePredictionRecord(
            strategy_id=self.strategy_id.value,
            strategy_name=self.strategy_name,
            is_recommended=is_rec,
            recommendation=rec_text,
            recommendation_type=RecommendationType.DETERMINISTIC_RECOMMENDATION.value,
            evidence_features_used={
                "session_duration_minutes": context.session_duration_minutes,
                "consecutive_error_count": context.consecutive_error_count,
                "patient_requested_break": context.patient_requested_break
            },
            model_method_source="session_fatigue_policy_v1",
            is_genuine_ml=False,
            confidence_score=confidence,
            limitations="Deterministic policy rule. STRICT SAFETY RULE: Breaks must remain optional with explicit 'Keep Playing' option. Automatic lockouts strictly forbidden.",
            requires_patient_choice=True,
            requires_policy_approval=True
        )


# ==============================================================================
# AS09: Verbal Explanation
# ==============================================================================

class AS09VerbalExplanationPredictor(BaseStrategyPredictor):
    """
    AS09: Verbal Explanation — PROVISIONAL recommendation.

    Uses the M6 RidgeCV dialogue scorer as a *supporting signal* to assess candidate
    explanation text surface quality (empathy and actionability).

    IMPORTANT: M6 is a text-quality scorer trained on AI4Hope dialogue ratings.
    It is NOT a trained predictor of whether Verbal Explanation will benefit a patient.
    AS09 is therefore a provisional recommendation informed by an ML supporting signal,
    not a genuine assistance-benefit ML prediction.
    Method: Provisional — ML Supporting Signal (M6 text quality) + deterministic fallback.
    """

    def __init__(self, **kwargs):
        super().__init__(StrategyID.AS09_VERBAL_EXPLANATION, "Verbal Explanation", **kwargs)

    def predict(self, context: InteractionTelemetryContext) -> AssistancePredictionRecord:
        candidate_text = context.candidate_prompt_text
        m6_eval = self.signals.get_m6_dialogue_quality(candidate_text)

        emp = m6_eval["predicted_empathy"]
        act = m6_eval["predicted_actionability"]
        is_fallback = m6_eval.get("is_fallback", False)

        has_valid_text = bool(candidate_text and len(candidate_text.strip()) > 0)
        meets_quality = bool(emp >= self.config.dialogue_min_empathy_score and act >= self.config.dialogue_min_actionability_score)
        is_rec = bool(has_valid_text and meets_quality)

        confidence = None
        if has_valid_text and not is_fallback:
            # Calibrated against M6 empirical Spearman rho = 0.3313
            confidence = round(float(np.clip((emp / 5.0) * 0.3313 + 0.50, 0.50, 0.90)), 3)
            rec_text = f"M6 Text Quality: Empathy={emp:.2f}/5.0, Actionability={act:.2f}/5.0. Explanation suitable for companion voicing."
        elif has_valid_text and is_fallback:
            rec_text = f"Fallback Text Quality: Empathy={emp:.2f}/5.0, Actionability={act:.2f}/5.0. Model artifact unavailable."
        else:
            rec_text = "No candidate explanation text provided to evaluate."

        return AssistancePredictionRecord(
            strategy_id=self.strategy_id.value,
            strategy_name=self.strategy_name,
            is_recommended=is_rec,
            recommendation=rec_text,
            recommendation_type=RecommendationType.ML_SUPPORTING_SIGNAL.value,
            evidence_features_used={
                "has_candidate_text": has_valid_text,
                "m6_predicted_empathy": emp,
                "m6_predicted_actionability": act,
                "is_fallback": is_fallback
            },
            model_method_source=m6_eval["source"],
            # AS09 is PROVISIONAL: M6 is a text-quality scorer, not a trained AS09 benefit predictor.
            # is_genuine_ml reflects whether M6 artifact was available, NOT that AS09 is ML-validated.
            is_genuine_ml=False,
            confidence_score=confidence,
            limitations=(
                "PROVISIONAL. M6 scores surface empathy and actionability of text only. "
                "M6 is NOT a trained predictor of whether Verbal Explanation will benefit this patient. "
                "No model in Phase 4 was trained to predict assistance benefit directly."
            ),
            requires_patient_choice=True,
            requires_policy_approval=True
        )


# ==============================================================================
# AS10: Independent Completion
# ==============================================================================

class AS10IndependentCompletionPredictor(BaseStrategyPredictor):
    """
    AS10: Independent Completion — PROVISIONAL recommendation.

    Uses M3 Hurdle classifier error-free probability as a *supporting signal* to estimate
    task completion risk, combined with observable session behaviour (zero errors, no help requests).

    IMPORTANT: M3 predicts task error occurrence and error counts on ADL datasets.
    It is NOT a trained independent-completion predictor and was NOT trained to predict
    whether a patient will successfully complete a task autonomously.
    AS10 is therefore a provisional recommendation based on available completion observations
    and M3 error-risk signal — not a trained ML assistance-benefit prediction.
    Method: Provisional — ML Supporting Signal (M3 error-free probability) + behavioural observations.
    """

    def __init__(self, **kwargs):
        super().__init__(StrategyID.AS10_INDEPENDENT, "Independent Completion", **kwargs)

    def predict(self, context: InteractionTelemetryContext) -> AssistancePredictionRecord:
        rt_sec = (context.response_latency_ms or 0.0) / 1000.0
        m3_risk = self.signals.get_m3_error_risk(context.task_name, rt_sec)
        m4_prior = self.signals.get_m4_task_prior()

        # M3 Error-free probability = 1.0 - error_probability
        error_prob = m3_risk["error_probability"]
        error_free_prob = round(float(1.0 - error_prob), 4)

        has_no_errors = bool(context.step_error_count == 0)
        no_help_requested = bool(not context.patient_requested_help and not context.patient_requested_repeat)
        is_autonomous = bool(has_no_errors and no_help_requested and (error_free_prob >= self.config.independence_min_error_free_prob))

        confidence = round(error_free_prob, 3) if is_autonomous else None
        if is_autonomous:
            rec_text = f"M3 Error-free probability is {error_free_prob:.2f}. Patient proceeding autonomously; withhold prompts."
        else:
            rec_text = f"Step error count ({context.step_error_count}) or help request active. Autonomy suppressed."

        return AssistancePredictionRecord(
            strategy_id=self.strategy_id.value,
            strategy_name=self.strategy_name,
            is_recommended=is_autonomous,
            recommendation=rec_text,
            recommendation_type=RecommendationType.ML_SUPPORTING_SIGNAL.value,
            evidence_features_used={
                "step_error_count": context.step_error_count,
                "patient_requested_help": context.patient_requested_help,
                "m3_error_probability": error_prob,
                "m3_error_free_probability": error_free_prob,
                "m4_historical_independence_rate": m4_prior["overall_independence_rate"]
            },
            model_method_source=m3_risk["source"],
            # AS10 is PROVISIONAL: M3 is a task error-risk classifier, not a trained autonomy predictor.
            # is_genuine_ml is always False — no Phase 4 model predicts independent-completion benefit.
            is_genuine_ml=False,
            confidence_score=confidence,
            limitations=(
                "PROVISIONAL. M3 predicts task error occurrence (trained on healthy older adults, 77.5% zero-inflation). "
                "M3 is NOT a trained predictor of patient independent-completion readiness or assistance benefit. "
                "Recommendation is based on observable session behaviour (zero errors, no help requests) "
                "informed by M3 error-risk signal as a supporting proxy only."
            ),
            requires_patient_choice=False,
            requires_policy_approval=True
        )
