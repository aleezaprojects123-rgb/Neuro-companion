"""
Conversational and Text Data Preparer for Phase 4.3.
Prepares AI4HOPE dialogue ratings and Chinese Dataset 1 Cookie Theft transcripts.
Enforces evaluator/user-grouped validation.
"""

import os
import glob
import re
from typing import Dict, Any, Tuple
import pandas as pd
import numpy as np
from .base_preparer import BaseDatasetPreparer


def compute_flesch_kincaid_grade_level(text: str) -> float:
    """Computes standard Flesch-Kincaid Grade Level formula for English text."""
    if not isinstance(text, str) or len(text.strip()) == 0:
        return 0.0
    
    # Split sentences and words
    sentences = re.split(r"[.!?]+", text)
    sentences = [s.strip() for s in sentences if len(s.strip()) > 0]
    num_sentences = max(1, len(sentences))

    words = re.findall(r"\b[A-Za-z]+\b", text)
    num_words = max(1, len(words))

    # Approximate syllable count (vowel group heuristics)
    def count_syllables(w):
        w = w.lower()
        count = len(re.findall(r"[aeiouy]+", w))
        if w.endswith("e") and not w.endswith("le") and count > 1:
            count -= 1
        return max(1, count)

    num_syllables = sum(count_syllables(w) for w in words)

    # FKGL = 0.39 * (words / sentences) + 11.8 * (syllables / words) - 15.59
    fkgl = 0.39 * (num_words / num_sentences) + 11.8 * (num_syllables / num_words) - 15.59
    return round(float(np.clip(fkgl, 1.0, 18.0)), 2)


class AI4HOPEPreparer(BaseDatasetPreparer):
    """Prepares AI4HOPE conversational AI dialogue evaluations and readability metrics."""

    def __init__(self, prompts_csv_path: str, ratings_csv_path: str):
        super().__init__("AI4HOPE_Conversational_AI", "conversational_text")
        self.prompts_csv_path = prompts_csv_path
        self.ratings_csv_path = ratings_csv_path

    def inspect(self) -> Dict[str, Any]:
        df_p = pd.read_csv(self.prompts_csv_path)
        return {
            "prompts_path": self.prompts_csv_path,
            "prompts_sha256": self.calculate_sha256(self.prompts_csv_path),
            "total_turns": len(df_p),
            "unique_evaluators": int(df_p["user_id"].nunique())
        }

    def prepare(self) -> Tuple[pd.DataFrame, Dict[str, Any]]:
        df_p = pd.read_csv(self.prompts_csv_path)
        source_sha256 = self.calculate_sha256(self.prompts_csv_path)

        df = pd.DataFrame()
        df["evaluator_id"] = "AI4H_EVAL_" + df_p["user_id"].astype(str)
        df["interaction_id"] = "INTERACT_" + df_p["interaction_id"].astype(str)
        df["evaluator_group"] = df_p["study_group"].astype(str)
        df["answer_style"] = df_p["answer_style"].astype(str)
        df["question_text"] = df_p["question"].astype(str)
        df["answer_text"] = df_p["answer"].astype(str)

        # PRESERVE ORIGINAL CONVERSATIONAL QUALITY TARGETS
        df["target_empathy"] = pd.to_numeric(df_p["answer_rating_empathy"], errors="coerce")
        df["target_clarity"] = pd.to_numeric(df_p["answer_rating_clarity"], errors="coerce")
        df["target_actionability"] = pd.to_numeric(df_p["answer_rating_actionability"], errors="coerce")
        df["target_relevance"] = pd.to_numeric(df_p["answer_rating_relevance"], errors="coerce")
        df["misinformation_flag"] = (df_p["answer_rating_misinformation_flagged"].astype(str).str.lower() == "yes").astype(int)

        # Extract linguistic and readability features
        df["word_count"] = df["answer_text"].apply(lambda t: len(t.split()))
        df["character_count"] = df["answer_text"].apply(len)
        df["sentence_count"] = df["answer_text"].apply(lambda t: max(1, len(re.split(r"[.!?]+", t))))
        df["mean_sentence_length"] = df["word_count"] / df["sentence_count"]
        
        def type_token_ratio(t):
            words = [w.lower() for w in re.findall(r"\b[A-Za-z]+\b", t)]
            return round(len(set(words)) / max(1, len(words)), 3)

        df["lexical_diversity_ttr"] = df["answer_text"].apply(type_token_ratio)
        df["fkgl_reading_grade_level"] = df["answer_text"].apply(compute_flesch_kincaid_grade_level)
        df["is_accessible_grade_6"] = (df["fkgl_reading_grade_level"] <= 6.0).astype(int)

        meta = {
            "dataset": "AI4HOPE_Conversational_AI",
            "modality": "conversational_text_quality",
            "source_file": self.prompts_csv_path,
            "source_sha256": source_sha256,
            "population": "120 independent evaluators (60 clinicians, 60 family carers) rating palliative dementia dialogue",
            "total_dialogue_turns_n": len(df),
            "unique_evaluators_n": int(df["evaluator_id"].nunique()),
            "leakage_grouping_variable": "evaluator_id",
            "targets": ["target_empathy", "target_clarity", "target_actionability", "target_relevance"],
            "license": "Creative Commons Attribution (CC-BY 4.0)",
            "clinical_governance_note": "Conversational Response Quality Evaluation Model. Does NOT predict patient assistance needs."
        }

        self.save_output(df, meta, "ai4hope_dialogue")
        return df, meta


class ChineseTranscriptsPreparer(BaseDatasetPreparer):
    """Prepares Chinese Dataset 1 Cookie Theft dialogue transcripts."""

    def __init__(self, tsv_directory: str):
        super().__init__("Chinese_AD_Dataset_1_Transcripts", "conversational_text")
        self.tsv_directory = tsv_directory

    def inspect(self) -> Dict[str, Any]:
        tsvs = glob.glob(os.path.join(self.tsv_directory, "*.tsv"))
        return {
            "tsv_directory": self.tsv_directory,
            "total_tsv_files": len(tsvs)
        }

    def prepare(self) -> Tuple[pd.DataFrame, Dict[str, Any]]:
        tsvs = sorted(glob.glob(os.path.join(self.tsv_directory, "*.tsv")))
        records = []

        for fpath in tsvs:
            fname = os.path.basename(fpath)
            session_id = os.path.splitext(fname)[0]
            try:
                tsv_df = pd.read_csv(fpath, sep="\t", header=None)
                # TSV typically has timestamp, speaker, and text
                total_utterances = len(tsv_df)
                all_text = " ".join(tsv_df.iloc[:, -1].astype(str).tolist())
                total_chars = len(all_text.replace(" ", ""))
                tokens = list(all_text.replace(" ", ""))
                ttr = round(len(set(tokens)) / max(1, len(tokens)), 3)
                mlu = round(total_chars / max(1, total_utterances), 2)
            except Exception:
                total_utterances = 0
                total_chars = 0
                ttr = 0.0
                mlu = 0.0

            records.append({
                "session_id": session_id,
                "total_utterances": total_utterances,
                "total_characters": total_chars,
                "mean_length_of_utterance_mlu": mlu,
                "character_type_token_ratio": ttr
            })

        df = pd.DataFrame(records)

        meta = {
            "dataset": "Chinese_AD_Dataset_1_Transcripts",
            "modality": "transcript_linguistic_metrics",
            "source_directory": self.tsv_directory,
            "total_transcripts_n": len(df),
            "unique_sessions_n": int(df["session_id"].nunique()),
            "leakage_grouping_variable": "session_id",
            "license": "Academic Research License",
            "clinical_governance_note": "Linguistic syntactic degradation baseline in Cookie Theft picture description."
        }

        self.save_output(df, meta, "chinese1_transcripts")
        return df, meta
