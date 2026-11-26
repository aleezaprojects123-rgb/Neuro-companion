"""
Serialized Pipeline Containers for NeuroCompanion Phase 4 Trained Models.
Defines pickle-safe class structures for:
- M6: Dialogue Intelligence (TF-IDF + Dual RidgeCV for Empathy & Actionability)
- M3: Task Performance (Hurdle Logistic Regression + Poisson Count)
- M1: Latency Pacing Reference (Parametric Log-Normal distributions)
"""

from typing import Dict, Any, List, Optional
import numpy as np
import pandas as pd
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.linear_model import RidgeCV, LogisticRegression, PoissonRegressor
from sklearn.compose import ColumnTransformer


class M6DialogueIntelligencePipeline:
    """
    Trained multi-target dialogue quality predictor.
    Wraps TF-IDF vectorizer and RidgeCV regressors for empathy and actionability.
    """
    def __init__(self, vectorizer: TfidfVectorizer, model_empathy: RidgeCV, model_actionability: RidgeCV):
        self.vectorizer = vectorizer
        self.model_empathy = model_empathy
        self.model_actionability = model_actionability

    def predict(self, text_samples: List[str]) -> Dict[str, np.ndarray]:
        """Predicts empathy and actionability scores for given text strings."""
        X = self.vectorizer.transform(text_samples)
        pred_empathy = np.clip(self.model_empathy.predict(X), 1.0, 5.0)
        pred_actionability = np.clip(self.model_actionability.predict(X), 1.0, 5.0)
        return {
            "pred_empathy": pred_empathy,
            "pred_actionability": pred_actionability
        }

    def predict_single(self, text: str) -> Dict[str, float]:
        res = self.predict([text])
        return {
            "predicted_empathy": round(float(res["pred_empathy"][0]), 3),
            "predicted_actionability": round(float(res["pred_actionability"][0]), 3)
        }


class M3TaskPerformancePipeline:
    """
    Two-stage Hurdle count model for task performance errors.
    Stage 1: LogisticRegression predicts probability of >=1 errors.
    Stage 2: PoissonRegressor predicts expected error count.
    Uses strictly pre-trial / onset features: game_type and game_reaction_time_ms.
    """
    def __init__(self, preprocessor: ColumnTransformer, clf_hurdle: LogisticRegression, reg_count: PoissonRegressor):
        self.preprocessor = preprocessor
        self.clf_hurdle = clf_hurdle
        self.reg_count = reg_count

    def predict(self, df_input: pd.DataFrame) -> Dict[str, np.ndarray]:
        X_trans = self.preprocessor.transform(df_input)
        prob_error = self.clf_hurdle.predict_proba(X_trans)[:, 1]
        raw_count = self.reg_count.predict(X_trans)
        hurdle_count = np.where(prob_error >= 0.5, raw_count, 0.0)
        return {
            "error_probability": prob_error,
            "predicted_errors_count": hurdle_count,
            "expected_errors_count": prob_error * raw_count
        }

    def predict_single(self, game_type: str, game_reaction_time_ms: float) -> Dict[str, float]:
        df_single = pd.DataFrame([{
            "game_type": game_type,
            "game_reaction_time_ms": game_reaction_time_ms
        }])
        res = self.predict(df_single)
        return {
            "error_probability": round(float(res["error_probability"][0]), 4),
            "predicted_errors_count": round(float(res["predicted_errors_count"][0]), 2),
            "expected_errors_count": round(float(res["expected_errors_count"][0]), 3)
        }


class M1PacingEstimatorArtifact:
    """Statistical Reference Artifact for M1 Pacing."""
    def __init__(self, msl_params: Dict[str, Any], nback_params: Dict[str, Any]):
        self.msl_params = msl_params
        self.nback_params = nback_params

    def get_pacing_reference(self, domain: str = "motor_tapping") -> Dict[str, Any]:
        if domain == "motor_tapping":
            return self.msl_params
        return self.nback_params
