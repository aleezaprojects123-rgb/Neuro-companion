"""
config.py - Centralised configuration for the NeuroCompanion Phase 1 backend.

Target: 3-class CDR classification from the OASIS-1 dataset.
  - Class 0: CDR = 0.0 (No dementia pattern)
  - Class 1: CDR = 0.5 (Very mild dementia pattern)
  - Class 2: CDR >= 1.0 (Mild to moderate dementia pattern)

DISCLAIMER: Educational machine-learning prototype. Not a medical diagnostic tool.
"""

from pathlib import Path

# ---------------------------------------------------------------------------
# Root anchor – the directory that contains this file (Backend/model/)
# ---------------------------------------------------------------------------
_MODEL_DIR = Path(__file__).resolve().parent          # Backend/model/
_BACKEND_DIR = _MODEL_DIR.parent                      # Backend/

# ---------------------------------------------------------------------------
# Data paths
# ---------------------------------------------------------------------------
RAW_DATA_PATH: Path = _BACKEND_DIR / "data" / "raw" / "oasis_cross-sectional_utf8.csv"
RAW_XLSX_PATH: Path = _BACKEND_DIR / "data" / "raw" / "oasis_cross-sectional.csv"
PROCESSED_DATA_DIR: Path = _BACKEND_DIR / "data" / "processed"
PROCESSED_DATA_PATH: Path = PROCESSED_DATA_DIR / "processed_data.pkl"

# ---------------------------------------------------------------------------
# Model artefact paths
# ---------------------------------------------------------------------------
SAVED_MODEL_DIR: Path = _BACKEND_DIR / "saved_model"
MODEL_PATH: Path = SAVED_MODEL_DIR / "pipeline.joblib"
SCHEMA_PATH: Path = SAVED_MODEL_DIR / "schema.json"

# ---------------------------------------------------------------------------
# Report paths
# ---------------------------------------------------------------------------
REPORTS_DIR: Path = _BACKEND_DIR / "reports"
METRICS_PATH: Path = REPORTS_DIR / "metrics.json"
CONFUSION_MATRIX_PATH: Path = REPORTS_DIR / "confusion_matrix.png"
MODEL_COMPARISON_PATH: Path = REPORTS_DIR / "model_comparison.json"

# ---------------------------------------------------------------------------
# Reproducibility & Cross-Validation
# ---------------------------------------------------------------------------
RANDOM_STATE: int = 42
TEST_SIZE: float = 0.2
CV_SPLITS: int = 5
CV_REPEATS: int = 10

# ---------------------------------------------------------------------------
# Versioning
# ---------------------------------------------------------------------------
MODEL_VERSION: str = "oasis-practical-v1"

# ---------------------------------------------------------------------------
# Target definition
# ---------------------------------------------------------------------------
RAW_TARGET_COLUMN: str = "CDR"
TARGET_COLUMN: str = "Target"
TARGET_MAPPING: dict[int, str] = {
    0: "CDR 0: No dementia pattern",
    1: "CDR 0.5: Very mild dementia pattern",
    2: "CDR 1+: Mild to moderate dementia pattern",
}

# ---------------------------------------------------------------------------
# Feature sets for practical experiments
# ---------------------------------------------------------------------------
# Experiment A (Demographics + Cognitive screener)
FEATURES_EXP_A: list[str] = ["Age", "M/F", "Educ", "MMSE"]

# Experiment B (Demographics only - to measure MMSE contribution)
FEATURES_EXP_B: list[str] = ["Age", "M/F", "Educ"]

# Categorical and Numeric feature subsets
NUMERIC_FEATURES_EXP_A: list[str] = ["Age", "Educ", "MMSE"]
NUMERIC_FEATURES_EXP_B: list[str] = ["Age", "Educ"]
CATEGORICAL_FEATURES: list[str] = ["M/F"]

# Columns excluded from practical input
EXCLUDED_COLUMNS: list[str] = ["ID", "Hand", "Delay", "SES", "eTIV", "nWBV", "ASF", "CDR"]
