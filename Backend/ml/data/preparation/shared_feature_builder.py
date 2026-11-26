"""
Shared Feature Builder for Phase 4.3 Unified Data Preparation.
Builds shared feature representations where variables genuinely have compatible meanings:
1. Shared Pacing & Latency Representation (N-Back, MSL, CASAS)
2. Shared Structural Text Complexity Representation (AI4HOPE, Chinese Transcripts)
Preserves dataset-specific target variables and population tags.
"""

import os
from typing import Dict, Any, Tuple
import pandas as pd
import numpy as np
from .base_preparer import BaseDatasetPreparer, PROCESSED_BASE_DIR


class SharedFeatureBuilder(BaseDatasetPreparer):
    """Constructs cross-dataset shared representations for pacing and linguistic complexity."""

    def __init__(self):
        super().__init__("Shared_Representations", "shared_representations")

    def inspect(self) -> Dict[str, Any]:
        return {"description": "Shared feature space constructor"}

    def prepare(self) -> Tuple[Dict[str, pd.DataFrame], Dict[str, Any]]:
        # 1. Build Shared Pacing & Latency Table
        pacing_records = []

        # Load prepared MSL
        msl_csv = os.path.join(PROCESSED_BASE_DIR, "latency_pacing", "msl_tapping_prepared.csv")
        if os.path.exists(msl_csv):
            df_msl = pd.read_csv(msl_csv)
            for _, r in df_msl.iterrows():
                pacing_records.append({
                    "source_dataset": "MSL_Lifespan",
                    "participant_id": r["participant_id"],
                    "task_domain": "fine_motor_tapping",
                    "cohort_type": "healthy_lifespan" if r["is_older_adult_cohort"] == 0 else "healthy_older_adult",
                    "observed_latency_ms": r["mean_motor_tapping_rt_ms"],
                    "is_dementia_cohort": 0
                })

        # Load prepared N-Back
        nback_csv = os.path.join(PROCESSED_BASE_DIR, "latency_pacing", "nback_latency_prepared.csv")
        if os.path.exists(nback_csv):
            df_nback = pd.read_csv(nback_csv)
            for _, r in df_nback.iterrows():
                pacing_records.append({
                    "source_dataset": "N-Back_Dementia",
                    "participant_id": r["participant_id"],
                    "task_domain": "working_memory_retrieval",
                    "cohort_type": f"dementia_speed_group_{r['dementia_speed_group']}",
                    "observed_latency_ms": r["mean_working_memory_rt_ms"],
                    "is_dementia_cohort": 1
                })

        # Load prepared CASAS pauses
        casas_csv = os.path.join(PROCESSED_BASE_DIR, "activity_task", "casas_iadl_prepared.csv")
        if os.path.exists(casas_csv):
            df_casas = pd.read_csv(casas_csv)
            # Sample or summarize by participant to maintain subject balance
            casas_sub = df_casas.groupby("participant_id").agg({
                "duration_sec": "mean",
                "max_pause_sec": "mean"
            }).reset_index()
            for _, r in casas_sub.iterrows():
                pacing_records.append({
                    "source_dataset": "CASAS_Smart_Home",
                    "participant_id": r["participant_id"],
                    "task_domain": "physical_iadl_pause",
                    "cohort_type": "community_older_adult",
                    "observed_latency_ms": round(r["max_pause_sec"] * 1000.0, 2),
                    "is_dementia_cohort": 0
                })

        df_pacing = pd.DataFrame(pacing_records)
        if len(df_pacing) > 0:
            df_pacing["log_latency_ms"] = np.log1p(df_pacing["observed_latency_ms"])
            # Healthy older adult baseline (MSL older cohort mean ~550 ms)
            normative_baseline = 550.0
            normative_std = 150.0
            df_pacing["normative_z_score"] = ((df_pacing["observed_latency_ms"] - normative_baseline) / normative_std).round(2)
            # Dynamic timeout prior
            df_pacing["empirical_pacing_multiplier_prior"] = np.clip(1.0 + 0.35 * np.maximum(0, df_pacing["normative_z_score"]), 1.0, 2.5).round(2)

        meta_pacing = {
            "representation_name": "Shared_Pacing_Latencies",
            "modality": "shared_reaction_time_distributions",
            "source_datasets": ["Motor_Sequence_Learning", "N-Back_Dementia", "CASAS_Smart_Home"],
            "total_records_n": len(df_pacing),
            "feature_definitions": {
                "observed_latency_ms": "Raw continuous response time in milliseconds",
                "log_latency_ms": "Log-transformed latency for variance stabilization",
                "normative_z_score": "Z-score relative to healthy older adult baseline (mean=550ms, std=150ms)",
                "empirical_pacing_multiplier_prior": "Prior distribution for UI timeout dilation (AS07) in [1.0, 2.5]"
            },
            "clinical_governance_note": "Shared latency feature representation. Preserves task domain and cohort tags; does NOT combine into a single ML classification model."
        }
        self.save_output(df_pacing, meta_pacing, "shared_pacing_latencies")

        # 2. Build Shared Linguistic Complexity Table
        ling_records = []
        ai4h_csv = os.path.join(PROCESSED_BASE_DIR, "conversational_text", "ai4hope_dialogue_prepared.csv")
        if os.path.exists(ai4h_csv):
            df_ai4h = pd.read_csv(ai4h_csv)
            for _, r in df_ai4h.iterrows():
                ling_records.append({
                    "source_dataset": "AI4HOPE",
                    "text_unit_id": r["interaction_id"],
                    "language": "English",
                    "mean_sentence_length_words": round(r["mean_sentence_length"], 2),
                    "lexical_diversity_ttr": r["lexical_diversity_ttr"],
                    "reading_grade_level": r["fkgl_reading_grade_level"],
                    "is_accessible_grade_6": r["is_accessible_grade_6"]
                })

        ch1_csv = os.path.join(PROCESSED_BASE_DIR, "conversational_text", "chinese1_transcripts_prepared.csv")
        if os.path.exists(ch1_csv):
            df_ch1 = pd.read_csv(ch1_csv)
            for _, r in df_ch1.iterrows():
                ling_records.append({
                    "source_dataset": "Chinese_AD_Dataset_1",
                    "text_unit_id": r["session_id"],
                    "language": "Mandarin",
                    "mean_sentence_length_words": r["mean_length_of_utterance_mlu"],
                    "lexical_diversity_ttr": r["character_type_token_ratio"],
                    "reading_grade_level": np.nan,
                    "is_accessible_grade_6": 1 if r["mean_length_of_utterance_mlu"] <= 10.0 else 0
                })

        df_ling = pd.DataFrame(ling_records)
        meta_ling = {
            "representation_name": "Shared_Text_Readability_And_Complexity",
            "modality": "shared_structural_linguistics",
            "source_datasets": ["AI4HOPE", "Chinese_AD_Dataset_1_Transcripts"],
            "total_records_n": len(df_ling),
            "feature_definitions": {
                "mean_sentence_length_words": "Mean length of utterance or words per sentence",
                "lexical_diversity_ttr": "Type-Token Ratio of distinct lexical units",
                "is_accessible_grade_6": "Binary constraint indicating whether text complexity is cognitively accessible"
            },
            "clinical_governance_note": "Harmonized at the abstract structural syntactic level. Raw cross-lingual vocabulary tokens remain uncombined."
        }
        self.save_output(df_ling, meta_ling, "shared_text_readability")

        return {"pacing": df_pacing, "linguistics": df_ling}, {"pacing": meta_pacing, "linguistics": meta_ling}
