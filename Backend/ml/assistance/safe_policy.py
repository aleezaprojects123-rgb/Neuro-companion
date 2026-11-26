"""
SafeAssistancePolicy — Phase 4.6 integrated policy decision engine.

Implements the full decision flow:

    InteractionTelemetryContext
        -> AssistancePredictionEngine  (Phase 4.5: generate candidate recommendations)
        -> AssistancePolicyInterface   (Phase 4.5: apply safety/governance rules)
        -> SafeAssistancePolicy        (Phase 4.6: produce auditable PolicyDecision)

Design rules (non-negotiable):
    1. A prediction is a candidate, not permission to act.
    2. Caregiver locks and explicit overrides take absolute precedence.
    3. Patient agency: help requests are always honoured; dismissals always respected.
    4. AS08 Break is always optional — never a lockout.
    5. Missing or insufficient telemetry -> INSUFFICIENT_DATA or NO_ADDITIONAL_ASSISTANCE.
    6. Slow response or isolated error alone -> never automatic assistance.
    7. CDR scores are never accepted and never mapped to strategies.
    8. No automatic escalation after a fixed error count.
    9. NO_ADDITIONAL_ASSISTANCE is a valid and expected first-class outcome.
   10. No diagnosis, clinical claims, or claims of proven assistance effectiveness.
"""

import logging
from typing import Any, Dict, List, Optional

from .config import AssistanceConfig, DEFAULT_CONFIG
from .policy_decision import DecisionStatus, PatientChoiceStatus, PolicyDecision
from .policy_interface import AssistancePolicyInterface
from .prediction_engine import AssistancePredictionEngine
from .schemas import InteractionTelemetryContext, StrategyID

logger = logging.getLogger(__name__)


# ---------------------------------------------------------------------------
# Audit rule name constants
# ---------------------------------------------------------------------------
RULE_DATA_SUFFICIENCY = "DATA_SUFFICIENCY_CHECK"
RULE_CDR_PROHIBITION = "CDR_PROHIBITION"
RULE_CAREGIVER_LOCK = "CAREGIVER_LOCK_PRIORITY"
RULE_PATIENT_HELP_REQUEST = "PATIENT_HELP_REQUEST_IMMEDIACY"
RULE_PATIENT_BREAK_REQUEST = "PATIENT_BREAK_REQUEST"
RULE_PATIENT_REPEAT_REQUEST = "PATIENT_REPEAT_REQUEST"
RULE_PATIENT_DISMISSAL = "PATIENT_DISMISSAL_GUARD"
RULE_COOLDOWN = "SPONTANEOUS_PROMPT_COOLDOWN_45S"
RULE_BREAK_OPTIONAL = "AS08_BREAK_MUST_BE_OPTIONAL"
RULE_NO_FORCED_ESCALATION = "NO_AUTOMATIC_ESCALATION_ON_ERROR_COUNT"
RULE_CANDIDATE_SELECTION = "CANDIDATE_STRATEGY_SELECTION"
RULE_SILENT_AUTONOMY = "SILENT_AUTONOMY_FALLBACK"

# Strategies that require the patient to actively accept before Carl delivers them
_PATIENT_CHOICE_REQUIRED_STRATEGIES = {
    StrategyID.AS02_SIMPLIFY.value,
    StrategyID.AS03_STEP_GUIDANCE.value,
    StrategyID.AS05_DEMONSTRATION.value,
    StrategyID.AS08_BREAK_PAUSE.value,
    StrategyID.AS09_VERBAL_EXPLANATION.value,
}

# Mandatory identifiers — decisions cannot be made without these
_REQUIRED_TELEMETRY_FIELDS = ["session_id", "patient_id"]


class SafeAssistancePolicy:
    """
    Phase 4.6 integrated safe assistance policy.

    Usage
    -----
    policy = SafeAssistancePolicy()
    decision = policy.decide(context, seconds_since_last_prompt=60.0)

    Returns a PolicyDecision — always. Never raises for expected inputs.
    """

    def __init__(
        self,
        config: Optional[AssistanceConfig] = None,
        engine: Optional[AssistancePredictionEngine] = None,
        policy_interface: Optional[AssistancePolicyInterface] = None,
    ):
        self.config = config or DEFAULT_CONFIG
        self.engine = engine or AssistancePredictionEngine(config=self.config)
        self.policy = policy_interface or AssistancePolicyInterface(config=self.config)

    # ------------------------------------------------------------------
    # Public API
    # ------------------------------------------------------------------

    def decide(
        self,
        context: InteractionTelemetryContext,
        seconds_since_last_prompt: float = 60.0,
        cdr_score: Optional[float] = None,
    ) -> PolicyDecision:
        """
        Run the full Phase 4.6 decision flow and return a PolicyDecision.

        Parameters
        ----------
        context : InteractionTelemetryContext
            Runtime interaction telemetry.
        seconds_since_last_prompt : float
            Seconds since last spontaneous prompt. Must be >= 45 s for new
            spontaneous prompts to be approved.
        cdr_score : Optional[float]
            Accepted but NEVER used. CDR scores must never influence decisions.
            Any value is silently discarded; the prohibition is logged.

        Returns
        -------
        PolicyDecision
        """
        rules_applied: List[str] = []

        # Rule 0 — CDR prohibition: accept, discard, audit
        rules_applied.append(RULE_CDR_PROHIBITION)
        if cdr_score is not None:
            logger.warning(
                "SafeAssistancePolicy.decide: cdr_score=%s supplied but is "
                "PROHIBITED from influencing decisions. Value discarded.",
                cdr_score,
            )

        # Rule 1 — data sufficiency check
        rules_applied.append(RULE_DATA_SUFFICIENCY)
        insufficiency_reason = self._check_data_sufficiency(context)
        if insufficiency_reason:
            return self._make_decision(
                context=context,
                selected_strategy_id=None,
                status=DecisionStatus.INSUFFICIENT_DATA,
                reason=insufficiency_reason,
                prediction_ref=None,
                rules=rules_applied,
                caregiver=False,
                patient_choice=False,
            )

        # Rule 2 — generate Phase 4.5 candidate predictions
        try:
            batch = self.engine.predict_all(context)
        except Exception as exc:  # pragma: no cover
            logger.error("AssistancePredictionEngine failed: %s", exc, exc_info=True)
            return self._make_decision(
                context=context,
                selected_strategy_id=None,
                status=DecisionStatus.NO_ADDITIONAL_ASSISTANCE,
                reason=f"Prediction engine error — no assistance offered. ({exc})",
                prediction_ref=None,
                rules=rules_applied,
                caregiver=False,
                patient_choice=False,
            )

        # Rule 3 — apply Phase 4.5 safety/governance arbitration
        raw_policy = self.policy.evaluate_policy(
            batch_result=batch,
            context=context,
            seconds_since_last_prompt=seconds_since_last_prompt,
            cdr_score=None,  # never forwarded
        )

        action_type: str = raw_policy.get("action_type", "")
        approved_id: Optional[str] = raw_policy.get("approved_strategy")
        policy_rationale: str = raw_policy.get("policy_rationale", "")
        selected_record: Optional[Dict[str, Any]] = raw_policy.get("selected_record")

        # ------------------------------------------------------------------
        # Map Phase 4.5 policy action_type -> Phase 4.6 DecisionStatus
        # ------------------------------------------------------------------

        if action_type == "CAREGIVER_LOCK":
            rules_applied.append(RULE_CAREGIVER_LOCK)
            return self._make_decision(
                context=context,
                selected_strategy_id=approved_id,
                status=DecisionStatus.CAREGIVER_OVERRIDE,
                reason=policy_rationale,
                prediction_ref=selected_record,
                rules=rules_applied,
                caregiver=True,
                patient_choice=(
                    approved_id in _PATIENT_CHOICE_REQUIRED_STRATEGIES
                    if approved_id else False
                ),
            )

        if action_type == "SUPPRESS_DISMISSED":
            rules_applied.append(RULE_PATIENT_DISMISSAL)
            return self._make_decision(
                context=context,
                selected_strategy_id=None,
                status=DecisionStatus.SUPPRESSED_BY_PATIENT,
                reason=policy_rationale,
                prediction_ref=None,
                rules=rules_applied,
                caregiver=False,
                patient_choice=False,
            )

        if action_type == "SUPPRESS_COOLDOWN":
            rules_applied.append(RULE_COOLDOWN)
            return self._make_decision(
                context=context,
                selected_strategy_id=None,
                status=DecisionStatus.SUPPRESSED_COOLDOWN,
                reason=policy_rationale,
                prediction_ref=None,
                rules=rules_applied,
                caregiver=False,
                patient_choice=False,
            )

        if action_type == "OFFER_BREAK_OPTIONAL":
            rules_applied.append(RULE_BREAK_OPTIONAL)
            if context.patient_requested_break:
                rules_applied.append(RULE_PATIENT_BREAK_REQUEST)
                status = DecisionStatus.PATIENT_HELP_REQUEST
            else:
                status = DecisionStatus.APPROVED
            return self._make_decision(
                context=context,
                selected_strategy_id=StrategyID.AS08_BREAK_PAUSE.value,
                status=status,
                reason=policy_rationale,
                prediction_ref=selected_record,
                rules=rules_applied,
                caregiver=False,
                patient_choice=True,  # AS08 is ALWAYS patient-choice
            )

        if action_type == "OFFER_PROMPT":
            rules_applied.append(RULE_NO_FORCED_ESCALATION)
            rules_applied.append(RULE_CANDIDATE_SELECTION)
            is_patient_initiated = (
                context.patient_requested_help or context.patient_requested_repeat
            )
            if is_patient_initiated:
                rules_applied.append(RULE_PATIENT_HELP_REQUEST)
                status = DecisionStatus.PATIENT_HELP_REQUEST
            else:
                status = DecisionStatus.APPROVED
            choice_required = (
                approved_id in _PATIENT_CHOICE_REQUIRED_STRATEGIES
                if approved_id else False
            )
            return self._make_decision(
                context=context,
                selected_strategy_id=approved_id,
                status=status,
                reason=policy_rationale,
                prediction_ref=selected_record,
                rules=rules_applied,
                caregiver=False,
                patient_choice=choice_required,
            )

        if action_type == "SILENT_AUTONOMY":
            rules_applied.append(RULE_SILENT_AUTONOMY)
            return self._make_decision(
                context=context,
                selected_strategy_id=None,
                status=DecisionStatus.NO_ADDITIONAL_ASSISTANCE,
                reason=policy_rationale,
                prediction_ref=selected_record,
                rules=rules_applied,
                caregiver=False,
                patient_choice=False,
            )

        # Unknown action_type from upstream — safe default
        logger.warning(
            "SafeAssistancePolicy: unrecognised action_type '%s'. "
            "Returning NO_ADDITIONAL_ASSISTANCE.",
            action_type,
        )
        rules_applied.append(RULE_SILENT_AUTONOMY)
        return self._make_decision(
            context=context,
            selected_strategy_id=None,
            status=DecisionStatus.NO_ADDITIONAL_ASSISTANCE,
            reason=f"Unrecognised policy action '{action_type}'. No assistance offered.",
            prediction_ref=None,
            rules=rules_applied,
            caregiver=False,
            patient_choice=False,
        )

    # ------------------------------------------------------------------
    # Private helpers
    # ------------------------------------------------------------------

    @staticmethod
    def _check_data_sufficiency(
        context: InteractionTelemetryContext,
    ) -> Optional[str]:
        """
        Returns a human-readable reason if mandatory identifiers are absent.
        Returns None when data is sufficient.

        IMPORTANT: Optional numeric fields (latency, errors, etc.) being None or
        zero is normal and must NOT trigger automatic assistance or INSUFFICIENT_DATA.
        Only absent mandatory identifiers are checked here.
        """
        for fname in _REQUIRED_TELEMETRY_FIELDS:
            val = getattr(context, fname, None)
            if not val or (isinstance(val, str) and not val.strip()):
                return (
                    f"Required telemetry field '{fname}' is missing or empty. "
                    "No decision can be made without valid patient and session identifiers."
                )
        return None

    @staticmethod
    def _make_decision(
        context: InteractionTelemetryContext,
        selected_strategy_id: Optional[str],
        status: DecisionStatus,
        reason: str,
        prediction_ref: Optional[Dict[str, Any]],
        rules: List[str],
        caregiver: bool,
        patient_choice: bool,
    ) -> PolicyDecision:
        choice_status = (
            PatientChoiceStatus.PENDING
            if patient_choice
            else PatientChoiceStatus.NOT_REQUIRED
        )
        return PolicyDecision(
            patient_id=getattr(context, "patient_id", "UNKNOWN"),
            session_id=getattr(context, "session_id", "UNKNOWN"),
            task_name=getattr(context, "task_name", "UNKNOWN"),
            selected_strategy_id=selected_strategy_id,
            decision_status=status,
            decision_reason=reason,
            prediction_reference=prediction_ref,
            policy_rules_applied=list(rules),
            caregiver_control_applied=caregiver,
            patient_choice_required=patient_choice,
            patient_choice_status=choice_status,
        )
