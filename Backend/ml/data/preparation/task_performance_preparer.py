"""
Task Performance and Activity Data Preparer for Phase 4.3.
Prepares CASAS Smart Home and Robot Cognitive Training datasets.
Preserves separate score categories and enforces participant-level grouping.
"""

import os
import zipfile
import io
from typing import Dict, Any, Tuple
import pandas as pd
import numpy as np
from .base_preparer import BaseDatasetPreparer


class CASASPreparer(BaseDatasetPreparer):
    """Prepares CASAS smart home cognitive assessment IADL task audit data."""

    def __init__(self, csv_path: str):
        super().__init__("CASAS_Cognitive_Assessment", "activity_task")
        self.csv_path = csv_path

    def inspect(self) -> Dict[str, Any]:
        df = pd.read_csv(self.csv_path)
        return {
            "source_path": self.csv_path,
            "raw_sha256": self.calculate_sha256(self.csv_path),
            "total_runs": len(df),
            "unique_pids": int(df["pid"].nunique()),
            "score_counts": df["score"].value_counts().to_dict()
        }

    def prepare(self) -> Tuple[pd.DataFrame, Dict[str, Any]]:
        df_raw = pd.read_csv(self.csv_path)
        source_sha256 = self.calculate_sha256(self.csv_path)

        df = pd.DataFrame()
        df["participant_id"] = "CASAS_" + df_raw["pid"].astype(str)
        df["activity_id"] = df_raw["act"].astype(int)
        
        # PRESERVE ALL ORIGINAL CLINICAL SCORE CATEGORIES SEPARATELY
        df["clinical_score_target"] = df_raw["score"].astype(int)
        
        # Continuous execution duration and pause features
        df["duration_sec"] = pd.to_numeric(df_raw["duration"], errors="coerce")
        df["hesitation_pause_count_5s"] = pd.to_numeric(df_raw["pause_cnt_5_int"], errors="coerce").fillna(0).astype(int)
        df["max_pause_sec"] = pd.to_numeric(df_raw["max_pause_int"], errors="coerce").fillna(0.0)

        # Derived engineering features
        df["pause_to_duration_ratio"] = np.where(df["duration_sec"] > 0, df["max_pause_sec"] / df["duration_sec"], 0.0)
        df["pause_frequency_per_minute"] = np.where(df["duration_sec"] > 0, df["hesitation_pause_count_5s"] / (df["duration_sec"] / 60.0), 0.0)

        meta = {
            "dataset": "CASAS_Cognitive_Assessment",
            "modality": "activity_task_performance",
            "source_file": self.csv_path,
            "source_sha256": source_sha256,
            "population": "Community-dwelling older adults performing 8 physical IADLs in smart home",
            "total_task_runs_n": len(df),
            "unique_participants_n": int(df["participant_id"].nunique()),
            "target_variable": "clinical_score_target (1: Independent, 2: Verbal Prompt, 3: Incomplete/Error, 5: Severe Breakdown)",
            "score_distribution": df["clinical_score_target"].value_counts().to_dict(),
            "leakage_grouping_variable": "participant_id",
            "license": "Creative Commons Attribution (CC-BY 4.0)",
            "clinical_governance_note": "Exploratory physical IADL task performance research model. Does NOT predict Unity UI AS03/AS10."
        }

        self.save_output(df, meta, "casas_iadl")
        return df, meta


class RobotGamesPreparer(BaseDatasetPreparer):
    """Prepares Robot Cognitive Training tablet interaction dataset."""

    def __init__(self, zip_path: str):
        super().__init__("Robot_Cognitive_Training", "activity_task")
        self.zip_path = zip_path

    def inspect(self) -> Dict[str, Any]:
        return {
            "source_path": self.zip_path,
            "raw_sha256": self.calculate_sha256(self.zip_path),
            "zip_members_count": len(zipfile.ZipFile(self.zip_path).namelist())
        }

    def prepare(self) -> Tuple[pd.DataFrame, Dict[str, Any]]:
        source_sha256 = self.calculate_sha256(self.zip_path)

        with zipfile.ZipFile(self.zip_path, "r") as z:
            excel_bytes = z.read("data/database/session_played.xlsx")
            df_sessions = pd.read_excel(io.BytesIO(excel_bytes))

        # Filter valid user rows
        valid_mask = pd.to_numeric(df_sessions["ID_USER"], errors="coerce").notna()
        df_clean = df_sessions[valid_mask].copy()

        df = pd.DataFrame()
        df["participant_id"] = "ROBOT_U" + df_clean["ID_USER"].astype(int).astype(str)
        df["game_run_id"] = "RUN_" + df_clean["idsession_played_game"].astype(str)
        df["game_type"] = df_clean["type"].astype(str)
        df["errors_count"] = pd.to_numeric(df_clean["numErrors"], errors="coerce").fillna(0).astype(int)
        df["total_points"] = pd.to_numeric(df_clean["totalPoints"], errors="coerce").fillna(0)
        df["game_time_sec"] = pd.to_numeric(df_clean["gameTime"], errors="coerce").fillna(0.0)
        df["game_reaction_time_ms"] = pd.to_numeric(df_clean["gameReactTime"], errors="coerce").fillna(0.0)
        df["touch_interaction_flag"] = pd.to_numeric(df_clean["touch_interaction"], errors="coerce").fillna(0).astype(int)
        df["vocal_interaction_flag"] = pd.to_numeric(df_clean["vocal_interaction"], errors="coerce").fillna(0).astype(int)
        
        # Enforce explicit cohort tag to prevent clinical pooling
        df["cohort_designation"] = "HEALTHY_OLDER_ADULT_NORMATIVE"

        meta = {
            "dataset": "Robot_Cognitive_Training",
            "modality": "activity_game_telemetry",
            "source_file": self.zip_path,
            "source_sha256": source_sha256,
            "population": "Healthy Older Adults (MMSE 28.65 +/- 1.65; ZERO Dementia)",
            "total_game_runs_n": len(df),
            "unique_participants_n": int(df["participant_id"].nunique()),
            "leakage_grouping_variable": "participant_id",
            "license": "Creative Commons Attribution (CC-BY 4.0)",
            "clinical_governance_note": "Normative healthy older adult tablet baseline only. Forbidden to pool into dementia diagnostic models."
        }

        self.save_output(df, meta, "robot_games")
        return df, meta
