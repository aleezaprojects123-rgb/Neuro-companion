"""
Reaction-Time and Latency Data Preparer for Phase 4.3.
Prepares N-Back Dementia (Roncero et al.) and Motor Sequence Learning (King et al.).
Preserves observed continuous response-time distributions; disclaims supervised timeout targets.
"""

import os
from typing import Dict, Any, Tuple
import pandas as pd
import numpy as np
from .base_preparer import BaseDatasetPreparer


class NBackPreparer(BaseDatasetPreparer):
    """Prepares N-Back Dementia working memory latency records."""

    def __init__(self, excel_path: str):
        super().__init__("N-Back_Dementia", "latency_pacing")
        self.excel_path = excel_path

    def inspect(self) -> Dict[str, Any]:
        df = pd.read_excel(self.excel_path, sheet_name=0)
        return {
            "source_path": self.excel_path,
            "raw_sha256": self.calculate_sha256(self.excel_path),
            "raw_rows": len(df),
            "raw_columns": list(df.columns)
        }

    def prepare(self) -> Tuple[pd.DataFrame, Dict[str, Any]]:
        source_sha256 = self.calculate_sha256(self.excel_path)
        raw_df = pd.read_excel(self.excel_path, sheet_name=0)

        # Filter strictly to valid numeric participant rows 1-24
        valid_rows = raw_df[pd.to_numeric(raw_df["Participant Number"], errors="coerce").notna()].copy()
        valid_rows["Participant Number"] = valid_rows["Participant Number"].astype(int)
        clean_df = valid_rows[valid_rows["Participant Number"].between(1, 24)].copy()

        # Build standardized latency records
        df = pd.DataFrame()
        df["participant_id"] = "NBACK_P" + clean_df["Participant Number"].astype(str).str.zfill(2)
        df["dementia_speed_group"] = clean_df["Severity Group"].astype(str)
        
        # Sham baseline reaction times (unstimulated baseline RTs: ST1, ST2, ST3)
        sham_cols = [c for c in ["ST1", "ST2", "ST3"] if c in clean_df.columns]
        if len(sham_cols) > 0:
            df["sham_baseline_rt_ms"] = clean_df[sham_cols].apply(pd.to_numeric, errors="coerce").mean(axis=1).round(2)
        
        # Overall reaction times across all stimulation conditions (ms)
        all_rt_cols = [c for c in ["BT1", "BT2", "BT3", "UT1", "UT2", "UT3", "ST1", "ST2", "ST3"] if c in clean_df.columns]
        df["mean_working_memory_rt_ms"] = clean_df[all_rt_cols].apply(pd.to_numeric, errors="coerce").mean(axis=1).round(2)
        df["std_working_memory_rt_ms"] = clean_df[all_rt_cols].apply(pd.to_numeric, errors="coerce").std(axis=1).round(2)

        # Baseline MMSE score
        mmse_cols = [c for c in clean_df.columns if "mmse" in str(c).lower()]
        if len(mmse_cols) > 0:
            df["baseline_mmse"] = clean_df[mmse_cols].apply(pd.to_numeric, errors="coerce").mean(axis=1).round(1)

        # Disclaim supervised timeout targets
        df["is_supervised_timeout_target"] = 0 # Explicitly false

        meta = {
            "dataset": "N-Back_Dementia",
            "modality": "reaction_time_latency",
            "source_file": self.excel_path,
            "source_sha256": source_sha256,
            "population": "Mild-to-Moderate Dementia patients (N=24; Group 1 normal RT 777.6 ms vs Group 2 slow RT 1,303.9 ms)",
            "unique_participants_n": int(df["participant_id"].nunique()),
            "leakage_grouping_variable": "participant_id",
            "license": "Creative Commons Attribution (CC-BY 4.0)",
            "clinical_governance_note": "Empirical working memory latency reference prior. Does NOT contain supervised timeout multiplier targets."
        }

        self.save_output(df, meta, "nback_latency")
        return df, meta


class MSLPreparer(BaseDatasetPreparer):
    """Prepares Motor Sequence Learning (MSL) lifespan psychomotor tapping latency records."""

    def __init__(self, excel_path: str):
        super().__init__("Motor_Sequence_Learning", "latency_pacing")
        self.excel_path = excel_path

    def inspect(self) -> Dict[str, Any]:
        df = pd.read_excel(self.excel_path, sheet_name="Figure2A - Norm RT", header=1)
        return {
            "source_path": self.excel_path,
            "raw_sha256": self.calculate_sha256(self.excel_path),
            "total_participants": len(df),
            "groups": df["Group"].value_counts().to_dict()
        }

    def prepare(self) -> Tuple[pd.DataFrame, Dict[str, Any]]:
        source_sha256 = self.calculate_sha256(self.excel_path)
        raw_df = pd.read_excel(self.excel_path, sheet_name="Figure2A - Norm RT", header=1)

        # Standardize participant columns
        df = pd.DataFrame()
        df["participant_id"] = "MSL_" + raw_df["ID"].astype(str)
        df["age_group"] = raw_df["Group"].astype(str)
        df["is_older_adult_cohort"] = (raw_df["Group"].astype(str).str.lower().str.contains("older")).astype(int)

        # Extract reaction time columns (recorded in seconds, convert to milliseconds)
        rt_cols = [c for c in raw_df.columns if c.startswith("RTBlock")]
        rt_values_ms = raw_df[rt_cols] * 1000.0 # Convert seconds to ms

        df["mean_motor_tapping_rt_ms"] = rt_values_ms.mean(axis=1).round(2)
        df["std_motor_tapping_rt_ms"] = rt_values_ms.std(axis=1).round(2)
        df["min_motor_tapping_rt_ms"] = rt_values_ms.min(axis=1).round(2)
        df["max_motor_tapping_rt_ms"] = rt_values_ms.max(axis=1).round(2)

        meta = {
            "dataset": "Motor_Sequence_Learning",
            "modality": "reaction_time_latency",
            "source_file": self.excel_path,
            "source_sha256": source_sha256,
            "population": "Lifespan cohort (N=130: 33 Children, 33 Adolescents, 32 Young adults, 32 Healthy older adults ages 60-80)",
            "unique_participants_n": int(df["participant_id"].nunique()),
            "older_adults_cohort_n": int(df["is_older_adult_cohort"].sum()),
            "leakage_grouping_variable": "participant_id",
            "license": "Creative Commons Attribution (CC-BY 4.0)",
            "clinical_governance_note": "Normative healthy psychomotor tapping latency baseline (450-650 ms in older adults). Zero dementia."
        }

        self.save_output(df, meta, "msl_tapping")
        return df, meta
