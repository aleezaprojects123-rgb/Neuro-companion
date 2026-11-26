"""
Abstract Base Dataset Preparer for Phase 4.3 Unified Data Preparation.
Ensures standardized inspection, cleaning, transformation, metadata generation,
and strict data leakage prevention.
"""

from abc import ABC, abstractmethod
import os
import json
import hashlib
from typing import Dict, Any, Tuple, List, Optional
import pandas as pd


BACKEND_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
PROCESSED_BASE_DIR = os.path.join(BACKEND_DIR, "data", "processed", "phase4")


class BaseDatasetPreparer(ABC):
    """Abstract interface and helper utilities for all Phase 4.3 dataset preparers."""

    def __init__(self, dataset_name: str, subfolder: str):
        self.dataset_name = dataset_name
        self.subfolder = subfolder
        self.output_dir = os.path.join(PROCESSED_BASE_DIR, subfolder)
        os.makedirs(self.output_dir, exist_ok=True)
        self.metadata: Dict[str, Any] = {}

    @staticmethod
    def calculate_sha256(file_path: str) -> str:
        """Calculates SHA-256 hash of a file."""
        if not os.path.exists(file_path):
            return "FILE_NOT_FOUND"
        hasher = hashlib.sha256()
        with open(file_path, "rb") as f:
            for chunk in iter(lambda: f.read(65536), b""):
                hasher.update(chunk)
        return hasher.hexdigest()

    @staticmethod
    def audit_missingness(df: pd.DataFrame) -> Dict[str, int]:
        """Returns count of missing values per column."""
        return df.isnull().sum().to_dict()

    @staticmethod
    def audit_duplicates(df: pd.DataFrame, subset: Optional[List[str]] = None) -> int:
        """Returns count of duplicate rows given an optional subset of key columns."""
        return int(df.duplicated(subset=subset).sum())

    @abstractmethod
    def inspect(self) -> Dict[str, Any]:
        """Inspects raw dataset before preparation."""
        pass

    @abstractmethod
    def prepare(self) -> Tuple[pd.DataFrame, Dict[str, Any]]:
        """Cleans, normalizes, and prepares dataset, returning DataFrame and metadata."""
        pass

    def save_output(self, df: pd.DataFrame, meta: Dict[str, Any], filename_prefix: str) -> Tuple[str, str]:
        """Saves prepared CSV and companion metadata JSON, returning file paths."""
        csv_path = os.path.join(self.output_dir, f"{filename_prefix}_prepared.csv")
        json_path = os.path.join(self.output_dir, f"{filename_prefix}_meta.json")

        # Save prepared CSV
        df.to_csv(csv_path, index=False, encoding="utf-8")
        out_sha256 = self.calculate_sha256(csv_path)

        # Update metadata
        meta["output_file"] = csv_path
        meta["output_sha256"] = out_sha256
        meta["output_row_count"] = len(df)
        meta["output_column_count"] = len(df.columns)
        meta["columns"] = list(df.columns)
        meta["missing_values"] = self.audit_missingness(df)
        meta["total_duplicates"] = self.audit_duplicates(df)

        with open(json_path, "w", encoding="utf-8") as f:
            json.dump(meta, f, indent=2, default=str)

        print(f"[{self.dataset_name}] Prepared -> {csv_path} ({len(df):,} rows, {len(df.columns)} cols)")
        return csv_path, json_path
