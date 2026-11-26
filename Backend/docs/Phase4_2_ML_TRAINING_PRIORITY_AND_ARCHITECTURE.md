# NEUROCOMPANION PHASE 4.2 â€” MACHINE LEARNING TRAINING PRIORITIES AND SUBSYSTEM ARCHITECTURE
**Document Status:** Complete Architecture & Engineering Training Strategy  
**System Module:** `Backend/ml/` (Phase 4 Intelligence Subsystem)  
**Verification Date:** November 2026  
**Auditor:** Antigravity AI Engineering & Research Verification Agent  

---

## 1. Executive Summary & Core Engineering Principles

NeuroCompanion Phase 4 introduces a scalable, scientifically grounded machine learning intelligence subsystem. To ensure patient safety, maintain scientific validity, and preserve the frozen foundations of Phases 1, 2A, and 3, the architecture enforces five core engineering principles:

1. **One Unified Intelligence Subsystem, Multiple Specialized Models:** Rather than attempting to force all heterogeneous datasets into an uninterpretable monolith, Phase 4 implements specialized, independently trained, and independently versioned micro-models that report through a unified prediction interface.
2. **Local and Offline Execution:** 100% of data loading, feature extraction, model training, evaluation, and inference executes locally on the host machine. Zero mandatory third-party cloud AI APIs or telemetry transmissions exist.
3. **Strict Participant-Grouped Cross-Validation (`GroupKFold`):** Every model trained on repeated-measures data must enforce participant grouping to prevent data leakage between training, validation, and test sets.
4. **Calibrated Probabilities, Epistemic Uncertainty & Mandatory Abstention:** Every classifier outputs well-calibrated class probabilities. If an input vector exhibits high epistemic uncertainty (out-of-distribution or high prediction variance), the model **abstains**, immediately deferring to deterministic clinical fallbacks.
5. **Architectural Isolation & Zero Database Corruption:** Phase 4 models read data strictly through verified loaders and communicate with the frontend/backend via standard API contracts. Phase 4 **never writes directly to the production SQLite database** (`neurocompanion.db`), preserving Phase 2A/Phase 3 clinical integrity.

---

## 2. Three-Tier Model Training Priorities

```
+----------------------------------------------------------------------------------------------------+
|                                    MODEL TRAINING PRIORITIES IN PHASE 4                            |
+----------------------------------------------------------------------------------------------------+
|                                                                                                    |
|  PRIORITY 1: MODELS TRAINABLE ON EXISTING AUTHENTIC RESEARCH EVIDENCE (Phase 4.3 Immediate)        |
|  - Model 1: Cognitive Screening Classifier (OASIS-1, N=235) [COMPLETED & FROZEN]                   |
|  - Model 2: Task Breakdown & Failure Predictor (CASAS Smart Home, N=327, 2,074 runs)               |
|  - Model 3: Conversational Empathy & Readability Scorer (AI4HOPE, N=120, 600 interactions)         |
|  - Model 4: Acoustic Cognitive Screening Classifier (NCMMSC2021 + Chinese Dataset 1, 1,207 files)  |
|  - Model 5: Hierarchical Bayesian Latency Pacing Estimator (N-Back N=24 + MSL N=130 + CASAS N=327) |
|                                                                                                    |
+----------------------------------------------------------------------------------------------------+
                                                 |
                                                 v
+----------------------------------------------------------------------------------------------------+
|  PRIORITY 2: RESTRICTED HEURISTIC & SPECIFICITY BENCHMARKS (Phase 4.3 Calibration)                 |
|  - Benchmark A: Out-of-Domain Acoustic Specificity Benchmark (MDVR-KCL, N=73 Parkinson's/HC)       |
|  - Benchmark B: Intra-Session Disengagement Trajectory Prior (VASE, N=11, 44,400 ratings)          |
|  - Benchmark C: Longitudinal Session Fatigue & Attendance Prior (Brain-IT N=41 + Tablet RCT N=25)  |
|  - Benchmark D: Circadian Sleep & Nighttime Distress Prior (TIHM, N=54 households)                 |
|                                                                                                    |
+----------------------------------------------------------------------------------------------------+
                                                 |
                                                 v
+----------------------------------------------------------------------------------------------------+
|  PRIORITY 3: IN-APP DIGITAL ASSISTANCE PREDICTIONS REQUIRING NATIVE TELEMETRY (Future Phase 4.4+) |
|  - Model AS01â€“AS09: Real-Time Digital UI Assistance Predictor (Touch/Gaze/Voice Cues in Unity)     |
|  - Model AS10: In-App Digital Task Independence Classifier (Virtual 3D Task Autonomy)              |
|  - Model Efficacy: Assistance Outcome Predictor (Requires consented micro-feedback logs)          |
|                                                                                                    |
+----------------------------------------------------------------------------------------------------+
```

---

### Priority 1: Models Trainable Today on Existing Evidence
These five models possess complete, verified on-disk data, explicit target labels, sufficiently large sample sizes, and approved open licenses:
1. **Model 1: Cognitive Screening Classifier (`OASIS-1`):**
   - *Status:* Completed, calibrated, and frozen in Phase 1.
   - *Role:* Baseline patient profile initializer.
2. **Model 2: Task Breakdown & Failure Predictor (`CASAS Cognitive Assessment`):**
   - *Data:* 2,074 task runs across 327 older adults (`task_audit_data.csv`).
   - *Target:* Predict whether an ongoing multi-step task will experience a stall, omission, or failure (`score != 1`).
   - *Features:* Duration, pause counts $>5.0	ext{ s}$, max pause length, activity difficulty index, sensor sequence entropy.
   - *Algorithm:* Calibrated Random Forest / XGBoost with `GroupKFold(groups=pid)`.
3. **Model 3: Conversational Empathy & Readability Scorer (`AI4HOPE`):**
   - *Data:* 600 multi-turn interaction evaluations across 120 clinical experts and family caregivers.
   - *Target:* Continuous multi-target regression of `answer_rating_empathy`, `answer_rating_clarity`, and `answer_rating_actionability`, constrained by FKGL $\le 6.0$.
   - *Features:* Mean sentence length, lexical diversity (TTR), Dale-Chall score, sentiment polarity, compact local sentence embeddings.
   - *Algorithm:* Multi-target Ridge Regressor or Calibrated Gradient Boosted Decision Trees.
4. **Model 4: Acoustic Cognitive Screening Classifier (`NCMMSC2021` + `Chinese Dataset 1`):**
   - *Data:* 806 WAV audio recordings (4.93 GB) + 401 eGeMAPS acoustic session records.
   - *Target:* 3-class diagnosis: Alzheimer's Disease (AD) vs. Mild Cognitive Impairment (MCI) vs. Healthy Controls (HC).
   - *Features:* Standardized 88-dimensional openSMILE eGeMAPS prosodic features (pitch, jitter, shimmer, formant frequencies, silence-to-speech ratio).
   - *Algorithm:* Calibrated ExtraTreesClassifier / Logistic Regression with ElasticNet penalty and `GroupKFold(groups=speaker_id)`.
5. **Model 5: Hierarchical Bayesian Latency Pacing Estimator (`N-Back` + `MSL` + `CASAS`):**
   - *Data:* $N=24$ mild-to-moderate dementia patients (N-Back), $N=130$ lifespan healthy participants (MSL), $N=327$ CASAS older adults.
   - *Target:* Continuous dynamic timeout dilation multiplier $lpha_{pacing} \in [1.0, 2.5]$.
   - *Features:* Observed millisecond response latency, age-stratified baseline tapping latency, task complexity index.
   - *Formulation:* Empirical Bayesian distribution estimator (`Backend/ml/features/latency_features.py`).

---

### Priority 2: Restricted Heuristic & Specificity Benchmarks
These benchmarks provide vital empirical boundaries and specificity tests, but must carry strictly restricted scientific claims:
1. **Benchmark A: Out-of-Domain Acoustic Specificity Benchmark (`MDVR-KCL`):**
   - Evaluates Model 4 on 73 Parkinson's disease and healthy control audio recordings to ensure that hypokinetic dysarthria is NOT falsely classified as Alzheimer's pathology.
2. **Benchmark B: Intra-Session Disengagement Trajectory Prior (`VASE`):**
   - Leverages 44,400 ratings across 11 patients to parameterize the $60	ext{ s}$ inactivity pause threshold. Not trained as an individual patient classifier.
3. **Benchmark C: Longitudinal Session Fatigue & Attendance Prior (`Brain-IT` + `Tablet RCT`):**
   - Provides descriptive longitudinal adherence research on attendance attrition. Patient autonomy to pause, stop, or continue is 100% preserved.
4. **Benchmark D: Circadian Sleep & Nighttime Distress Prior (`TIHM`):**
   - Calibrates nighttime restlessness thresholds for caregiver notifications.

---

### Priority 3: Assistance Predictions Requiring Future Consented Native Telemetry
These assistance-selection models cannot be trained on external data and must await authentic patient interaction data gathered through the NeuroCompanion application:
1. **Model AS01â€“AS09: Real-Time Digital UI Assistance Selection:**
   - Requires genuine user-initiated requests ("Repeat", "Hint", "Go Slower") and gaze/touch stall events inside the Unity application.
2. **Model AS10: In-App Digital Task Independence:**
   - Requires confirmed in-app task completions without prompts inside the Unity 3D virtual environment.
3. **Model Efficacy: Assistance Outcome Predictor:**
   - Requires in-situ 1-click micro-feedback ("Was this helpful?") linked to subsequent task accuracy improvements.

---

## 3. Proposed Phase 4 Architecture & Subsystem Layout

The Phase 4 machine learning architecture is organized cleanly within `Backend/ml/`:

```text
Backend/ml/
â”œâ”€â”€ api/
â”‚   â”œâ”€â”€ router.py                    # Unified FastAPI prediction router (Phase 2A contract)
â”‚   â”œâ”€â”€ fallbacks.py                 # Deterministic clinical safety rules & patient agency overrides
â”‚   â””â”€â”€ contracts.py                 # Pydantic input/output schemas for AS01â€“AS10 predictions
â”‚
â”œâ”€â”€ data/
â”‚   â”œâ”€â”€ loaders/
â”‚   â”‚   â”œâ”€â”€ oasis_loader.py          # OASIS-1 verified loader (235 assessed records)
â”‚   â”‚   â”œâ”€â”€ casas_loader.py          # CASAS smart home task loader (2,074 runs)
â”‚   â”‚   â”œâ”€â”€ ai4hope_loader.py        # AI4HOPE dialogue ratings loader (600 turns)
â”‚   â”‚   â”œâ”€â”€ acoustic_loader.py       # NCMMSC2021 + Chinese Dataset 1 openSMILE loader
â”‚   â”‚   â””â”€â”€ latency_loader.py        # N-Back + MSL reaction time loader
â”‚   â”œâ”€â”€ validation.py                # Schema checks, null checks, audio sample rate validation
â”‚   â””â”€â”€ splitting.py                 # GroupKFold participant-level splitters
â”‚
â”œâ”€â”€ features/
â”‚   â”œâ”€â”€ telemetry_schema.py          # 4-Tier Native Telemetry data contracts
â”‚   â”œâ”€â”€ latency_features.py          # Reaction time extraction & z-score calculation
â”‚   â”œâ”€â”€ acoustic_features.py         # openSMILE 88-dim eGeMAPS extraction pipeline
â”‚   â”œâ”€â”€ text_features.py             # Readability, FKGL, sentence length, and TTR
â”‚   â””â”€â”€ task_features.py             # Sequential duration, pause counts, and transition timing
â”‚
â”œâ”€â”€ models/
â”‚   â”œâ”€â”€ base.py                      # Abstract Base Estimator interface with uncertainty estimation
â”‚   â”œâ”€â”€ screening_model.py           # Model 1: OASIS-1 practical model wrapper
â”‚   â”œâ”€â”€ task_model.py                # Model 2: CASAS task failure predictor
â”‚   â”œâ”€â”€ dialogue_model.py            # Model 3: AI4HOPE conversational quality scorer
â”‚   â”œâ”€â”€ acoustic_model.py            # Model 4: Speech cognitive decline classifier
â”‚   â””â”€â”€ pacing_model.py              # Model 5: Hierarchical Bayesian pacing estimator
â”‚
â”œâ”€â”€ training/
â”‚   â”œâ”€â”€ train_task_model.py          # Training script with GroupKFold for CASAS
â”‚   â”œâ”€â”€ train_dialogue_model.py      # Training script for AI4HOPE
â”‚   â”œâ”€â”€ train_acoustic_model.py      # Training script with openSMILE extraction
â”‚   â””â”€â”€ calibrate_pacing.py          # Empirical calibration script for N-Back/MSL
â”‚
â”œâ”€â”€ registry/
â”‚   â”œâ”€â”€ registry.py                  # Model registry: metadata, versioning, artifacts, git commit hash
â”‚   â””â”€â”€ artifacts/                   # Serialized joblib/onnx model weights & scalers
â”‚
â””â”€â”€ tests/
    â”œâ”€â”€ test_loaders.py              # Unit tests verifying zero corruption in data loading
    â”œâ”€â”€ test_feature_extractors.py   # Unit tests for latency, acoustic, and text feature pipelines
    â”œâ”€â”€ test_groupkfold_integrity.py # Tests verifying zero participant leakage between folds
    â””â”€â”€ test_abstention_logic.py     # Tests verifying immediate fallback on high uncertainty
```

---

## 4. Key Subsystem Architectural Specifications

### 4.1 Data Validation and Quality Enforcers (`Backend/ml/data/validation.py`)
- **Strict Input Schema Enforcement:** Guarantees that every incoming batch satisfies type contracts before entering feature transformers.
- **Audio Integrity Scanner:** Automatically verifies WAV headers, channels, bit-depth, and enforces automated resampling to 16 kHz mono prior to openSMILE extraction.
- **Null Value Rejection:** Refuses to silently impute missing diagnostic labels; raises immediate validation exceptions.

### 4.2 Feature Pipelines & Shared Representations (`Backend/ml/features/`)
- **Latency Pipeline:** Computes continuous millisecond latencies, normative z-scores relative to healthy older adults, and pacing brackets.
- **Acoustic Pipeline:** Wraps standardized `openSMILE` configuration (`eGeMAPSv02.conf`) to extract the exact 88 extended Geneva parameters across all audio clips.
- **Text Pipeline:** Computes Flesch-Kincaid Grade Level, Dale-Chall score, Mean Length of Utterance, and lexical diversity.
- **Task Pipeline:** Analyzes inter-sensor pause durations, cumulative deliberation times, and transition frequencies.

### 4.3 Training & Participant-Grouped Splitting (`Backend/ml/training/`)
- Every training pipeline utilizes `GroupKFold(n_splits=5)`:
  ```python
  from sklearn.model_selection import GroupKFold
  gkf = GroupKFold(n_splits=5)
  for train_idx, test_idx in gkf.split(X, y, groups=groups):
      # Zero participant overlap between folds
      X_train, X_test = X.iloc[train_idx], X.iloc[test_idx]
      y_train, y_test = y.iloc[train_idx], y.iloc[test_idx]
  ```
- **Probability Calibration:** All classifiers wrap estimators in `CalibratedClassifierCV(method='sigmoid', cv='prefit')` to ensure output scores reflect genuine statistical probabilities.

### 4.4 Model Registry, Versioning, and Artifact Storage (`Backend/ml/registry/`)
- Every trained model is registered with a structured manifest containing:
  - Model Name and Semantic Version (e.g., `acoustic_classifier_v1.0.0`).
  - Training Dataset Hash and Git Commit SHA.
  - Cross-Validation Metrics (Balanced Accuracy, Macro-F1, Brier Score, PR-AUC).
  - Feature Input Names and Scaling Parameters.
  - Epistemic Uncertainty Threshold ($\sigma_{abstain}^2$).
- Serialized artifacts are saved as versioned `.joblib` or `.onnx` files in `Backend/ml/registry/artifacts/`.

### 4.5 Uncertainty Estimation and Epistemic Abstention
Every model inherits from `BasePredictor` (`Backend/ml/models/base.py`) which implements an uncertainty interface:
```python
class BasePredictor(ABC):
    @abstractmethod
    def predict_with_uncertainty(self, X: pd.DataFrame) -> Tuple[np.ndarray, np.ndarray, np.ndarray]:
        """
        Returns:
            predictions: Predicted class or value
            probabilities: Calibrated class probabilities
            uncertainty: Epistemic uncertainty score (e.g., prediction variance or entropy)
        """
        pass
```
- **Abstention Policy:** If `uncertainty > threshold`, the model outputs `abstention_triggered = True`. The `router.py` intercepts this signal and immediately falls back to the deterministic safety arbitrator (`fallbacks.py`).

### 4.6 Stable Integration Contract with Phase 2A (`Backend/ml/api/router.py`)
- **Read-Only Operation:** Phase 4 models read inputs from incoming HTTP payloads and return structured JSON responses conforming to `API_CONTRACT_v1.md`.
- **Zero Database Modification:** The ML subsystem **never opens a write connection** to `neurocompanion.db`. All state persistence, session auditing, and patient records remain the exclusive responsibility of frozen Phase 2A routes.

---
*Report certified as complete, evidence-based, and clinically governed.*
