"""
Model Version Catalog and Registry Manager.
Tracks versioned model artifacts, training timestamps, and provenance.
"""

import os
import json
from typing import Dict, Any, List, Optional


class ModelVersionCatalog:
    """Manages versioned models, metadata, and status in Backend/saved_model/phase4/."""

    def __init__(self, models_dir: Optional[str] = None):
        if models_dir is None:
            current_dir = os.path.dirname(os.path.abspath(__file__))
            models_dir = os.path.abspath(os.path.join(current_dir, "..", "..", "saved_model", "phase4"))
        self.models_dir = models_dir
        os.makedirs(self.models_dir, exist_ok=True)
        self.catalog_path = os.path.join(self.models_dir, "model_catalog.json")
        self._catalog = self._load_catalog()

    def _load_catalog(self) -> Dict[str, Any]:
        if os.path.exists(self.catalog_path):
            with open(self.catalog_path, "r", encoding="utf-8") as f:
                return json.load(f)
        return {"models": {}}

    def _save_catalog(self):
        with open(self.catalog_path, "w", encoding="utf-8") as f:
            json.dump(self._catalog, f, indent=2)

    def register_model_version(self, model_id: str, version: str, metadata: Dict[str, Any]):
        """Registers metadata for a versioned model."""
        if model_id not in self._catalog["models"]:
            self._catalog["models"][model_id] = {}
        self._catalog["models"][model_id][version] = metadata
        self._save_catalog()

    def get_model_metadata(self, model_id: str, version: str) -> Optional[Dict[str, Any]]:
        """Retrieves metadata for a specific model version."""
        return self._catalog.get("models", {}).get(model_id, {}).get(version)

    def list_models(self) -> List[str]:
        """Lists all registered model IDs."""
        return list(self._catalog.get("models", {}).keys())

    def list_versions(self, model_id: str) -> List[str]:
        """Lists all versions registered for a model ID."""
        return list(self._catalog.get("models", {}).get(model_id, {}).keys())
