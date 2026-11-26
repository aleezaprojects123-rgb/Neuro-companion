"""
Dataset Loader for VASE (Video Analysis Scale of Engagement) Collection.
Parses second-by-second multi-rater observational engagement time series.
"""

import os
from typing import Dict, Any, Tuple
import pandas as pd
from .base_loader import BaseDatasetLoader


class VASELoader(BaseDatasetLoader):
    """Encapsulates loading and validation of VASE observational engagement records."""

    EXPECTED_COLS = [
        "Video", "Passive", "Session", "Seconds", "VidSubject",
        "Rater", "Rating"
    ]

    def __init__(self, excel_path: str):
        super().__init__(excel_path)
        self.main_df: pd.DataFrame = pd.DataFrame()

    def load(self, nrows: int = None) -> pd.DataFrame:
        if not os.path.exists(self.dataset_path):
            raise FileNotFoundError(f"VASE file not found: {self.dataset_path}")

        self.main_df = pd.read_excel(
            self.dataset_path, sheet_name="MAINDATA 6-30 (SC)", nrows=nrows
        )
        self._is_validated, msg = self.validate()
        if not self._is_validated:
            raise ValueError(f"VASE validation failed: {msg}")

        return self.main_df

    def validate(self) -> Tuple[bool, str]:
        if self.main_df.empty:
            return False, "VASE dataframe is empty."

        for c in self.EXPECTED_COLS:
            if c not in self.main_df.columns:
                return False, f"Missing expected column '{c}' in VASE."

        # If full dataset loaded
        if len(self.main_df) >= 44400:
            if self.main_df["VidSubject"].nunique() != 11:
                return False, f"Expected 11 subjects, got {self.main_df['VidSubject'].nunique()}"
            if self.main_df["Rater"].nunique() != 37:
                return False, f"Expected 37 raters, got {self.main_df['Rater'].nunique()}"

        return True, "VASE time-series verified."

    def get_summary(self) -> Dict[str, Any]:
        return {
            "total_records": len(self.main_df),
            "unique_subjects": int(self.main_df["VidSubject"].nunique()) if not self.main_df.empty else 0,
            "unique_raters": int(self.main_df["Rater"].nunique()) if not self.main_df.empty else 0,
            "unique_clips": int(self.main_df["Video"].nunique()) if not self.main_df.empty else 0,
            "mean_engagement_rating": float(self.main_df["Rating"].mean()) if not self.main_df.empty else None,
            "passive_proportion": float(self.main_df["Passive"].mean()) if not self.main_df.empty else None
        }
