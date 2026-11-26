"""
Assistance Prediction Engine for NeuroCompanion Phase 4.5.
Coordinates all ten AS01-AS10 strategy predictors, executes batch and single predictions,
and standardizes structured outputs.
"""

from typing import Dict, Any, List, Optional
from datetime import datetime, timezone

from .schemas import (
    StrategyID,
    InteractionTelemetryContext,
    AssistancePredictionRecord,
    PredictionBatchResult
)
from .config import AssistanceConfig, DEFAULT_CONFIG
from .model_signals import ModelSignalProvider
from .feature_extractor import RuntimeFeatureExtractor
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


class AssistancePredictionEngine:
    """
    Central coordinator for AS01-AS10 AI assistance predictions.
    Modular, thread-safe, and decoupled from the safe assistance policy layer.
    """

    def __init__(
        self,
        config: Optional[AssistanceConfig] = None,
        model_signals: Optional[ModelSignalProvider] = None,
        feature_extractor: Optional[RuntimeFeatureExtractor] = None
    ):
        self.config = config or DEFAULT_CONFIG
        self.signals = model_signals or ModelSignalProvider()
        self.extractor = feature_extractor or RuntimeFeatureExtractor(self.signals)

        # Initialize all ten strategy predictors
        self.predictors: Dict[str, BaseStrategyPredictor] = {
            StrategyID.AS01_REPEAT.value: AS01RepeatPredictor(
                config=self.config, model_signals=self.signals, feature_extractor=self.extractor
            ),
            StrategyID.AS02_SIMPLIFY.value: AS02SimplifyPredictor(
                config=self.config, model_signals=self.signals, feature_extractor=self.extractor
            ),
            StrategyID.AS03_STEP_GUIDANCE.value: AS03StepGuidancePredictor(
                config=self.config, model_signals=self.signals, feature_extractor=self.extractor
            ),
            StrategyID.AS04_VISUAL_HINT.value: AS04VisualHintPredictor(
                config=self.config, model_signals=self.signals, feature_extractor=self.extractor
            ),
            StrategyID.AS05_DEMONSTRATION.value: AS05DemonstrationPredictor(
                config=self.config, model_signals=self.signals, feature_extractor=self.extractor
            ),
            StrategyID.AS06_SLOWER_SPEECH.value: AS06SlowerSpeechPredictor(
                config=self.config, model_signals=self.signals, feature_extractor=self.extractor
            ),
            StrategyID.AS07_RESPONSE_TIME.value: AS07ResponseTimePredictor(
                config=self.config, model_signals=self.signals, feature_extractor=self.extractor
            ),
            StrategyID.AS08_BREAK_PAUSE.value: AS08BreakPausePredictor(
                config=self.config, model_signals=self.signals, feature_extractor=self.extractor
            ),
            StrategyID.AS09_VERBAL_EXPLANATION.value: AS09VerbalExplanationPredictor(
                config=self.config, model_signals=self.signals, feature_extractor=self.extractor
            ),
            StrategyID.AS10_INDEPENDENT.value: AS10IndependentCompletionPredictor(
                config=self.config, model_signals=self.signals, feature_extractor=self.extractor
            )
        }

    def predict_all(self, context_or_raw: Any) -> PredictionBatchResult:
        """
        Executes predictions across all ten assistance strategies for the given telemetry context.
        """
        ctx = self.extractor.sanitize_context(context_or_raw)
        recommendations: Dict[str, AssistancePredictionRecord] = {}

        for strategy_id, predictor in self.predictors.items():
            record = predictor.predict(ctx)
            recommendations[strategy_id] = record

        return PredictionBatchResult(
            session_id=ctx.session_id,
            patient_id=ctx.patient_id,
            task_name=ctx.task_name,
            recommendations=recommendations,
            timestamp=datetime.now(timezone.utc)
        )

    def predict_strategy(self, strategy_id: str, context_or_raw: Any) -> AssistancePredictionRecord:
        """
        Executes prediction for a single specific strategy.
        """
        ctx = self.extractor.sanitize_context(context_or_raw)
        if strategy_id not in self.predictors:
            raise KeyError(f"Unknown assistance strategy ID: '{strategy_id}'. Must be one of {list(self.predictors.keys())}")

        return self.predictors[strategy_id].predict(ctx)

    def list_supported_strategies(self) -> List[str]:
        """Returns the full list of all ten canonical strategy identifiers."""
        return list(self.predictors.keys())
