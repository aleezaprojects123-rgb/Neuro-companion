"""
evaluate_model.py - Final held-out test set evaluation for NeuroCompanion Phase 1.

Evaluates the winning pipeline ONCE on the untouched test set (N=47).
Computes:
  - Accuracy
  - Macro-averaged F1
  - Balanced Accuracy
  - Class-specific precision, recall, F1
  - Full confusion matrix

Persists:
  - Backend/reports/metrics.json
  - Backend/reports/confusion_matrix.png

DISCLAIMER: Educational machine-learning prototype. Not a clinical diagnosis system.
"""

from __future__ import annotations

import json
import pickle
import sys
from pathlib import Path

import joblib
import matplotlib
matplotlib.use("Agg")  # Non-interactive backend
import matplotlib.pyplot as plt
import numpy as np
from sklearn.metrics import (
    ConfusionMatrixDisplay,
    accuracy_score,
    balanced_accuracy_score,
    classification_report,
    confusion_matrix,
    f1_score,
    precision_score,
    recall_score,
)

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from model.config import (
    CONFUSION_MATRIX_PATH,
    METRICS_PATH,
    MODEL_PATH,
    MODEL_VERSION,
    PROCESSED_DATA_PATH,
    REPORTS_DIR,
    TARGET_MAPPING,
)


# ---------------------------------------------------------------------------
# Public API
# ---------------------------------------------------------------------------

def run_evaluation() -> dict:
    """
    Load the winning pipeline and evaluate once on the untouched held-out test fold.
    Persist metrics JSON and confusion matrix PNG.
    """
    _print_disclaimer()

    # 1. Load artefacts
    pipeline = _load_pipeline()
    processed = _load_processed_data()

    X_test = processed["X_test"][processed["features_exp_a"]]
    y_test = processed["y_test"]

    # 2. Predict on untouched test set
    y_pred = pipeline.predict(X_test)

    # 3. Compute Metrics
    metrics = _compute_metrics(y_test, y_pred)

    # 4. Print Detailed Evaluation Report
    _print_report(y_test, y_pred, metrics)

    # 5. Save Report Artefacts
    REPORTS_DIR.mkdir(parents=True, exist_ok=True)
    _save_metrics_json(metrics)
    _save_confusion_matrix_plot(y_test, y_pred)

    print("\n[SUCCESS] Final evaluation complete.")
    print(f"          Metrics JSON      -> {METRICS_PATH}")
    print(f"          Confusion Matrix  -> {CONFUSION_MATRIX_PATH}")

    return metrics


# ---------------------------------------------------------------------------
# Private helpers
# ---------------------------------------------------------------------------

def _print_disclaimer() -> None:
    sep = "!" * 70
    print("\n" + sep)
    print("  EDUCATIONAL / RESEARCH PROTOTYPE EVALUATION ONLY")
    print("  This model does NOT perform medical or clinical diagnosis.")
    print("  Trained on historical research data (OASIS-1, N=235).")
    print(sep + "\n")


def _load_pipeline():
    if not MODEL_PATH.exists():
        raise FileNotFoundError(
            f"[ERROR] Trained pipeline not found at {MODEL_PATH}. "
            "Run 'python model/train_model.py' first."
        )
    return joblib.load(MODEL_PATH)


def _load_processed_data() -> dict:
    if not PROCESSED_DATA_PATH.exists():
        raise FileNotFoundError(
            f"[ERROR] Processed data not found at {PROCESSED_DATA_PATH}. "
            "Run 'python model/preprocess.py' first."
        )
    with open(PROCESSED_DATA_PATH, "rb") as f:
        return pickle.load(f)


def _compute_metrics(y_true, y_pred) -> dict:
    classes = [0, 1, 2]
    class_counts = {int(c): int(np.sum(y_true == c)) for c in classes}

    prec_per_class = precision_score(y_true, y_pred, labels=classes, average=None, zero_division=0)
    rec_per_class = recall_score(y_true, y_pred, labels=classes, average=None, zero_division=0)
    f1_per_class = f1_score(y_true, y_pred, labels=classes, average=None, zero_division=0)

    class_metrics = {}
    for idx, c in enumerate(classes):
        class_metrics[f"class_{c}"] = {
            "label": TARGET_MAPPING[c],
            "test_sample_count": class_counts[c],
            "precision": round(float(prec_per_class[idx]), 4),
            "recall": round(float(rec_per_class[idx]), 4),
            "f1_score": round(float(f1_per_class[idx]), 4),
        }

    cm = confusion_matrix(y_true, y_pred, labels=classes).tolist()

    return {
        "model_version": MODEL_VERSION,
        "evaluation_dataset": "OASIS-1 Held-out Test Set (20% Stratified)",
        "total_test_samples": int(len(y_true)),
        "class_test_distribution": class_counts,
        "overall_accuracy": round(float(accuracy_score(y_true, y_pred)), 4),
        "balanced_accuracy": round(float(balanced_accuracy_score(y_true, y_pred)), 4),
        "macro_f1": round(float(f1_score(y_true, y_pred, average="macro", zero_division=0)), 4),
        "class_specific_metrics": class_metrics,
        "confusion_matrix": cm,
        "class_2_limitation_warning": (
            "Class 2 contains only 6 test instances (30 total in dataset). "
            "Metrics for Class 2 carry high sample variance."
        ),
        "disclaimer": (
            "Educational machine-learning demonstration only. "
            "Not a clinical diagnostic tool. Not validated for medical decision-making."
        ),
    }


def _print_report(y_true, y_pred, metrics: dict) -> None:
    sep = "=" * 70
    print(sep)
    print("  FINAL HELD-OUT TEST EVALUATION (N = 47)")
    print(sep)
    print(f"  Overall Accuracy   : {metrics['overall_accuracy'] * 100:.2f}%")
    print(f"  Balanced Accuracy  : {metrics['balanced_accuracy'] * 100:.2f}%")
    print(f"  Macro-averaged F1  : {metrics['macro_f1']:.4f}")
    print("\n  Class Distribution in Test Set:")
    for c, count in metrics["class_test_distribution"].items():
        print(f"    Class {c} ({TARGET_MAPPING[c]}): {count} samples")

    print("\n  Classification Report:")
    target_names = [f"Class {c} ({TARGET_MAPPING[c]})" for c in [0, 1, 2]]
    print(classification_report(y_true, y_pred, target_names=target_names, zero_division=0))

    print("  Confusion Matrix (Rows = Actual, Columns = Predicted):")
    cm = np.array(metrics["confusion_matrix"])
    print(f"             Pred 0  Pred 1  Pred 2")
    for i, row in enumerate(cm):
        print(f"    Actual {i}:   {row[0]:>5}   {row[1]:>5}   {row[2]:>5}")
    print(sep)


def _save_metrics_json(metrics: dict) -> None:
    with open(METRICS_PATH, "w", encoding="utf-8") as f:
        json.dump(metrics, f, indent=2)


def _save_confusion_matrix_plot(y_true, y_pred) -> None:
    classes = [0, 1, 2]
    cm = confusion_matrix(y_true, y_pred, labels=classes)
    display_labels = ["Class 0\n(CDR 0)", "Class 1\n(CDR 0.5)", "Class 2\n(CDR 1+)"]

    fig, ax = plt.subplots(figsize=(6, 5))
    disp = ConfusionMatrixDisplay(confusion_matrix=cm, display_labels=display_labels)
    disp.plot(ax=ax, cmap="Blues", colorbar=True)

    ax.set_title(
        "NeuroCompanion Practical Model (Exp A)\n"
        "Held-Out Test Set (N=47) - OASIS-1\n"
        "(Educational Prototype Only)",
        fontsize=10,
        pad=10,
    )
    plt.tight_layout()
    plt.savefig(CONFUSION_MATRIX_PATH, dpi=150, bbox_inches="tight")
    plt.close(fig)


# ---------------------------------------------------------------------------
# CLI entry-point
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    run_evaluation()
