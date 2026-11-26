"""
Model Signal Provider for NeuroCompanion Phase 4.5.
Safely loads and extracts supporting signals from existing Phase 4.4 model artifacts:
- M3: Task Performance Error Risk Signal (Robot Games Hurdle Model)
- M6: Dialogue Quality Text Scorer (AI4HOPE RidgeCV Regressors)
- M1: Latency Pacing Reference (Independent MSL and N-Back Log-Normal Densities)
- M4: Descriptive Domestic Task Autonomy Prior (CASAS Activity Base Rates)

Enforces strict clinical limitations and graceful fallback handling if artifacts are missing.
"""

import os
import sys
import json
import logging
import warnings
from typing import Dict, Any, Optional
import joblib
import numpy as np

# Ensure Backend/ root is in sys.path
CURRENT_DIR = os.path.dirname(os.path.abspath(__file__))
BACKEND_ROOT = os.path.abspath(os.path.join(CURRENT_DIR, "..", ".."))
if BACKEND_ROOT not in sys.path:
    sys.path.insert(0, BACKEND_ROOT)

from ml.models.pipelines import (
    M6DialogueIntelligencePipeline,
    M3TaskPerformancePipeline,
    M1PacingEstimatorArtifact
)

logger = logging.getLogger(__name__)


class ModelSignalProvider:
    """
    Safely interfaces with trained Phase 4.4 models without mutating them.
    Guarantees robust fallback execution if artifacts are temporarily inaccessible.
    """

    def __init__(self, backend_root: Optional[str] = None):
        self.backend_root = backend_root or BACKEND_ROOT
        self.saved_dir = os.path.join(self.backend_root, "saved_model", "phase4")

        self.m1_model: Optional[M1PacingEstimatorArtifact] = None
        self.m3_model: Optional[M3TaskPerformancePipeline] = None
        self.m6_model: Optional[M6DialogueIntelligencePipeline] = None
        self.m4_priors: Optional[Dict[str, Any]] = None

        self._load_artifacts()

    def _load_artifacts(self):
        """Loads available model artifacts with individual try/except blocks for fault tolerance."""
        # 1. Load M6 Dialogue Intelligence
        m6_path = os.path.join(self.saved_dir, "m6_dialogue", "v1", "model.joblib")
        if os.path.exists(m6_path):
            try:
                self.m6_model = joblib.load(m6_path)
            except Exception as e:
                logger.warning(f"Could not load M6 model from {m6_path}: {e}")

        # 2. Load M3 Task Performance
        m3_path = os.path.join(self.saved_dir, "m3_task_perf", "v1", "model.joblib")
        if os.path.exists(m3_path):
            try:
                self.m3_model = joblib.load(m3_path)
            except Exception as e:
                logger.warning(f"Could not load M3 model from {m3_path}: {e}")

        # 3. Load M1 Latency Pacing
        m1_path = os.path.join(self.saved_dir, "m1_pacing", "v1", "model.joblib")
        if os.path.exists(m1_path):
            try:
                self.m1_model = joblib.load(m1_path)
            except Exception as e:
                logger.warning(f"Could not load M1 model from {m1_path}: {e}")

        # 4. Load M4 Historical Independence Prior Table
        m4_path = os.path.join(self.saved_dir, "m4_independence", "v1", "metrics.json")
        if os.path.exists(m4_path):
            try:
                with open(m4_path, "r", encoding="utf-8") as f:
                    self.m4_priors = json.load(f)
            except Exception as e:
                logger.warning(f"Could not load M4 prior table from {m4_path}: {e}")

    # =========================================================================
    # M3: Task Performance Error Risk Signal
    # =========================================================================

    def get_m3_error_risk(self, game_type: str, reaction_time_sec: float) -> Dict[str, Any]:
        """
        Extracts supporting task-error-risk signals from M3 Hurdle model.
        LIMITATION: Normative older adult baseline only. Does NOT directly predict
        assistance requirement or patient autonomy.
        """
        if self.m3_model is not None:
            try:
                with warnings.catch_warnings():
                    warnings.filterwarnings("ignore", category=UserWarning)
                    pred = self.m3_model.predict_single(game_type, reaction_time_sec)
                return {
                    "source": "M3_TASK_PERF_v1.0.0",
                    "error_probability": pred["error_probability"],
                    "predicted_errors_count": pred["predicted_errors_count"],
                    "expected_errors_count": pred["expected_errors_count"],
                    "is_fallback": False,
                    "limitation": "Supporting task error risk only. Does not predict clinical assistance benefit."
                }
            except Exception as e:
                logger.warning(f"M3 inference error: {e}")

        # Graceful fallback: Default normative prior
        return {
            "source": "M3_FALLBACK_HEURISTIC",
            "error_probability": 0.22,  # Baseline 1 - 0.775 zero fraction
            "predicted_errors_count": 0.0,
            "expected_errors_count": 0.15,
            "is_fallback": True,
            "limitation": "M3 artifact unavailable. Using population baseline prior."
        }

    # =========================================================================
    # M6: Dialogue Text Quality Signal
    # =========================================================================

    def get_m6_dialogue_quality(self, candidate_text: Optional[str]) -> Dict[str, Any]:
        """
        Evaluates candidate speech text quality using M6 RidgeCV regressors.
        LIMITATION: Evaluates text surface empathy and actionability only.
        Does NOT prove that verbal explanation will benefit the patient.
        """
        if not candidate_text or len(candidate_text.strip()) == 0:
            return {
                "source": "M6_EMPTY_TEXT",
                "predicted_empathy": 3.0,
                "predicted_actionability": 3.0,
                "is_fallback": True,
                "limitation": "No candidate text provided to evaluate."
            }

        if self.m6_model is not None:
            try:
                pred = self.m6_model.predict_single(candidate_text)
                return {
                    "source": "M6_DIALOGUE_v1.0.0",
                    "predicted_empathy": pred["predicted_empathy"],
                    "predicted_actionability": pred["predicted_actionability"],
                    "is_fallback": False,
                    "limitation": "Scores text empathy and actionability only. Does not predict patient comprehension or assistance need."
                }
            except Exception as e:
                logger.warning(f"M6 inference error: {e}")

        # Fallback lexical heuristic
        has_empathy = any(w in candidate_text.lower() for w in ["understand", "together", "breathe", "support", "comfort", "okay"])
        has_action = any(w in candidate_text.lower() for w in ["click", "press", "step", "first", "next", "choose"])
        return {
            "source": "M6_FALLBACK_LEXICAL",
            "predicted_empathy": 3.5 if has_empathy else 3.0,
            "predicted_actionability": 3.8 if has_action else 3.2,
            "is_fallback": True,
            "limitation": "M6 artifact unavailable. Lexical keyword fallback applied."
        }

    # =========================================================================
    # M1: Latency Pacing Reference Signal
    # =========================================================================

    def get_m1_pacing_reference(self, domain: str = "cognitive_selection") -> Dict[str, Any]:
        """
        Retrieves separate MSL and N-Back Log-Normal latency reference statistics.
        LIMITATION: Normative population percentiles only. FORBIDDEN to enforce
        automatic patient timeouts or treat as an individual clinical requirement.
        """
        # Distinguish domain: motor tapping vs cognitive selection
        key = "motor_tapping" if "motor" in domain.lower() or "tap" in domain.lower() else "working_memory"

        if self.m1_model is not None:
            try:
                ref = self.m1_model.get_pacing_reference(key)
                return {
                    "source": f"M1_PACING_v1.0.0_{key}",
                    "domain": key,
                    "p50_ms": ref["fitted_percentiles_ms"]["p50"],
                    "p75_ms": ref["fitted_percentiles_ms"]["p75"],
                    "p90_ms": ref["fitted_percentiles_ms"]["p90"],
                    "p95_ms": ref["fitted_percentiles_ms"]["p95"],
                    "sample_size": ref["sample_size"],
                    "is_fallback": False,
                    "limitation": "Population reference distribution only. Not an individual patient requirement or timeout."
                }
            except Exception as e:
                logger.warning(f"M1 retrieval error: {e}")

        # Fallback certified constants from Phase 4.4
        if key == "motor_tapping":
            return {
                "source": "M1_FALLBACK_MOTOR",
                "domain": "motor_tapping",
                "p50_ms": 881.22,
                "p75_ms": 939.91,
                "p90_ms": 996.07,
                "p95_ms": 1031.27,
                "sample_size": 32,
                "is_fallback": True,
                "limitation": "Fallback constants from MSL older adults."
            }
        return {
            "source": "M1_FALLBACK_COGNITIVE",
            "domain": "working_memory",
            "p50_ms": 883.89,
            "p75_ms": 1061.03,
            "p90_ms": 1250.61,
            "p95_ms": 1379.91,
            "sample_size": 24,
            "is_fallback": True,
            "limitation": "Fallback constants from N-Back dementia cohort."
        }

    # =========================================================================
    # M4: Historical ADL Independence Prior
    # =========================================================================

    def get_m4_task_prior(self, activity_id: Optional[int] = None) -> Dict[str, Any]:
        """
        Retrieves descriptive physical ADL chore independence prior from CASAS.
        LIMITATION: Domestic smart home chores do not map directly to Unity companion gameplay.
        """
        overall_rate = 0.6977
        act_rate = overall_rate
        if self.m4_priors is not None:
            overall_rate = self.m4_priors.get("overall_historical_independence_rate", 0.6977)
            if activity_id is not None:
                act_str = str(activity_id)
                act_dict = self.m4_priors.get("activity_specific_priors", {}).get(act_str)
                if act_dict:
                    act_rate = act_dict.get("independence_rate", overall_rate)

        return {
            "source": "M4_CASAS_DESCRIPTIVE_PRIOR",
            "overall_independence_rate": overall_rate,
            "activity_specific_independence_rate": act_rate,
            "limitation": "Historical physical ADL base rates. Not direct digital gaming autonomy labels."
        }
