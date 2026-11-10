"""
preprocess.py - Feature engineering and stratified train/test splitting.

Workflow
--------
1. Load assessed OASIS-1 dataset via data_loader (235 records).
2. Perform a STRATIFIED train/test split (80/20, random_state=42).
   Splitting happens FIRST before any transformer fitting to prevent leakage.
3. Construct ColumnTransformers for:
   - Experiment A (Practical Model with Screener): Age, M/F, Educ, MMSE
   - Experiment B (Demographics Only): Age, M/F, Educ
4. Save schema.json for runtime validation and API inference.
5. Persist processed train/test splits for train_model.py.

DISCLAIMER: Educational machine-learning prototype. Not a medical diagnostic tool.
"""

from __future__ import annotations

import json
import pickle
import sys
from pathlib import Path

import numpy as np
import pandas as pd
from sklearn.compose import ColumnTransformer
from sklearn.impute import SimpleImputer
from sklearn.model_selection import train_test_split
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import OneHotEncoder, StandardScaler

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from model.config import (
    CATEGORICAL_FEATURES,
    FEATURES_EXP_A,
    FEATURES_EXP_B,
    NUMERIC_FEATURES_EXP_A,
    NUMERIC_FEATURES_EXP_B,
    PROCESSED_DATA_DIR,
    PROCESSED_DATA_PATH,
    RANDOM_STATE,
    SCHEMA_PATH,
    TARGET_COLUMN,
    TARGET_MAPPING,
    TEST_SIZE,
)
from model.data_loader import load_data


# ---------------------------------------------------------------------------
# Public API
# ---------------------------------------------------------------------------

def run_preprocessing() -> dict:
    """
    Execute the stratified train/test split, construct unfitted preprocessors,
    and persist schema and processed splits.

    Returns
    -------
    dict
        Dictionary containing train/test data splits and preprocessor templates.
    """
    df = load_data()

    X = df.drop(columns=[TARGET_COLUMN])
    y = df[TARGET_COLUMN].astype(int)

    # 1. Stratified Train / Test Split
    # Must occur before any fitting to prevent information leakage.
    X_train, X_test, y_train, y_test = train_test_split(
        X, y,
        test_size=TEST_SIZE,
        random_state=RANDOM_STATE,
        stratify=y,
    )

    print(f"[INFO] Total assessed records : {len(df)}")
    print(f"[INFO] Training set size      : {len(X_train)} (80%)")
    print(f"[INFO] Untouched test set size: {len(X_test)} (20%)")
    print("\n[INFO] Train Class Balance:")
    print(y_train.value_counts().sort_index())
    print("\n[INFO] Test Class Balance:")
    print(y_test.value_counts().sort_index())

    # 2. Build unfitted ColumnTransformers for both experiments
    preprocessor_a = build_preprocessor(NUMERIC_FEATURES_EXP_A, CATEGORICAL_FEATURES)
    preprocessor_b = build_preprocessor(NUMERIC_FEATURES_EXP_B, CATEGORICAL_FEATURES)

    # 3. Save processed artefacts
    _ensure_dirs()
    processed_payload = {
        "X_train": X_train,
        "X_test": X_test,
        "y_train": y_train,
        "y_test": y_test,
        "features_exp_a": FEATURES_EXP_A,
        "features_exp_b": FEATURES_EXP_B,
        "preprocessor_a": preprocessor_a,
        "preprocessor_b": preprocessor_b,
    }

    with open(PROCESSED_DATA_PATH, "wb") as f:
        pickle.dump(processed_payload, f)
    print(f"\n[INFO] Processed dataset splits saved -> {PROCESSED_DATA_PATH}")

    # 4. Save schema.json for API and inference validation
    save_schema()

    return processed_payload


def build_preprocessor(numeric_features: list[str], categorical_features: list[str]) -> ColumnTransformer:
    """
    Construct an unfitted ColumnTransformer:
      - Numeric: median imputation + standard scaling
      - Categorical: most frequent imputation + OneHotEncoding
    """
    numeric_pipe = Pipeline([
        ("imputer", SimpleImputer(strategy="median")),
        ("scaler", StandardScaler()),
    ])

    categorical_pipe = Pipeline([
        ("imputer", SimpleImputer(strategy="most_frequent")),
        ("encoder", OneHotEncoder(handle_unknown="ignore", sparse_output=False)),
    ])

    transformers = [
        ("num", numeric_pipe, numeric_features),
        ("cat", categorical_pipe, categorical_features),
    ]

    return ColumnTransformer(transformers=transformers, remainder="drop")


def save_schema() -> None:
    """Persist feature schema and validation limits for inference."""
    schema = {
        "model_version": "oasis-practical-v1",
        "description": "3-class CDR experimental classification from OASIS-1 practical features.",
        "feature_names": FEATURES_EXP_A,
        "numeric_features": NUMERIC_FEATURES_EXP_A,
        "categorical_features": CATEGORICAL_FEATURES,
        "target_column": TARGET_COLUMN,
        "target_mapping": {str(k): v for k, v in TARGET_MAPPING.items()},
        "feature_ranges": {
            "Age": {"type": "float", "min": 30.0, "max": 100.0, "description": "Participant age in years"},
            "M/F": {"type": "string", "allowed": ["M", "F"], "description": "Biological sex"},
            "Educ": {"type": "float", "min": 1.0, "max": 5.0, "description": "Education code (1 to 5)"},
            "MMSE": {"type": "float", "min": 0.0, "max": 30.0, "description": "Mini-Mental State Examination score"},
        },
        "train_test_split": {
            "test_size": TEST_SIZE,
            "stratified": True,
            "random_state": RANDOM_STATE,
        },
    }
    SCHEMA_PATH.parent.mkdir(parents=True, exist_ok=True)
    with open(SCHEMA_PATH, "w", encoding="utf-8") as f:
        json.dump(schema, f, indent=2)
    print(f"[INFO] Schema JSON saved              -> {SCHEMA_PATH}")


def _ensure_dirs() -> None:
    """Create processed and saved_model directories if missing."""
    PROCESSED_DATA_DIR.mkdir(parents=True, exist_ok=True)
    SCHEMA_PATH.parent.mkdir(parents=True, exist_ok=True)


# ---------------------------------------------------------------------------
# CLI entry-point
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    run_preprocessing()
