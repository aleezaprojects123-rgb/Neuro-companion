"""
Abstract Base Phase 4 Model.
Enforces model versioning, explainability, uncertainty estimation, and abstention logic.
"""

from abc import ABC, abstractmethod
from typing import Dict, Any, Optional
import numpy as np


class BasePhase4Model(ABC):
    """Abstract interface for all independently versioned Phase 4 models."""

    def __init__(self, model_name: str, version: str):
        self.model_name = model_name
        self.version = version
        self.is_trained = False
        self.metadata: Dict[str, Any] = {
            "model_name": model_name,
            "version": version,
            "training_samples": 0,
            "validation_metrics": {},
            "status": "uninitialized"
        }

    @abstractmethod
    def fit(self, X: Any, y: Any, groups: Optional[Any] = None) -> Any:
        """Trains the model enforcing grouped validation."""
        pass

    @abstractmethod
    def predict(self, X: Any) -> np.ndarray:
        """Generates point predictions."""
        pass

    @abstractmethod
    def predict_with_uncertainty(self, X: Any) -> Dict[str, Any]:
        """Generates predictions with variance/confidence bounds and abstention flag."""
        pass

    @abstractmethod
    def explain(self, X_sample: Any) -> Dict[str, float]:
        """Returns feature attribution dictionary (SHAP or linear weights)."""
        pass
