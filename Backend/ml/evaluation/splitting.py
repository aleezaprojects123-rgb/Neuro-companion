"""
Participant-Level and User-Grouped Data Splitting.
Strictly prevents rater, expert, or patient leakage between train and test partitions.
"""

from typing import Generator, Tuple, List
import numpy as np
import pandas as pd
from sklearn.model_selection import GroupKFold, GroupShuffleSplit


class GroupedDataSplitter:
    """Manages leakage-free grouped cross-validation and holdout splitting."""

    @staticmethod
    def get_group_kfold_splits(
        df: pd.DataFrame, group_col: str, n_splits: int = 5
    ) -> Generator[Tuple[np.ndarray, np.ndarray], None, None]:
        """Yields train and test index arrays guaranteed to have zero overlapping groups."""
        if group_col not in df.columns:
            raise KeyError(f"Group column '{group_col}' not found in DataFrame.")

        groups = df[group_col].values
        unique_groups = len(np.unique(groups))
        if unique_groups < n_splits:
            raise ValueError(
                f"Cannot create {n_splits} folds with only {unique_groups} unique groups."
            )

        gkf = GroupKFold(n_splits=n_splits)
        for train_idx, test_idx in gkf.split(df, groups=groups):
            # Assert zero group leakage
            train_groups = set(df.iloc[train_idx][group_col].unique())
            test_groups = set(df.iloc[test_idx][group_col].unique())
            overlap = train_groups.intersection(test_groups)
            if overlap:
                raise RuntimeError(f"Group leakage detected in split! Overlapping groups: {overlap}")
            yield train_idx, test_idx

    @staticmethod
    def train_test_group_split(
        df: pd.DataFrame, group_col: str, test_size: float = 0.2, random_state: int = 42
    ) -> Tuple[pd.DataFrame, pd.DataFrame]:
        """Splits DataFrame into train and test sets with zero group overlap."""
        gss = GroupShuffleSplit(n_splits=1, test_size=test_size, random_state=random_state)
        groups = df[group_col].values
        train_idx, test_idx = next(gss.split(df, groups=groups))

        train_df = df.iloc[train_idx].copy().reset_index(drop=True)
        test_df = df.iloc[test_idx].copy().reset_index(drop=True)

        # Integrity verification
        train_groups = set(train_df[group_col].unique())
        test_groups = set(test_df[group_col].unique())
        overlap = train_groups.intersection(test_groups)
        if overlap:
            raise RuntimeError(f"Group leakage detected! Overlapping groups: {overlap}")

        return train_df, test_df
