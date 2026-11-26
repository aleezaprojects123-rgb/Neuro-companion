"""
Clinical and Tabular Data Preparer for Phase 4.3.
Prepares OASIS-1 and DementiaBank Pitt Clinical Metadata with participant grouping.
"""

import os
from typing import Dict, Any, Tuple
import pandas as pd
import numpy as np
from .base_preparer import BaseDatasetPreparer


class OASIS1Preparer(BaseDatasetPreparer):
    """Prepares OASIS-1 cross-sectional clinical screener and MRI volumetric dataset."""

    def __init__(self, raw_csv_path: str):
        super().__init__("OASIS-1", "clinical_tabular")
        self.raw_csv_path = raw_csv_path

    def inspect(self) -> Dict[str, Any]:
        df = pd.read_csv(self.raw_csv_path)
        return {
            "source_path": self.raw_csv_path,
            "raw_sha256": self.calculate_sha256(self.raw_csv_path),
            "raw_rows": len(df),
            "raw_columns": list(df.columns),
            "missing_cdr": int(df["CDR"].isnull().sum())
        }

    def prepare(self) -> Tuple[pd.DataFrame, Dict[str, Any]]:
        df_raw = pd.read_csv(self.raw_csv_path)
        source_sha256 = self.calculate_sha256(self.raw_csv_path)

        # Standardize columns and preserve participant ID
        df = df_raw.copy()
        df = df.rename(columns={"ID": "participant_id", "M/F": "sex", "Age": "age", "Educ": "education_code",
                                "SES": "ses", "MMSE": "mmse_score", "CDR": "cdr_score",
                                "eTIV": "etiv_volume", "nWBV": "nwbv_ratio", "ASF": "asf_factor"})

        # Record clinical assessment flag (assessed older cohort vs unassessed young controls)
        df["is_clinically_assessed"] = df["cdr_score"].notnull().astype(int)

        # Map CDR to standard 3-class target for assessed subset
        def map_cdr_class(val):
            if pd.isnull(val):
                return -1
            if val == 0.0:
                return 0 # Non-demented
            elif val == 0.5:
                return 1 # Very mild dementia
            elif val >= 1.0:
                return 2 # Mild to moderate dementia
            return -1

        df["cdr_class_target"] = df["cdr_score"].apply(map_cdr_class)

        meta = {
            "dataset": "OASIS-1",
            "modality": "clinical_tabular",
            "source_file": self.raw_csv_path,
            "source_sha256": source_sha256,
            "population": "Older adults (assessed) and young adult controls (unassessed)",
            "total_raw_records": len(df),
            "assessed_records_n": int((df["is_clinically_assessed"] == 1).sum()),
            "unassessed_records_n": int((df["is_clinically_assessed"] == 0).sum()),
            "class_distribution_assessed": df[df["is_clinically_assessed"] == 1]["cdr_class_target"].value_counts().to_dict(),
            "target_variable": "cdr_class_target (0: CDR 0.0, 1: CDR 0.5, 2: CDR 1.0+)",
            "leakage_grouping_variable": "participant_id",
            "license": "OASIS Open Access / CC-BY 4.0",
            "clinical_governance_note": "Frozen Phase 1 model trained strictly on assessed records (N=235)."
        }

        self.save_output(df, meta, "oasis1")
        return df, meta


class PittMetadataPreparer(BaseDatasetPreparer):
    """Prepares DementiaBank Pitt longitudinal clinical metadata."""

    def __init__(self, excel_path: str):
        super().__init__("DementiaBank_Pitt_Metadata", "clinical_tabular")
        self.excel_path = excel_path

    def inspect(self) -> Dict[str, Any]:
        df = pd.read_excel(self.excel_path, sheet_name="data", header=2)
        return {
            "source_path": self.excel_path,
            "raw_sha256": self.calculate_sha256(self.excel_path),
            "raw_rows": len(df),
            "raw_columns": list(df.columns)
        }

    def prepare(self) -> Tuple[pd.DataFrame, Dict[str, Any]]:
        source_sha256 = self.calculate_sha256(self.excel_path)
        df_raw = pd.read_excel(self.excel_path, sheet_name="data", header=2)

        # Standardize core clinical columns
        df = pd.DataFrame()
        df["participant_id"] = "PITT_" + df_raw["id"].astype(str)
        df["visit_number"] = df_raw["year"].fillna(0).astype(int)
        df["baseline_diagnosis"] = df_raw["basedx"].fillna(-1).astype(int)
        df["visit_diagnosis"] = df_raw["dx1"].fillna(-1).astype(int)

        # Extract neuropsychological scores if available
        if "mmse" in df_raw.columns:
            df["mmse_score"] = pd.to_numeric(df_raw["mmse"], errors="coerce")
        else:
            # Check other columns
            for col in df_raw.columns:
                if "mmse" in str(col).lower():
                    df["mmse_score"] = pd.to_numeric(df_raw[col], errors="coerce")
                    break

        if "cdr" in df_raw.columns:
            df["cdr_score"] = pd.to_numeric(df_raw["cdr"], errors="coerce")

        meta = {
            "dataset": "DementiaBank_Pitt_Metadata",
            "modality": "clinical_tabular_longitudinal",
            "source_file": self.excel_path,
            "source_sha256": source_sha256,
            "population": "Alzheimer's Disease, MCI, and Control older adults across longitudinal visits",
            "unique_participants_n": int(df["participant_id"].nunique()),
            "total_visits_n": len(df),
            "leakage_grouping_variable": "participant_id",
            "license": "TalkBank Consortium Research Agreement (Metadata open; audio/transcripts gated)",
            "clinical_governance_note": "Longitudinal neuropsychological decline trajectory reference."
        }

        self.save_output(df, meta, "pitt_clinical")
        return df, meta
