"""
NeuroCompanion Phase 2 - Exploratory Binary Task-Quality Classification Training Pipeline
Evaluates Experiment A (Primary Features) and Experiment B (Primary Features + Proctor Duration)
using 5-Fold StratifiedGroupKFold grouped strictly by Participant_ID.
"""

import os
import json
import joblib
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt

from sklearn.base import clone
from sklearn.model_selection import StratifiedGroupKFold
from sklearn.preprocessing import StandardScaler
from sklearn.pipeline import Pipeline
from sklearn.dummy import DummyClassifier
from sklearn.linear_model import LogisticRegression
from sklearn.ensemble import RandomForestClassifier
from sklearn.metrics import (
    accuracy_score,
    balanced_accuracy_score,
    f1_score,
    precision_score,
    recall_score,
    roc_auc_score,
    confusion_matrix
)

# Paths
BASE_DIR = r"C:\Users\Admin\OneDrive\Desktop\NeuroCompanion\Backend"
DATA_PATH = os.path.join(BASE_DIR, "data", "raw", "casas", "extracted", "task_audit_data.csv")
SAVED_MODEL_DIR = os.path.join(BASE_DIR, "saved_model_phase2")
REPORTS_DIR = os.path.join(BASE_DIR, "reports_phase2")

os.makedirs(SAVED_MODEL_DIR, exist_ok=True)
os.makedirs(REPORTS_DIR, exist_ok=True)


def verify_dataset(df):
    n_total = len(df)
    n_pids = df["pid"].nunique()
    c0 = (df["score"] == 1).sum()
    c1 = (df["score"] != 1).sum()

    print(f"=== Dataset Verification ===")
    print(f"Total task observations: {n_total} (Expected: 2,074)")
    print(f"Distinct participants:   {n_pids} (Expected: 327)")
    print(f"Class 0 (Score 1):       {c0} ({c0/n_total*100:.2f}%) (Expected: 1,447)")
    print(f"Class 1 (Scores 2,3,5,6):{c1} ({c1/n_total*100:.2f}%) (Expected: 627)")

    assert n_total == 2074, f"Mismatch in total observations: {n_total}"
    assert n_pids == 327, f"Mismatch in participant count: {n_pids}"
    assert c0 == 1447, f"Mismatch in Class 0 count: {c0}"
    assert c1 == 627, f"Mismatch in Class 1 count: {c1}"
    print("Verification passed successfully!\n")


def evaluate_cv(models_dict, X, y, groups, cv, feature_names):
    results = {}
    oof_preds = {name: np.zeros(len(y), dtype=int) for name in models_dict}
    oof_probs = {name: np.zeros(len(y), dtype=float) for name in models_dict}

    for name, model_cls_or_pipe in models_dict.items():
        fold_metrics = []

        for fold, (train_idx, val_idx) in enumerate(cv.split(X, y, groups)):
            # Check participant leakage
            train_pids = set(groups.iloc[train_idx])
            val_pids = set(groups.iloc[val_idx])
            leakage = train_pids.intersection(val_pids)
            assert len(leakage) == 0, f"Participant leakage detected in fold {fold}: {leakage}"

            X_train, X_val = X.iloc[train_idx], X.iloc[val_idx]
            y_train, y_val = y.iloc[train_idx], y.iloc[val_idx]

            # Clone fresh pipeline/model
            model = clone(model_cls_or_pipe)

            model.fit(X_train, y_train)

            y_pred = model.predict(X_val)
            oof_preds[name][val_idx] = y_pred

            # Probabilities if available
            if hasattr(model, "predict_proba"):
                y_prob = model.predict_proba(X_val)[:, 1]
                oof_probs[name][val_idx] = y_prob
                roc = roc_auc_score(y_val, y_prob)
            else:
                roc = 0.5

            acc = accuracy_score(y_val, y_pred)
            bal_acc = balanced_accuracy_score(y_val, y_pred)
            macro_f1 = f1_score(y_val, y_pred, average="macro", zero_division=0)
            f1_c0 = f1_score(y_val, y_pred, pos_label=0, zero_division=0)
            f1_c1 = f1_score(y_val, y_pred, pos_label=1, zero_division=0)
            prec_c0 = precision_score(y_val, y_pred, pos_label=0, zero_division=0)
            prec_c1 = precision_score(y_val, y_pred, pos_label=1, zero_division=0)
            rec_c0 = recall_score(y_val, y_pred, pos_label=0, zero_division=0)
            rec_c1 = recall_score(y_val, y_pred, pos_label=1, zero_division=0)

            fold_metrics.append({
                "fold": fold + 1,
                "accuracy": acc,
                "balanced_accuracy": bal_acc,
                "macro_f1": macro_f1,
                "precision_class_0": prec_c0,
                "recall_class_0": rec_c0,
                "f1_class_0": f1_c0,
                "precision_class_1": prec_c1,
                "recall_class_1": rec_c1,
                "f1_class_1": f1_c1,
                "roc_auc": roc
            })

        # Calculate mean & std across folds
        df_fold = pd.DataFrame(fold_metrics)
        cm = confusion_matrix(y, oof_preds[name])

        results[name] = {
            "folds": fold_metrics,
            "mean": {k: float(df_fold[k].mean()) for k in df_fold.columns if k != "fold"},
            "std": {k: float(df_fold[k].std()) for k in df_fold.columns if k != "fold"},
            "oof_confusion_matrix": cm.tolist()
        }

    return results, oof_preds, oof_probs


def plot_confusion_matrices(expA_cm, expB_cm, expA_best_name, expB_best_name, save_path):
    fig, axes = plt.subplots(1, 2, figsize=(12, 5))

    classes = ["Class 0\n(Score 1)", "Class 1\n(Scores 2,3,5,6)"]

    for ax, cm, title in zip(
        axes,
        [expA_cm, expB_cm],
        [f"Experiment A (Primary Features)\nBest: {expA_best_name}", f"Experiment B (Primary + Duration)\nBest: {expB_best_name}"]
    ):
        im = ax.imshow(cm, interpolation='nearest', cmap=plt.cm.Blues)
        ax.set_title(title, fontsize=12, pad=10)
        tick_marks = np.arange(len(classes))
        ax.set_xticks(tick_marks)
        ax.set_xticklabels(classes, fontsize=10)
        ax.set_yticks(tick_marks)
        ax.set_yticklabels(classes, fontsize=10)
        ax.set_ylabel("True Target Category", fontsize=11)
        ax.set_xlabel("Predicted Target Category", fontsize=11)

        # Annotate numbers
        thresh = np.array(cm).max() / 2.0
        for i in range(len(cm)):
            for j in range(len(cm[i])):
                val = cm[i][j]
                color = "white" if val > thresh else "black"
                ax.text(j, i, f"{val:,}", ha="center", va="center", color=color, fontsize=13, fontweight="bold")

    plt.tight_layout()
    plt.savefig(save_path, dpi=300)
    plt.close()
    print(f"Saved confusion matrix comparison plot to: {save_path}")


def main():
    print("Loading data from:", DATA_PATH)
    df = pd.read_csv(DATA_PATH)

    verify_dataset(df)

    # Define binary target: Class 0 = Score 1, Class 1 = Score in {2, 3, 5, 6}
    y = (df["score"] != 1).astype(int)
    groups = df["pid"]

    # 5-fold StratifiedGroupKFold
    cv = StratifiedGroupKFold(n_splits=5, shuffle=True, random_state=42)

    # Define model pipelines
    def get_models():
        return {
            "Dummy (Most Frequent)": DummyClassifier(strategy="most_frequent"),
            "Dummy (Stratified)": DummyClassifier(strategy="stratified", random_state=42),
            "Logistic Regression (Unweighted)": Pipeline([
                ("scaler", StandardScaler()),
                ("clf", LogisticRegression(random_state=42, max_iter=1000))
            ]),
            "Logistic Regression (Balanced)": Pipeline([
                ("scaler", StandardScaler()),
                ("clf", LogisticRegression(class_weight="balanced", random_state=42, max_iter=1000))
            ]),
            "Random Forest (Unweighted)": Pipeline([
                ("clf", RandomForestClassifier(n_estimators=100, max_depth=6, random_state=42))
            ]),
            "Random Forest (Balanced)": Pipeline([
                ("clf", RandomForestClassifier(n_estimators=100, max_depth=6, class_weight="balanced", random_state=42))
            ])
        }

    # ==========================================
    # Experiment A: Primary Features
    # ==========================================
    feat_expA = ["pause_cnt_5_int", "max_pause_int"]
    X_expA = df[feat_expA]
    print("Running Experiment A: Primary Features:", feat_expA)
    results_expA, oof_preds_A, oof_probs_A = evaluate_cv(get_models(), X_expA, y, groups, cv, feat_expA)

    # ==========================================
    # Experiment B: Primary + Duration
    # ==========================================
    feat_expB = ["pause_cnt_5_int", "max_pause_int", "duration"]
    X_expB = df[feat_expB]
    print("Running Experiment B: Primary + Duration:", feat_expB)
    results_expB, oof_preds_B, oof_probs_B = evaluate_cv(get_models(), X_expB, y, groups, cv, feat_expB)

    # Print summary tables
    def print_summary(res, title):
        print(f"\n========================================================")
        print(f" Summary Results: {title}")
        print(f"========================================================")
        print(f"{'Model':<35} | {'Macro-F1':<12} | {'Bal. Acc':<12} | {'Accuracy':<10} | {'Rec C1':<8} | {'ROC-AUC':<8}")
        print("-" * 95)
        for name, data in res.items():
            m = data["mean"]
            s = data["std"]
            print(f"{name:<35} | {m['macro_f1']:.4f}±{s['macro_f1']:.3f} | {m['balanced_accuracy']:.4f}±{s['balanced_accuracy']:.3f} | {m['accuracy']:.4f}     | {m['recall_class_1']:.4f}   | {m['roc_auc']:.4f}")

    print_summary(results_expA, "Experiment A (Primary Features)")
    print_summary(results_expB, "Experiment B (Primary Features + Duration)")

    # Identify best models based on Macro-F1
    best_name_A = max(results_expA.keys(), key=lambda k: results_expA[k]["mean"]["macro_f1"])
    best_name_B = max(results_expB.keys(), key=lambda k: results_expB[k]["mean"]["macro_f1"])

    print(f"\nBest model for Experiment A: {best_name_A} (Macro-F1: {results_expA[best_name_A]['mean']['macro_f1']:.4f})")
    print(f"Best model for Experiment B: {best_name_B} (Macro-F1: {results_expB[best_name_B]['mean']['macro_f1']:.4f})")

    # Plot and save confusion matrices
    cm_plot_path = os.path.join(REPORTS_DIR, "confusion_matrix_comparison.png")
    plot_confusion_matrices(
        results_expA[best_name_A]["oof_confusion_matrix"],
        results_expB[best_name_B]["oof_confusion_matrix"],
        best_name_A,
        best_name_B,
        cm_plot_path
    )

    # Train final full models for deployment artifacts
    print("\nTraining final full-dataset models for artifact persistence...")
    final_pipeline_A = get_models()[best_name_A]
    final_pipeline_A.fit(X_expA, y)
    joblib.dump(final_pipeline_A, os.path.join(SAVED_MODEL_DIR, "pipeline_expA.joblib"))

    final_pipeline_B = get_models()[best_name_B]
    final_pipeline_B.fit(X_expB, y)
    joblib.dump(final_pipeline_B, os.path.join(SAVED_MODEL_DIR, "pipeline_expB.joblib"))

    # Select overall champion (Experiment A is preferred if performance is close, due to lack of proctor duration dependency)
    # But let's check which has higher Macro-F1
    if results_expB[best_name_B]["mean"]["macro_f1"] > results_expA[best_name_A]["mean"]["macro_f1"]:
        champion_pipeline = final_pipeline_B
        champion_name = best_name_B
        champion_exp = "Experiment B"
        champion_features = feat_expB
    else:
        champion_pipeline = final_pipeline_A
        champion_name = best_name_A
        champion_exp = "Experiment A"
        champion_features = feat_expA

    joblib.dump(champion_pipeline, os.path.join(SAVED_MODEL_DIR, "pipeline.joblib"))
    print(f"Saved champion pipeline ({champion_exp}: {champion_name}) to: {os.path.join(SAVED_MODEL_DIR, 'pipeline.joblib')}")

    # Feature Schema
    schema = {
        "metadata": {
            "model_phase": "Phase 2",
            "model_type": "Exploratory Binary Task-Quality Classifier",
            "task_type": "Binary Classification",
            "dataset": "CASAS Cognitive Assessment Dataset (Zenodo 15717153)",
            "n_observations": 2074,
            "n_participants": 327,
            "validation_scheme": "StratifiedGroupKFold(5 splits, grouped by Participant_ID)"
        },
        "experiments": {
            "Experiment_A": {
                "description": "Primary Features (Strictly Internal Sensor Telemetry)",
                "features": {
                    "pause_cnt_5_int": {
                        "type": "integer",
                        "description": "Count of internal inter-sensor inactivity intervals >= 5.0 seconds during task window",
                        "unit": "count",
                        "valid_range": [0, 100]
                    },
                    "max_pause_int": {
                        "type": "float",
                        "description": "Maximum internal inter-sensor inactivity interval during task window",
                        "unit": "seconds",
                        "valid_range": [0.0, 300.0]
                    }
                }
            },
            "Experiment_B": {
                "description": "Exploratory Feature Addition (Primary Features + Proctor Duration)",
                "features": {
                    "pause_cnt_5_int": {
                        "type": "integer",
                        "description": "Count of internal inter-sensor inactivity intervals >= 5.0 seconds during task window",
                        "unit": "count",
                        "valid_range": [0, 100]
                    },
                    "max_pause_int": {
                        "type": "float",
                        "description": "Maximum internal inter-sensor inactivity interval during task window",
                        "unit": "seconds",
                        "valid_range": [0.0, 300.0]
                    },
                    "duration": {
                        "type": "float",
                        "description": "Proctor-bounded active task duration (T_end - T_start)",
                        "unit": "seconds",
                        "valid_range": [0.0, 1500.0],
                        "limitation_note": "Directly bounded by human proctor start/end tags"
                    }
                }
            }
        },
        "target": {
            "name": "task_quality_category",
            "classes": {
                "0": "Class 0: High Task Quality (CASAS Score 1; unassisted/standard execution)",
                "1": "Class 1: Degraded Task Quality (CASAS Scores 2, 3, 5, 6; verbal prompt, incomplete, breakdown, or abort)"
            },
            "class_distribution": {
                "0": 1447,
                "1": 627
            },
            "formal_classification": "Researcher-defined binary task-quality classification"
        },
        "champion_model": {
            "selected_experiment": champion_exp,
            "model_algorithm": champion_name,
            "feature_list": champion_features
        }
    }

    with open(os.path.join(SAVED_MODEL_DIR, "schema.json"), "w") as f:
        json.dump(schema, f, indent=2)

    # Save Cross-Validation Detailed Results
    all_cv_results = {
        "metadata": schema["metadata"],
        "target": schema["target"],
        "experiment_A_primary_features": results_expA,
        "experiment_B_primary_plus_duration": results_expB
    }
    with open(os.path.join(REPORTS_DIR, "cv_results.json"), "w") as f:
        json.dump(all_cv_results, f, indent=2)

    # Save Model Comparison Report
    comparison_summary = {
        "metadata": schema["metadata"],
        "comparison_table": []
    }
    for exp_label, res_dict in [("Exp A (Primary)", results_expA), ("Exp B (Primary + Duration)", results_expB)]:
        for m_name, m_data in res_dict.items():
            comparison_summary["comparison_table"].append({
                "experiment": exp_label,
                "model": m_name,
                "macro_f1": f"{m_data['mean']['macro_f1']:.4f} ± {m_data['std']['macro_f1']:.4f}",
                "balanced_accuracy": f"{m_data['mean']['balanced_accuracy']:.4f} ± {m_data['std']['balanced_accuracy']:.4f}",
                "accuracy": f"{m_data['mean']['accuracy']:.4f} ± {m_data['std']['accuracy']:.4f}",
                "recall_class_0": f"{m_data['mean']['recall_class_0']:.4f} ± {m_data['std']['recall_class_0']:.4f}",
                "recall_class_1": f"{m_data['mean']['recall_class_1']:.4f} ± {m_data['std']['recall_class_1']:.4f}",
                "roc_auc": f"{m_data['mean']['roc_auc']:.4f} ± {m_data['std']['roc_auc']:.4f}"
            })

    with open(os.path.join(REPORTS_DIR, "model_comparison.json"), "w") as f:
        json.dump(comparison_summary, f, indent=2)

    print("\nAll deliverables saved successfully:")
    print(" - Saved Model Dir:", SAVED_MODEL_DIR)
    print(" - Reports Dir:    ", REPORTS_DIR)


if __name__ == "__main__":
    main()
