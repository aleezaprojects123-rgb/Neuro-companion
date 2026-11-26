"""
Reusable Training Engine for NeuroCompanion Phase 4 Machine Learning Models.
Coordinates dataset validation, grouped cross-validation, metric calculation, artifact serialization,
and version registration in the ModelVersionCatalog.
"""

import os
import sys
import json
import hashlib
from datetime import datetime, timezone
from typing import Dict, Any, List, Optional, Tuple, Callable
import joblib
import numpy as np
import pandas as pd
from sklearn.pipeline import Pipeline
from sklearn.compose import ColumnTransformer
from sklearn.preprocessing import StandardScaler, OneHotEncoder
from sklearn.impute import SimpleImputer
from sklearn.feature_extraction.text import TfidfVectorizer

# Ensure Backend/ is in sys.path
CURRENT_DIR = os.path.dirname(os.path.abspath(__file__))
BACKEND_ROOT = os.path.abspath(os.path.join(CURRENT_DIR, "..", ".."))
if BACKEND_ROOT not in sys.path:
    sys.path.insert(0, BACKEND_ROOT)

from ml.training.dataset_validator import DatasetValidator, DatasetValidationResult
from ml.evaluation.splitting import GroupedDataSplitter
from ml.evaluation.metrics import (
    calculate_ordinal_metrics,
    calculate_classification_metrics
)
from ml.models.model_registry import ModelVersionCatalog


class TrainingEngine:
    """Reusable training orchestrator for Phase 4 models."""

    def __init__(self, backend_root: Optional[str] = None):
        self.backend_root = backend_root or BACKEND_ROOT
        self.catalog = ModelVersionCatalog(
            models_dir=os.path.join(self.backend_root, "saved_model", "phase4")
        )

    def resolve_path(self, relative_path: str) -> str:
        """Resolves relative file paths against the Backend root directory."""
        if os.path.isabs(relative_path):
            return relative_path
        return os.path.abspath(os.path.join(self.backend_root, relative_path))

    @staticmethod
    def calculate_file_sha256(file_path: str) -> str:
        """Computes SHA-256 checksum of a file for provenance verification."""
        sha256 = hashlib.sha256()
        with open(file_path, "rb") as f:
            for chunk in iter(lambda: f.read(65536), b""):
                sha256.update(chunk)
        return sha256.hexdigest()

    def validate_config(self, config: Dict[str, Any]) -> Tuple[bool, List[str]]:
        """Verifies that a model configuration contains all mandatory keys."""
        required_keys = ["model_id", "version", "architecture_type"]
        errors = [f"Missing configuration key: '{k}'" for k in required_keys if k not in config]
        return len(errors) == 0, errors

    def load_and_validate_data(
        self,
        file_path: str,
        feature_cols: List[str],
        target_col: Optional[str] = None,
        group_col: Optional[str] = None
    ) -> Tuple[pd.DataFrame, DatasetValidationResult]:
        """Loads and validates a dataset using DatasetValidator."""
        abs_path = self.resolve_path(file_path)
        val_result = DatasetValidator.validate_dataset(
            file_path=abs_path,
            feature_cols=feature_cols,
            target_col=target_col,
            group_col=group_col
        )
        if not val_result.is_valid:
            raise ValueError(f"Dataset validation failed for '{file_path}': {val_result.errors}")

        df = pd.read_csv(abs_path)
        return df, val_result

    def save_model_artifacts(
        self,
        model_id: str,
        version: str,
        fitted_model: Any,
        schema: Dict[str, Any],
        metrics: Dict[str, Any],
        source_data_path: str
    ) -> Dict[str, str]:
        """Serializes fitted pipeline, schema, metrics, and provenance to disk."""
        target_dir = os.path.join(
            self.backend_root, "saved_model", "phase4", model_id.lower(), f"v{version}"
        )
        os.makedirs(target_dir, exist_ok=True)

        model_path = os.path.join(target_dir, "model.joblib")
        schema_path = os.path.join(target_dir, "schema.json")
        metrics_path = os.path.join(target_dir, "metrics.json")
        provenance_path = os.path.join(target_dir, "provenance.json")

        # 1. Save fitted model artifact
        joblib.dump(fitted_model, model_path)

        # 2. Save feature schema
        with open(schema_path, "w", encoding="utf-8") as f:
            json.dump(schema, f, indent=2)

        # 3. Save evaluation metrics
        with open(metrics_path, "w", encoding="utf-8") as f:
            json.dump(metrics, f, indent=2)

        # 4. Save cryptographic provenance
        abs_source = self.resolve_path(source_data_path)
        provenance = {
            "model_id": model_id,
            "version": version,
            "training_timestamp": datetime.now(timezone.utc).isoformat(),
            "source_dataset_path": source_data_path,
            "source_dataset_sha256": self.calculate_file_sha256(abs_source) if os.path.exists(abs_source) else "N/A",
            "model_artifact_sha256": self.calculate_file_sha256(model_path),
            "status": "active"
        }
        with open(provenance_path, "w", encoding="utf-8") as f:
            json.dump(provenance, f, indent=2)

        # 5. Register in ModelVersionCatalog
        catalog_metadata = {
            "artifact_path": os.path.relpath(model_path, os.path.join(self.backend_root, "saved_model", "phase4")),
            "sha256": provenance["model_artifact_sha256"],
            "trained_at": provenance["training_timestamp"],
            "metrics": metrics,
            "status": "active"
        }
        self.catalog.register_model_version(model_id, f"v{version}", catalog_metadata)

        return {
            "model_path": model_path,
            "schema_path": schema_path,
            "metrics_path": metrics_path,
            "provenance_path": provenance_path
        }
