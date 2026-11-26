"""
Feature engineering module for NeuroCompanion Phase 4 ML models.
"""

from .text_features import TextFeatureExtractor
from .latency_features import LatencyPacingEstimator
from .adherence_features import AdherenceTrajectoryExtractor
from .telemetry_schema import (
    PatientBehavioralTelemetry,
    PatientCueResponse,
    CaregiverSessionAnnotation,
    ModelInferenceRecord
)

__all__ = [
    "TextFeatureExtractor",
    "LatencyPacingEstimator",
    "AdherenceTrajectoryExtractor",
    "PatientBehavioralTelemetry",
    "PatientCueResponse",
    "CaregiverSessionAnnotation",
    "ModelInferenceRecord",
]
