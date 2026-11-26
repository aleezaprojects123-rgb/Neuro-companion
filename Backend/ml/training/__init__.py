"""
Training Package for NeuroCompanion Phase 4 Machine Learning Models.
Provides the TrainingEngine, DatasetValidator, and Master Runner CLI.
"""

from .dataset_validator import DatasetValidator, DatasetValidationResult
from .trainer import TrainingEngine
from .runner import Phase4TrainingFrameworkRunner

__all__ = [
    "DatasetValidator",
    "DatasetValidationResult",
    "TrainingEngine",
    "Phase4TrainingFrameworkRunner",
]
