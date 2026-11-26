"""
Abstract Base Dataset Loader for Phase 4 ML Pipelines.
Enforces strict schema validation, provenance verification, and immutability.
"""

from abc import ABC, abstractmethod
from typing import Dict, Any, Tuple
import pandas as pd


class BaseDatasetLoader(ABC):
    """Abstract interface for all Phase 4 dataset loaders."""

    def __init__(self, dataset_path: str):
        self.dataset_path = dataset_path
        self._is_validated = False
        self._raw_data: Any = None

    @abstractmethod
    def load(self) -> Any:
        """Loads dataset from disk in read-only mode."""
        pass

    @abstractmethod
    def validate(self) -> Tuple[bool, str]:
        """Validates that loaded data matches expected schema, types, and constraints."""
        pass

    @abstractmethod
    def get_summary(self) -> Dict[str, Any]:
        """Returns verified descriptive statistics (sample size, participant count, etc.)."""
        pass
