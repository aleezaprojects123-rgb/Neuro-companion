"""
Unit and Integration Tests for NeuroCompanion Phase 4.3 Training Framework.
Verifies dataset validation, participant-level splitting, config integrity, and artifact management.
"""

import os
import sys
import tempfile
import unittest
import numpy as np
import pandas as pd
from sklearn.linear_model import LogisticRegression

# Ensure Backend/ is in sys.path
CURRENT_DIR = os.path.dirname(os.path.abspath(__file__))
BACKEND_ROOT = os.path.abspath(os.path.join(CURRENT_DIR, ".."))
if BACKEND_ROOT not in sys.path:
    sys.path.insert(0, BACKEND_ROOT)

from ml.config import list_available_configs, load_model_config
from ml.training.dataset_validator import DatasetValidator
from ml.training.trainer import TrainingEngine
from ml.evaluation.splitting import GroupedDataSplitter
from ml.evaluation.metrics import (
    calculate_ordinal_metrics,
    calculate_classification_metrics,
    calculate_quadratic_weighted_kappa
)


class TestPhase43TrainingFramework(unittest.TestCase):
    """Test suite for Phase 4.3 reusable training framework components."""

    def setUp(self):
        self.backend_root = BACKEND_ROOT
        self.engine = TrainingEngine(self.backend_root)

    def test_list_and_load_all_configs(self):
        """Verifies that all 10 model configurations exist and load valid JSON schemas."""
        configs = list_available_configs()
        self.assertGreaterEqual(len(configs), 10)
        expected_ids = [
            "M1_PACING", "M2_LANGUAGE", "M3_TASK_PERF", "M4_INDEPENDENCE",
            "M5_INTERACTION", "M6_DIALOGUE", "M7_SEQUENCE", "M8_SPEECH",
            "M9_ENGAGEMENT", "M10_UNIFIED"
        ]
        found_ids = []
        for cfg_file in configs:
            cfg = load_model_config(cfg_file)
            self.assertIn("model_id", cfg)
            self.assertIn("version", cfg)
            found_ids.append(cfg["model_id"])

        for exp in expected_ids:
            self.assertIn(exp, found_ids, f"Expected config {exp} not found among loaded configs.")

    def test_dataset_validator_valid_dataset(self):
        """Verifies DatasetValidator on prepared AI4HOPE dialogue dataset."""
        csv_path = os.path.join(
            self.backend_root, "data", "processed", "phase4", "conversational_text", "ai4hope_dialogue_prepared.csv"
        )
        if os.path.exists(csv_path):
            res = DatasetValidator.validate_dataset(
                file_path=csv_path,
                feature_cols=["question_text", "answer_text"],
                target_col="target_empathy",
                group_col="evaluator_id",
                min_samples=50,
                min_groups=5
            )
            self.assertTrue(res.is_valid)
            self.assertEqual(len(res.errors), 0)
            self.assertEqual(res.summary["row_count"], 600)
            self.assertEqual(res.summary["unique_groups_count"], 60)

    def test_dataset_validator_missing_file_and_columns(self):
        """Verifies that DatasetValidator correctly catches missing files and missing columns."""
        res_missing_file = DatasetValidator.validate_dataset(
            file_path="non_existent_file.csv",
            feature_cols=["feature_1"]
        )
        self.assertFalse(res_missing_file.is_valid)
        self.assertIn("File not found", res_missing_file.errors[0])

        # Test missing feature column on existing file
        csv_path = os.path.join(
            self.backend_root, "data", "processed", "phase4", "activity_task", "robot_games_prepared.csv"
        )
        if os.path.exists(csv_path):
            res_bad_col = DatasetValidator.validate_dataset(
                file_path=csv_path,
                feature_cols=["non_existent_column_xyz"],
                group_col="participant_id"
            )
            self.assertFalse(res_bad_col.is_valid)
            self.assertTrue(any("Missing required feature columns" in e for e in res_bad_col.errors))

    def test_leak_free_participant_splitting(self):
        """Verifies that GroupedDataSplitter guarantees zero participant group leakage."""
        df_sample = pd.DataFrame({
            "participant_id": ["P1", "P1", "P2", "P2", "P3", "P3", "P4", "P4", "P5", "P5", "P6", "P6"],
            "feature_x": np.random.randn(12),
            "target_y": np.random.randint(0, 2, 12)
        })

        train_df, test_df = GroupedDataSplitter.train_test_group_split(
            df_sample, group_col="participant_id", test_size=0.33, random_state=42
        )

        train_groups = set(train_df["participant_id"].unique())
        test_groups = set(test_df["participant_id"].unique())
        self.assertEqual(len(train_groups.intersection(test_groups)), 0)

        # Test GroupKFold
        splits = list(GroupedDataSplitter.get_group_kfold_splits(df_sample, group_col="participant_id", n_splits=3))
        self.assertEqual(len(splits), 3)
        for tr_idx, te_idx in splits:
            tr_g = set(df_sample.iloc[tr_idx]["participant_id"].unique())
            te_g = set(df_sample.iloc[te_idx]["participant_id"].unique())
            self.assertEqual(len(tr_g.intersection(te_g)), 0)

    def test_artifact_saving_and_catalog_registration(self):
        """Verifies that TrainingEngine correctly serializes artifacts and updates catalog."""
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_engine = TrainingEngine(backend_root=temp_dir)
            dummy_model = LogisticRegression()
            dummy_X = np.array([[1.0, 2.0], [3.0, 4.0], [5.0, 6.0], [7.0, 8.0]])
            dummy_y = np.array([0, 0, 1, 1])
            dummy_model.fit(dummy_X, dummy_y)

            # Create dummy source data file
            src_path = os.path.join(temp_dir, "dummy_data.csv")
            with open(src_path, "w") as f:
                f.write("f1,f2,target\n1,2,0\n3,4,0\n5,6,1\n7,8,1\n")

            schema = {"feature_names": ["f1", "f2"], "target": "target"}
            metrics = {"balanced_accuracy": 1.0, "macro_f1": 1.0}

            artifacts = temp_engine.save_model_artifacts(
                model_id="TEST_MODEL",
                version="1.0.0",
                fitted_model=dummy_model,
                schema=schema,
                metrics=metrics,
                source_data_path=src_path
            )

            self.assertTrue(os.path.exists(artifacts["model_path"]))
            self.assertTrue(os.path.exists(artifacts["schema_path"]))
            self.assertTrue(os.path.exists(artifacts["metrics_path"]))
            self.assertTrue(os.path.exists(artifacts["provenance_path"]))

            # Verify registered catalog entry
            meta = temp_engine.catalog.get_model_metadata("TEST_MODEL", "v1.0.0")
            self.assertIsNotNone(meta)
            self.assertEqual(meta["status"], "active")
            self.assertEqual(meta["metrics"]["balanced_accuracy"], 1.0)

    def test_evaluation_metrics_computation(self):
        """Verifies calculation of ordinal and balanced classification metrics."""
        y_true = np.array([1, 2, 3, 4, 5, 1, 2, 3, 4, 5])
        y_pred = np.array([1.1, 1.9, 3.2, 3.8, 4.9, 1.0, 2.1, 2.9, 4.2, 4.8])

        ord_metrics = calculate_ordinal_metrics(y_true, y_pred)
        self.assertIn("mean_absolute_error", ord_metrics)
        self.assertIn("quadratic_weighted_kappa", ord_metrics)
        self.assertIn("spearman_rank_correlation", ord_metrics)
        self.assertGreater(ord_metrics["spearman_rank_correlation"], 0.90)

        clf_metrics = calculate_classification_metrics(np.array([0, 1, 0, 1]), np.array([0, 1, 0, 1]))
        self.assertEqual(clf_metrics["balanced_accuracy"], 1.0)
        self.assertEqual(clf_metrics["macro_f1"], 1.0)


if __name__ == "__main__":
    unittest.main()
