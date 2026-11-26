"""
Dataset Loader for CASAS Smart Home Cognitive Assessment Dataset.
Loads task_audit_data.csv for task completion and pause duration analysis.
"""

import os
from typing import Dict, Any, Tuple
import pandas as pd
from .base_loader import BaseDatasetLoader


class CASASLoader(BaseDatasetLoader):
    """Encapsulates loading and validation of CASAS task audit data."""

    EXPECTED_COLS = ["score", "tier", "duration", "pause_cnt_5_int", "max_pause_int"]

    def __init__(self, csv_path: str):
        super().__init__(csv_path)
        self.audit_df: pd.DataFrame = pd.DataFrame()

    def load(self) -> pd.DataFrame:
        if not os.path.exists(self.dataset_path):
            raise FileNotFoundError(f"CASAS task audit file not found: {self.dataset_path}")

        self.audit_df = pd.read_csv(self.dataset_path)
        self._is_validated, msg = self.validate()
        if not self._is_validated:
            raise ValueError(f"CASAS validation failed: {msg}")

        return self.audit_df

    def validate(self) -> Tuple[bool, str]:
        if self.audit_df.empty:
            return False, "CASAS dataframe is empty."

        for c in self.EXPECTED_COLS:
            if c not in self.audit_df.columns:
                return False, f"Missing expected column '{c}' in CASAS task audit."

        if len(self.audit_df) != 2074:
            return False, f"Expected 2074 task records in task_audit_data.csv, got {len(self.audit_df)}"

        return True, "CASAS task audit records verified."

    def get_summary(self) -> Dict[str, Any]:
        return {
            "total_records": len(self.audit_df),
            "score_distribution": self.audit_df["score"].value_counts().to_dict() if not self.audit_df.empty else {},
            "mean_duration_s": float(self.audit_df["duration"].mean()) if not self.audit_df.empty else None,
            "mean_pause_count": float(self.audit_df["pause_cnt_5_int"].mean()) if not self.audit_df.empty else None
        }
