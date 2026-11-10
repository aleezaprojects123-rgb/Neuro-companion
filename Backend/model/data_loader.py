"""
data_loader.py - Load, validate, and prepare the OASIS-1 dataset.

Target: 3-class CDR classification from the OASIS-1 dataset.
  - Class 0: CDR = 0.0 (No dementia pattern)
  - Class 1: CDR = 0.5 (Very mild dementia pattern)
  - Class 2: CDR >= 1.0 (Mild to moderate dementia pattern)

CRITICAL RULES:
  - Only records with an actual recorded CDR are retained (235 records).
  - Missing CDR values are NEVER imputed or assigned to Class 0.
  - Identifiers (ID), zero-variance (Hand), non-informative (Delay),
    and excluded features (SES, MRI metrics) are omitted from the practical model.

DISCLAIMER: Educational machine-learning prototype. Not a medical diagnostic tool.
"""

from __future__ import annotations

import sys
from pathlib import Path

import pandas as pd

# Allow running directly from Backend/ or from Backend/model/
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from model.config import (
    FEATURES_EXP_A,
    RAW_DATA_PATH,
    RAW_TARGET_COLUMN,
    RAW_XLSX_PATH,
    TARGET_COLUMN,
    TARGET_MAPPING,
)


# ---------------------------------------------------------------------------
# Public API
# ---------------------------------------------------------------------------

def load_data(path: Path = RAW_DATA_PATH) -> pd.DataFrame:
    """
    Load the dataset, filter strictly to assessed CDR records, map to
    3 experimental classes, and validate data integrity.

    Returns
    -------
    pd.DataFrame
        DataFrame with 235 assessed rows containing practical features and Target.
    """
    df_raw = _load_raw_file(path)
    df_assessed = _filter_assessed_cdr(df_raw)
    df_clean = _map_target_and_select_features(df_assessed)
    _print_eda_summary(df_clean)
    return df_clean


# ---------------------------------------------------------------------------
# Private helpers
# ---------------------------------------------------------------------------

def _load_raw_file(path: Path) -> pd.DataFrame:
    """Load the UTF-8 CSV, with automatic fallback to read_excel if needed."""
    if path.exists():
        try:
            return pd.read_csv(path)
        except Exception:
            pass

    # Fallback to xlsx if UTF-8 CSV not found or corrupted
    if RAW_XLSX_PATH.exists():
        print(f"[INFO] Loading raw workbook from {RAW_XLSX_PATH}")
        return pd.read_excel(RAW_XLSX_PATH)

    raise FileNotFoundError(
        f"[ERROR] Neither {path} nor {RAW_XLSX_PATH} could be found. "
        "Please verify the OASIS dataset is placed in Backend/data/raw/."
    )


def _filter_assessed_cdr(df: pd.DataFrame) -> pd.DataFrame:
    """
    Filter strictly to participants with an assessed CDR.
    Explicitly refuses to impute missing CDR values.
    """
    if RAW_TARGET_COLUMN not in df.columns:
        raise ValueError(f"[ERROR] Target column '{RAW_TARGET_COLUMN}' not found in dataset.")

    total_rows = len(df)
    missing_cdr = df[RAW_TARGET_COLUMN].isnull().sum()
    df_assessed = df[df[RAW_TARGET_COLUMN].notnull()].copy()

    print(f"\n[INFO] Total raw rows: {total_rows}")
    print(f"[INFO] Missing CDR rows excluded: {missing_cdr} (young control cohort, ages 18-58)")
    print(f"[INFO] Valid assessed CDR records: {len(df_assessed)}")

    if len(df_assessed) != 235:
        print(f"[WARNING] Expected 235 assessed records, but found {len(df_assessed)}.")

    return df_assessed


def _map_target_and_select_features(df: pd.DataFrame) -> pd.DataFrame:
    """
    Map CDR to 3 experimental classes:
      - Class 0: CDR == 0.0
      - Class 1: CDR == 0.5
      - Class 2: CDR >= 1.0 (1.0 or 2.0)
    """
    def _map_cdr(cdr_val: float) -> int:
        if cdr_val == 0.0:
            return 0
        elif cdr_val == 0.5:
            return 1
        elif cdr_val >= 1.0:
            return 2
        raise ValueError(f"Unexpected CDR value: {cdr_val}")

    df[TARGET_COLUMN] = df[RAW_TARGET_COLUMN].apply(_map_cdr)

    # Validate that candidate practical features are present
    missing_feats = [f for f in FEATURES_EXP_A if f not in df.columns]
    if missing_feats:
        raise ValueError(f"[ERROR] Required candidate features missing: {missing_feats}")

    # Retain only practical candidate features plus the Target column
    selected_cols = FEATURES_EXP_A + [TARGET_COLUMN]
    df_clean = df[selected_cols].copy()

    return df_clean


def _print_eda_summary(df: pd.DataFrame) -> None:
    """Print an EDA summary of the assessed cohort."""
    sep = "=" * 65
    print(f"\n{sep}")
    print("  OASIS-1 ASSESSED COHORT SUMMARY (Practical Features)")
    print(sep)
    print(f"  Rows × Columns : {df.shape[0]} × {df.shape[1]}")
    print(f"  Features       : {FEATURES_EXP_A}")
    print(f"  Target         : {TARGET_COLUMN}")

    print("\n  Class Distribution:")
    counts = df[TARGET_COLUMN].value_counts().sort_index()
    for cls_idx, count in counts.items():
        pct = (count / len(df)) * 100
        print(f"    Class {cls_idx} ({TARGET_MAPPING[cls_idx]:<42}): {count:>3} ({pct:>5.2f}%)")

    print("\n  Missing Values per Feature:")
    missing = df.isnull().sum()
    if missing.sum() == 0:
        print("    None — all practical features are complete in assessed cohort.")
    else:
        for col, cnt in missing.items():
            print(f"    {col:<10}: {cnt} missing")

    print(f"\n{sep}\n")


# ---------------------------------------------------------------------------
# CLI entry-point
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    df = load_data()
    print("[INFO] Data loading and validation complete.")
