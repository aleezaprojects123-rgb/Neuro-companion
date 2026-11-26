"""
Evaluation Metrics for Phase 4 Machine Learning Models.
Includes Quadratic Weighted Kappa, Spearman rank correlation, and balanced metrics.
"""

from typing import Dict, Any
import numpy as np
from scipy.stats import spearmanr
from sklearn.metrics import (
    cohen_kappa_score,
    mean_absolute_error,
    balanced_accuracy_score,
    f1_score,
    confusion_matrix
)


def calculate_quadratic_weighted_kappa(y_true: np.ndarray, y_pred: np.ndarray) -> float:
    """Calculates Quadratic Weighted Kappa for ordinal ratings."""
    y_t = np.round(np.clip(y_true, 0, 5)).astype(int)
    y_p = np.round(np.clip(y_pred, 0, 5)).astype(int)
    return float(cohen_kappa_score(y_t, y_p, weights="quadratic"))


def calculate_ordinal_metrics(y_true: np.ndarray, y_pred: np.ndarray) -> Dict[str, float]:
    """Calculates comprehensive ordinal evaluation metrics for regression models."""
    mae = float(mean_absolute_error(y_true, y_pred))
    qwk = calculate_quadratic_weighted_kappa(y_true, y_pred)
    rho, _ = spearmanr(y_true, y_pred)
    if np.isnan(rho):
        rho = 0.0

    return {
        "mean_absolute_error": round(mae, 4),
        "quadratic_weighted_kappa": round(qwk, 4),
        "spearman_rank_correlation": round(float(rho), 4)
    }


def calculate_classification_metrics(y_true: np.ndarray, y_pred: np.ndarray) -> Dict[str, float]:
    """Calculates balanced classification metrics for quality gating."""
    bal_acc = float(balanced_accuracy_score(y_true, y_pred))
    macro_f1 = float(f1_score(y_true, y_pred, average="macro", zero_division=0))

    return {
        "balanced_accuracy": round(bal_acc, 4),
        "macro_f1": round(macro_f1, 4)
    }
