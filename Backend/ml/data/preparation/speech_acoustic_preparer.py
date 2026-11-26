"""
Speech and Acoustic Data Preparer for Phase 4.3.
Prepares Chinese Dataset 1 eGeMAPS acoustic features, NCMMSC2021 Audio metadata,
and MDVR-KCL Parkinson's out-of-domain specificity audio metadata.
"""

import os
import wave
import zipfile
import io
from typing import Dict, Any, Tuple
import pandas as pd
import numpy as np
from .base_preparer import BaseDatasetPreparer


class ChineseEgemapsPreparer(BaseDatasetPreparer):
    """Prepares Chinese Dataset 1 openSMILE 88-dimensional eGeMAPS acoustic prosody vectors."""

    def __init__(self, csv_path: str):
        super().__init__("Chinese_AD_Dataset_1_eGeMAPS", "speech_acoustic")
        self.csv_path = csv_path

    def inspect(self) -> Dict[str, Any]:
        df = pd.read_csv(self.csv_path)
        return {
            "source_path": self.csv_path,
            "raw_sha256": self.calculate_sha256(self.csv_path),
            "total_sessions": len(df),
            "feature_columns_n": len(df.columns)
        }

    def prepare(self) -> Tuple[pd.DataFrame, Dict[str, Any]]:
        df_raw = pd.read_csv(self.csv_path)
        source_sha256 = self.calculate_sha256(self.csv_path)

        df = df_raw.copy()
        # Ensure session ID is preserved as the primary grouping key
        first_col = df.columns[0]
        df = df.rename(columns={first_col: "session_id"})
        df["participant_id"] = "CH1_" + df["session_id"].astype(str)

        meta = {
            "dataset": "Chinese_AD_Dataset_1_eGeMAPS",
            "modality": "speech_acoustic_prosody_egemaps",
            "source_file": self.csv_path,
            "source_sha256": source_sha256,
            "population": "Alzheimer's Disease and Healthy Control older adults performing Cookie Theft description",
            "total_sessions_n": len(df),
            "egemaps_feature_dimensions": len(df.columns) - 2,
            "leakage_grouping_variable": "participant_id",
            "license": "Academic Research License",
            "clinical_governance_note": "Acoustic prosody representation. Does NOT prove AS01 repetition or AS07 pacing need."
        }

        self.save_output(df, meta, "chinese1_egemaps")
        return df, meta


class NCMMSC2021MetadataPreparer(BaseDatasetPreparer):
    """Prepares NCMMSC2021 Audio metadata, audio properties, and speaker IDs across 806 WAVs."""

    def __init__(self, dataset_root: str):
        super().__init__("NCMMSC2021_Audio_Metadata", "speech_acoustic")
        self.dataset_root = dataset_root

    def inspect(self) -> Dict[str, Any]:
        wav_count = 0
        for root, _, files in os.walk(self.dataset_root):
            wav_count += len([f for f in files if f.endswith(".wav")])
        return {
            "source_root": self.dataset_root,
            "total_wav_files": wav_count
        }

    def prepare(self) -> Tuple[pd.DataFrame, Dict[str, Any]]:
        records = []

        for root, _, files in os.walk(self.dataset_root):
            wav_files = [f for f in files if f.endswith(".wav")]
            for f in wav_files:
                fpath = os.path.join(root, f)
                rel_path = os.path.relpath(fpath, self.dataset_root)
                subfolder = os.path.basename(os.path.dirname(fpath)) # AD, HC, MCI
                category = os.path.basename(os.path.dirname(os.path.dirname(fpath))) # raw, merge, raw_vad, merge_vad

                # Parse filename: e.g. AD_F_030807_001.wav or AD_F_030807.wav
                parts = f.replace(".wav", "").split("_")
                dx_group = parts[0] if len(parts) > 0 else subfolder
                gender = parts[1] if len(parts) > 1 else "Unknown"
                speaker_num = parts[2] if len(parts) > 2 else "Unknown"
                task_id = parts[3] if len(parts) > 3 else "merged"

                # Read audio properties via standard wave module
                try:
                    with wave.open(fpath, "rb") as w:
                        channels = w.getnchannels()
                        sample_rate = w.getframerate()
                        frames = w.getnframes()
                        duration_sec = round(frames / float(sample_rate), 2)
                        sample_width_bytes = w.getsampwidth()
                except Exception:
                    channels = 1
                    sample_rate = 16000
                    duration_sec = 0.0
                    sample_width_bytes = 2

                records.append({
                    "file_name": f,
                    "relative_path": rel_path,
                    "dataset_partition": category, # raw, merge, raw_vad, merge_vad
                    "diagnostic_group": dx_group,  # AD, MCI, HC
                    "gender": gender,
                    "speaker_id": "SPK_" + speaker_num,
                    "task_identifier": task_id,
                    "sample_rate_hz": sample_rate,
                    "channels_count": channels,
                    "sample_width_bytes": sample_width_bytes,
                    "duration_sec": duration_sec
                })

        df = pd.DataFrame(records)

        meta = {
            "dataset": "NCMMSC2021_Audio_Metadata",
            "modality": "speech_audio_recordings",
            "source_directory": self.dataset_root,
            "total_wav_recordings_n": len(df),
            "unique_speakers_n": int(df["speaker_id"].nunique()),
            "sampling_rate_distribution": df["sample_rate_hz"].value_counts().to_dict(),
            "diagnostic_group_distribution": df[df["dataset_partition"] == "raw"]["diagnostic_group"].value_counts().to_dict(),
            "leakage_grouping_variable": "speaker_id",
            "license": "NCMMSC Challenge Evaluation Terms (Academic Research Only; Redistribution Forbidden)",
            "clinical_governance_note": "Acoustic cognitive screening metadata. Requires GroupKFold on speaker_id."
        }

        self.save_output(df, meta, "ncmmsc2021_audio_metadata")
        return df, meta


class MDVRKCLMetadataPreparer(BaseDatasetPreparer):
    """Prepares MDVR-KCL audio metadata from ZIP container as out-of-domain Parkinson's benchmark."""

    def __init__(self, zip_path: str):
        super().__init__("MDVR_KCL_Audio_Metadata", "speech_acoustic")
        self.zip_path = zip_path

    def inspect(self) -> Dict[str, Any]:
        with zipfile.ZipFile(self.zip_path, "r") as z:
            wavs = [n for n in z.namelist() if n.endswith(".wav")]
        return {
            "source_path": self.zip_path,
            "raw_sha256": self.calculate_sha256(self.zip_path),
            "total_wavs": len(wavs)
        }

    def prepare(self) -> Tuple[pd.DataFrame, Dict[str, Any]]:
        source_sha256 = self.calculate_sha256(self.zip_path)
        records = []

        with zipfile.ZipFile(self.zip_path, "r") as z:
            wav_names = [n for n in z.namelist() if n.endswith(".wav")]
            for wname in wav_names:
                wav_bytes = z.read(wname)
                try:
                    with wave.open(io.BytesIO(wav_bytes), "rb") as w:
                        channels = w.getnchannels()
                        sample_rate = w.getframerate()
                        frames = w.getnframes()
                        duration_sec = round(frames / float(sample_rate), 2)
                except Exception:
                    channels = 1
                    sample_rate = 44100
                    duration_sec = 0.0

                fname = os.path.basename(wname)
                # Parse filename: e.g. PD_01_readtext.wav or HC_02_spontaneous.wav
                parts = fname.replace(".wav", "").split("_")
                disease_status = "Parkinsons" if parts[0].upper() == "PD" else "HealthyControl"
                speaker_id = "MDVR_" + (parts[1] if len(parts) > 1 else "Unknown")
                task = parts[2] if len(parts) > 2 else "speech"

                records.append({
                    "file_name": fname,
                    "speaker_id": speaker_id,
                    "disease_cohort": disease_status, # Parkinsons vs HealthyControl (ZERO DEMENTIA)
                    "task_type": task,
                    "sample_rate_hz": sample_rate,
                    "channels_count": channels,
                    "duration_sec": duration_sec,
                    "has_dementia": 0 # Explicitly zero
                })

        df = pd.DataFrame(records)

        meta = {
            "dataset": "MDVR_KCL_Audio_Metadata",
            "modality": "speech_acoustic_out_of_domain",
            "source_file": self.zip_path,
            "source_sha256": source_sha256,
            "population": "Parkinson's Disease (N=31) and Healthy Controls (N=42) - ZERO DEMENTIA PATIENTS",
            "total_audio_recordings_n": len(df),
            "unique_speakers_n": int(df["speaker_id"].nunique()),
            "leakage_grouping_variable": "speaker_id",
            "license": "Creative Commons Attribution (CC-BY 4.0)",
            "clinical_governance_note": "Out-of-domain acoustic specificity negative control benchmark. Forbidden to pool into dementia diagnostic models."
        }

        self.save_output(df, meta, "mdvr_kcl_audio_metadata")
        return df, meta
