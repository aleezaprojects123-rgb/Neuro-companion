"""
NeuroCompanion Phase 4.5 AI Assistance Prediction Subsystem.
Modular hybrid prediction engine for AS01-AS10 assistance strategies.
"""

from .schemas import (
    StrategyID,
    RecommendationType,
    InteractionTelemetryContext,
    AssistancePredictionRecord,
    PredictionBatchResult
)
from .config import AssistanceConfig, DEFAULT_CONFIG
from .model_signals import ModelSignalProvider
from .feature_extractor import RuntimeFeatureExtractor, compute_flesch_kincaid_grade_level
from .predictors import (
    BaseStrategyPredictor,
    AS01RepeatPredictor,
    AS02SimplifyPredictor,
    AS03StepGuidancePredictor,
    AS04VisualHintPredictor,
    AS05DemonstrationPredictor,
    AS06SlowerSpeechPredictor,
    AS07ResponseTimePredictor,
    AS08BreakPausePredictor,
    AS09VerbalExplanationPredictor,
    AS10IndependentCompletionPredictor
)
from .prediction_engine import AssistancePredictionEngine
from .policy_interface import AssistancePolicyInterface
from .policy_decision import PolicyDecision, DecisionStatus, PatientChoiceStatus
from .safe_policy import SafeAssistancePolicy
from .carl_action import CarlActionType, CarlAction, CarlActionAdapter
from .runtime_schemas import RuntimeStatus, AssistanceRuntimeRequest, AssistanceRuntimeResponse
from .persistence import AssistanceDecisionRepository
from .runtime import AssistanceRuntime

__all__ = [
    "StrategyID",
    "RecommendationType",
    "InteractionTelemetryContext",
    "AssistancePredictionRecord",
    "PredictionBatchResult",
    "AssistanceConfig",
    "DEFAULT_CONFIG",
    "ModelSignalProvider",
    "RuntimeFeatureExtractor",
    "compute_flesch_kincaid_grade_level",
    "BaseStrategyPredictor",
    "AS01RepeatPredictor",
    "AS02SimplifyPredictor",
    "AS03StepGuidancePredictor",
    "AS04VisualHintPredictor",
    "AS05DemonstrationPredictor",
    "AS06SlowerSpeechPredictor",
    "AS07ResponseTimePredictor",
    "AS08BreakPausePredictor",
    "AS09VerbalExplanationPredictor",
    "AS10IndependentCompletionPredictor",
    "AssistancePredictionEngine",
    "AssistancePolicyInterface",
    "PolicyDecision",
    "DecisionStatus",
    "PatientChoiceStatus",
    "SafeAssistancePolicy",
    "CarlActionType",
    "CarlAction",
    "CarlActionAdapter",
    "RuntimeStatus",
    "AssistanceRuntimeRequest",
    "AssistanceRuntimeResponse",
    "AssistanceDecisionRepository",
    "AssistanceRuntime",
]
