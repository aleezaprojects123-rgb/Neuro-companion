"""
Master Pipeline Runner for Phase 4.3 Unified Data Preparation.
Orchestrates all individual preparers, generates shared feature spaces,
and compiles the master preparation manifest and checksums file.
"""

import os
import sys
import json
from pathlib import Path
import pandas as pd

# Allow direct imports
backend_dir = str(Path(__file__).resolve().parent.parent.parent.parent)
if backend_dir not in sys.path:
    sys.path.insert(0, backend_dir)

from ml.data.preparation.clinical_tabular_preparer import OASIS1Preparer, PittMetadataPreparer
from ml.data.preparation.task_performance_preparer import CASASPreparer, RobotGamesPreparer
from ml.data.preparation.conversational_text_preparer import AI4HOPEPreparer, ChineseTranscriptsPreparer
from ml.data.preparation.latency_pacing_preparer import NBackPreparer, MSLPreparer
from ml.data.preparation.adherence_engagement_preparer import (
    BrainITPreparer, TabletRCTPreparer, VASEPreparer, TIHMPreparer, CanterburyPreparer
)
from ml.data.preparation.speech_acoustic_preparer import (
    ChineseEgemapsPreparer, NCMMSC2021MetadataPreparer, MDVRKCLMetadataPreparer
)
from ml.data.preparation.shared_feature_builder import SharedFeatureBuilder, PROCESSED_BASE_DIR


# Define raw archive source locations
RA_DIR = os.path.join(backend_dir, "research_archive")
DLA_DIR = r"C:\Users\Admin\OneDrive\Desktop\digital life analyzer\data"
RAW_DATA_DIR = os.path.join(backend_dir, "data", "raw")


def run_unified_data_preparation() -> pd.DataFrame:
    """Executes the end-to-end data preparation pipeline for Phase 4.3."""
    print("=" * 80)
    print("NEUROCOMPANION PHASE 4.3 â€” UNIFIED DATA PREPARATION PIPELINE")
    print("=" * 80)

    manifest_entries = []

    # 1. Clinical & Tabular
    print("\n--- 1. PREPARING CLINICAL & TABULAR DATASETS ---")
    oasis = OASIS1Preparer(os.path.join(RAW_DATA_DIR, "oasis_cross-sectional_utf8.csv"))
    df_oasis, meta_oasis = oasis.prepare()
    manifest_entries.append({
        "dataset_id": "DS01_OASIS1",
        "dataset_name": meta_oasis["dataset"],
        "modality": meta_oasis["modality"],
        "source_file": meta_oasis["source_file"],
        "source_sha256": meta_oasis["source_sha256"],
        "prepared_file": meta_oasis["output_file"],
        "prepared_sha256": meta_oasis["output_sha256"],
        "row_count": meta_oasis["output_row_count"],
        "column_count": meta_oasis["output_column_count"],
        "unique_participants": meta_oasis["total_raw_records"],
        "target_variables": meta_oasis["target_variable"],
        "leakage_grouping_variable": meta_oasis["leakage_grouping_variable"],
        "preparation_status": "SUCCESS",
        "limitations_and_governance": meta_oasis["clinical_governance_note"]
    })

    pitt_path = os.path.join(RA_DIR, "Phase4_New_Datasets", "DementiaBank_Pitt", "PItt-data.xlsx")
    if os.path.exists(pitt_path):
        pitt = PittMetadataPreparer(pitt_path)
        df_pitt, meta_pitt = pitt.prepare()
        manifest_entries.append({
            "dataset_id": "DS13_PITT_META",
            "dataset_name": meta_pitt["dataset"],
            "modality": meta_pitt["modality"],
            "source_file": meta_pitt["source_file"],
            "source_sha256": meta_pitt["source_sha256"],
            "prepared_file": meta_pitt["output_file"],
            "prepared_sha256": meta_pitt["output_sha256"],
            "row_count": meta_pitt["output_row_count"],
            "column_count": meta_pitt["output_column_count"],
            "unique_participants": meta_pitt["unique_participants_n"],
            "target_variables": "visit_diagnosis, mmse_score, cdr_score",
            "leakage_grouping_variable": meta_pitt["leakage_grouping_variable"],
            "preparation_status": "SUCCESS",
            "limitations_and_governance": meta_pitt["clinical_governance_note"]
        })

    # 2. Activity & Task Performance
    print("\n--- 2. PREPARING ACTIVITY & TASK PERFORMANCE DATASETS ---")
    casas_path = os.path.join(RA_DIR, "casas_phase2_exploratory", "data", "casas", "extracted", "task_audit_data.csv")
    casas = CASASPreparer(casas_path)
    df_casas, meta_casas = casas.prepare()
    manifest_entries.append({
        "dataset_id": "DS02_CASAS",
        "dataset_name": meta_casas["dataset"],
        "modality": meta_casas["modality"],
        "source_file": meta_casas["source_file"],
        "source_sha256": meta_casas["source_sha256"],
        "prepared_file": meta_casas["output_file"],
        "prepared_sha256": meta_casas["output_sha256"],
        "row_count": meta_casas["output_row_count"],
        "column_count": meta_casas["output_column_count"],
        "unique_participants": meta_casas["unique_participants_n"],
        "target_variables": meta_casas["target_variable"],
        "leakage_grouping_variable": meta_casas["leakage_grouping_variable"],
        "preparation_status": "SUCCESS",
        "limitations_and_governance": meta_casas["clinical_governance_note"]
    })

    robot_path = os.path.join(RA_DIR, "Phase4_New_Datasets", "data&prompts.zip")
    if os.path.exists(robot_path):
        robot = RobotGamesPreparer(robot_path)
        df_robot, meta_robot = robot.prepare()
        manifest_entries.append({
            "dataset_id": "DS09_ROBOT_COGNITIVE",
            "dataset_name": meta_robot["dataset"],
            "modality": meta_robot["modality"],
            "source_file": meta_robot["source_file"],
            "source_sha256": meta_robot["source_sha256"],
            "prepared_file": meta_robot["output_file"],
            "prepared_sha256": meta_robot["output_sha256"],
            "row_count": meta_robot["output_row_count"],
            "column_count": meta_robot["output_column_count"],
            "unique_participants": meta_robot["unique_participants_n"],
            "target_variables": "accuracy_score, errors_count",
            "leakage_grouping_variable": meta_robot["leakage_grouping_variable"],
            "preparation_status": "SUCCESS",
            "limitations_and_governance": meta_robot["clinical_governance_note"]
        })

    # 3. Conversational Text
    print("\n--- 3. PREPARING CONVERSATIONAL TEXT DATASETS ---")
    ai4h_prompts = os.path.join(RA_DIR, "expert_prompts.csv")
    ai4h_ratings = os.path.join(RA_DIR, "response_ratings.csv")
    ai4h = AI4HOPEPreparer(ai4h_prompts, ai4h_ratings)
    df_ai4h, meta_ai4h = ai4h.prepare()
    manifest_entries.append({
        "dataset_id": "DS04_AI4HOPE",
        "dataset_name": meta_ai4h["dataset"],
        "modality": meta_ai4h["modality"],
        "source_file": meta_ai4h["source_file"],
        "source_sha256": meta_ai4h["source_sha256"],
        "prepared_file": meta_ai4h["output_file"],
        "prepared_sha256": meta_ai4h["output_sha256"],
        "row_count": meta_ai4h["output_row_count"],
        "column_count": meta_ai4h["output_column_count"],
        "unique_participants": meta_ai4h["unique_evaluators_n"],
        "target_variables": ", ".join(meta_ai4h["targets"]),
        "leakage_grouping_variable": meta_ai4h["leakage_grouping_variable"],
        "preparation_status": "SUCCESS",
        "limitations_and_governance": meta_ai4h["clinical_governance_note"]
    })

    ch1_tsv_dir = os.path.join(DLA_DIR, "chinese_dataset_1", "downloads")
    if os.path.exists(ch1_tsv_dir):
        ch1_trans = ChineseTranscriptsPreparer(ch1_tsv_dir)
        df_ch1_t, meta_ch1_t = ch1_trans.prepare()
        manifest_entries.append({
            "dataset_id": "DS14_CHINESE1_TRANSCRIPTS",
            "dataset_name": meta_ch1_t["dataset"],
            "modality": meta_ch1_t["modality"],
            "source_file": meta_ch1_t["source_directory"],
            "source_sha256": "DIRECTORY_401_TSVS",
            "prepared_file": meta_ch1_t["output_file"],
            "prepared_sha256": meta_ch1_t["output_sha256"],
            "row_count": meta_ch1_t["output_row_count"],
            "column_count": meta_ch1_t["output_column_count"],
            "unique_participants": meta_ch1_t["unique_sessions_n"],
            "target_variables": "mean_length_of_utterance_mlu, character_type_token_ratio",
            "leakage_grouping_variable": meta_ch1_t["leakage_grouping_variable"],
            "preparation_status": "SUCCESS",
            "limitations_and_governance": meta_ch1_t["clinical_governance_note"]
        })

    # 4. Latency & Pacing
    print("\n--- 4. PREPARING LATENCY & PACING DATASETS ---")
    nback_path = os.path.join(RA_DIR, "Phase4_New_Datasets", "N_Back_Dementia", "Reproducibility_Data_SI_Modified.xlsx")
    nback = NBackPreparer(nback_path)
    df_nback, meta_nback = nback.prepare()
    manifest_entries.append({
        "dataset_id": "DS11_NBACK_DEMENTIA",
        "dataset_name": meta_nback["dataset"],
        "modality": meta_nback["modality"],
        "source_file": meta_nback["source_file"],
        "source_sha256": meta_nback["source_sha256"],
        "prepared_file": meta_nback["output_file"],
        "prepared_sha256": meta_nback["output_sha256"],
        "row_count": meta_nback["output_row_count"],
        "column_count": meta_nback["output_column_count"],
        "unique_participants": meta_nback["unique_participants_n"],
        "target_variables": "mean_working_memory_rt_ms (continuous timing prior)",
        "leakage_grouping_variable": meta_nback["leakage_grouping_variable"],
        "preparation_status": "SUCCESS",
        "limitations_and_governance": meta_nback["clinical_governance_note"]
    })

    msl_path = os.path.join(RA_DIR, "Phase4_New_Datasets", "FiguresTables_SourceData.xlsx")
    msl = MSLPreparer(msl_path)
    df_msl, meta_msl = msl.prepare()
    manifest_entries.append({
        "dataset_id": "DS10_MSL_LIFESPAN",
        "dataset_name": meta_msl["dataset"],
        "modality": meta_msl["modality"],
        "source_file": meta_msl["source_file"],
        "source_sha256": meta_msl["source_sha256"],
        "prepared_file": meta_msl["output_file"],
        "prepared_sha256": meta_msl["output_sha256"],
        "row_count": meta_msl["output_row_count"],
        "column_count": meta_msl["output_column_count"],
        "unique_participants": meta_msl["unique_participants_n"],
        "target_variables": "mean_motor_tapping_rt_ms (continuous timing prior)",
        "leakage_grouping_variable": meta_msl["leakage_grouping_variable"],
        "preparation_status": "SUCCESS",
        "limitations_and_governance": meta_msl["clinical_governance_note"]
    })

    # 5. Adherence & Engagement
    print("\n--- 5. PREPARING ADHERENCE & ENGAGEMENT DATASETS ---")
    brainit_path = os.path.join(RA_DIR, "Data_Brain-IT_RCT_for-publication.xlsx")
    brainit = BrainITPreparer(brainit_path)
    df_bi, meta_bi = brainit.prepare()
    manifest_entries.append({
        "dataset_id": "DS05_BRAINIT_RCT",
        "dataset_name": meta_bi["dataset"],
        "modality": meta_bi["modality"],
        "source_file": meta_bi["source_file"],
        "source_sha256": meta_bi["source_sha256"],
        "prepared_file": meta_bi["output_file"],
        "prepared_sha256": meta_bi["output_sha256"],
        "row_count": meta_bi["output_row_count"],
        "column_count": meta_bi["output_column_count"],
        "unique_participants": meta_bi["unique_participants_n"],
        "target_variables": "total_sessions_attended, mean_duration_minutes_per_session",
        "leakage_grouping_variable": meta_bi["leakage_grouping_variable"],
        "preparation_status": "SUCCESS",
        "limitations_and_governance": meta_bi["clinical_governance_note"]
    })

    tablet_path = os.path.join(
        RA_DIR,
        "DATA - The effect of a 6-month tablet-based multimodal training for cognitive functioning in Alzheimer’s disease in a randomized controlled trial.xlsx"
    )
    tablet = TabletRCTPreparer(tablet_path)
    df_tab, meta_tab = tablet.prepare()
    manifest_entries.append({
        "dataset_id": "DS07_TABLET_RCT",
        "dataset_name": meta_tab["dataset"],
        "modality": meta_tab["modality"],
        "source_file": meta_tab["source_file"],
        "source_sha256": meta_tab["source_sha256"],
        "prepared_file": meta_tab["output_file"],
        "prepared_sha256": meta_tab["output_sha256"],
        "row_count": meta_tab["output_row_count"],
        "column_count": meta_tab["output_column_count"],
        "unique_participants": meta_tab["unique_participants_n"],
        "target_variables": "followup_mmse_6m, intervention_group_flag",
        "leakage_grouping_variable": meta_tab["leakage_grouping_variable"],
        "preparation_status": "SUCCESS",
        "limitations_and_governance": meta_tab["clinical_governance_note"]
    })

    vase_path = os.path.join(RA_DIR, "Complete data set, Lai et al..xlsx")
    vase = VASEPreparer(vase_path)
    df_vase, meta_vase = vase.prepare()
    manifest_entries.append({
        "dataset_id": "DS06_VASE_ENGAGEMENT",
        "dataset_name": meta_vase["dataset"],
        "modality": meta_vase["modality"],
        "source_file": meta_vase["source_file"],
        "source_sha256": meta_vase["source_sha256"],
        "prepared_file": meta_vase["output_file"],
        "prepared_sha256": meta_vase["output_sha256"],
        "row_count": meta_vase["output_row_count"],
        "column_count": meta_vase["output_column_count"],
        "unique_participants": meta_vase["unique_patients_n"],
        "target_variables": "observer_engagement_rating (1-7 Likert)",
        "leakage_grouping_variable": meta_vase["leakage_grouping_variable"],
        "preparation_status": "SUCCESS",
        "limitations_and_governance": meta_vase["clinical_governance_note"]
    })

    tihm_path = os.path.join(RA_DIR, "TIHM_Dataset.zip")
    tihm = TIHMPreparer(tihm_path)
    df_tihm, meta_tihm = tihm.prepare()
    manifest_entries.append({
        "dataset_id": "DS03_TIHM_IOT",
        "dataset_name": meta_tihm["dataset"],
        "modality": meta_tihm["modality"],
        "source_file": meta_tihm["source_file"],
        "source_sha256": meta_tihm["source_sha256"],
        "prepared_file": meta_tihm["output_file"],
        "prepared_sha256": meta_tihm["output_sha256"],
        "row_count": meta_tihm["output_row_count"],
        "column_count": meta_tihm["output_column_count"],
        "unique_participants": meta_tihm["unique_households_n"],
        "target_variables": "living_alone, age",
        "leakage_grouping_variable": meta_tihm["leakage_grouping_variable"],
        "preparation_status": "SUCCESS",
        "limitations_and_governance": meta_tihm["clinical_governance_note"]
    })

    cws_path = os.path.join(RA_DIR, "Phase4_New_Datasets", "Canterbury Wellbeing Scales raw dataset.xlsx")
    if os.path.exists(cws_path):
        cws = CanterburyPreparer(cws_path)
        df_cws, meta_cws = cws.prepare()
        manifest_entries.append({
            "dataset_id": "DS08_CANTERBURY_WELLBEING",
            "dataset_name": meta_cws["dataset"],
            "modality": meta_cws["modality"],
            "source_file": meta_cws["source_file"],
            "source_sha256": meta_cws["source_sha256"],
            "prepared_file": meta_cws["output_file"],
            "prepared_sha256": meta_cws["output_sha256"],
            "row_count": meta_cws["output_row_count"],
            "column_count": meta_cws["output_column_count"],
            "unique_participants": meta_cws["total_evaluations_n"],
            "target_variables": "cws_composite_change",
            "leakage_grouping_variable": meta_cws["leakage_grouping_variable"],
            "preparation_status": "SUCCESS",
            "limitations_and_governance": meta_cws["clinical_governance_note"]
        })

    # 6. Speech & Acoustic
    print("\n--- 6. PREPARING SPEECH & ACOUSTIC DATASETS ---")
    ch1_egemaps_path = os.path.join(DLA_DIR, "chinese_dataset_1", "downloads", "egemaps_final.csv")
    ch1_eg = ChineseEgemapsPreparer(ch1_egemaps_path)
    df_ch1_eg, meta_ch1_eg = ch1_eg.prepare()
    manifest_entries.append({
        "dataset_id": "DS14_CHINESE1_EGEMAPS",
        "dataset_name": meta_ch1_eg["dataset"],
        "modality": meta_ch1_eg["modality"],
        "source_file": meta_ch1_eg["source_file"],
        "source_sha256": meta_ch1_eg["source_sha256"],
        "prepared_file": meta_ch1_eg["output_file"],
        "prepared_sha256": meta_ch1_eg["output_sha256"],
        "row_count": meta_ch1_eg["output_row_count"],
        "column_count": meta_ch1_eg["output_column_count"],
        "unique_participants": meta_ch1_eg["total_sessions_n"],
        "target_variables": "88-dimensional eGeMAPS prosody vectors",
        "leakage_grouping_variable": meta_ch1_eg["leakage_grouping_variable"],
        "preparation_status": "SUCCESS",
        "limitations_and_governance": meta_ch1_eg["clinical_governance_note"]
    })

    ncmmsc_ds_dir = os.path.join(DLA_DIR, "ncmmsc2021", "repo", "dataset")
    ncmmsc = NCMMSC2021MetadataPreparer(ncmmsc_ds_dir)
    df_nc, meta_nc = ncmmsc.prepare()
    manifest_entries.append({
        "dataset_id": "DS15_NCMMSC2021_AUDIO",
        "dataset_name": meta_nc["dataset"],
        "modality": meta_nc["modality"],
        "source_file": meta_nc["source_directory"],
        "source_sha256": "DIRECTORY_806_WAVS",
        "prepared_file": meta_nc["output_file"],
        "prepared_sha256": meta_nc["output_sha256"],
        "row_count": meta_nc["output_row_count"],
        "column_count": meta_nc["output_column_count"],
        "unique_participants": meta_nc["unique_speakers_n"],
        "target_variables": "diagnostic_group (AD vs MCI vs HC)",
        "leakage_grouping_variable": meta_nc["leakage_grouping_variable"],
        "preparation_status": "SUCCESS",
        "limitations_and_governance": meta_nc["clinical_governance_note"]
    })

    mdvr_zip = os.path.join(RA_DIR, "Phase4_New_Datasets", "MDVR_KCL", "26_29_09_2017_KCL.zip")
    mdvr = MDVRKCLMetadataPreparer(mdvr_zip)
    df_mdvr, meta_mdvr = mdvr.prepare()
    manifest_entries.append({
        "dataset_id": "DS12_MDVR_KCL_AUDIO",
        "dataset_name": meta_mdvr["dataset"],
        "modality": meta_mdvr["modality"],
        "source_file": meta_mdvr["source_file"],
        "source_sha256": meta_mdvr["source_sha256"],
        "prepared_file": meta_mdvr["output_file"],
        "prepared_sha256": meta_mdvr["output_sha256"],
        "row_count": meta_mdvr["output_row_count"],
        "column_count": meta_mdvr["output_column_count"],
        "unique_participants": meta_mdvr["unique_speakers_n"],
        "target_variables": "disease_cohort (Parkinsons vs HealthyControl)",
        "leakage_grouping_variable": meta_mdvr["leakage_grouping_variable"],
        "preparation_status": "SUCCESS",
        "limitations_and_governance": meta_mdvr["clinical_governance_note"]
    })

    # 7. Shared Cross-Dataset Representations
    print("\n--- 7. BUILDING SHARED CROSS-DATASET REPRESENTATIONS ---")
    shared_builder = SharedFeatureBuilder()
    shared_dfs, shared_metas = shared_builder.prepare()

    manifest_entries.append({
        "dataset_id": "SHARED_REP_01_PACING",
        "dataset_name": shared_metas["pacing"]["representation_name"],
        "modality": shared_metas["pacing"]["modality"],
        "source_file": ", ".join(shared_metas["pacing"]["source_datasets"]),
        "source_sha256": "MULTI_SOURCE",
        "prepared_file": shared_metas["pacing"]["output_file"],
        "prepared_sha256": shared_metas["pacing"]["output_sha256"],
        "row_count": shared_metas["pacing"]["output_row_count"],
        "column_count": shared_metas["pacing"]["output_column_count"],
        "unique_participants": shared_metas["pacing"]["output_row_count"],
        "target_variables": "empirical_pacing_multiplier_prior",
        "leakage_grouping_variable": "participant_id",
        "preparation_status": "SUCCESS",
        "limitations_and_governance": shared_metas["pacing"]["clinical_governance_note"]
    })

    manifest_entries.append({
        "dataset_id": "SHARED_REP_02_TEXT",
        "dataset_name": shared_metas["linguistics"]["representation_name"],
        "modality": shared_metas["linguistics"]["modality"],
        "source_file": ", ".join(shared_metas["linguistics"]["source_datasets"]),
        "source_sha256": "MULTI_SOURCE",
        "prepared_file": shared_metas["linguistics"]["output_file"],
        "prepared_sha256": shared_metas["linguistics"]["output_sha256"],
        "row_count": shared_metas["linguistics"]["output_row_count"],
        "column_count": shared_metas["linguistics"]["output_column_count"],
        "unique_participants": shared_metas["linguistics"]["output_row_count"],
        "target_variables": "is_accessible_grade_6",
        "leakage_grouping_variable": "text_unit_id",
        "preparation_status": "SUCCESS",
        "limitations_and_governance": shared_metas["linguistics"]["clinical_governance_note"]
    })

    # Save Master Manifest
    manifest_df = pd.DataFrame(manifest_entries)
    manifest_out = os.path.join(PROCESSED_BASE_DIR, "manifests", "phase4_3_master_preparation_manifest.csv")
    manifest_df.to_csv(manifest_out, index=False, encoding="utf-8")

    print("\n" + "=" * 80)
    print(f"PIPELINE COMPLETE: {len(manifest_df)} prepared entities saved.")
    print(f"Master Manifest -> {manifest_out}")
    print("=" * 80)
    return manifest_df


if __name__ == "__main__":
    run_unified_data_preparation()
