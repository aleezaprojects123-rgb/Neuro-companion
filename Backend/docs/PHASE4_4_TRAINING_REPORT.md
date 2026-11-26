# NeuroCompanion Phase 4.4 — First Controlled Training Batch Report

**Milestone:** Phase 4.4 First Controlled Model Training Batch  
**Document Location:** `Backend/docs/PHASE4_4_TRAINING_REPORT.md`  
**Execution Date:** November 21, 2026  
**Execution Environment:** Windows, Python 3.14.7 (`Backend/venv/Scripts/python.exe`)  
**ML Libraries:** scikit-learn 1.9.1, scipy 1.16.2, pandas 3.1.1, joblib 1.5.1  
**Total Tests Executed:** **131 tests in 53.049s — 100% pass rate, 0 failures, 0 errors**  
**Governance Standard:** Evidence-based clinical safety, zero data fabrication, strict anti-leakage grouping  

---

## 1. Executive Summary & Training Governance

This document certifies the successful execution of **Phase 4.4: First Controlled Training Batch** of the NeuroCompanion AI Intelligence Subsystem.

In strict compliance with clinical AI safety principles, no synthetic data was utilized, all participant/evaluator groupings were enforced to prevent cross-fold data leakage, all preprocessing transformations were fitted exclusively within training folds, and baseline comparisons (dummy mean/median/prior estimators) were evaluated alongside all candidate models.

### Certified Model Portfolio Summary

| Model ID | Module Name | Dataset ($N$) | Final Status | Primary Validation Metric vs Baseline | Artifact Path |
|:---|:---|:---|:---:|:---|:---|
| **M6** | Dialogue Quality Intelligence | AI4HOPE ($N=600$, 60 evaluators) | **TRAINED AND VALIDATED** | Empathy MAE: **0.9296** (vs 1.0350 Dummy); Spearman $\rho$: **0.3313** | `saved_model/phase4/m6_dialogue/v1/` |
| **M3** | Task Performance Intelligence | Robot Cognitive Training ($N=436$, 20 participants) | **TRAINED — LIMITED PERFORMANCE** | Error Occurrence Balanced Acc: **64.06%** (vs 50.00% Dummy); Count MAE: **0.5298** | `saved_model/phase4/m3_task_perf/v1/` |
| **M1** | Pacing & Latency Estimator | MSL ($N=32$ older) & N-Back ($N=24$ dementia) | **NOT TRAINED — DETERMINISTIC / STATISTICAL COMPONENT** | Log-Normal 95th Percentile: **1,031 ms** (MSL older) / **1,380 ms** (N-Back dementia); KS $p > 0.50$ | `saved_model/phase4/m1_pacing/v1/` |
| **M4** | Historical ADL Autonomy Prior | CASAS Smart Home ($N=2,074$, 327 participants) | **NOT TRAINED — DETERMINISTIC / STATISTICAL COMPONENT** | Empirical Base Rate Independence: **69.77%** overall (Activity range: 24.46% to 94.42%) | `saved_model/phase4/m4_independence/v1/` |
| **M2** | Language Simplicity | Internal Lexical Rules | **NOT TRAINED — DETERMINISTIC / STATISTICAL COMPONENT** | Closed-form Flesch-Kincaid Grade Level & Sentence Length | Deterministic Engine |
| **M5** | Interaction Patterns | CASAS Smart Home | **BLOCKED — DATA OR LABEL ISSUE** | Pause clustering only; clusters cannot be clinically mapped to fatigue | Not Trained |
| **M7** | Task Sequence Engine | Companion Task Tree | **NOT TRAINED — DETERMINISTIC / STATISTICAL COMPONENT** | Deterministic hierarchical activity state transition graph | Unity / Backend Engine |
| **M8** | Speech Prosody Pacing | MDVR-KCL Audio ($N=73$) | **NOT TRAINED — DETERMINISTIC / STATISTICAL COMPONENT** | Caregiver-configured speech rate multipliers (0.85×, 0.75×) | Deterministic TTS |
| **M9** | Session & Fatigue Guard | Brain-IT ($N=21$) | **NOT TRAINED — DETERMINISTIC / STATISTICAL COMPONENT** | Patient-dismissible 20-minute break suggestion timer | Deterministic Policy |
| **M10** | Unified Assistance Engine | Multi-Model Synthesis | **NOT TRAINED — DETERMINISTIC / STATISTICAL COMPONENT** | Transparent rule-based safety priority arbitrator | Deterministic Engine |

---

## 2. Model M6 — Dialogue Quality Intelligence

### 2.1 Training Specifications
- **Dataset:** `data/processed/phase4/conversational_text/ai4hope_dialogue_prepared.csv`
- **Total Records:** $N = 600$ dialogue ratings across 60 independent clinical experts and family carers (10 interactions per evaluator).
- **Leakage Prevention:** 5-Fold `GroupKFold` strictly grouped on `evaluator_id`.
  - Zero evaluators crossed between training and validation folds.
  - Zero answer texts crossed between folds (all 593 unique answers were evaluator-specific).
- **Target Variables:**
  - `target_empathy`: 1–5 human rating of empathetic tone and emotional attunement.
  - `target_actionability`: 1–5 human rating of concrete, practical caregiver guidance.
- **Algorithm:** Dual `RidgeCV` text regressors on `TfidfVectorizer` (sublinear term frequency, word/char $n$-grams (1, 2), $\max\_features = 1,000$).
- **Pipelines:** Feature extraction and vectorization fitted exclusively within each training fold.

### 2.2 Empirical Cross-Validation Results

| Evaluation Metric | Target Empathy | Dummy Baseline (Mean) | Target Actionability | Dummy Baseline (Mean) |
|:---|:---:|:---:|:---:|:---:|
| **Mean Absolute Error (MAE)** | **0.9296** | 1.0350 | **0.7694** | 0.7532 |
| **Root Mean Squared Error (RMSE)** | **1.1639** | 1.2257 | **0.9971** | 1.0043 |
| **Spearman Rank Correlation ($\rho$)** | **+0.3313** | 0.0000 | **+0.1500** | 0.0000 |
| **Quadratic Weighted Kappa (QWK)** | **0.1885** | 0.0000 | **-0.0016** | 0.0000 |

### 2.3 Scientific Assessment & Limitations
1. **Empathy Prediction:** Demonstrates genuine, reproducible learning. RidgeCV improves MAE by 0.1054 points over the dummy baseline and achieves a statistically positive rank correlation ($\rho = 0.3313, p < 0.001$).
2. **Actionability Prediction:** Demonstrates limited predictive utility. The distribution of `target_actionability` is severely right-skewed (76.5% of ratings are 4 or 5), resulting in the dummy mean baseline having a competitive MAE (0.7532 vs 0.7694). However, RidgeCV captures a weak positive correlation ($\rho = 0.1500$).
3. **Clinical Scope & Safety:** M6 evaluates **candidate dialogue text strings** before Carl voices them. It does **not** predict whether a patient requires assistance; it scores the clinical quality of the explanation itself.
4. **Final Status:** **`TRAINED AND VALIDATED`** (Suitable as an offline candidate prompt filter).

### 2.4 Artifact Paths
- **Fitted Model:** `Backend/saved_model/phase4/m6_dialogue/v1/model.joblib` (SHA-256: `add6fb5b83906a87e2fef4cb348be46787b85d6a175f368d59a552ecf975ae3c`)
- **Schema:** `Backend/saved_model/phase4/m6_dialogue/v1/schema.json`
- **Metrics:** `Backend/saved_model/phase4/m6_dialogue/v1/metrics.json`
- **Provenance:** `Backend/saved_model/phase4/m6_dialogue/v1/provenance.json`

---

## 3. Model M3 — Task Performance Intelligence

### 3.1 Training Specifications
- **Dataset:** `data/processed/phase4/activity_task/robot_games_prepared.csv`
- **Total Records:** $N = 436$ game runs across $N = 20$ older adult participants.
- **Population:** Normative healthy older adults (MMSE $28.65 \pm 1.65$; zero dementia diagnosis).
- **Leakage Prevention & Feature Timing:**
  - `game_time_sec` and `total_points` were strictly removed (post-trial leakage).
  - `touch_interaction_flag` and `vocal_interaction_flag` were strictly removed (cumulative post-trial counters).
  - Remaining pre-trial/onset features: `game_type` (categorical) and `game_reaction_time_ms` (standardized).
  - Grouped strictly by `participant_id` across 5 folds.
- **Target Variable:** `errors_count` (integer error count during gameplay; 77.52% zeros).
- **Algorithm:** Two-stage Hurdle count model:
  - Stage 1: `LogisticRegression(class_weight='balanced')` predicting error occurrence ($P(\text{errors} \ge 1)$).
  - Stage 2: `PoissonRegressor(alpha=1.0)` predicting expected error intensity.

### 3.2 Empirical Cross-Validation Results

| Evaluation Component | Model Metric | Dummy Baseline |
|:---|:---:|:---:|
| **Stage 1: Error Occurrence ($\ge 1$ vs 0)** | **64.06% Balanced Accuracy** | 50.00% (Prior) |
| **Stage 1: ROC-AUC** | **0.6838** | 0.5000 |
| **Stage 2: Hurdle Count MAE** | **0.5298** | 0.4654 (Median = 0) |
| **Zero Inflation Ratio** | **77.52% zero errors** | — |

### 3.3 Scientific Assessment & Limitations
1. **Zero-Inflation Effect:** Because 77.52% of game rounds have zero errors, a trivial baseline predicting zero for all games achieves an artificially low MAE of 0.4654. However, that baseline has 0% sensitivity to error occurrence.
2. **Error Detection Capability:** The Hurdle classifier achieves a 64.06% Balanced Accuracy and 0.6838 ROC-AUC on detecting whether an error episode will occur, demonstrating that game type and initial reaction latency provide modest predictive signal.
3. **Cohort Mismatch Warning:** The training cohort consists entirely of cognitively healthy older adults (MMSE 26–30). It cannot be assumed to generalize directly to individuals with Alzheimer's disease or vascular dementia.
4. **Final Status:** **`TRAINED — LIMITED PERFORMANCE`** (Retained for experimental research only; forbidden to trigger autonomous UI interventions without real-time patient confirmation).

### 3.4 Artifact Paths
- **Fitted Model:** `Backend/saved_model/phase4/m3_task_perf/v1/model.joblib` (SHA-256: `709cd8186ad3a9a823bc9e726a7278e8fa62796c726eeea7a03a4657307ac5d0`)
- **Schema:** `Backend/saved_model/phase4/m3_task_perf/v1/schema.json`
- **Metrics:** `Backend/saved_model/phase4/m3_task_perf/v1/metrics.json`
- **Provenance:** `Backend/saved_model/phase4/m3_task_perf/v1/provenance.json`

---

## 4. Model M1 — Pacing & Response Latency Estimator

### 4.1 Fitting Specifications
- **Datasets Analyzed Independently:**
  - **MSL Motor Tapping:** $N = 32$ older adult participants (mean motor reaction time).
  - **N-Back Working Memory:** $N = 24$ individuals diagnosed with dementia (mean working memory latency).
- **Statistical Method:** Independent parametric 2-parameter Log-Normal density estimation with fixed location ($\text{floc} = 0$, guaranteeing strictly positive physical time).
- **Goodness-of-Fit:** Evaluated via two-sided Kolmogorov-Smirnov test against fitted distribution.

### 4.2 Fitted Distribution Parameters & Percentiles

| Latency Parameter / Metric | MSL Motor Tapping (Older Adults, $N=32$) | N-Back Cognitive Memory (Dementia, $N=24$) |
|:---|:---:|:---:|
| **Empirical Mean $\pm$ SD** | $885.14 \pm 81.21\text{ ms}$ | $915.44 \pm 232.91\text{ ms}$ |
| **Empirical Median (50th %)** | $902.01\text{ ms}$ | $936.50\text{ ms}$ |
| **Empirical 95th Percentile** | $994.60\text{ ms}$ | $1,272.98\text{ ms}$ |
| **Fitted Log-Normal Shape ($\sigma$)** | **0.0956** | **0.2708** |
| **Fitted Log-Normal Scale ($e^\mu$)** | **881.22 ms** | **883.89 ms** |
| **Fitted 95th Percentile** | **1,031.27 ms** | **1,379.91 ms** |
| **KS Goodness-of-Fit Statistic** | **0.1301** ($p = 0.6048$) | **0.1592** ($p = 0.5259$) |
| **Fit Acceptability** | **Valid ($p > 0.05$)** | **Valid ($p > 0.05$)** |

### 4.3 Scientific Assessment & Clinical Governance
1. **Separation of Domains:** Motor tapping and cognitive working memory exhibit distinct dispersion parameters ($\sigma = 0.0956$ for simple motor tapping vs $\sigma = 0.2708$ for dementia working memory). Pooling these datasets would introduce profound confounding.
2. **Clinical Governance Constraint:** These upper 95th percentile bounds (1,031 ms for motor taps, 1,380 ms for cognitive selections) are technical pacing references for Carl's dialogue pauses. They are **never** used to enforce premature timeouts or task aborts on patients.
3. **Final Status:** **`NOT TRAINED — DETERMINISTIC / STATISTICAL COMPONENT`** (Registered as statistical reference artifact).

### 4.4 Artifact Paths
- **Fitted Object:** `Backend/saved_model/phase4/m1_pacing/v1/model.joblib` (SHA-256: `16e26cdd06cce69b54228437d73674ed5f1c339026a29fc55301e7ae0120864b`)
- **Schema:** `Backend/saved_model/phase4/m1_pacing/v1/schema.json`
- **Metrics:** `Backend/saved_model/phase4/m1_pacing/v1/metrics.json`
- **Provenance:** `Backend/saved_model/phase4/m1_pacing/v1/provenance.json`

---

## 5. Model M4 — Historical ADL Independence Prior

### 5.1 Audit & Investigation Findings
- **Dataset:** `data/processed/phase4/activity_task/casas_iadl_prepared.csv`
- **Total Records:** $N = 2,074$ domestic smart-apartment activity runs across $N = 327$ older adult participants.
- **Target Audit:** Ground truth `clinical_score_target == 1` represents independent completion of physical domestic chores without verbal prompting by a human examiner ($n = 1,447, 69.77\%$).
- **Feature Audit:** Duration (`duration_sec`) and hesitation pause features (`hesitation_pause_count_5s`, `max_pause_sec`) were measured during/after the chore and represent post-outcome leakage. The only valid pre-task feature is `activity_id` (chores 1 through 8).

### 5.2 Empirical Base-Rate Independence by Activity

| Activity ID | Task Description | Total Runs ($n$) | Independent Runs ($n$) | Empirical Independence Rate |
|:---:|:---|:---:|:---:|:---:|
| **1** | Phone call / communications | 260 | 180 | 69.23% |
| **2** | Medication management | 260 | 208 | 80.00% |
| **3** | Cooking / food preparation | 256 | 190 | 74.22% |
| **4** | Household chore / water plants | 303 | 172 | 56.77% |
| **5** | Hand washing / hygiene | 317 | 294 | 92.74% |
| **6** | Sweeping / dusting | 197 | 186 | 94.42% |
| **7** | Day Out planning (multi-step scheduling) | 278 | 68 | **24.46%** |
| **8** | Leisure / reading | 203 | 149 | 73.40% |
| **Overall** | **All 8 Domestic ADL Activities** | **2,074** | **1,447** | **69.77%** |

### 5.3 Scientific Decision
Fitting a machine learning model on a single 8-class categorical feature without clinical covariates merely fits an empirical lookup table of activity difficulty. Furthermore, physical apartment chores (e.g. sweeping or hand washing) do not represent digital companion interaction autonomy in Unity.

Therefore, M4 is retained as a transparent descriptive prior table rather than forced into an artificial ML model.
- **Final Status:** **`NOT TRAINED — DETERMINISTIC / STATISTICAL COMPONENT`** (Registered as descriptive prior).
- **Artifact Path:** `Backend/saved_model/phase4/m4_independence/v1/metrics.json`

---

## 6. Test Suite Execution & Verification

The complete NeuroCompanion automated backend test suite was executed in Python 3.14.7.

```text
======================================================================
TEST EXECUTION SUMMARY
======================================================================
Execution Command: venv\Scripts\python.exe -m unittest discover -s tests
Test Files Executed:
  - tests/test_phase4_4_trained_models.py       (5 tests: M6, M3, M1, M4 artifact loading & inference)
  - tests/test_phase4_3_training_framework.py   (6 tests: validator, splitter, configs, serialization)
  - tests/test_phase4_ml.py                    (15 tests: loaders, text/latency extractors, registries)
  - tests/test_v3_extended.py                  (13 tests: extended schemas, database, routes)
  - tests/test_p2a30_api_contract.py           (30 tests: Phase 2A REST API contracts)
  - tests/test_step4_phase2a.py                (18 tests: session flows, telemetry persistence)
  - tests/test_database_and_api.py             (23 tests: SQLite CRUD, metrics validation)
  - tests/test_audit_step3_1.py                (21 tests: database integrity, OASIS pipeline)

Total Tests Run: 131 tests
Pass Rate: 100% (131 Passed, 0 Failed, 0 Errored, 0 Skipped)
Execution Time: 53.049 seconds
======================================================================
```

---

## 7. Compliance with Project Protection Rules

1. **Phase 1 (OASIS Baseline):** Untouched. Model weights in `saved_model/pipeline.joblib` and metrics in `reports/metrics.json` (76.60% Accuracy, 68.87% Balanced Accuracy) are completely preserved.
2. **Phase 2A (FastAPI & SQLite):** Untouched. The production database schema, endpoints, and session telemetry routes remain intact.
3. **Phase 3 (Unity Companion):** Untouched. No scenes, C# scripts, or jaw audio assets were altered.
4. **No Production API Mounting:** Phase 4 models are isolated in `Backend/saved_model/phase4/` and are not mounted to active live routes.

---
*Phase 4.4 First Controlled Training Batch complete. All trained model artifacts and provenance hashes are saved on disk. Ready for human review.*
