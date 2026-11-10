"""
train_model.py - Model training, experimental comparison, and pipeline serialization.

Experiments
-----------
- Experiment A: Features = ['Age', 'M/F', 'Educ', 'MMSE']
- Experiment B: Features = ['Age', 'M/F', 'Educ'] (measures MMSE contribution)

Algorithms Compared (within each experiment)
--------------------------------------------
1. DummyClassifier (baseline, strategy="most_frequent")
2. LogisticRegression (multinomial, class_weight="balanced", random_state=42)
3. RandomForestClassifier (max_depth=3, min_samples_leaf=3, class_weight="balanced", random_state=42)

Validation Protocol
-------------------
Repeated Stratified 5-Fold Cross-Validation (5 splits x 10 repeats = 50 folds)
performed ONLY on the training fold (N=188).
Metrics: Accuracy, Macro-F1, Balanced Accuracy.

DISCLAIMER: Educational machine-learning prototype. Not a medical diagnostic tool.
"""

from __future__ import annotations

import json
import pickle
import sys
from pathlib import Path
from typing import Any

import joblib
import numpy as np
from sklearn.base import clone
from sklearn.dummy import DummyClassifier
from sklearn.ensemble import RandomForestClassifier
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import accuracy_score, balanced_accuracy_score, f1_score
from sklearn.model_selection import RepeatedStratifiedKFold
from sklearn.pipeline import Pipeline

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from model.config import (
    CV_REPEATS,
    CV_SPLITS,
    MODEL_COMPARISON_PATH,
    MODEL_PATH,
    PROCESSED_DATA_PATH,
    RANDOM_STATE,
    SAVED_MODEL_DIR,
)


# ---------------------------------------------------------------------------
# Public API
# ---------------------------------------------------------------------------

def run_training() -> dict[str, Any]:
    """
    Execute Repeated Stratified Cross-Validation on training data for both
    Experiment A and Experiment B across Dummy, Logistic Regression, and
    Random Forest. Persist comparison report and save winning pipeline.
    """
    processed = _load_processed_data()
    X_train = processed["X_train"]
    y_train = processed["y_train"]

    preprocessor_a = processed["preprocessor_a"]
    preprocessor_b = processed["preprocessor_b"]
    features_a = processed["features_exp_a"]
    features_b = processed["features_exp_b"]

    print("=" * 70)
    print("  NEUROCOMPANION PHASE 1: EXPERIMENTAL MODEL TRAINING & COMPARISON")
    print("=" * 70)
    print(f"  Training cohort size : {len(X_train)} samples")
    print(f"  Target classes       : 3 (Class 0: {sum(y_train == 0)}, Class 1: {sum(y_train == 1)}, Class 2: {sum(y_train == 2)})")
    print(f"  CV Protocol          : Repeated Stratified {CV_SPLITS}-Fold ({CV_REPEATS} repeats = {CV_SPLITS * CV_REPEATS} total folds)")

    # 1. Evaluate Experiment A (with MMSE)
    print("\n" + "-" * 70)
    print("  EXPERIMENT A: Demographic + Cognitive Screener (Age, M/F, Educ, MMSE)")
    print("-" * 70)
    models_exp_a = _build_candidate_models(preprocessor_a)
    results_exp_a = _cross_validate_experiment(
        models=models_exp_a,
        X=X_train[features_a],
        y=y_train,
        feature_names=features_a,
    )

    # 2. Evaluate Experiment B (without MMSE)
    print("\n" + "-" * 70)
    print("  EXPERIMENT B: Demographics Only (Age, M/F, Educ) - Ablation of MMSE")
    print("-" * 70)
    models_exp_b = _build_candidate_models(preprocessor_b)
    results_exp_b = _cross_validate_experiment(
        models=models_exp_b,
        X=X_train[features_b],
        y=y_train,
        feature_names=features_b,
    )

    # 3. Model Comparison Summary
    comparison_summary = {
        "metadata": {
            "training_samples": len(X_train),
            "cv_splits": CV_SPLITS,
            "cv_repeats": CV_REPEATS,
            "random_state": RANDOM_STATE,
        },
        "experiment_a_with_mmse": results_exp_a,
        "experiment_b_without_mmse": results_exp_b,
    }

    _print_comparison_table(results_exp_a, results_exp_b)
    _save_comparison_results(comparison_summary)

    # 4. Select and Refit Winning Pipeline from Experiment A
    best_model_name = _select_best_model(results_exp_a)
    best_pipeline = models_exp_a[best_model_name]

    print(f"\n[INFO] Selecting '{best_model_name}' (Experiment A) as winning model.")
    print(f"[INFO] Fitting winning pipeline on full training set ({len(X_train)} samples)...")
    best_pipeline.fit(X_train[features_a], y_train)

    _save_winning_pipeline(best_pipeline)

    return comparison_summary


# ---------------------------------------------------------------------------
# Private helpers
# ---------------------------------------------------------------------------

def _load_processed_data() -> dict[str, Any]:
    """Load train/test splits and preprocessor templates."""
    if not PROCESSED_DATA_PATH.exists():
        raise FileNotFoundError(
            f"[ERROR] Processed data not found at {PROCESSED_DATA_PATH}. "
            "Run 'python model/preprocess.py' first."
        )
    with open(PROCESSED_DATA_PATH, "rb") as f:
        return pickle.load(f)


def _build_candidate_models(preprocessor) -> dict[str, Pipeline]:
    """
    Construct sklearn Pipelines combining preprocessor with candidate estimators.
    Uses conservative hyperparameters to guard against overfitting on N=188.
    """
    return {
        "DummyClassifier": Pipeline([
            ("preprocessor", clone(preprocessor)),
            ("classifier", DummyClassifier(strategy="most_frequent", random_state=RANDOM_STATE)),
        ]),
        "LogisticRegression": Pipeline([
            ("preprocessor", clone(preprocessor)),
            ("classifier", LogisticRegression(
                class_weight="balanced",
                max_iter=1000,
                random_state=RANDOM_STATE,
            )),
        ]),
        "RandomForestClassifier": Pipeline([
            ("preprocessor", clone(preprocessor)),
            ("classifier", RandomForestClassifier(
                n_estimators=100,
                max_depth=3,            # Strictly constrained depth to prevent memorization
                min_samples_leaf=3,      # Regularization for small classes
                class_weight="balanced", # Compensates for Class 2 (N=24 in train)
                random_state=RANDOM_STATE,
            )),
        ]),
    }


def _cross_validate_experiment(
    models: dict[str, Pipeline],
    X,
    y,
    feature_names: list[str],
) -> dict[str, Any]:
    """
    Run Repeated Stratified 5-Fold Cross-Validation across candidate models.
    """
    rskf = RepeatedStratifiedKFold(
        n_splits=CV_SPLITS,
        n_repeats=CV_REPEATS,
        random_state=RANDOM_STATE,
    )

    results: dict[str, Any] = {}

    for name, pipeline in models.items():
        acc_scores = []
        macro_f1_scores = []
        bal_acc_scores = []

        for train_idx, val_idx in rskf.split(X, y):
            X_fold_train, X_fold_val = X.iloc[train_idx], X.iloc[val_idx]
            y_fold_train, y_fold_val = y.iloc[train_idx], y.iloc[val_idx]

            fold_pipe = clone(pipeline)
            fold_pipe.fit(X_fold_train, y_fold_train)
            preds = fold_pipe.predict(X_fold_val)

            acc_scores.append(accuracy_score(y_fold_val, preds))
            macro_f1_scores.append(f1_score(y_fold_val, preds, average="macro", zero_division=0))
            bal_acc_scores.append(balanced_accuracy_score(y_fold_val, preds))

        mean_acc = float(np.mean(acc_scores))
        std_acc = float(np.std(acc_scores))
        mean_f1 = float(np.mean(macro_f1_scores))
        std_f1 = float(np.std(macro_f1_scores))
        mean_bal = float(np.mean(bal_acc_scores))
        std_bal = float(np.std(bal_acc_scores))

        results[name] = {
            "accuracy_mean": round(mean_acc, 4),
            "accuracy_std": round(std_acc, 4),
            "macro_f1_mean": round(mean_f1, 4),
            "macro_f1_std": round(std_f1, 4),
            "balanced_accuracy_mean": round(mean_bal, 4),
            "balanced_accuracy_std": round(std_bal, 4),
        }

        print(
            f"  {name:<24} | "
            f"Acc: {mean_acc * 100:.2f}% (+/- {std_acc * 100:.2f}%) | "
            f"Macro-F1: {mean_f1:.4f} (+/- {std_f1:.4f}) | "
            f"Bal-Acc: {mean_bal * 100:.2f}%"
        )

    return results


def _print_comparison_table(results_a: dict[str, Any], results_b: dict[str, Any]) -> None:
    """Print a clean comparative view highlighting the contribution of MMSE."""
    sep = "=" * 70
    print("\n" + sep)
    print("  MODEL COMPARISON SUMMARY: Exp A (with MMSE) vs Exp B (without MMSE)")
    print(sep)
    print(f"  {'Model':<22} | {'Exp A Macro-F1':<16} | {'Exp B Macro-F1':<16} | {'Delta (MMSE)':<12}")
    print("-" * 70)

    for model_name in results_a.keys():
        f1_a = results_a[model_name]["macro_f1_mean"]
        f1_b = results_b[model_name]["macro_f1_mean"]
        delta = f1_a - f1_b
        delta_str = f"+{delta:.4f}" if delta > 0 else f"{delta:.4f}"
        print(f"  {model_name:<22} | {f1_a:<16.4f} | {f1_b:<16.4f} | {delta_str:<12}")

    print(sep)


def _select_best_model(results_exp_a: dict[str, Any]) -> str:
    """Select model with the highest CV Macro-F1 score."""
    best_model = max(results_exp_a, key=lambda m: results_exp_a[m]["macro_f1_mean"])
    return best_model


def _save_comparison_results(summary: dict[str, Any]) -> None:
    """Persist comparison metrics to Backend/reports/model_comparison.json."""
    MODEL_COMPARISON_PATH.parent.mkdir(parents=True, exist_ok=True)
    with open(MODEL_COMPARISON_PATH, "w", encoding="utf-8") as f:
        json.dump(summary, f, indent=2)
    print(f"\n[INFO] Model comparison report saved -> {MODEL_COMPARISON_PATH}")


def _save_winning_pipeline(pipeline: Pipeline) -> None:
    """Save fitted pipeline to Backend/saved_model/pipeline.joblib."""
    SAVED_MODEL_DIR.mkdir(parents=True, exist_ok=True)
    joblib.dump(pipeline, MODEL_PATH)
    print(f"[INFO] Winning pipeline persisted    -> {MODEL_PATH}")


# ---------------------------------------------------------------------------
# CLI entry-point
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    run_training()
