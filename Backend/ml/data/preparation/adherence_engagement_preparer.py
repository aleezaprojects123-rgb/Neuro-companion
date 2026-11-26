"""
Adherence, Engagement, and Longitudinal Data Preparer for Phase 4.3.
Prepares Brain-IT Exergame RCT, Alzheimer's Tablet RCT, VASE, and TIHM.
Preserves observational engagement and trial adherence; removes unsupported mandatory limits.
"""

import os
import zipfile
import io
from typing import Dict, Any, Tuple
import pandas as pd
import numpy as np
from .base_preparer import BaseDatasetPreparer


class BrainITPreparer(BaseDatasetPreparer):
    """Prepares Brain-IT 12-week motor-cognitive exergame adherence matrices."""

    def __init__(self, excel_path: str):
        super().__init__("Brain-IT_RCT", "adherence_engagement")
        self.excel_path = excel_path

    def inspect(self) -> Dict[str, Any]:
        return {
            "source_path": self.excel_path,
            "raw_sha256": self.calculate_sha256(self.excel_path)
        }

    def prepare(self) -> Tuple[pd.DataFrame, Dict[str, Any]]:
        source_sha256 = self.calculate_sha256(self.excel_path)
        
        # Load Attendance Adherence sheet
        raw_att = pd.read_excel(self.excel_path, sheet_name="Attendance Adherence", header=None)
        headers = raw_att.iloc[4, :].dropna().tolist()
        att_data = raw_att.iloc[5:, :len(headers)].copy()
        att_data.columns = [str(h).strip().replace(":", "") for h in headers]
        att_clean = att_data[att_data.iloc[:, 0].astype(str).str.startswith("P-")].copy()
        
        df = pd.DataFrame()
        df["participant_id"] = "BRAINIT_" + att_clean.iloc[:, 0].astype(str)
        
        # Attendance across 12 weeks
        week_cols = [c for c in att_clean.columns if "week" in str(c).lower() or str(c).isdigit()]
        if len(week_cols) > 0:
            df["total_sessions_attended"] = att_clean[week_cols].apply(pd.to_numeric, errors="coerce").sum(axis=1)
        else:
            df["total_sessions_attended"] = pd.to_numeric(att_clean.iloc[:, -1], errors="coerce").fillna(0)

        # Load Duration Adherence sheet
        raw_dur = pd.read_excel(self.excel_path, sheet_name="Duration Adherence", header=None)
        dur_headers = raw_dur.iloc[4, :].dropna().tolist()
        dur_data = raw_dur.iloc[5:, :len(dur_headers)].copy()
        dur_data.columns = [str(h).strip().replace(":", "") for h in dur_headers]
        dur_clean = dur_data[dur_data.iloc[:, 0].astype(str).str.startswith("P-")].copy()
        
        dur_cols = [c for c in dur_clean.columns if "week" in str(c).lower() or str(c).isdigit()]
        if len(dur_cols) > 0:
            df["mean_duration_minutes_per_session"] = dur_clean[dur_cols].apply(pd.to_numeric, errors="coerce").mean(axis=1).round(2)
        else:
            df["mean_duration_minutes_per_session"] = 20.0

        meta = {
            "dataset": "Brain-IT_RCT",
            "modality": "longitudinal_adherence_exercise",
            "source_file": self.excel_path,
            "source_sha256": source_sha256,
            "population": "Older adults with mild neurocognitive disorder (N=41: 21 intervention, 20 control)",
            "unique_participants_n": int(df["participant_id"].nunique()),
            "leakage_grouping_variable": "participant_id",
            "license": "Creative Commons Attribution (CC-BY 4.0)",
            "clinical_governance_note": "Descriptive longitudinal adherence research. Does NOT prove a mandatory 20-min session cap."
        }

        self.save_output(df, meta, "brainit_adherence")
        return df, meta


class TabletRCTPreparer(BaseDatasetPreparer):
    """Prepares Alzheimer's Tablet Multimodal Training RCT (Brodal et al., 2023)."""

    def __init__(self, excel_path: str):
        super().__init__("Alzheimers_Tablet_RCT", "adherence_engagement")
        self.excel_path = excel_path

    def inspect(self) -> Dict[str, Any]:
        df = pd.read_excel(self.excel_path, sheet_name="data4repository")
        return {
            "source_path": self.excel_path,
            "raw_sha256": self.calculate_sha256(self.excel_path),
            "rows": len(df),
            "columns": list(df.columns)
        }

    def prepare(self) -> Tuple[pd.DataFrame, Dict[str, Any]]:
        source_sha256 = self.calculate_sha256(self.excel_path)
        raw_df = pd.read_excel(self.excel_path, sheet_name="data4repository")

        df = pd.DataFrame()
        df["participant_id"] = "TABLET_RCT_" + raw_df["participant_id"].astype(str)
        df["intervention_group_flag"] = pd.to_numeric(raw_df["ig_cg"], errors="coerce").fillna(0).astype(int) # 1: Intervention, 0: Control
        df["age"] = pd.to_numeric(raw_df["age_at_exam1"], errors="coerce").round(1)
        df["disease_duration_years"] = pd.to_numeric(raw_df["disease_duration"], errors="coerce").round(2)
        df["gender"] = pd.to_numeric(raw_df["bl_gender"], errors="coerce").fillna(-1).astype(int)
        df["education_years"] = pd.to_numeric(raw_df["bl_yearseducation"], errors="coerce")
        df["baseline_mmse"] = pd.to_numeric(raw_df["bl_mmse_total"], errors="coerce")
        df["followup_mmse_6m"] = pd.to_numeric(raw_df["fup_mmse_total"], errors="coerce")
        df["baseline_moca"] = pd.to_numeric(raw_df["bl_moca_total"], errors="coerce")

        meta = {
            "dataset": "Alzheimers_Tablet_RCT",
            "modality": "longitudinal_neuropsychological_rct",
            "source_file": self.excel_path,
            "source_sha256": source_sha256,
            "population": "Mild-to-Moderate Alzheimer's Disease patients (N=25: 13 intervention, 12 control across 6 months)",
            "unique_participants_n": int(df["participant_id"].nunique()),
            "leakage_grouping_variable": "participant_id",
            "license": "Creative Commons Attribution (CC-BY 4.0)",
            "clinical_governance_note": "Longitudinal tablet cognitive maintenance research. Descriptive RCT reference."
        }

        self.save_output(df, meta, "tablet_rct")
        return df, meta


class VASEPreparer(BaseDatasetPreparer):
    """Prepares VASE observational video engagement time series."""

    def __init__(self, excel_path: str):
        super().__init__("VASE_Dementia_Engagement", "adherence_engagement")
        self.excel_path = excel_path

    def inspect(self) -> Dict[str, Any]:
        return {
            "source_path": self.excel_path,
            "raw_sha256": self.calculate_sha256(self.excel_path)
        }

    def prepare(self) -> Tuple[pd.DataFrame, Dict[str, Any]]:
        source_sha256 = self.calculate_sha256(self.excel_path)
        raw_df = pd.read_excel(self.excel_path, sheet_name="MAINDATA 6-30 (SC)")

        df = pd.DataFrame()
        df["participant_id"] = "VASE_SUBJ_" + raw_df["VidSubject"].astype(str)
        df["video_clip_id"] = "CLIP_" + raw_df["Video"].astype(str)
        df["rater_id"] = "RATER_" + raw_df["Rater"].astype(str)
        df["second_timestamp"] = pd.to_numeric(raw_df["Seconds"], errors="coerce").fillna(0).astype(int)
        df["observer_engagement_rating"] = pd.to_numeric(raw_df["Rating"], errors="coerce")

        meta = {
            "dataset": "VASE_Dementia_Engagement",
            "modality": "observational_engagement_time_series",
            "source_file": self.excel_path,
            "source_sha256": source_sha256,
            "population": "Late-stage dementia nursing home residents (N=11 patients, 37 raters, 48 video clips)",
            "total_second_ratings_n": len(df),
            "unique_patients_n": int(df["participant_id"].nunique()),
            "unique_raters_n": int(df["rater_id"].nunique()),
            "leakage_grouping_variable": "participant_id",
            "license": "Creative Commons Attribution (CC-BY 4.0)",
            "clinical_governance_note": "Observational engagement ratings during sensory music therapy. Does NOT validate 60s software timeouts."
        }

        self.save_output(df, meta, "vase_engagement")
        return df, meta


class TIHMPreparer(BaseDatasetPreparer):
    """Prepares TIHM for Dementia IoT telemonitoring summaries."""

    def __init__(self, zip_path: str):
        super().__init__("TIHM_for_Dementia", "adherence_engagement")
        self.zip_path = zip_path

    def inspect(self) -> Dict[str, Any]:
        return {
            "source_path": self.zip_path,
            "raw_sha256": self.calculate_sha256(self.zip_path)
        }

    def prepare(self) -> Tuple[pd.DataFrame, Dict[str, Any]]:
        source_sha256 = self.calculate_sha256(self.zip_path)

        with zipfile.ZipFile(self.zip_path, "r") as z:
            demo_bytes = z.read("Dataset/Demographics.csv")
            df_demo = pd.read_csv(io.BytesIO(demo_bytes))

        df = pd.DataFrame()
        df["participant_id"] = "TIHM_H_" + df_demo["patient_id"].astype(str)
        df["gender"] = df_demo["sex"].astype(str)
        df["age_bracket"] = df_demo["age"].astype(str)

        meta = {
            "dataset": "TIHM_for_Dementia",
            "modality": "iot_home_telemonitoring",
            "source_file": self.zip_path,
            "source_sha256": source_sha256,
            "population": "54 dementia households under continuous IoT environmental and physiological monitoring",
            "unique_households_n": int(df["participant_id"].nunique()),
            "leakage_grouping_variable": "participant_id",
            "license": "Creative Commons Non-Commercial (CC-BY-NC 4.0)",
            "clinical_governance_note": "Ambient IoT telemonitoring baseline. Informs longitudinal distress heuristics."
        }

        self.save_output(df, meta, "tihm_telemonitoring")
        return df, meta


class CanterburyPreparer(BaseDatasetPreparer):
    """Prepares Canterbury Wellbeing Scales (CWS) sensory object handling records."""

    def __init__(self, excel_path: str):
        super().__init__("Canterbury_Wellbeing_Scales", "adherence_engagement")
        self.excel_path = excel_path

    def inspect(self) -> Dict[str, Any]:
        df = pd.read_excel(self.excel_path)
        return {
            "source_path": self.excel_path,
            "raw_sha256": self.calculate_sha256(self.excel_path),
            "rows": len(df)
        }

    def prepare(self) -> Tuple[pd.DataFrame, Dict[str, Any]]:
        source_sha256 = self.calculate_sha256(self.excel_path)
        raw_df = pd.read_excel(self.excel_path)

        df = pd.DataFrame()
        df["session_evaluation_id"] = [f"CWS_{i+1:03d}" for i in range(len(raw_df))]
        df["location"] = raw_df["Location"].astype(str)
        df["dementia_type"] = raw_df["Type of dementia"].astype(str)
        df["age"] = pd.to_numeric(raw_df["age"], errors="coerce")
        df["gender"] = raw_df["Gender"].astype(str)
        df["cws_composite_pre"] = pd.to_numeric(raw_df["Composite CWS pre"], errors="coerce")
        df["cws_composite_post"] = pd.to_numeric(raw_df["Composite CWS post"], errors="coerce")
        df["cws_composite_change"] = pd.to_numeric(raw_df["Composite CWS change"], errors="coerce")

        meta = {
            "dataset": "Canterbury_Wellbeing_Scales",
            "modality": "sensory_object_wellbeing",
            "source_file": self.excel_path,
            "source_sha256": source_sha256,
            "population": "People with dementia (N=201 session evaluations: Alzheimer's 92, Vascular 37, Mixed 35)",
            "total_evaluations_n": len(df),
            "leakage_grouping_variable": "session_evaluation_id",
            "license": "Creative Commons Attribution (CC-BY 4.0)",
            "clinical_governance_note": "Methodological qualitative baseline for wellbeing dynamics. Class C."
        }

        self.save_output(df, meta, "canterbury_wellbeing")
        return df, meta

