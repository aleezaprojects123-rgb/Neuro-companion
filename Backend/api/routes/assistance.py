"""
FastAPI Router for NeuroCompanion Assistance Runtime (Phase 4.8).

Exposes the operational assistance runtime at:
    POST /api/v1/assistance/evaluate

Responsibilities:
- Transport and validation only.
- Delegates completely to AssistanceRuntime (Phase 4.7).
- Rejects missing patient/session identifiers with HTTP 422.
- Enforces CDR prohibition and safety governance.
- Never returns an executable Carl action unless authorized by safe policy.
"""

import logging
from typing import Optional
from fastapi import APIRouter, Depends, HTTPException, status

from api.schemas.assistance import AssistanceEvaluateRequest, AssistanceEvaluateResponse
from ml.assistance.persistence import AssistanceDecisionRepository
from ml.assistance.runtime import AssistanceRuntime
from ml.assistance.runtime_schemas import AssistanceRuntimeRequest
from ml.assistance.schemas import InteractionTelemetryContext

logger = logging.getLogger(__name__)

# Router definition with canonical versioned prefix
assistance_router = APIRouter(prefix="/api/v1/assistance", tags=["AS01-AS10 Assistance Runtime"])

# Shared singleton runtime instance
_runtime_instance: Optional[AssistanceRuntime] = None


def get_assistance_runtime() -> AssistanceRuntime:
    """Dependency provider for shared, thread-safe AssistanceRuntime instance."""
    global _runtime_instance
    if _runtime_instance is None:
        repo = AssistanceDecisionRepository()
        _runtime_instance = AssistanceRuntime(repository=repo)
    return _runtime_instance


@assistance_router.post(
    "/evaluate",
    response_model=AssistanceEvaluateResponse,
    status_code=status.HTTP_200_OK,
    summary="Evaluate patient interaction for AI assistance",
    description=(
        "Processes real-time interaction telemetry through feature extraction, "
        "AS01-AS10 prediction hypotheses, and safe assistance policy evaluation. "
        "Returns approved companion actions or non-intrusive statuses."
    ),
)
async def evaluate_assistance(
    payload: AssistanceEvaluateRequest,
    runtime: AssistanceRuntime = Depends(get_assistance_runtime),
) -> AssistanceEvaluateResponse:
    """
    Evaluates patient interaction context and returns a structured decision for Carl.
    """
    # 1. Resolve and validate patient and session identifiers
    if payload.context:
        patient_id = payload.context.patient_id
        session_id = payload.context.session_id
        telemetry_dict = payload.context.model_dump()
    else:
        patient_id = payload.patient_id
        session_id = payload.session_id
        telemetry_dict = payload.model_dump(
            exclude={"context", "seconds_since_last_prompt", "cdr_score", "request_id", "metadata"}
        )

    if not patient_id or not patient_id.strip() or not session_id or not session_id.strip():
        logger.warning("Rejected assistance evaluate request: missing mandatory patient_id or session_id.")
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail="Mandatory fields 'patient_id' and 'session_id' must be non-empty strings.",
        )

    # 2. Safety Rule: CDR scores are strictly prohibited from influencing assistance decisions
    if payload.cdr_score is not None:
        logger.warning(
            "Assistance evaluate request received cdr_score=%s. "
            "PROHIBITED from influencing assistance decisions. Value discarded.",
            payload.cdr_score,
        )

    # 3. Construct Telemetry Context
    telemetry_dict["patient_id"] = patient_id.strip()
    telemetry_dict["session_id"] = session_id.strip()

    context_obj = InteractionTelemetryContext(
        session_id=telemetry_dict["session_id"],
        patient_id=telemetry_dict["patient_id"],
        task_name=telemetry_dict.get("task_name") or "DefaultTask",
        task_domain=telemetry_dict.get("task_domain") or "cognitive_selection",
        step_index=telemetry_dict.get("step_index") or 1,
        step_total_steps=telemetry_dict.get("step_total_steps") or 1,
        response_latency_ms=telemetry_dict.get("response_latency_ms"),
        idle_duration_ms=telemetry_dict.get("idle_duration_ms"),
        target_miss_count=telemetry_dict.get("target_miss_count") or 0,
        repeated_click_count=telemetry_dict.get("repeated_click_count") or 0,
        step_error_count=telemetry_dict.get("step_error_count") or 0,
        cumulative_session_errors=telemetry_dict.get("cumulative_session_errors") or 0,
        consecutive_error_count=telemetry_dict.get("consecutive_error_count") or 0,
        session_duration_minutes=telemetry_dict.get("session_duration_minutes") or 0.0,
        patient_requested_help=bool(telemetry_dict.get("patient_requested_help", False)),
        patient_requested_repeat=bool(telemetry_dict.get("patient_requested_repeat", False)),
        patient_requested_break=bool(telemetry_dict.get("patient_requested_break", False)),
        patient_dismissed_prompt=bool(telemetry_dict.get("patient_dismissed_prompt", False)),
        caregiver_locked_strategy=telemetry_dict.get("caregiver_locked_strategy"),
        caregiver_speech_rate_setting=telemetry_dict.get("caregiver_speech_rate_setting"),
        candidate_prompt_text=telemetry_dict.get("candidate_prompt_text"),
        prior_strategy_offered=telemetry_dict.get("prior_strategy_offered"),
        prior_strategy_success=telemetry_dict.get("prior_strategy_success"),
        client_version=telemetry_dict.get("client_version") or "3.0.0",
    )

    # 4. Invoke AssistanceRuntime
    try:
        runtime_request = AssistanceRuntimeRequest(
            context=context_obj,
            seconds_since_last_prompt=payload.seconds_since_last_prompt,
            cdr_score=None,  # explicitly discarded
            request_id=payload.request_id or "",
            metadata=payload.metadata,
        )
        runtime_response = runtime.process(runtime_request)
    except Exception as exc:
        logger.error("AssistanceRuntime process failed unexpectedly: %s", exc, exc_info=True)
        # Fail safe: return structured error response without exposing internal traces or fabricating cues
        return AssistanceEvaluateResponse(
            request_id=payload.request_id or "ERROR_REQ",
            patient_id=patient_id,
            session_id=session_id,
            runtime_status="ERROR",
            decision_id=None,
            decision_status=None,
            selected_strategy_id=None,
            decision_reason="Internal assistance runtime evaluation failed safely.",
            prediction_reference=None,
            policy_rules_applied=["PREFLIGHT_INPUT_VALIDATION"],
            caregiver_control_applied=False,
            patient_choice_required=False,
            patient_choice_status="NOT_REQUIRED",
            requires_carl_action=False,
            carl_action=None,
            created_at="",
        )

    # 5. Return Validated Response
    return AssistanceEvaluateResponse(
        request_id=runtime_response.request_id,
        patient_id=runtime_response.patient_id,
        session_id=runtime_response.session_id,
        runtime_status=runtime_response.runtime_status.value,
        decision_id=runtime_response.decision_id,
        decision_status=runtime_response.decision_status,
        selected_strategy_id=runtime_response.selected_strategy_id,
        decision_reason=runtime_response.decision_reason,
        prediction_reference=runtime_response.prediction_reference,
        policy_rules_applied=runtime_response.policy_rules_applied,
        caregiver_control_applied=runtime_response.caregiver_control_applied,
        patient_choice_required=runtime_response.patient_choice_required,
        patient_choice_status=runtime_response.patient_choice_status,
        requires_carl_action=runtime_response.requires_carl_action,
        carl_action=runtime_response.carl_action,
        created_at=runtime_response.created_at.isoformat(),
    )
