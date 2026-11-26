"""
Dataset Loader for N-Back Working Memory & tDCS in Dementia (Roncero et al., 2025).
Extracts 24 participant records across bilateral, unilateral, and sham conditions.
"""

import os
from typing import Dict, Any, Tuple
import pandas as pd
from .base_loader import BaseDatasetLoader


class NBackLoader(BaseDatasetLoader):
    """Encapsulates loading and validation of N-Back dementia working memory records."""

    def __init__(self, excel_path: str):
        super().__init__(excel_path)
        self.participants_df: pd.DataFrame = pd.DataFrame()

    def load(self) -> pd.DataFrame:
        if not os.path.exists(self.dataset_path):
            raise FileNotFoundError(f"N-Back file not found: {self.dataset_path}")

        raw_df = pd.read_excel(self.dataset_path, sheet_name=0)

        # Filter rows: Participant Number must be numeric 1 to 24
        clean_df = raw_df[
            pd.to_numeric(raw_df["Participant Number"], errors="coerce").notna()
        ].copy()
        clean_df["Participant Number"] = clean_df["Participant Number"].astype(int)
        clean_df = clean_df[clean_df["Participant Number"].between(1, 24)].copy()

        # Coerce reaction times and scores to float
        numeric_cols = [c for c in clean_df.columns if c not in ["Participant Number", "Severity Group"]]
        for col in numeric_cols:
            clean_df[col] = pd.to_numeric(clean_df[col], errors="coerce")

        self.participants_df = clean_df.reset_index(drop=True)
        self._is_validated, msg = self.validate()
        if not self._is_validated:
            raise ValueError(f"N-Back validation failed: {msg}")

        return self.participants_df

    def validate(self) -> Tuple[bool, str]:
        if self.participants_df.empty:
            return False, "N-Back dataframe is empty."

        if len(self.participants_df) != 24:
            return False, f"Expected exactly 24 participant records, got {len(self.participants_df)}"

        if "Severity Group" not in self.participants_df.columns or "BT1" not in self.participants_df.columns:
            return False, "Missing critical columns (Severity Group, BT1)."

        return True, "N-Back dementia records verified."

    def get_summary(self) -> Dict[str, Any]:
        return {
            "total_participants": len(self.participants_df),
            "severity_group_1_normal_rt_count": int((self.participants_df["Severity Group"] == 1).sum()) if not self.participants_df.empty else 0,
            "severity_group_2_slow_rt_count": int((self.participants_df["Severity Group"] == 2).sum()) if not self.participants_df.empty else 0,
            "group_1_mean_bt1": float(self.participants_df[self.participants_df["Severity Group"] == 1]["BT1"].mean()) if not self.participants_df.empty else None,
            "group_2_mean_bt1": float(self.participants_df[self.participants_df["Severity Group"] == 2]["BT1"].mean()) if not self.participants_df.empty else None
        }
