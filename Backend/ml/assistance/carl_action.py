"""
Carl Action Adapter for NeuroCompanion Phase 4.7.

Translates approved policy decisions into abstract, executable companion actions
independent of Unity scenes, Animators, audio clips, or game objects.

Safety Guarantees:
- Only approved, patient-requested, or caregiver-overridden decisions generate executable actions.
- AS08 always produces an optional break offer (never a forced break).
- Neutral, suppressed, rejected, or insufficient decisions produce NO action (requires_carl_action=False).
"""

from dataclasses import dataclass, field
from enum import Enum
from typing import Any, Dict, Optional

from .policy_decision import DecisionStatus, PolicyDecision
from .schemas import StrategyID


class CarlActionType(str, Enum):
    """Abstract action identifiers for Carl companion interventions."""
    REPEAT_CURRENT_INSTRUCTION = "REPEAT_CURRENT_INSTRUCTION"
    SIMPLIFY_CURRENT_INSTRUCTION = "SIMPLIFY_CURRENT_INSTRUCTION"
    PROVIDE_STEP_BY_STEP_GUIDANCE = "PROVIDE_STEP_BY_STEP_GUIDANCE"
    SHOW_VISUAL_HINT = "SHOW_VISUAL_HINT"
    SHOW_DEMONSTRATION = "SHOW_DEMONSTRATION"
    SLOW_SPEECH = "SLOW_SPEECH"
    ALLOW_ADDITIONAL_RESPONSE_TIME = "ALLOW_ADDITIONAL_RESPONSE_TIME"
    OFFER_OPTIONAL_BREAK = "OFFER_OPTIONAL_BREAK"
    PROVIDE_VERBAL_EXPLANATION = "PROVIDE_VERBAL_EXPLANATION"
    ALLOW_INDEPENDENT_COMPLETION = "ALLOW_INDEPENDENT_COMPLETION"


@dataclass
class CarlAction:
    """
    Abstract, platform-agnostic companion action specification.

    Attributes:
        action_type: Canonical CarlActionType enum value.
        strategy_id: Originating StrategyID string.
        is_optional: True if action requires explicit patient consent before execution (always True for AS08).
        parameters: Action-specific execution parameters (e.g. speech rate, hint duration).
        description: Human-readable narrative description of what Carl does.
    """
    action_type: CarlActionType
    strategy_id: str
    is_optional: bool
    parameters: Dict[str, Any] = field(default_factory=dict)
    description: str = ""

    def to_dict(self) -> Dict[str, Any]:
        return {
            "action_type": self.action_type.value,
            "strategy_id": self.strategy_id,
            "is_optional": self.is_optional,
            "parameters": self.parameters,
            "description": self.description,
        }


class CarlActionAdapter:
    """
    Translates approved PolicyDecision records into abstract CarlActions.

    Guarantees:
    - Neutral/suppressed/insufficient outcomes return None.
    - AS08 always maps to an optional break offer with is_optional=True.
    - Parameters are cleanly extracted from prediction evidence.
    """

    # Direct mapping from StrategyID values to CarlActionTypes
    _STRATEGY_TO_ACTION: Dict[str, CarlActionType] = {
        StrategyID.AS01_REPEAT.value: CarlActionType.REPEAT_CURRENT_INSTRUCTION,
        StrategyID.AS02_SIMPLIFY.value: CarlActionType.SIMPLIFY_CURRENT_INSTRUCTION,
        StrategyID.AS03_STEP_GUIDANCE.value: CarlActionType.PROVIDE_STEP_BY_STEP_GUIDANCE,
        StrategyID.AS04_VISUAL_HINT.value: CarlActionType.SHOW_VISUAL_HINT,
        StrategyID.AS05_DEMONSTRATION.value: CarlActionType.SHOW_DEMONSTRATION,
        StrategyID.AS06_SLOWER_SPEECH.value: CarlActionType.SLOW_SPEECH,
        StrategyID.AS07_RESPONSE_TIME.value: CarlActionType.ALLOW_ADDITIONAL_RESPONSE_TIME,
        StrategyID.AS08_BREAK_PAUSE.value: CarlActionType.OFFER_OPTIONAL_BREAK,
        StrategyID.AS09_VERBAL_EXPLANATION.value: CarlActionType.PROVIDE_VERBAL_EXPLANATION,
        StrategyID.AS10_INDEPENDENT.value: CarlActionType.ALLOW_INDEPENDENT_COMPLETION,
    }

    # Allowed statuses that can trigger executable Carl actions
    _EXECUTABLE_STATUSES = {
        DecisionStatus.APPROVED,
        DecisionStatus.PATIENT_HELP_REQUEST,
        DecisionStatus.CAREGIVER_OVERRIDE,
    }

    def adapt(self, decision: PolicyDecision) -> Optional[CarlAction]:
        """
        Translates a PolicyDecision into a CarlAction if approved.
        Returns None if decision is suppressed, rejected, neutral, or insufficient.
        """
        # Rule 1: Only executable statuses with an explicit selected strategy may produce actions
        if decision.decision_status not in self._EXECUTABLE_STATUSES:
            return None

        if not decision.selected_strategy_id:
            return None

        strategy_id = decision.selected_strategy_id
        action_type = self._STRATEGY_TO_ACTION.get(strategy_id)
        if not action_type:
            # Check if strategy_id matches by prefix or enum name
            for sid, at in self._STRATEGY_TO_ACTION.items():
                if strategy_id == sid or strategy_id in sid:
                    action_type = at
                    strategy_id = sid
                    break

        if not action_type:
            return None

        # Build action parameters from prediction reference if available
        params = self._extract_parameters(action_type, strategy_id, decision)

        # Rule 2: AS08 must always be optional, never forced
        is_optional = (
            action_type == CarlActionType.OFFER_OPTIONAL_BREAK
            or decision.patient_choice_required
        )

        description = self._generate_description(action_type, decision)

        return CarlAction(
            action_type=action_type,
            strategy_id=strategy_id,
            is_optional=is_optional,
            parameters=params,
            description=description,
        )

    def _extract_parameters(
        self,
        action_type: CarlActionType,
        strategy_id: str,
        decision: PolicyDecision
    ) -> Dict[str, Any]:
        """Extracts specific action parameters safely from decision and prediction references."""
        params: Dict[str, Any] = {}
        pred_ref = decision.prediction_reference or {}
        evidence = pred_ref.get("evidence_features_used", {})

        if action_type == CarlActionType.REPEAT_CURRENT_INSTRUCTION:
            params["repeat_mode"] = "verbatim"
            params["speech_rate"] = 1.0

        elif action_type == CarlActionType.SIMPLIFY_CURRENT_INSTRUCTION:
            params["target_fkgl"] = 4.0
            params["max_words"] = 8
            params["simplification_mode"] = "direct_short_prompt"

        elif action_type == CarlActionType.PROVIDE_STEP_BY_STEP_GUIDANCE:
            params["guidance_mode"] = "step_by_step"
            params["step_error_count"] = evidence.get("step_error_count", 0)

        elif action_type == CarlActionType.SHOW_VISUAL_HINT:
            params["hint_type"] = "spatial_highlight"
            params["duration_ms"] = 3000
            if "m3_error_probability" in evidence:
                params["error_risk_weight"] = evidence["m3_error_probability"]

        elif action_type == CarlActionType.SHOW_DEMONSTRATION:
            params["demonstration_type"] = "full_sequence"
            params["interactive"] = True

        elif action_type == CarlActionType.SLOW_SPEECH:
            params["speech_rate_multiplier"] = 0.85

        elif action_type == CarlActionType.ALLOW_ADDITIONAL_RESPONSE_TIME:
            params["patience_window_ms"] = evidence.get("pacing_window_ms", 1880.0)
            params["cohort_reference"] = "dementia_nback_p95_buffer"

        elif action_type == CarlActionType.OFFER_OPTIONAL_BREAK:
            params["break_type"] = "optional_pause"
            params["allow_keep_playing"] = True
            params["force_lockout"] = False

        elif action_type == CarlActionType.PROVIDE_VERBAL_EXPLANATION:
            params["explanation_type"] = "verbal_reassurance"
            if "m6_predicted_empathy" in evidence:
                params["empathy_score"] = evidence["m6_predicted_empathy"]
            if "m6_predicted_actionability" in evidence:
                params["actionability_score"] = evidence["m6_predicted_actionability"]

        elif action_type == CarlActionType.ALLOW_INDEPENDENT_COMPLETION:
            params["independent_mode"] = True
            params["suppress_cues"] = True

        return params

    def _generate_description(
        self,
        action_type: CarlActionType,
        decision: PolicyDecision
    ) -> str:
        """Produces a clear narrative description of Carl's intended intervention."""
        prefix = f"Carl executes {action_type.value}"
        if decision.decision_status == DecisionStatus.CAREGIVER_OVERRIDE:
            return f"{prefix} as mandated by caregiver portal preset lock."
        if decision.decision_status == DecisionStatus.PATIENT_HELP_REQUEST:
            return f"{prefix} in immediate response to patient request."
        if action_type == CarlActionType.OFFER_OPTIONAL_BREAK:
            return "Carl gently offers an optional rest break while keeping the activity fully available."
        return f"{prefix} based on evaluated safe policy recommendation: {decision.decision_reason}"
