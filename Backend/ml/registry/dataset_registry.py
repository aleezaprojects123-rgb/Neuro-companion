"""
Dataset Registry for NeuroCompanion Phase 4.
Provides cryptographic verification, provenance tracking, and metadata access
for all 11 research collections in Backend/research_archive/.
"""

import os
import json
import hashlib
from typing import Dict, Any, List, Optional


class DatasetRegistry:
    """Manages dataset metadata, provenance, and file availability."""

    def __init__(self, backend_root: Optional[str] = None):
        if backend_root is None:
            # Default to Backend directory based on this file's path
            current_dir = os.path.dirname(os.path.abspath(__file__))
            backend_root = os.path.abspath(os.path.join(current_dir, "..", ".."))
        self.backend_root = backend_root
        self.manifest_path = os.path.join(
            self.backend_root, "ml", "registry", "manifest.json"
        )
        self._manifest = self._load_manifest()

    def _load_manifest(self) -> Dict[str, Any]:
        if not os.path.exists(self.manifest_path):
            raise FileNotFoundError(f"Manifest not found: {self.manifest_path}")
        with open(self.manifest_path, "r", encoding="utf-8") as f:
            return json.load(f)

    def list_datasets(self) -> List[str]:
        """Returns list of all registered dataset keys."""
        return list(self._manifest.get("datasets", {}).keys())

    def get_dataset_info(self, key: str) -> Dict[str, Any]:
        """Returns full metadata dictionary for a dataset key."""
        datasets = self._manifest.get("datasets", {})
        if key not in datasets:
            raise KeyError(f"Dataset '{key}' not registered in manifest.")
        return datasets[key]

    def resolve_path(self, key: str) -> str:
        """Resolves the absolute path for a dataset's primary file/folder."""
        info = self.get_dataset_info(key)
        rel_path = info.get("relative_path", "")
        return os.path.abspath(os.path.join(self.backend_root, rel_path))

    def verify_availability(self, key: str) -> bool:
        """Checks whether the dataset files physically exist on disk."""
        target_path = self.resolve_path(key)
        info = self.get_dataset_info(key)
        if "files" in info:
            # Check collection of files
            for f in info["files"]:
                full_f = os.path.join(target_path, f)
                if not os.path.exists(full_f):
                    return False
            return True
        return os.path.exists(target_path)

    def compute_sha256(self, key: str) -> Optional[str]:
        """Computes SHA-256 for single-file datasets."""
        target_path = self.resolve_path(key)
        if not os.path.isfile(target_path):
            return None
        sha = hashlib.sha256()
        with open(target_path, "rb") as f:
            for chunk in iter(lambda: f.read(65536), b""):
                sha.update(chunk)
        return sha.hexdigest()

    def get_summary(self) -> Dict[str, Any]:
        """Returns availability and metadata status across all datasets."""
        summary = {}
        for key in self.list_datasets():
            info = self.get_dataset_info(key)
            available = self.verify_availability(key)
            summary[key] = {
                "name": info.get("name"),
                "sample_size": info.get("sample_size"),
                "available": available,
                "scope": info.get("legitimate_scope"),
                "license": info.get("license")
            }
        return summary
