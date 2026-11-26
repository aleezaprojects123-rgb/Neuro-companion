"""
Configuration package for NeuroCompanion Phase 4 Machine Learning Models.
Provides schemas and loader utilities for model-specific training and validation configurations.
"""

import os
import json
from typing import Dict, Any, List

CONFIG_DIR = os.path.dirname(os.path.abspath(__file__))


def load_model_config(config_name: str) -> Dict[str, Any]:
    """Loads a specific model configuration JSON file by name."""
    if not config_name.endswith(".json"):
        config_name += ".json"
    config_path = os.path.join(CONFIG_DIR, config_name)
    if not os.path.exists(config_path):
        raise FileNotFoundError(f"Model configuration file not found: {config_path}")
    with open(config_path, "r", encoding="utf-8") as f:
        return json.load(f)


def list_available_configs() -> List[str]:
    """Returns a list of all model configuration files in the directory."""
    return sorted([f for f in os.listdir(CONFIG_DIR) if f.endswith(".json")])
