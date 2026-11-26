"""
Text Feature Extraction for Conversational Response Quality (AI4HOPE).
Extracts stylistic, readability, and lexical markers from clinical prompts and answers.
"""

import re
from typing import Dict, Any, List
import pandas as pd
import numpy as np
from sklearn.feature_extraction.text import TfidfVectorizer


class TextFeatureExtractor:
    """Extracts lexical and stylistic features from QA prompt-response text."""

    EMPATHIC_MARKERS = [
        "understand", "challenging", "support", "comfort", "difficult",
        "gentle", "care", "patience", "listen", "reassure", "peaceful", "help"
    ]

    ACTIONABLE_MARKERS = [
        "step", "first", "next", "routine", "contact", "doctor", "schedule",
        "technique", "ensure", "avoid", "recommend", "action", "practice"
    ]

    def __init__(self, max_tfidf_features: int = 100):
        self.max_tfidf_features = max_tfidf_features
        self.tfidf_vectorizer = TfidfVectorizer(
            max_features=self.max_tfidf_features,
            ngram_range=(1, 2),
            stop_words="english"
        )
        self._is_fitted = False

    def extract_single(self, question: str, answer: str, persona: str = "Professional caregiver") -> Dict[str, float]:
        """Extracts numerical features for a single prompt-answer pair."""
        q_len = len(question) if question else 0
        a_len = len(answer) if answer else 0
        q_words = len(question.split()) if question else 0
        a_words = len(answer.split()) if answer else 0

        # Lexical markers
        a_lower = answer.lower() if answer else ""
        empathy_hits = sum(1 for w in self.EMPATHIC_MARKERS if re.search(r"\b" + w + r"\b", a_lower))
        actionable_hits = sum(1 for w in self.ACTIONABLE_MARKERS if re.search(r"\b" + w + r"\b", a_lower))

        # Sentence count and average sentence length
        sentences = re.split(r"[.!?]+", answer) if answer else []
        sentences = [s.strip() for s in sentences if s.strip()]
        sentence_count = len(sentences)
        avg_sentence_len = (a_words / sentence_count) if sentence_count > 0 else 0.0

        # Ratio of answer words to question words (elaboration ratio)
        elaboration_ratio = (a_words / max(q_words, 1))

        features = {
            "question_char_len": float(q_len),
            "answer_char_len": float(a_len),
            "question_word_count": float(q_words),
            "answer_word_count": float(a_words),
            "sentence_count": float(sentence_count),
            "avg_sentence_len": float(avg_sentence_len),
            "elaboration_ratio": float(elaboration_ratio),
            "empathy_marker_count": float(empathy_hits),
            "actionable_marker_count": float(actionable_hits),
            "is_professional_persona": 1.0 if persona == "Professional caregiver" else 0.0,
            "is_patient_persona": 1.0 if persona == "Patient" else 0.0,
            "is_family_persona": 1.0 if persona == "Family caregiver" else 0.0,
        }
        return features

    def fit_transform_corpus(self, df: pd.DataFrame) -> Tuple_Features:
        """Fits TF-IDF on corpus answers and combines with manual features."""
        # Manual features
        records = []
        for _, row in df.iterrows():
            q = str(row.get("question", ""))
            a = str(row.get("answer", ""))
            p = str(row.get("answer_style", "Professional caregiver"))
            records.append(self.extract_single(q, a, p))

        manual_df = pd.DataFrame(records)

        # Fit TF-IDF on answers
        answers = df["answer"].fillna("").astype(str).tolist()
        tfidf_matrix = self.tfidf_vectorizer.fit_transform(answers).toarray()
        self._is_fitted = True

        tfidf_cols = [f"tfidf_{i}" for i in range(tfidf_matrix.shape[1])]
        tfidf_df = pd.DataFrame(tfidf_matrix, columns=tfidf_cols, index=df.index)

        return pd.concat([manual_df, tfidf_df], axis=1)

    def transform_corpus(self, df: pd.DataFrame) -> pd.DataFrame:
        """Transforms new corpus using fitted TF-IDF and manual features."""
        if not self._is_fitted:
            raise RuntimeError("Extractor must be fitted with fit_transform_corpus first.")

        records = []
        for _, row in df.iterrows():
            q = str(row.get("question", ""))
            a = str(row.get("answer", ""))
            p = str(row.get("answer_style", "Professional caregiver"))
            records.append(self.extract_single(q, a, p))

        manual_df = pd.DataFrame(records)

        answers = df["answer"].fillna("").astype(str).tolist()
        tfidf_matrix = self.tfidf_vectorizer.transform(answers).toarray()
        tfidf_cols = [f"tfidf_{i}" for i in range(tfidf_matrix.shape[1])]
        tfidf_df = pd.DataFrame(tfidf_matrix, columns=tfidf_cols, index=df.index)

        return pd.concat([manual_df, tfidf_df], axis=1)


Tuple_Features = pd.DataFrame
