# NeuroCompanion Phase 4 — Model Readiness Report & Task Verification Audit

**Document Status:** Approved ML Readiness Audit & Experimental Specification  
**System Module:** `Backend/ml/` (Phase 4 Intelligence Subsystem)  
**Parent Framework:** NeuroCompanion AI/ML Research Subsystem  
**Date:** November 2026  

---

## 1. Executive Summary & Purpose

This report provides a formal readiness assessment for all candidate Machine Learning (ML) research tasks in **NeuroCompanion Phase 4**. 

Phase 4 introduces a dedicated, modular AI/ML subsystem in `Backend/ml/` designed to conduct rigorous scientific research using 11 external cognitive, clinical, sensor, and behavioral datasets. In strict adherence to project governance, Phase 4 maintains absolute isolation from frozen Phase 1 (OASIS structural MRI model), Phase 2A (FastAPI production services and SQLite database `neurocompanion.db`), and Phase 3 (Unity 3D conversational avatar foundation).

### Key Audit Conclusions
1. **Infrastructure Readiness:** The Phase 4 architectural foundation (`Backend/ml/`) is fully implemented, verified, and operational. All dataset loaders, feature extractors, grouped data splitters, and evaluation metric suites execute with $100\%$ test pass rates (`Backend/tests/test_phase4_ml.py`).
2. **Task Categorization:** Seven discrete research tasks have been identified where genuine empirical data and valid ground-truth labels exist.
3. **Data Integrity & Anti-Leakage:** To ensure clinical validity, `GroupKFold` cross-validation grouped strictly by `participant_id` (or `user_id`/`subject_id`) is enforced across all tasks. Zero synthetic data is permitted for claiming ML performance.
4. **Clinical Safety & Governance:** For digital assistance selection (AS01–AS10), zero third-party datasets contain direct in-app UI prompt labels. Consequently, assistance selection in production remains strictly governed by deterministic rules and pacing priors (`Backend/ml/api/fallbacks.py`), supported by a 4-tier native telemetry collection framework for future training.

---

## 2. Research Tasks Readiness Matrix

| Task # | Research Task Title | Primary Dataset(s) | Sample Size & Units | Ground Truth Labels | Readiness Status | Proposed Model Algorithm | Anti-Leakage Group Key | Primary Evaluation Metric(s) |
| :---: | :--- | :--- | :--- | :--- | :---: | :--- | :--- | :--- |
| **T1** | **Conversational Quality & Empathy Prediction** | AI4HOPE | $N=600$ dialog pairs, 60 experts, 60 caregivers | `rating_expert`, `rating_caregiver` (1–5 Likert) | **READY** | Ridge Regression / LightGBM Regressor with NLP Readability Features | `user_id` (Prompt ID) | Quadratic Weighted Kappa (QWK), MAE, Spearman $\rho$ |
| **T2** | **Task Completion & Prompted Performance** | CASAS Cognitive Assessment | $N=2,074$ task records across 339 participants | `score` (1,2,3,5), `tier` (1=Indep, 2=Assist, 3=Degrad) | **READY** | Balanced Random Forest / Gradient Boosting Classifier | `participant_id` | Balanced Accuracy, Macro F1, Per-Tier Precision/Recall |
| **T3** | **Observational Engagement Dynamics** | VASE | $N=44,400$ second-level records (11 subjects, 37 raters) | Continuous engagement score (0–100 / 1–5 scale) | **READY** | First-Order Hidden Markov Model (HMM) & Autoregressive Pacing | `subject_id` | Intraclass Correlation (ICC), MAE, State Transition Matrix |
| **T4** | **Longitudinal Adherence & Drop-out Risk** | Brain-IT | $n=21$ intervention subjects, 12 weeks of sessions | Weekly attendance percentage, session durations | **READY** | OLS Trajectory Slope + Logistic Risk Classifier (High/Med/At-Risk) | `patient_id` | Brier Score, ROC-AUC, Trajectory RMSE |
| **T5** | **Cognitive Latency & Pacing Prior Estimation** | N-Back Dementia | $N=24$ dementia participants (Severity 1 vs 2) | Reaction time (ms), accuracy (hit/miss/false alarm) | **READY** | Generalized Linear Model (GLM) / Log-Normal Latency Estimator | `patient_id` | Kolmogorov-Smirnov Test, Mean Calibration Error |
| **T6** | **Response Timing & Psychomotor Baselines** | Motor Sequence Learning & Robot Cognitive | $N=18$ healthy older adults + 437 robot game runs | Psychomotor tapping intervals (ms), touch x/y coordinates | **READY** | Empirical Bayes Baseline Estimator & K-Means Clustering | `participant_id` | Empirical Quantile Fit, Mean Squared Error |
| **T7** | **Wellbeing & Affective Outcome Modeling** | Canterbury Wellbeing | $N=201$ session evaluations (92 Alzheimer's disease) | Pre-to-post composite wellbeing change ($\Delta \text{Wellbeing}$) | **READY** | Mixed-Effects Linear Regression / ElasticNet Regressor | `participant_id` | $R^2$, MAE, Wilcoxon Effect Size ($d$) |

---

## 3. Deep-Dive Readiness Audits by Task

### Task 1: Conversational Quality & Empathy Prediction (AI4HOPE)
- **Clinical Objective:** Evaluate patient-facing conversational responses to ensure high empathy, emotional validation, clarity, and low cognitive burden ($\le 6$th-grade reading level).
- **Data Audit:**
  - Files: `expert_evaluation.csv` (600 rows), `caregiver_evaluation.csv` (601 rows).
  - Evaluators: 60 geriatric healthcare experts and 60 experienced dementia caregivers.
  - Features Extracted: Character length, word count, sentence count, average syllables per word, Flesch-Kincaid Grade Level, Flesch Reading Ease score, lexical diversity (Type-Token Ratio), and TF-IDF n-grams (`Backend/ml/features/text_features.py`).
- **Splitting & Leakage Prevention:** `GroupKFold(n_splits=5)` grouped strictly on `user_id`. This guarantees that dialog turns originating from the same prompt family are never split across training and testing sets.
- **Evaluation Standards:** Standard regression metrics (MAE $\le 0.45$) and ordinal agreement metrics (Quadratic Weighted Kappa $\ge 0.65$, Spearman rank correlation $\rho \ge 0.70$).

### Task 2: Task Completion & Prompted Performance (CASAS)
- **Clinical Objective:** Distinguish between independent cognitive task completion and the emergence of hesitation requiring assistance.
- **Data Audit:**
  - File: `task_audit_data.csv` (2,074 rows, 339 unique participants).
  - Class Distribution:
    - Independent (`tier = 1`, `score = 5`): 1,180 records ($56.9\%$).
    - Assisted (`tier = 2`, `score = 2` or `3`): 532 records ($25.6\%$).
    - Degraded / Incomplete (`tier = 3`, `score = 1`): 362 records ($17.5\%$).
  - Features: Cumulative task duration, 5-second pause frequency (`pause_cnt_5_int`), maximum hesitation interval (`max_pause_int`), and activity complexity index.
- **Splitting & Leakage Prevention:** `GroupKFold(n_splits=5)` grouped strictly by `participant_id`. Zero subject overlap across folds.
- **Evaluation Standards:** Balanced Accuracy $\ge 72\%$, Macro F1 $\ge 0.70$. Cost-sensitive weighting applied to prevent majority-class bias toward independent completion.

### Task 3: Observational Engagement Dynamics (VASE)
- **Clinical Objective:** Model temporal fluctuations in patient engagement during structured therapeutic activities to detect disengagement episodes before complete session abandonment occurs.
- **Data Audit:**
  - Data: 44,400 second-by-second observer engagement ratings across 11 participants and 37 video raters.
  - Dynamics: Displays distinct state transitions (active engagement $\leftrightarrow$ passive observation $\leftrightarrow$ distraction/fatigue).
  - Rater Variance: Inter-rater reliability demonstrates variance across annotators; models must learn consensus distributions rather than single-rater noise.
- **Splitting & Leakage Prevention:** Leave-One-Subject-Out (LOSO) or `GroupKFold(n_splits=5)` on `subject_id`.
- **Target Modeling:** Discrete-state Hidden Markov Models (3 states: High, Medium, Low Engagement) to compute state transition probabilities and expected dwell times.

### Task 4: Longitudinal Adherence & Drop-out Risk (Brain-IT)
- **Clinical Objective:** Model patient compliance trajectories over a 12-week cognitive training intervention to proactively identify participants at risk of disengagement.
- **Data Audit:**
  - Data: 21 intervention participants tracked across 12 consecutive weeks.
  - Extracted Metrics: Weekly training completion percentage, session minutes per week, slope of adherence change ($\beta_1$), and variance of engagement.
- **Splitting & Leakage Prevention:** Stratified `GroupKFold` on `patient_id`.
- **Target Modeling:** The `AdherenceTrajectoryExtractor` (`Backend/ml/features/adherence_features.py`) calculates ordinary least squares (OLS) linear drift ($\Delta \text{adherence} / \text{week}$) and brackets patients into 3 clinical risk categories:
  - *High Adherence:* Mean completion $\ge 80\%$, non-negative trajectory slope.
  - *Moderate Adherence:* Mean completion $50\%\text{--}79\%$.
  - *At-Risk:* Mean completion $< 50\%$ or steep negative trajectory slope ($\beta_1 < -5.0\%/\text{week}$).

### Task 5: Cognitive Latency & Pacing Prior Estimation (N-Back Dementia)
- **Clinical Objective:** Establish empirical psychomotor and working memory latency baselines across differing stages of cognitive impairment to calibrate software timeout thresholds.
- **Data Audit:**
  - Cohort: 24 clinically diagnosed dementia participants (Severity Group 1 vs Severity Group 2).
  - Empirical Latencies:
    - Mild Impairment (Group 1): Mean reaction time $= 777.6\text{ ms}$ ($\text{SD} = 142.1\text{ ms}$).
    - Moderate Impairment (Group 2): Mean reaction time $= 1,303.9\text{ ms}$ ($\text{SD} = 287.4\text{ ms}$).
- **Target Modeling:** Calibrates the `LatencyPacingEstimator` (`Backend/ml/features/latency_features.py`). Evaluates whether observed patient latency falls within expected bounds ($z \le 1.96$) or represents severe hesitation ($z > 2.5$), dynamically expanding UI response windows without recording false-negative errors.

### Task 6: Response Timing & Psychomotor Baselines (MSL & Robot Cognitive)
- **Clinical Objective:** Separate purely physical/motor tapping latencies from cognitive processing delays.
- **Data Audit:**
  - Motor Sequence Learning (MSL): Provides clean psychomotor tapping baselines ($400\text{--}650\text{ ms}$) in healthy older adults free of cognitive deficits.
  - Robot Cognitive Training: 437 game runs across 4 game types (visual matching, audio recall, sequencing, spatial memory).
- **Target Modeling:** Dual-distribution mixture modeling separating physical touch preparation latency from decision-making cognitive latency.

### Task 7: Wellbeing & Affective Outcome Modeling (Canterbury Wellbeing)
- **Clinical Objective:** Quantify the therapeutic impact of interactive sessions on patient emotional wellbeing and identify which session parameters maximize positive affective response in Alzheimer's disease.
- **Data Audit:**
  - Sample: 201 completed session evaluations (92 confirmed Alzheimer's disease participants).
  - Metrics: Pre-session wellbeing score, post-session wellbeing score, session activity category, participant mood indicators.
- **Splitting & Leakage Prevention:** `GroupKFold(n_splits=5)` grouped by `participant_id`.
- **Target Modeling:** Mixed-effects regression assessing fixed effects (activity type, duration) and random effects (individual baseline wellbeing) on delta wellbeing ($\Delta = \text{Post} - \text{Pre}$).

---

## 4. Verification and Automated Test Evidence

To guarantee operational readiness, the complete Phase 4 subsystem was subjected to automated unit, regression, and integration testing:

```text
======================================================================
NeuroCompanion Phase 4 Automated Test Suite Execution
Target: Backend/tests/test_phase4_ml.py
Python: Backend/venv/Scripts/python.exe
Platform: Windows (PowerShell)
======================================================================
Ran 15 tests in 2.825s
Result: OK (100% Passing)

Verified Test Coverage:
[PASS] test_manifest_loads_and_verifies_all_datasets
[PASS] test_ai4hope_loader_shapes_and_columns
[PASS] test_brainit_loader_extracts_adherence_records
[PASS] test_vase_loader_extracts_engagement_series
[PASS] test_canterbury_loader_filters_alzheimers_cohort
[PASS] test_nback_loader_extracts_severity_subsets
[PASS] test_robot_loader_extracts_game_runs_and_prompts
[PASS] test_casas_loader_validates_task_audit_data
[PASS] test_text_feature_extraction
[PASS] test_latency_pacing_estimator_bounds
[PASS] test_adherence_trajectory_extractor_math
[PASS] test_telemetry_dataclasses_instantiate_cleanly
[PASS] test_grouped_data_splitter_no_leakage
[PASS] test_evaluation_metrics_computations
[PASS] test_deterministic_fallbacks_and_pydantic_schemas
======================================================================
```

Simultaneously, Phase 2A database and API regression tests were executed:
```text
======================================================================
Phase 2A Regression Test Suite: Backend/tests/test_database_and_api.py
Ran 5 tests in 1.274s
Result: OK (100% Passing)
Phase 1 (OASIS Model): Frozen & Preserved
Phase 2A (SQLite & API): Zero Schema Mutation, Zero Regressions
Phase 3 (Unity Foundation): Fully Preserved
======================================================================
```

---

## 5. Strict Governance & Ethical Commitments

The Phase 4 AI/ML subsystem adheres to the following clinical and technical constraints:

1. **No Synthetic Data for Accuracy Claims:** Under no circumstances will synthetic, LLM-generated, or artificially augmented patient records be used to claim clinical accuracy or model superiority.
2. **Strict Group-Level Splitting:** Every model trained within `Backend/ml/` must utilize `GroupKFold` or `GroupShuffleSplit` on participant/user identifiers. Cross-validation runs exhibiting patient leakage will fail automated CI/CD checks.
3. **Immediate Patient Agency:** When a patient explicitly requests assistance (e.g., "repeat", "slower", "help"), the system triggers the requested modality immediately via deterministic fallbacks, bypassing model inference.
4. **Mandatory Caregiver Oversight:** NeuroCompanion is an assistive companion, not an autonomous diagnostic or prescribing medical device. All longitudinal risk alerts, adherence flags, and cognitive trend reports are delivered to designated caregivers and clinicians for human-in-the-loop review.
5. **Decoupled Architecture:** All Phase 4 inference logic is deployed under `/api/v1/ml/` (`Backend/ml/api/router.py`) and does not modify existing Phase 2A routes, authentication tokens, or database tables.
