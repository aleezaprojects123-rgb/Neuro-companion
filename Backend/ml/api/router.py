"""
FastAPI Router for Phase 4 Machine Learning Services.
Mounted independently without mutating existing Phase 2A routes.
"""

import time
from fastapi import APIRouter, HTTPException
from .schemas import (
    ConversationalEvaluationRequest,
    ConversationalEvaluationResponse,
    LatencyPacingRequest,
    LatencyPacingResponse,
    AdherencePredictionRequest,
    AdherencePredictionResponse
)
from .fallbacks import DeterministicFallbackManager
from ..features.latency_features import LatencyPacingEstimator
from ..features.adherence_features import AdherenceTrajectoryExtractor

ml_router = APIRouter(prefix="/api/v1/ml", tags=["Phase 4 Machine Learning"])


@ml_router.post("/nlp/evaluate-response", response_model=ConversationalEvaluationResponse)
def evaluate_conversational_response(req: ConversationalEvaluationRequest):
    """Evaluates empathy and actionability of generated caregiver communications."""
    t0 = time.perf_counter()
    # At this stage before training, use the validated fallback manager
    res = DeterministicFallbackManager.get_conversational_fallback(req.question_text, req.answer_text)
    latency_ms = (time.perf_counter() - t0) * 1000.0
    res["latency_ms"] = round(latency_ms, 2)
    return ConversationalEvaluationResponse(**res)


@ml_router.post("/pacing/evaluate-latency", response_model=LatencyPacingResponse)
def evaluate_latency_pacing(req: LatencyPacingRequest):
    """Evaluates trial response latency against normative older adult and dementia baselines."""
    res = LatencyPacingEstimator.evaluate_latency(req.response_latency_ms)
    return LatencyPacingResponse(**res)


@ml_router.post("/adherence/evaluate-trajectory", response_model=AdherencePredictionResponse)
def evaluate_adherence_trajectory(req: AdherencePredictionRequest):
    """Evaluates longitudinal session attendance trend and compliance bracket."""
    res = AdherenceTrajectoryExtractor.extract_adherence_metrics(
        req.weekly_attendance, req.planned_sessions_per_week
    )
    return AdherencePredictionResponse(
        patient_id=req.patient_id,
        **res
    )
