"""
Pydantic Schemas for Phase 4 ML Prediction Services.
"""

from typing import List, Dict, Any, Optional
from pydantic import BaseModel, Field


class ConversationalEvaluationRequest(BaseModel):
    """Request schema for evaluating generated text in Caregiver Portal."""
    question_text: str = Field(..., description="Prompt or query asked by user/caregiver")
    answer_text: str = Field(..., description="Generated AI response to be evaluated")
    persona_style: str = Field(default="Professional caregiver", description="Persona style used")
    model_version: str = Field(default="v1.0.0", description="Requested model version")


class ConversationalEvaluationResponse(BaseModel):
    """Response schema returning quality scores, uncertainty, and review recommendations."""
    model_version: str
    empathy_score: float = Field(..., ge=0.0, le=5.0, description="Predicted empathy rating")
    actionability_score: float = Field(..., ge=0.0, le=5.0, description="Predicted actionability rating")
    prediction_variance: float = Field(..., ge=0.0, description="Estimated predictive uncertainty")
    abstention_triggered: bool = Field(default=False, description="Whether prediction uncertainty exceeded threshold")
    review_recommended: bool = Field(default=False, description="Flag for manual caregiver review")
    latency_ms: float = Field(..., description="Inference latency in milliseconds")


class LatencyPacingRequest(BaseModel):
    """Request schema for evaluating interaction latency."""
    response_latency_ms: float = Field(..., ge=0.0, description="Observed trial latency in milliseconds")
    room_id: str = Field(default="cognitive_garden", description="Active room environment")


class LatencyPacingResponse(BaseModel):
    """Response schema providing normative pacing advice without diagnostic claims."""
    response_latency_ms: float
    normative_z_score: float
    pacing_bracket: str
    pacing_guidance: str
    timeout_exceeded: bool


class AdherencePredictionRequest(BaseModel):
    """Request schema for tracking longitudinal adherence."""
    patient_id: str
    weekly_attendance: List[int] = Field(..., min_items=1, description="Sessions attended per week")
    planned_sessions_per_week: int = Field(default=3, ge=1)


class AdherencePredictionResponse(BaseModel):
    """Response schema for longitudinal adherence risk tracking."""
    patient_id: str
    total_weeks_recorded: int
    overall_adherence_pct: float
    attendance_slope: float
    adherence_risk_bracket: str
