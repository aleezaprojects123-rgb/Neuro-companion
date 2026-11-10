"""
predict.py - Model inference module for the NeuroCompanion Phase 1 backend.

Accepts practical OASIS-1 candidate features:
  - Age (float/int, e.g. 60-90)
  - M/F (str: "M" or "F")
  - Educ (float/int: 1 to 5)
  - MMSE (float/int: 0 to 30)

Returns:
  - predicted_class (0, 1, or 2)
  - predicted_label (human-readable string)
  - class_probabilities (dict of probabilities per class)
  - model_version
  - disclaimer

DISCLAIMER: Educational machine-learning prototype. Not a medical diagnostic tool.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path
from typing import Any

import joblib
import pandas as pd

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from model.config import MODEL_PATH, MODEL_VERSION, SCHEMA_PATH, TARGET_MAPPING

_pipeline = None
_schema: dict[str, Any] | None = None


def _load_artefacts() -> None:
    """Load fitted pipeline and schema once into module memory."""
    global _pipeline, _schema

    if _pipeline is None:
        if not MODEL_PATH.exists():
            raise FileNotFoundError(
                f"[ERROR] Trained pipeline not found at {MODEL_PATH}. "
                "Run 'python model/train_model.py' first."
            )
        _pipeline = joblib.load(MODEL_PATH)

    if _schema is None:
        if not SCHEMA_PATH.exists():
            raise FileNotFoundError(
                f"[ERROR] Schema file not found at {SCHEMA_PATH}. "
                "Run 'python model/preprocess.py' first."
            )
        with open(SCHEMA_PATH, "r", encoding="utf-8") as f:
            _schema = json.load(f)


def predict(input_data: dict[str, Any]) -> dict[str, Any]:
    """
    Validate input payload, run inference with the trained pipeline, and return prediction.
    """
    _load_artefacts()
    _validate_input(input_data)

    feature_names: list[str] = _schema["feature_names"]
    df_input = pd.DataFrame([input_data])[feature_names]

    predicted_class = int(_pipeline.predict(df_input)[0])
    predicted_label = TARGET_MAPPING[predicted_class]

    # Probabilities if supported by the model
    probabilities: dict[str, float] = {}
    if hasattr(_pipeline, "predict_proba"):
        probs = _pipeline.predict_proba(df_input)[0]
        for idx, p in enumerate(probs):
            probabilities[f"class_{idx}"] = round(float(p), 4)

    return {
        "model_version": MODEL_VERSION,
        "predicted_class": predicted_class,
        "predicted_label": predicted_label,
        "class_probabilities": probabilities,
        "disclaimer": (
            "Educational machine-learning demonstration only. "
            "Not a medical diagnosis. "
            "MMSE must be a properly administered screening assessment."
        ),
    }


def is_model_loaded() -> bool:
    """Check if model and schema are cached in memory."""
    return _pipeline is not None and _schema is not None


def get_feature_names() -> list[str]:
    """Return ordered list of required feature names."""
    _load_artefacts()
    return _schema["feature_names"]


def _validate_input(input_data: dict[str, Any]) -> None:
    """Validate presence and basic domains of practical input features."""
    required = _schema["feature_names"]
    missing = [f for f in required if f not in input_data]
    if missing:
        raise ValueError(f"Missing required features: {missing}. Required: {required}")

    # Check M/F
    mf_val = str(input_data["M/F"]).strip().upper()
    if mf_val not in ["M", "F"]:
        raise ValueError(f"Invalid 'M/F' value: '{input_data['M/F']}'. Expected 'M' or 'F'.")


# ---------------------------------------------------------------------------
# CLI smoke test
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    _load_artefacts()
    sample = {"Age": 74.0, "M/F": "F", "Educ": 2.0, "MMSE": 22.0}
    print("[INFO] Running test prediction on sample:", sample)
    res = predict(sample)
    print("[RESULT]:", json.dumps(res, indent=2))
