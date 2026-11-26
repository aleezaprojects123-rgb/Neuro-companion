"""
Dataset Loader for Brain-IT Motor-Cognitive Exergame Adherence Collection.
Extracts 12-week longitudinal attendance and duration matrices.
"""

import os
from typing import Dict, Any, Tuple
import pandas as pd
import numpy as np
from .base_loader import BaseDatasetLoader


class BrainITLoader(BaseDatasetLoader):
    """Encapsulates parsing of Brain-IT 12-week adherence protocols."""

    def __init__(self, excel_path: str):
        super().__init__(excel_path)
        self.attendance_df: pd.DataFrame = pd.DataFrame()
        self.duration_df: pd.DataFrame = pd.DataFrame()
        self.participants_list: list = []

    def load(self) -> Dict[str, pd.DataFrame]:
        if not os.path.exists(self.dataset_path):
            raise FileNotFoundError(f"Brain-IT file not found: {self.dataset_path}")

        # Tab 2: Attendance Adherence
        raw_att = pd.read_excel(self.dataset_path, sheet_name="Attendance Adherence", header=None)
        # Row 4 is header
        headers = raw_att.iloc[4, :].dropna().tolist()
        att_data = raw_att.iloc[5:, :len(headers)].copy()
        att_data.columns = [str(h).strip().replace(":", "") for h in headers]
        # Filter rows with valid participant ID
        att_data = att_data[att_data.iloc[:, 0].astype(str).str.startswith("P-")].copy()
        att_data = att_data.rename(columns={att_data.columns[0]: "participant_id"})
        self.attendance_df = att_data

        # Tab 3: Duration Adherence
        raw_dur = pd.read_excel(self.dataset_path, sheet_name="Duration Adherence", header=None)
        dur_headers = raw_dur.iloc[4, :].dropna().tolist()
        dur_data = raw_dur.iloc[5:, :len(dur_headers)].copy()
        dur_data.columns = [str(h).strip().replace(":", "") for h in dur_headers]
        dur_data = dur_data[dur_data.iloc[:, 0].astype(str).str.startswith("P-")].copy()
        dur_data = dur_data.rename(columns={dur_data.columns[0]: "participant_id"})
        self.duration_df = dur_data

        self.participants_list = self.attendance_df["participant_id"].tolist()
        self._is_validated, msg = self.validate()
        if not self._is_validated:
            raise ValueError(f"Brain-IT validation failed: {msg}")

        return {
            "attendance_adherence": self.attendance_df,
            "duration_adherence": self.duration_df
        }

    def validate(self) -> Tuple[bool, str]:
        if self.attendance_df.empty or self.duration_df.empty:
            return False, "Adherence dataframes are empty."

        if len(self.attendance_df) != 21:
            return False, f"Expected 21 intervention participants, got {len(self.attendance_df)}"

        if len(self.duration_df) != 21:
            return False, f"Expected 21 intervention participants in duration tab, got {len(self.duration_df)}"

        return True, "Brain-IT adherence tables verified."

    def get_summary(self) -> Dict[str, Any]:
        return {
            "intervention_participants": len(self.participants_list),
            "attendance_table_rows": len(self.attendance_df),
            "duration_table_rows": len(self.duration_df),
            "training_weeks": 12
        }
