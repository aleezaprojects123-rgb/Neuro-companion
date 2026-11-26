"""
Dataset Loader for Canterbury Wellbeing Scales (CWS) for People with Dementia.
Loads 201 session evaluations across arts/heritage interventions.
"""

import os
from typing import Dict, Any, Tuple
import pandas as pd
from .base_loader import BaseDatasetLoader


class CanterburyLoader(BaseDatasetLoader):
    """Encapsulates loading and validation of Canterbury Wellbeing Scales."""

    EXPECTED_COLS = [
        "Location", "Carer present", "Type of dementia", "age", "Gender",
        "Session type", "Composite CWS pre", "Composite CWS post", "Composite CWS change"
    ]

    def __init__(self, excel_path: str):
        super().__init__(excel_path)
        self.cws_df: pd.DataFrame = pd.DataFrame()

    def load(self) -> pd.DataFrame:
        if not os.path.exists(self.dataset_path):
            raise FileNotFoundError(f"Canterbury file not found: {self.dataset_path}")

        self.cws_df = pd.read_excel(self.dataset_path, sheet_name=0)
        self._is_validated, msg = self.validate()
        if not self._is_validated:
            raise ValueError(f"Canterbury validation failed: {msg}")

        return self.cws_df

    def validate(self) -> Tuple[bool, str]:
        if self.cws_df.empty:
            return False, "Canterbury dataframe is empty."

        for c in self.EXPECTED_COLS:
            if c not in self.cws_df.columns:
                return False, f"Missing expected column '{c}' in Canterbury Wellbeing."

        if len(self.cws_df) != 201:
            return False, f"Expected 201 sessions, got {len(self.cws_df)}"

        return True, "Canterbury Wellbeing Scales verified."

    def get_summary(self) -> Dict[str, Any]:
        return {
            "total_sessions": len(self.cws_df),
            "alzheimers_sessions": int((self.cws_df["Type of dementia"].str.lower().str.contains("alzheimer", na=False)).sum()) if not self.cws_df.empty else 0,
            "object_handling_sessions": int((self.cws_df["Session type"] == "Object handling").sum()) if not self.cws_df.empty else 0,
            "mean_cws_pre": float(self.cws_df["Composite CWS pre"].mean()) if not self.cws_df.empty else None,
            "mean_cws_post": float(self.cws_df["Composite CWS post"].mean()) if not self.cws_df.empty else None,
            "mean_cws_change": float(self.cws_df["Composite CWS change"].mean()) if not self.cws_df.empty else None
        }
