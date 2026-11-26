"""
Pydantic Schemas for NeuroCompanion Assistance API Router (Phase 4.8).

Defines the transport-level request and response models for:
    POST /api/v1/assistance/evaluate

Adheres to:
- Zero CDR-to-assistance mapping.
- Rejection of raw audio.
- Distinction between approved, patient-initiated, caregiver-controlled, optional break,
  suppressed, neutral, insufficient, and error outcomes.
"""

from typing import Any, Dict, List, Optional
from pydantic import BaseModel, ConfigDict, Field


class InteractionTelemetrySchema(BaseModel):
    """
    Nested interaction telemetry context schema.
    """
    model_config = ConfigDict(extra="ignore")

    session_id: str = Field(..., min_length=1, description="Active session identifier")
    patient_id: str = Field(..., min_length=1, description="Anonymized patient identifier")
    task_name: str = Field("DefaultTask", description="Activity or task name in Unity")
    task_domain: str = Field("cognitive_selection", description="Domain classification of the task")
    step_index: int = Field(1, ge=1, description="1-based current step index")
    step_total_steps: int = Field(1, ge=1, description="Total steps in the activity")
    response_latency_ms: Optional[float] = Field(None, ge=0.0, description="Response time in milliseconds")
    idle_duration_ms: Optional[float] = Field(None, ge=0.0, description="Inactivity idle time in milliseconds")
    target_miss_count: int = Field(0, ge=0, description="Count of missed targets")
    repeated_click_count: int = Field(0, ge=0, description="Repeated click/tap count")
    step_error_count: int = Field(0, ge=0, description="Error count in current step")
    cumulative_session_errors: int = Field(0, ge=0, description="Total errors in current session")
    consecutive_error_count: int = Field(0, ge=0, description="Consecutive error count burst")
    session_duration_minutes: float = Field(0.0, ge=0.0, description="Elapsed session minutes")
    patient_requested_help: bool = Field(False, description="Patient pressed Help button")
    patient_requested_repeat: bool = Field(False, description="Patient requested audio/prompt repeat")
    patient_requested_break: bool = Field(False, description="Patient requested pause/break")
    patient_dismissed_prompt: bool = Field(False, description="Patient dismissed or muted prompt")
    caregiver_locked_strategy: Optional[str] = Field(None, description="Authoritative caregiver preset lock")
    caregiver_speech_rate_setting: Optional[float] = Field(None, description="Caregiver speech rate multiplier")
    candidate_prompt_text: Optional[str] = Field(None, description="Candidate dialogue text to evaluate")
    prior_strategy_offered: Optional[str] = Field(None, description="Strategy offered on prior step")
    prior_strategy_success: Optional[bool] = Field(None, description="Whether prior strategy succeeded")
    client_version: str = Field("3.0.0", description="Client runtime version")


class AssistanceEvaluateRequest(BaseModel):
    """
    Request model for POST /api/v1/assistance/evaluate.
    Supports either a nested `context` object or flattened top-level telemetry fields.
    """
    model_config = ConfigDict(extra="ignore")

    # Nested context (preferred)
    context: Optional[InteractionTelemetrySchema] = Field(None, description="Structured interaction context")

    # Top-level fallback fields (for flat JSON payloads from Unity / web clients)
    session_id: Optional[str] = Field(None, description="Active session identifier (if flat JSON)")
    patient_id: Optional[str] = Field(None, description="Anonymized patient identifier (if flat JSON)")
    task_name: Optional[str] = Field(None, description="Activity or task name (if flat JSON)")
    task_domain: Optional[str] = Field(None, description="Task domain (if flat JSON)")
    step_index: Optional[int] = Field(None, ge=1, description="1-based step index (if flat JSON)")
    step_total_steps: Optional[int] = Field(None, ge=1, description="Total steps (if flat JSON)")
    response_latency_ms: Optional[float] = Field(None, ge=0.0, description="Response time in ms (if flat JSON)")
    idle_duration_ms: Optional[float] = Field(None, ge=0.0, description="Idle duration in ms (if flat JSON)")
    target_miss_count: Optional[int] = Field(None, ge=0, description="Target miss count (if flat JSON)")
    repeated_click_count: Optional[int] = Field(None, ge=0, description="Repeated click count (if flat JSON)")
    step_error_count: Optional[int] = Field(None, ge=0, description="Step error count (if flat JSON)")
    cumulative_session_errors: Optional[int] = Field(None, ge=0, description="Cumulative error count (if flat JSON)")
    consecutive_error_count: Optional[int] = Field(None, ge=0, description="Consecutive error count (if flat JSON)")
    session_duration_minutes: Optional[float] = Field(None, ge=0.0, description="Session minutes (if flat JSON)")
    patient_requested_help: Optional[bool] = Field(None, description="Patient requested help (if flat JSON)")
    patient_requested_repeat: Optional[bool] = Field(None, description="Patient requested repeat (if flat JSON)")
    patient_requested_break: Optional[bool] = Field(None, description="Patient requested break (if flat JSON)")
    patient_dismissed_prompt: Optional[bool] = Field(None, description="Patient dismissed prompt (if flat JSON)")
    caregiver_locked_strategy: Optional[str] = Field(None, description="Caregiver locked strategy (if flat JSON)")
    caregiver_speech_rate_setting: Optional[float] = Field(None, description="Caregiver speech rate (if flat JSON)")
    candidate_prompt_text: Optional[str] = Field(None, description="Candidate prompt text (if flat JSON)")
    prior_strategy_offered: Optional[str] = Field(None, description="Prior strategy (if flat JSON)")
    prior_strategy_success: Optional[bool] = Field(None, description="Prior strategy success (if flat JSON)")
    client_version: Optional[str] = Field(None, description="Client version (if flat JSON)")

    # Runtime controls
    seconds_since_last_prompt: float = Field(60.0, ge=0.0, description="Seconds elapsed since last spontaneous cue")
    cdr_score: Optional[float] = Field(None, description="Prohibited from influencing decisions; discarded if provided")
    request_id: Optional[str] = Field(None, description="Client-provided request UUID")
    metadata: Dict[str, Any] = Field(default_factory=dict, description="Extensible client metadata")


class CarlActionSchema(BaseModel):
    """
    Abstract, platform-agnostic companion action specification.
    """
    action_type: str = Field(..., description="Canonical CarlActionType identifier")
    strategy_id: str = Field(..., description="Originating StrategyID")
    is_optional: bool = Field(..., description="True if action requires patient consent")
    parameters: Dict[str, Any] = Field(default_factory=dict, description="Action parameters")
    description: str = Field("", description="Narrative description of Carl's action")


class AssistanceEvaluateResponse(BaseModel):
    """
    Structured response contract for POST /api/v1/assistance/evaluate.
    """
    request_id: str = Field(..., description="UUID tracking this request")
    patient_id: str = Field(..., description="Anonymized patient identifier")
    session_id: str = Field(..., description="Session identifier")
    runtime_status: str = Field(..., description="High-level execution status of runtime")
    decision_id: Optional[str] = Field(None, description="UUID of policy decision")
    decision_status: Optional[str] = Field(None, description="Final policy disposition")
    selected_strategy_id: Optional[str] = Field(None, description="Selected strategy ID if authorized")
    decision_reason: str = Field(..., description="Clinical & safety justification")
    prediction_reference: Optional[Dict[str, Any]] = Field(None, description="Candidate prediction details")
    policy_rules_applied: List[str] = Field(default_factory=list, description="Rules applied")
    caregiver_control_applied: bool = Field(..., description="True if caregiver override was applied")
    patient_choice_required: bool = Field(..., description="True if delivery requires patient acceptance")
    patient_choice_status: str = Field(..., description="Status of patient choice")
    requires_carl_action: bool = Field(..., description="True ONLY when Carl is authorized to act")
    carl_action: Optional[CarlActionSchema] = Field(None, description="Carl action instructions")
    created_at: str = Field(..., description="UTC ISO-8601 timestamp")
