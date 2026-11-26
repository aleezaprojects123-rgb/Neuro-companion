"""
Automated Test Suite for Phase 4 Machine Learning Subsystem.
Verifies registry, dataset loaders, feature extractors, anti-leakage splitting, and schemas.
"""

import os
import sys
import unittest
import pandas as pd
import numpy as np

# Ensure Backend root is in sys.path
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from ml.registry.dataset_registry import DatasetRegistry
from ml.data.ai4hope_loader import AI4HOPELoader
from ml.data.brainit_loader import BrainITLoader
from ml.data.vase_loader import VASELoader
from ml.data.canterbury_loader import CanterburyLoader
from ml.data.nback_loader import NBackLoader
from ml.data.robot_loader import RobotCognitiveLoader
from ml.data.casas_loader import CASASLoader

from ml.features.text_features import TextFeatureExtractor
from ml.features.latency_features import LatencyPacingEstimator
from ml.features.adherence_features import AdherenceTrajectoryExtractor
from ml.features.telemetry_schema import (
    PatientBehavioralTelemetry,
    PatientCueResponse,
    CaregiverSessionAnnotation,
    ModelInferenceRecord
)

from ml.evaluation.splitting import GroupedDataSplitter
from ml.evaluation.metrics import (
    calculate_quadratic_weighted_kappa,
    calculate_ordinal_metrics,
    calculate_classification_metrics
)

from ml.models.model_registry import ModelVersionCatalog
from ml.api.schemas import (
    ConversationalEvaluationRequest,
    ConversationalEvaluationResponse,
    LatencyPacingRequest,
    LatencyPacingResponse,
    AdherencePredictionRequest,
    AdherencePredictionResponse
)
from ml.api.fallbacks import DeterministicFallbackManager


class TestPhase4Registry(unittest.TestCase):
    """Verifies Dataset Registry and Provenance Catalog."""

    def setUp(self):
        self.registry = DatasetRegistry()

    def test_manifest_contains_all_11_datasets(self):
        datasets = self.registry.list_datasets()
        expected = [
            "oasis_1", "casas", "tihm", "ai4hope", "brain_it", "vase",
            "canterbury_wellbeing", "robot_cognitive_training",
            "motor_sequence_learning", "n_back_dementia", "alzheimers_tablet_rct"
        ]
        for ds in expected:
            self.assertIn(ds, datasets, f"Dataset '{ds}' must be registered in manifest.")

    def test_dataset_availability_and_resolution(self):
        summary = self.registry.get_summary()
        self.assertTrue(summary["ai4hope"]["available"])
        self.assertTrue(summary["brain_it"]["available"])
        self.assertTrue(summary["canterbury_wellbeing"]["available"])
        self.assertTrue(summary["robot_cognitive_training"]["available"])
        self.assertTrue(summary["n_back_dementia"]["available"])


class TestPhase4Loaders(unittest.TestCase):
    """Verifies all Phase 4 dataset loaders without mutating source files."""

    def setUp(self):
        self.registry = DatasetRegistry()

    def test_ai4hope_loader(self):
        path = self.registry.resolve_path("ai4hope")
        loader = AI4HOPELoader(path)
        data = loader.load()
        self.assertIn("expert_prompts", data)
        self.assertEqual(len(data["expert_prompts"]), 600)
        self.assertEqual(data["expert_prompts"]["user_id"].nunique(), 60)
        summary = loader.get_summary()
        self.assertEqual(summary["unique_experts"], 60)
        self.assertEqual(summary["misinformation_positive_count"], 4)

    def test_brainit_loader(self):
        path = self.registry.resolve_path("brain_it")
        loader = BrainITLoader(path)
        data = loader.load()
        self.assertEqual(len(data["attendance_adherence"]), 21)
        self.assertEqual(len(data["duration_adherence"]), 21)
        summary = loader.get_summary()
        self.assertEqual(summary["intervention_participants"], 21)
        self.assertEqual(summary["training_weeks"], 12)

    def test_canterbury_loader(self):
        path = self.registry.resolve_path("canterbury_wellbeing")
        loader = CanterburyLoader(path)
        df = loader.load()
        self.assertEqual(len(df), 201)
        summary = loader.get_summary()
        self.assertEqual(summary["total_sessions"], 201)
        self.assertGreater(summary["alzheimers_sessions"], 80)

    def test_nback_loader(self):
        path = self.registry.resolve_path("n_back_dementia")
        loader = NBackLoader(path)
        df = loader.load()
        self.assertEqual(len(df), 24)
        summary = loader.get_summary()
        self.assertEqual(summary["severity_group_1_normal_rt_count"], 12)
        self.assertEqual(summary["severity_group_2_slow_rt_count"], 12)
        self.assertLess(summary["group_1_mean_bt1"], summary["group_2_mean_bt1"])

    def test_robot_loader(self):
        path = self.registry.resolve_path("robot_cognitive_training")
        loader = RobotCognitiveLoader(path)
        data = loader.load()
        self.assertEqual(len(data["session_played"]), 437)
        self.assertGreaterEqual(len(data["prompts"]), 5)
        summary = loader.get_summary()
        self.assertEqual(summary["total_game_runs"], 437)
        self.assertIn("StoryQuizGame", summary["game_types"])

    def test_casas_loader(self):
        path = self.registry.resolve_path("casas")
        loader = CASASLoader(path)
        df = loader.load()
        self.assertEqual(len(df), 2074)
        summary = loader.get_summary()
        self.assertEqual(summary["total_records"], 2074)


class TestPhase4FeaturesAndSplitting(unittest.TestCase):
    """Verifies feature extraction and anti-leakage splitting."""

    def test_text_feature_extraction(self):
        extractor = TextFeatureExtractor(max_tfidf_features=10)
        single = extractor.extract_single(
            question="What is the best routine for evening agitation?",
            answer="Evening routine should be calm, quiet, and peaceful to offer comfort.",
            persona="Professional caregiver"
        )
        self.assertIn("empathy_marker_count", single)
        self.assertGreater(single["empathy_marker_count"], 0)
        self.assertEqual(single["is_professional_persona"], 1.0)

        # Test corpus transform
        toy_df = pd.DataFrame([
            {"question": "Q1", "answer": "Answer one with support.", "answer_style": "Professional caregiver"},
            {"question": "Q2", "answer": "Answer two with comfort.", "answer_style": "Patient"}
        ])
        features_df = extractor.fit_transform_corpus(toy_df)
        self.assertEqual(len(features_df), 2)
        self.assertIn("question_word_count", features_df.columns)

    def test_latency_pacing_estimator(self):
        # Preserved speed (< 650 ms)
        res_fast = LatencyPacingEstimator.evaluate_latency(550.0)
        self.assertEqual(res_fast["pacing_bracket"], "preserved_speed")
        self.assertFalse(res_fast["timeout_exceeded"])

        # Slowed speed (1300 ms, typical of dementia slower group)
        res_slow = LatencyPacingEstimator.evaluate_latency(1300.0)
        self.assertEqual(res_slow["pacing_bracket"], "substantially_slowed_speed")

        # Timeout (> 2500 ms)
        res_to = LatencyPacingEstimator.evaluate_latency(3000.0)
        self.assertEqual(res_to["pacing_bracket"], "deliberation_timeout")
        self.assertTrue(res_to["timeout_exceeded"])

    def test_adherence_extractor(self):
        # High adherence
        high_att = [3, 3, 3, 3, 3, 2, 3, 3, 3, 3, 3, 3] # 35/36 = 97.2%
        res_high = AdherenceTrajectoryExtractor.extract_adherence_metrics(high_att, 3)
        self.assertEqual(res_high["adherence_risk_bracket"], "high_adherence_stable")

        # Declining adherence
        declining_att = [3, 2, 2, 1, 1, 0, 1, 0]
        res_dec = AdherenceTrajectoryExtractor.extract_adherence_metrics(declining_att, 3)
        self.assertLess(res_dec["attendance_slope"], 0.0)

    def test_leakage_free_group_kfold(self):
        toy_data = pd.DataFrame({
            "user_id": ["U1", "U1", "U2", "U2", "U3", "U3", "U4", "U4", "U5", "U5"],
            "val": [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]
        })
        splits = list(GroupedDataSplitter.get_group_kfold_splits(toy_data, "user_id", n_splits=5))
        self.assertEqual(len(splits), 5)
        for train_idx, test_idx in splits:
            train_users = set(toy_data.iloc[train_idx]["user_id"])
            test_users = set(toy_data.iloc[test_idx]["user_id"])
            self.assertEqual(len(train_users.intersection(test_users)), 0)

    def test_metrics(self):
        y_true = np.array([1, 2, 3, 4, 5])
        y_pred = np.array([1, 2, 3, 4, 5])
        qwk = calculate_quadratic_weighted_kappa(y_true, y_pred)
        self.assertAlmostEqual(qwk, 1.0, places=3)

        ord_metrics = calculate_ordinal_metrics(y_true, y_pred)
        self.assertEqual(ord_metrics["mean_absolute_error"], 0.0)
        self.assertEqual(ord_metrics["spearman_rank_correlation"], 1.0)


class TestPhase4ApiAndFallbacks(unittest.TestCase):
    """Verifies Pydantic schemas and fallback heuristics."""

    def test_schemas(self):
        req = ConversationalEvaluationRequest(
            question_text="How to manage night waking?",
            answer_text="Keep a gentle nightlight and calm routine.",
            persona_style="Family caregiver"
        )
        self.assertEqual(req.persona_style, "Family caregiver")

        pacing_req = LatencyPacingRequest(response_latency_ms=850.0)
        self.assertEqual(pacing_req.response_latency_ms, 850.0)

    def test_fallback_manager(self):
        fallback = DeterministicFallbackManager.get_conversational_fallback(
            question="What is the routine?",
            answer="Ensure calm routine and support."
        )
        self.assertIn("empathy_score", fallback)
        self.assertTrue(fallback["abstention_triggered"])


if __name__ == "__main__":
    unittest.main()
