"""
Dataset Loader for AI4HOPE Conversational AI Collection.
Loads expert prompts, caregiver ratings, and participant demographics.
"""

import os
from typing import Dict, Any, Tuple
import pandas as pd
from .base_loader import BaseDatasetLoader


class AI4HOPELoader(BaseDatasetLoader):
    """Encapsulates loading and validation of AI4HOPE CSV tables."""

    EXPECTED_EXPERT_COLS = [
        "user_id", "study_group", "interaction_id", "question",
        "answer_style", "answer", "language", "relationship",
        "answer_rating_relevance", "answer_rating_clarity",
        "answer_rating_empathy", "answer_rating_actionability",
        "answer_rating_misinformation_flagged", "answer_rating_bias_flagged"
    ]

    EXPECTED_RESPONSE_COLS = [
        "user_id", "study_group", "interaction_id", "answer_rating_role",
        "answer_rating_information_support", "answer_rating_supported_at_ease",
        "answer_rating_ease_of_understanding"
    ]

    def __init__(self, base_dir: str):
        super().__init__(base_dir)
        self.expert_prompts_df: pd.DataFrame = pd.DataFrame()
        self.response_ratings_df: pd.DataFrame = pd.DataFrame()
        self.participants_users_df: pd.DataFrame = pd.DataFrame()

    def load(self) -> Dict[str, pd.DataFrame]:
        ep_path = os.path.join(self.dataset_path, "expert_prompts.csv")
        rr_path = os.path.join(self.dataset_path, "response_ratings.csv")
        pu_path = os.path.join(self.dataset_path, "participants_users.csv")

        if not os.path.exists(ep_path) or not os.path.exists(rr_path):
            raise FileNotFoundError(f"AI4HOPE files not found in {self.dataset_path}")

        self.expert_prompts_df = pd.read_csv(ep_path)
        self.response_ratings_df = pd.read_csv(rr_path)
        if os.path.exists(pu_path):
            self.participants_users_df = pd.read_csv(pu_path)

        self._is_validated, msg = self.validate()
        if not self._is_validated:
            raise ValueError(f"AI4HOPE validation failed: {msg}")

        return {
            "expert_prompts": self.expert_prompts_df,
            "response_ratings": self.response_ratings_df,
            "participants_users": self.participants_users_df
        }

    def validate(self) -> Tuple[bool, str]:
        if self.expert_prompts_df.empty or self.response_ratings_df.empty:
            return False, "DataFrames are empty."

        for c in self.EXPECTED_EXPERT_COLS:
            if c not in self.expert_prompts_df.columns:
                return False, f"Missing expected column '{c}' in expert_prompts.csv"

        for c in self.EXPECTED_RESPONSE_COLS:
            if c not in self.response_ratings_df.columns:
                return False, f"Missing expected column '{c}' in response_ratings.csv"

        if len(self.expert_prompts_df) != 600:
            return False, f"Expected 600 expert prompts, got {len(self.expert_prompts_df)}"

        if self.expert_prompts_df["user_id"].nunique() != 60:
            return False, f"Expected 60 unique experts, got {self.expert_prompts_df['user_id'].nunique()}"

        return True, "AI4HOPE schema and record counts verified."

    def get_summary(self) -> Dict[str, Any]:
        return {
            "expert_prompts_count": len(self.expert_prompts_df),
            "unique_experts": int(self.expert_prompts_df["user_id"].nunique()) if not self.expert_prompts_df.empty else 0,
            "caregiver_ratings_count": len(self.response_ratings_df),
            "unique_caregivers": int(self.response_ratings_df["user_id"].nunique()) if not self.response_ratings_df.empty else 0,
            "empathy_mean": float(self.expert_prompts_df["answer_rating_empathy"].mean()) if not self.expert_prompts_df.empty else None,
            "actionability_mean": float(self.expert_prompts_df["answer_rating_actionability"].mean()) if not self.expert_prompts_df.empty else None,
            "misinformation_positive_count": int((self.expert_prompts_df["answer_rating_misinformation_flagged"] == "yes").sum()) if not self.expert_prompts_df.empty else 0
        }
