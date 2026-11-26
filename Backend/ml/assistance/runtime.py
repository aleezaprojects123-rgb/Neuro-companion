"""
Assistance Runtime for NeuroCompanion Phase 4.7.

The central operational entry point connecting:
    Patient Interaction Data
    -> Telemetry Validation & Feature Extraction
    -> AS01-AS10 Prediction Engine (Phase 4.5)
    -> Safe Assistance Policy (Phase 4.6)
    -> Final Assistance Decision
    -> Carl Action Adapter
    -> Structured Response for Carl/Unity & Traceable Persistence

Non-negotiable Rules:
- Only approved, patient-initiated, or caregiver-overridden decisions generate Carl actions.
- AS08 break is strictly optional; never force a break or lock out the patient.
- Suppressed, rejected, neutral, or insufficient telemetry produces NO Carl action.
- CDR scores are strictly prohibited and discarded.
- Runtime errors degrade gracefully to an ERROR status without fabricating assistance.
"""

import logging
import uuid
from typing import Any, Dict, Optional, Union

from .carl_action import CarlActionAdapter
from .config import AssistanceConfig, DEFAULT_CONFIG
from .feature_extractor import RuntimeFeatureExtractor
from .persistence import AssistanceDecisionRepository
from .policy_decision import DecisionStatus, PatientChoiceStatus, PolicyDecision
from .runtime_schemas import (
    AssistanceRuntimeRequest,
    AssistanceRuntimeResponse,
    RuntimeStatus,
)
from .safe_policy import SafeAssistancePolicy
from .schemas import InteractionTelemetryContext, StrategyID

logger = logging.getLogger(__name__)


class AssistanceRuntime:
    """
    Unified assistance runtime orchestrating the complete prediction-policy-action pipeline.
    """

    def __init__(
        self,
        config: Optional[AssistanceConfig] = None,
        policy: Optional[SafeAssistancePolicy] = None,
        action_adapter: Optional[CarlActionAdapter] = None,
        repository: Optional[AssistanceDecisionRepository] = None,
        feature_extractor: Optional[RuntimeFeatureExtractor] = None,
    ):
        self.config = config or DEFAULT_CONFIG
        self.policy = policy or SafeAssistancePolicy(config=self.config)
        self.action_adapter = action_adapter or CarlActionAdapter()
        self.repository = repository
        self.extractor = feature_extractor or RuntimeFeatureExtractor()

    def process(
        self,
        request_or_context: Union[AssistanceRuntimeRequest, InteractionTelemetryContext, Dict[str, Any]],
        seconds_since_last_prompt: float = 60.0,
        cdr_score: Optional[float] = None,
        request_id: Optional[str] = None,
    ) -> AssistanceRuntimeResponse:
        """
        Processes an interaction step through the unified assistance runtime.

        Parameters:
            request_or_context: AssistanceRuntimeRequest, InteractionTelemetryContext, or raw dictionary.
            seconds_since_last_prompt: Time in seconds since last spontaneous cue (used if request_or_context is not AssistanceRuntimeRequest).
            cdr_score: Ignored / discarded per safety governance.
            request_id: Optional tracking identifier.

        Returns:
            AssistanceRuntimeResponse: Complete decision metadata and Carl action specification.
        """
        # 1. Unpack Request
        req_id = request_id or str(uuid.uuid4())
        cooldown_seconds = seconds_since_last_prompt
        raw_cdr = cdr_score

        if isinstance(request_or_context, AssistanceRuntimeRequest):
            raw_context = request_or_context.context
            cooldown_seconds = request_or_context.seconds_since_last_prompt
            raw_cdr = request_or_context.cdr_score
            req_id = request_or_context.request_id
        else:
            raw_context = request_or_context

        # 2. Input Type Validation & Sanitization
        try:
            context = self._validate_and_sanitize(raw_context)
        except Exception as exc:
            logger.warning("Telemetry pre-flight validation failed: %s", exc)
            err_resp = self._make_insufficient_or_error_response(
                request_id=req_id,
                status=RuntimeStatus.INSUFFICIENT_DATA,
                reason=f"Invalid telemetry context format: {exc}",
                patient_id="UNKNOWN",
                session_id="UNKNOWN",
            )
            self._persist_if_available(err_resp)
            return err_resp

        # 3. Check Data Sufficiency
        if not context.patient_id or not context.patient_id.strip() or not context.session_id or not context.session_id.strip():
            logger.info("Context missing mandatory patient_id or session_id.")
            insufficient_resp = self._make_insufficient_or_error_response(
                request_id=req_id,
                status=RuntimeStatus.INSUFFICIENT_DATA,
                reason="Mandatory telemetry fields (patient_id, session_id) are missing or blank.",
                patient_id=context.patient_id or "UNKNOWN",
                session_id=context.session_id or "UNKNOWN",
            )
            self._persist_if_available(insufficient_resp)
            return insufficient_resp

        # 4. Evaluate Safe Policy Pipeline (coordinates prediction engine and governance)
        try:
            decision = self.policy.decide(
                context=context,
                seconds_since_last_prompt=cooldown_seconds,
                cdr_score=raw_cdr,
            )
        except Exception as exc:
            logger.error("Safe policy pipeline failed unexpectedly: %s", exc, exc_info=True)
            error_resp = self._make_insufficient_or_error_response(
                request_id=req_id,
                status=RuntimeStatus.ERROR,
                reason=f"Policy evaluation failure: {exc}",
                patient_id=context.patient_id,
                session_id=context.session_id,
            )
            self._persist_if_available(error_resp)
            return error_resp

        # 5. Map Decision to RuntimeStatus
        runtime_status = self._map_to_runtime_status(decision)

        # 6. Adapt to Carl Action
        carl_action_obj = None
        if decision.decision_status in {
            DecisionStatus.APPROVED,
            DecisionStatus.PATIENT_HELP_REQUEST,
            DecisionStatus.CAREGIVER_OVERRIDE,
        }:
            carl_action_obj = self.action_adapter.adapt(decision)

        requires_carl_action = carl_action_obj is not None
        carl_action_dict = carl_action_obj.to_dict() if carl_action_obj else None

        # 7. Assemble Structured Response
        response = AssistanceRuntimeResponse(
            request_id=req_id,
            patient_id=decision.patient_id,
            session_id=decision.session_id,
            runtime_status=runtime_status,
            decision_id=decision.decision_id,
            decision_status=decision.decision_status.value if decision.decision_status else None,
            selected_strategy_id=decision.selected_strategy_id,
            decision_reason=decision.decision_reason,
            prediction_reference=decision.prediction_reference,
            policy_rules_applied=decision.policy_rules_applied,
            caregiver_control_applied=decision.caregiver_control_applied,
            patient_choice_required=decision.patient_choice_required,
            patient_choice_status=(
                decision.patient_choice_status.value
                if isinstance(decision.patient_choice_status, PatientChoiceStatus)
                else str(decision.patient_choice_status)
            ),
            requires_carl_action=requires_carl_action,
            carl_action=carl_action_dict,
            created_at=decision.created_at,
        )

        # 8. Persistence & Traceability
        self._persist_if_available(response)

        return response

    def _validate_and_sanitize(self, raw: Any) -> InteractionTelemetryContext:
        """Validates input type and converts into InteractionTelemetryContext."""
        if raw is None:
            raise ValueError("Telemetry context cannot be None.")
        if not isinstance(raw, (InteractionTelemetryContext, dict)):
            raise TypeError(f"Expected InteractionTelemetryContext or dict, got {type(raw).__name__}")
        return self.extractor.sanitize_context(raw)

    def _map_to_runtime_status(self, decision: PolicyDecision) -> RuntimeStatus:
        """Determines the high-level RuntimeStatus from a PolicyDecision."""
        d_status = decision.decision_status

        if d_status == DecisionStatus.CAREGIVER_OVERRIDE:
            return RuntimeStatus.CAREGIVER_CONTROLLED

        if d_status == DecisionStatus.PATIENT_HELP_REQUEST:
            if decision.selected_strategy_id == StrategyID.AS08_BREAK_PAUSE.value:
                return RuntimeStatus.OPTIONAL_BREAK
            return RuntimeStatus.PATIENT_INITIATED

        if d_status == DecisionStatus.APPROVED:
            if decision.selected_strategy_id == StrategyID.AS08_BREAK_PAUSE.value:
                return RuntimeStatus.OPTIONAL_BREAK
            return RuntimeStatus.APPROVED

        if d_status in {DecisionStatus.SUPPRESSED_COOLDOWN, DecisionStatus.SUPPRESSED_BY_PATIENT}:
            return RuntimeStatus.SUPPRESSED

        if d_status == DecisionStatus.NO_ADDITIONAL_ASSISTANCE:
            return RuntimeStatus.NO_ADDITIONAL_ASSISTANCE

        if d_status == DecisionStatus.INSUFFICIENT_DATA:
            return RuntimeStatus.INSUFFICIENT_DATA

        return RuntimeStatus.ERROR

    def _make_insufficient_or_error_response(
        self,
        request_id: str,
        status: RuntimeStatus,
        reason: str,
        patient_id: str,
        session_id: str,
    ) -> AssistanceRuntimeResponse:
        """Creates a safe, non-actionable response for invalid data or exceptions."""
        return AssistanceRuntimeResponse(
            request_id=request_id,
            patient_id=patient_id,
            session_id=session_id,
            runtime_status=status,
            decision_id=None,
            decision_status=None,
            selected_strategy_id=None,
            decision_reason=reason,
            prediction_reference=None,
            policy_rules_applied=["PREFLIGHT_INPUT_VALIDATION"],
            caregiver_control_applied=False,
            patient_choice_required=False,
            patient_choice_status=PatientChoiceStatus.NOT_REQUIRED.value,
            requires_carl_action=False,
            carl_action=None,
        )

    def _persist_if_available(self, response: AssistanceRuntimeResponse) -> None:
        """Safely records decision to repository if one is configured."""
        if self.repository:
            try:
                self.repository.record_decision(response)
            except Exception as exc:
                logger.warning("Failed to persist assistance decision %s: %s", response.decision_id, exc)
