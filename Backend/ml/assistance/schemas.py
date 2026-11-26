"""
Data contracts and schemas for NeuroCompanion Phase 4.5
AS01-AS10 AI Assistance Prediction Subsystem.
"""

from enum import Enum
from dataclasses import dataclass, field
from typing import Dict, Any, List, Optional
from datetime import datetime, timezone


class StrategyID(str, Enum):
    """The ten canonical NeuroCompanion assistance strategies."""
    AS01_REPEAT = "AS01_REPEAT_INSTRUCTION"
    AS02_SIMPLIFY = "AS02_SIMPLIFY_INSTRUCTION"
    AS03_STEP_GUIDANCE = "AS03_STEP_BY_STEP"
    AS04_VISUAL_HINT = "AS04_VISUAL_HINT"
    AS05_DEMONSTRATION = "AS05_DEMONSTRATION"
    AS06_SLOWER_SPEECH = "AS06_SLOWER_SPEECH"
    AS07_RESPONSE_TIME = "AS07_ADDITIONAL_RESPONSE_TIME"
    AS08_BREAK_PAUSE = "AS08_BREAK_PAUSE"
    AS09_VERBAL_EXPLANATION = "AS09_VERBAL_EXPLANATION"
    AS10_INDEPENDENT = "AS10_INDEPENDENT_COMPLETION"


class RecommendationType(str, Enum):
    """Honest categorization of the scientific and technical origin of each prediction."""
    ML_SUPPORTING_SIGNAL = "ML_SUPPORTING_SIGNAL"
    STATISTICAL_REFERENCE = "STATISTICAL_REFERENCE"
    DETERMINISTIC_RECOMMENDATION = "DETERMINISTIC_RECOMMENDATION"
    PROVISIONAL_ESTIMATE = "PROVISIONAL_ESTIMATE"


@dataclass
class InteractionTelemetryContext:
    """
    Standardized runtime interaction context supplied by client (Unity / Web).
    Robust to missing or incomplete telemetry attributes.
    """
    session_id: str = "SESSION_DEFAULT"
    patient_id: str = "PATIENT_DEFAULT"
    task_name: str = "DefaultTask"
    task_domain: str = "cognitive_selection"  # "cognitive_selection", "motor_tapping", "sequential_reorder", "domestic_adl"
    step_index: int = 1
    step_total_steps: int = 1

    # Temporal & motor telemetry
    response_latency_ms: Optional[float] = None
    idle_duration_ms: Optional[float] = None
    target_miss_count: int = 0
    repeated_click_count: int = 0
    step_error_count: int = 0
    cumulative_session_errors: int = 0
    consecutive_error_count: int = 0
    session_duration_minutes: float = 0.0

    # Explicit patient actions
    patient_requested_help: bool = False
    patient_requested_repeat: bool = False
    patient_requested_break: bool = False
    patient_dismissed_prompt: bool = False

    # Caregiver portal configuration
    caregiver_locked_strategy: Optional[str] = None
    caregiver_speech_rate_setting: Optional[float] = None

    # Text & dialogue context
    candidate_prompt_text: Optional[str] = None

    # Historical context from immediate prior step
    prior_strategy_offered: Optional[str] = None
    prior_strategy_success: Optional[bool] = None

    # Metadata
    client_version: str = "3.0.0"


@dataclass
class AssistancePredictionRecord:
    """
    Standardized prediction/recommendation record for a single assistance strategy.
    Enforces honest method labeling, calibrated confidence handling, and clear limitations.
    """
    strategy_id: str
    strategy_name: str
    is_recommended: bool
    recommendation: str
    recommendation_type: str
    evidence_features_used: Dict[str, Any]
    model_method_source: str
    is_genuine_ml: bool
    confidence_score: Optional[float]  # None if not statistically calibrated or justified
    limitations: str
    requires_patient_choice: bool = True
    requires_policy_approval: bool = True
    timestamp: datetime = field(default_factory=lambda: datetime.now(timezone.utc))

    def to_dict(self) -> Dict[str, Any]:
        return {
            "strategy_id": self.strategy_id,
            "strategy_name": self.strategy_name,
            "is_recommended": self.is_recommended,
            "recommendation": self.recommendation,
            "recommendation_type": self.recommendation_type,
            "evidence_features_used": self.evidence_features_used,
            "model_method_source": self.model_method_source,
            "is_genuine_ml": self.is_genuine_ml,
            "confidence_score": self.confidence_score,
            "limitations": self.limitations,
            "requires_patient_choice": self.requires_patient_choice,
            "requires_policy_approval": self.requires_policy_approval,
            "timestamp": self.timestamp.isoformat()
        }


@dataclass
class PredictionBatchResult:
    """Container for predictions across all ten assistance strategies for an interaction step."""
    session_id: str
    patient_id: str
    task_name: str
    recommendations: Dict[str, AssistancePredictionRecord]
    timestamp: datetime = field(default_factory=lambda: datetime.now(timezone.utc))

    def get_recommended_strategies(self) -> List[AssistancePredictionRecord]:
        return [rec for rec in self.recommendations.values() if rec.is_recommended]

    def to_dict(self) -> Dict[str, Any]:
        return {
            "session_id": self.session_id,
            "patient_id": self.patient_id,
            "task_name": self.task_name,
            "recommendations": {k: v.to_dict() for k, v in self.recommendations.items()},
            "timestamp": self.timestamp.isoformat()
        }
