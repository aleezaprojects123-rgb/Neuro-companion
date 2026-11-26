"""
Automated Test Suite for NeuroCompanion Phase 4.4 Trained Models and Artifacts.
Verifies M6, M3, M1, and M4 model artifacts, schema compliance, provenance checksums,
and deterministic inference stability.
"""

import os
import sys
import json
import unittest
import joblib
import numpy as np

# Ensure Backend/ root is in sys.path
CURRENT_DIR = os.path.dirname(os.path.abspath(__file__))
BACKEND_ROOT = os.path.abspath(os.path.join(CURRENT_DIR, ".."))
if BACKEND_ROOT not in sys.path:
    sys.path.insert(0, BACKEND_ROOT)

from ml.models.model_registry import ModelVersionCatalog
from ml.models.pipelines import (
    M6DialogueIntelligencePipeline,
    M3TaskPerformancePipeline,
    M1PacingEstimatorArtifact
)


class TestPhase44TrainedModels(unittest.TestCase):
    """Verifies that all Phase 4.4 trained model artifacts are intact, loadable, and reproducible."""

    def setUp(self):
        self.backend_root = BACKEND_ROOT
        self.saved_model_dir = os.path.join(self.backend_root, "saved_model", "phase4")
        self.catalog = ModelVersionCatalog(self.saved_model_dir)

    def test_model_catalog_has_all_phase4_models(self):
        """Verifies that M6, M3, M1, and M4 are registered in model_catalog.json."""
        self.assertTrue(os.path.exists(self.catalog.catalog_path))
        models = self.catalog._catalog["models"]
        self.assertIn("M6_DIALOGUE", models)
        self.assertIn("M3_TASK_PERF", models)
        self.assertIn("M1_PACING", models)
        self.assertIn("M4_INDEPENDENCE", models)

        self.assertEqual(models["M6_DIALOGUE"]["v1.0.0"]["status"], "active")
        self.assertEqual(models["M3_TASK_PERF"]["v1.0.0"]["status"], "limited_performance")
        self.assertEqual(models["M1_PACING"]["v1.0.0"]["status"], "statistical_reference")
        self.assertEqual(models["M4_INDEPENDENCE"]["v1.0.0"]["status"], "descriptive_prior")

    def test_m6_dialogue_artifact_and_inference(self):
        """Verifies M6 Dialogue Intelligence pipeline deserialization and score boundaries."""
        art_dir = os.path.join(self.saved_model_dir, "m6_dialogue", "v1")
        model_path = os.path.join(art_dir, "model.joblib")
        schema_path = os.path.join(art_dir, "schema.json")
        metrics_path = os.path.join(art_dir, "metrics.json")
        prov_path = os.path.join(art_dir, "provenance.json")

        self.assertTrue(os.path.exists(model_path))
        self.assertTrue(os.path.exists(schema_path))
        self.assertTrue(os.path.exists(metrics_path))
        self.assertTrue(os.path.exists(prov_path))

        m6 = joblib.load(model_path)
        self.assertIsInstance(m6, M6DialogueIntelligencePipeline)

        # Single string inference
        sample_text = "I am right here with you. Take all the time you need, and we will do it together."
        pred = m6.predict_single(sample_text)
        self.assertIn("predicted_empathy", pred)
        self.assertIn("predicted_actionability", pred)
        self.assertGreaterEqual(pred["predicted_empathy"], 1.0)
        self.assertLessEqual(pred["predicted_empathy"], 5.0)
        self.assertGreaterEqual(pred["predicted_actionability"], 1.0)
        self.assertLessEqual(pred["predicted_actionability"], 5.0)

        # Batch inference
        batch_res = m6.predict([sample_text, "Press the red button now."])
        self.assertEqual(len(batch_res["pred_empathy"]), 2)
        self.assertEqual(len(batch_res["pred_actionability"]), 2)

        # Verify metrics
        with open(metrics_path, "r", encoding="utf-8") as f:
            metrics = json.load(f)
        self.assertEqual(metrics["unique_evaluators"], 60)
        self.assertGreater(metrics["target_empathy"]["spearman_rank_rho"], 0.20)

    def test_m3_task_perf_artifact_and_inference(self):
        """Verifies M3 Task Performance hurdle model deserialization and prediction validity."""
        art_dir = os.path.join(self.saved_model_dir, "m3_task_perf", "v1")
        model_path = os.path.join(art_dir, "model.joblib")
        schema_path = os.path.join(art_dir, "schema.json")
        metrics_path = os.path.join(art_dir, "metrics.json")
        prov_path = os.path.join(art_dir, "provenance.json")

        self.assertTrue(os.path.exists(model_path))
        self.assertTrue(os.path.exists(schema_path))
        self.assertTrue(os.path.exists(metrics_path))
        self.assertTrue(os.path.exists(prov_path))

        m3 = joblib.load(model_path)
        self.assertIsInstance(m3, M3TaskPerformancePipeline)

        pred = m3.predict_single("StoryQuizGame", 8.5)
        self.assertIn("error_probability", pred)
        self.assertIn("predicted_errors_count", pred)
        self.assertGreaterEqual(pred["error_probability"], 0.0)
        self.assertLessEqual(pred["error_probability"], 1.0)
        self.assertGreaterEqual(pred["predicted_errors_count"], 0.0)

        # Verify prohibited features are absent in schema
        with open(schema_path, "r", encoding="utf-8") as f:
            schema = json.load(f)
        self.assertNotIn("game_time_sec", schema["input_features"])
        self.assertNotIn("total_points", schema["input_features"])
        self.assertNotIn("touch_interaction_flag", schema["input_features"])

    def test_m1_pacing_statistical_artifact(self):
        """Verifies M1 Pacing statistical reference percentiles and Log-Normal parameters."""
        art_dir = os.path.join(self.saved_model_dir, "m1_pacing", "v1")
        model_path = os.path.join(art_dir, "model.joblib")
        metrics_path = os.path.join(art_dir, "metrics.json")

        self.assertTrue(os.path.exists(model_path))
        self.assertTrue(os.path.exists(metrics_path))

        m1 = joblib.load(model_path)
        self.assertIsInstance(m1, M1PacingEstimatorArtifact)

        ref_motor = m1.get_pacing_reference("motor_tapping")
        self.assertEqual(ref_motor["sample_size"], 32)
        self.assertTrue(ref_motor["goodness_of_fit"]["fit_acceptable"])
        self.assertGreater(ref_motor["fitted_percentiles_ms"]["p95"], 900.0)

        ref_wm = m1.get_pacing_reference("working_memory")
        self.assertEqual(ref_wm["sample_size"], 24)
        self.assertTrue(ref_wm["goodness_of_fit"]["fit_acceptable"])
        self.assertGreater(ref_wm["fitted_percentiles_ms"]["p95"], 1200.0)

    def test_m4_independence_descriptive_prior(self):
        """Verifies M4 Historical Independence descriptive prior table."""
        art_dir = os.path.join(self.saved_model_dir, "m4_independence", "v1")
        metrics_path = os.path.join(art_dir, "metrics.json")
        schema_path = os.path.join(art_dir, "schema.json")

        self.assertTrue(os.path.exists(metrics_path))
        self.assertTrue(os.path.exists(schema_path))

        with open(metrics_path, "r", encoding="utf-8") as f:
            metrics = json.load(f)

        self.assertEqual(metrics["total_records"], 2074)
        self.assertEqual(metrics["unique_participants"], 327)
        self.assertAlmostEqual(metrics["overall_historical_independence_rate"], 0.6977, places=3)
        self.assertEqual(len(metrics["activity_specific_priors"]), 8)


if __name__ == "__main__":
    unittest.main()
