"""
Dataset Validation Module for NeuroCompanion Phase 4 Training Framework.
Verifies file existence, feature columns, grouping variables, and missingness before model training.
"""

import os
from typing import Dict, Any, List, Optional
import pandas as pd
import numpy as np


class DatasetValidationResult:
    """Encapsulates validation diagnostics and status for a dataset."""

    def __init__(self, is_valid: bool, errors: List[str], warnings: List[str], summary: Dict[str, Any]):
        self.is_valid = is_valid
        self.errors = errors
        self.warnings = warnings
        self.summary = summary

    def to_dict(self) -> Dict[str, Any]:
        return {
            "is_valid": self.is_valid,
            "errors": self.errors,
            "warnings": self.warnings,
            "summary": self.summary
        }


class DatasetValidator:
    """Validates prepared datasets against training requirements and anti-leakage constraints."""

    @staticmethod
    def validate_dataset(
        file_path: str,
        feature_cols: List[str],
        target_col: Optional[str] = None,
        group_col: Optional[str] = None,
        min_samples: int = 10,
        min_groups: int = 5,
        max_missing_ratio: float = 0.20
    ) -> DatasetValidationResult:
        """Thoroughly checks dataset integrity, columns, group distribution, and missingness."""
        errors: List[str] = []
        warnings: List[str] = []
        summary: Dict[str, Any] = {}

        # 1. Existence and readability check
        if not os.path.exists(file_path):
            errors.append(f"File not found: {file_path}")
            return DatasetValidationResult(False, errors, warnings, summary)

        try:
            df = pd.read_csv(file_path)
        except Exception as exc:
            errors.append(f"Failed to parse CSV file: {exc}")
            return DatasetValidationResult(False, errors, warnings, summary)

        n_rows, n_cols = df.shape
        summary["row_count"] = n_rows
        summary["column_count"] = n_cols
        summary["file_path"] = file_path

        # 2. Row count minimum check
        if n_rows < min_samples:
            errors.append(f"Dataset has only {n_rows} rows; minimum required is {min_samples}.")

        # 3. Feature columns existence check
        missing_features = [col for col in feature_cols if col not in df.columns]
        if missing_features:
            errors.append(f"Missing required feature columns: {missing_features}")

        # 4. Target column check (if supervised)
        if target_col is not None:
            if target_col not in df.columns:
                errors.append(f"Missing target column: '{target_col}'")
            else:
                target_missing = df[target_col].isna().sum()
                if target_missing > 0:
                    warnings.append(f"Target column '{target_col}' contains {target_missing} null values.")
                summary["target_cardinality"] = int(df[target_col].nunique())

        # 5. Group column check (for anti-leakage splitting)
        if group_col is not None:
            if group_col not in df.columns:
                errors.append(f"Missing required grouping column for leak-free validation: '{group_col}'")
            else:
                n_unique_groups = int(df[group_col].nunique())
                summary["unique_groups_count"] = n_unique_groups
                if n_unique_groups < min_groups:
                    errors.append(
                        f"Grouping column '{group_col}' has only {n_unique_groups} unique groups; "
                        f"minimum {min_groups} required for cross-validation."
                    )

        # 6. Missing value analysis on required features
        present_features = [col for col in feature_cols if col in df.columns]
        if present_features:
            missing_per_col = df[present_features].isna().mean().to_dict()
            summary["missing_ratio_per_feature"] = {k: round(v, 4) for k, v in missing_per_col.items()}
            high_missing = [k for k, v in missing_per_col.items() if v > max_missing_ratio]
            if high_missing:
                warnings.append(
                    f"Features with missingness exceeding {max_missing_ratio*100}%: {high_missing}"
                )

        is_valid = len(errors) == 0
        return DatasetValidationResult(is_valid, errors, warnings, summary)
