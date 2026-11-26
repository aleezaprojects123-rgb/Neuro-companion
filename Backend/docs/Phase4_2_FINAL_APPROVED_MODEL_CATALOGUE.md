# NEUROCOMPANION PHASE 4.2 â€” FINAL APPROVED MODEL CATALOGUE
**Document Status:** Final Approved Model Catalogue & Engineering Specification  
**System Module:** `Backend/ml/` (Phase 4 Intelligence Subsystem)  
**Verification Date:** November 2026  
**Auditor:** Antigravity AI Engineering & Research Verification Agent  

---

## 1. Executive Summary & Model Classification Taxonomy

To ensure complete transparency, prevent pseudo-scientific data hallucination, and strictly comply with clinical governance, every model in the NeuroCompanion subsystem is catalogued according to its **true prediction category**:

1. **Clinical or Cognitive Research Classification Model:** Predicts an established clinical or cognitive diagnostic stage from clinical screeners or acoustic recordings.
2. **Behavioral or Performance Pattern Research Model:** Predicts an observed task execution outcome or performance score in an experimental environment.
3. **Conversational Response Quality Evaluation Model:** Scores generated companion dialogue text for empathy, clarity, and readability.
4. **Empirical Response-Time Distribution Baseline:** Descriptive statistical distributions of human response latencies; NOT a supervised prediction model.
5. **Observational / Adherence Research Benchmark:** Descriptive behavioral trajectories from observational studies or clinical trials; NOT an automated intervention trigger.
6. **In-App Digital Assistance Selection Model (AS01â€“AS10):** Predicts when and which digital UI cue should be presented in the Unity application. **Requires consented native telemetry.**

---

## 2. Master Catalogue of Subsystem Models

| Model ID | Model Name | True Prediction Category | Supporting Dataset(s) | Actual Target Label | Proposed ML Algorithm | Validation Grouping | Operational Status |
|:---:|:---|:---|:---|:---|:---|:---|:---:|
| **M1** | **Cognitive Screening Classifier** | Clinical Research Classification | OASIS-1 ($N=235$ assessed) | 3-Class `CDR` ($0.0, 0.5, 1.0+$) | Calibrated Random Forest | Stratified 80/20 split | **COMPLETED & FROZEN (Phase 1 Baseline)** |
| **M2** | **Physical IADL Task-Performance Model** | Behavioral Performance Pattern | CASAS Smart Home ($N=327$, 2,074 runs) | 4-Class `score` ($1, 2, 3, 5$) | Calibrated ExtraTrees / XGBoost | `GroupKFold(groups=pid)` ($N=327$) | **EXPLORATORY RESEARCH MODEL** |
| **M3** | **Conversational Quality & Readability Scorer** | Conversational Response Quality | AI4HOPE ($N=120$ raters, 600 dialogs) | Continuous Empathy, Clarity, Actionability (1â€“5) | Multi-target Ridge / SVR / GBDT | `GroupKFold(groups=evaluator_id)` ($N=120$) | **EXPLORATORY RESEARCH MODEL** |
| **M4** | **Speech Acoustic Cognitive Classifier** | Clinical Research Classification | NCMMSC2021 Audio ($N=280$, 806 WAVs) | 3-Class Diagnosis (AD, MCI, HC) | Calibrated ExtraTrees / Logistic Regression | `GroupKFold(groups=speaker_id)` ($N=280$) | **EXPLORATORY RESEARCH MODEL** |
| **M5** | **Response-Time Latency Baseline** | Empirical Latency Distribution | N-Back ($N=24$) & MSL ($N=130$) | *None* (Continuous observed latencies) | Hierarchical Bayesian Prior (Not supervised ML) | Stratified Leave-One-Group-Out | **DESCRIPTIVE TIMING PRIOR** |
| **B1** | **Out-of-Domain Acoustic Specificity Benchmark** | Clinical Research Benchmark | MDVR-KCL ($N=73$ Parkinson's/HC) | Negative control (PD vs. HC) | Evaluates Model M4 specificity | Leave-One-Subject-Out | **SPECIFICITY BENCHMARK** |
| **B2** | **In-Session Engagement Trajectory Reference** | Observational Research Benchmark | VASE ($N=11$, 44,400 ratings) | *None* (Second-by-second Likert ratings) | Descriptive trajectory analysis (Not ML) | Subject-isolated inspection | **OBSERVATIONAL REFERENCE** |
| **B3** | **Longitudinal Adherence Research Reference** | Adherence Research Benchmark | Brain-IT ($N=41$) & Tablet RCT ($N=25$) | *None* (Attendance & minutes per session) | Descriptive survival / retention curve | Trial cohort isolation | **OBSERVATIONAL REFERENCE** |
| **AS01-09** | **In-App Digital UI Assistance Selector** | Actual In-App Assistance Requirement | Consented Native Telemetry (Unity) | Consented user requests & accepted UI cues | Multi-Armed Bandit / Calibrated Neural Net | Participant-grouped temporal split | **BLOCKED (Awaiting Native Telemetry)** |
| **AS10** | **In-App Digital Task Independence Model** | Actual In-App Assistance Requirement | Consented Native Telemetry (Unity) | Unprompted successful virtual task completion | Calibrated Logistic Regression / RF | Strict `GroupKFold(groups=participant_id)` | **BLOCKED (Awaiting Native Telemetry)** |

---

## 3. Deep Forensic Specification of Approved Models

### Part A: Completed & Frozen Baseline Model

#### Model M1 â€” Cognitive Screening Classifier
- **True Prediction Category:** Clinical Cognitive Research Classification.
- **Dataset:** OASIS-1 cross-sectional cohort (`Backend/data/raw/oasis_cross-sectional_utf8.csv`).
- **Population:** Older adults evaluated at Washington University ADRC. Assessed cohort: $N=235$ (mean age $75.3$).
- **Features:** `Age`, `M/F` (Sex), `Educ` (Education code 1â€“5), `MMSE` (Mini-Mental State Exam 0â€“30).
- **Target Label:** 3-class Clinical Dementia Rating (`CDR`): Class 0 ($0.0$, Non-Demented, $n=135$), Class 1 ($0.5$, Very Mild Dementia, $n=70$), Class 2 ($\ge 1.0$, Mild-to-Moderate Dementia, $n=30$).
- **Algorithm & Validation:** Calibrated Random Forest with `StandardScaler` and `OneHotEncoder`. Stratified 80/20 train/test split (188 train / 47 test). Balanced Accuracy $0.745$, Macro-F1 $0.720$, Log-Loss $0.428$.
- **Governance Status:** **COMPLETED & FROZEN IN PHASE 1**. Code, weights, and artifacts are read-only.
- **Assistance Disclaimer:** M1 predicts clinical screening stage at patient onboarding. It does **NOT** predict real-time digital UI assistance needs.

---

### Part B: Exploratory Research Models Suitable for Training

#### Model M2 â€” Physical IADL Task-Performance Model
- **True Prediction Category:** Behavioral Performance Pattern Research Model.
- **Dataset:** CASAS Cognitive Assessment Smart Home (`task_audit_data.csv`).
- **Population:** 327 community-dwelling older adults performing 8 multi-step IADLs in an instrumented smart apartment.
- **Sample Size:** 2,074 task runs ($N=327$ unique participants).
- **Features:** `duration` (total task seconds), `pause_cnt_5_int` (hesitation pauses $>5.0	ext{ s}$), `max_pause_int` (longest pause duration), `act` (activity type index 1â€“8), cumulative sensor transition latencies.
- **Target Label:** 4-class observed clinical score:
  - `score = 1`: Independent, accurate physical completion ($n=1,447$, $69.8\%$).
  - `score = 2`: In-person verbal prompt required ($n=492$, $23.7\%$).
  - `score = 3`: Incomplete or omitted step performance ($n=84$, $4.1\%$).
  - `score = 5`: Severe cognitive/task breakdown ($n=51$, $2.5\%$).
  *(Mandatory Rule: Scores 2, 3, and 5 must remain separate; never collapse into an artificial binary label).*
- **Proposed Algorithm:** Calibrated ExtraTreesClassifier or Cost-Sensitive XGBoost.
- **Validation Approach:** Strict `GroupKFold(n_splits=5, groups=pid)` ($N=327$ groups). Zero participant overlap between training and testing folds.
- **Evaluation Metrics:** Macro-F1, Balanced Accuracy, Multiclass Brier Score.
- **Domain Limitations & Assistance Disclaimer:** M2 evaluates physical instrumental activities in an ambient smart apartment. **It does NOT predict digital UI step guidance (AS03) or virtual task independence (AS10) in a 3D Unity software application.**

---

#### Model M3 â€” Conversational Quality & Readability Scorer
- **True Prediction Category:** Conversational Response Quality Evaluation Model.
- **Dataset:** AI4HOPE Palliative Care Conversational AI (`expert_prompts.csv` and `response_ratings.csv`).
- **Population:** 120 independent evaluators (60 professional palliative clinicians, 60 family caregivers) evaluating 600 multi-turn dialogue turns.
- **Sample Size:** 600 verified rated dialogue turns across 120 evaluators.
- **Features:** Mean sentence length, Flesch-Kincaid Grade Level (FKGL), Dale-Chall score, sentiment polarity, lexical diversity (Type-Token Ratio), TF-IDF vectors / compact local sentence embeddings.
- **Target Labels:** Continuous ratings (1.0 to 5.0 scale):
  - `answer_rating_empathy` (Mean: $3.65 \pm 0.82$).
  - `answer_rating_clarity` (Mean: $4.25 \pm 0.69$).
  - `answer_rating_actionability` (Mean: $4.08 \pm 0.74$).
  - `answer_rating_relevance` (Mean: $4.33 \pm 0.65$).
  - Reading Grade Level constraint (predicting FKGL $\le 6.0$).
- **Proposed Algorithm:** Multi-target Ridge Regressor or Calibrated Gradient Boosted Decision Trees (LightGBM).
- **Validation Approach:** Strict `GroupKFold(n_splits=5, groups=evaluator_id)` ($N=120$ evaluator groups). Zero evaluator overlap across folds to prevent rater subjectivity leakage.
- **Evaluation Metrics:** Mean Squared Error (MSE), Mean Absolute Error (MAE), Pearson Correlation Coefficient ($r$).
- **Domain Limitations & Assistance Disclaimer:** M3 evaluates the quality, empathy, and readability of generated conversational text. **It does NOT predict patient assistance requirements (AS02 or AS09).**

---

#### Model M4 â€” Speech Acoustic Cognitive Classifier
- **True Prediction Category:** Clinical Cognitive Research Classification Model.
- **Dataset:** NCMMSC2021 Mandarin Audio Competition (`dataset/`).
- **Population:** 280 raw Chinese older adults performing spontaneous and picture-prompted speech tasks.
- **Sample Size:** 806 WAV audio recordings (4.93 GB) across 280 raw participants (123 merged participant sets).
- **Features:** Standardized 88-dimensional openSMILE extended Geneva (eGeMAPS) acoustic prosody vectors extracted after automated resampling to 16 kHz, 16-bit mono.
- **Target Label:** 3-class clinical diagnosis: Alzheimer's Disease (AD, $n=79$) vs. Mild Cognitive Impairment (MCI, $n=93$) vs. Healthy Controls (HC, $n=108$).
- **Proposed Algorithm:** Calibrated ExtraTreesClassifier, Logistic Regression with ElasticNet penalty, or LightGBM.
- **Validation Approach:** Strict `GroupKFold(n_splits=5, groups=speaker_id)` ($N=280$ groups). Raw recordings and merged audio for the same speaker reside in the same fold.
- **Evaluation Metrics:** Macro-F1, Balanced Accuracy, Multiclass Log-Loss.
- **Domain Limitations & Assistance Disclaimer:** M4 is an exploratory speech research classifier. **It does NOT predict an instruction repetition need (AS01) or pacing need (AS07).** Speech pauses and hesitations reflect lexical access difficulty (anomia), not auditory memory failure.

---

### Part C: Empirical Descriptive Baselines & Benchmarks

#### Model M5 â€” Empirical Response-Time Latency Baseline
- **True Category:** Empirical Response-Time Distribution Baseline (NOT a machine learning model).
- **Datasets:** N-Back Dementia ($N=24$) and Motor Sequence Learning ($N=130$).
- **Role:** Observational latency distributions:
  - MSL defines healthy older adult psychomotor finger tapping ($450\text{--}650\text{ ms}$).
  - N-Back defines working memory decision latency in dementia ($777.6\text{ ms} \to 1,303.9\text{ ms}$).
- **Assistance Disclaimer:** M5 contains **NO supervised target** for timeout multipliers or assistance decisions. It provides descriptive distribution bounds to guide user interface timeout engineering without failing patients prematurely.

---

#### Benchmark B1 â€” Out-of-Domain Acoustic Specificity Benchmark
- **True Category:** Clinical Research Specificity Benchmark.
- **Dataset:** MDVR-KCL Audio ($N=73$: 31 Parkinson's Disease, 42 Healthy Controls; ZERO dementia).
- **Role:** Evaluates whether Model M4 erroneously misclassifies motor speech dysarthria (hypophonia, vocal tremor) as Alzheimer's cognitive decline.

---

#### Benchmark B2 â€” In-Session Observational Engagement Reference
- **True Category:** Observational Research Reference.
- **Dataset:** VASE Dementia Video Engagement ($N=11$ nursing home residents, 44,400 ratings).
- **Role:** Qualitative research on engagement dynamics during live music/art therapy.
- **Assistance Disclaimer:** VASE does **NOT** validate a 60-second software inactivity threshold and must **NEVER** trigger automated breaks or UI interventions.

---

#### Benchmark B3 â€” Longitudinal Adherence Research Reference
- **True Category:** Adherence Research Reference.
- **Datasets:** Brain-IT Exergame RCT ($N=41$) and Alzheimer's Tablet RCT ($N=25$).
- **Role:** Descriptive research on multi-week exercise attendance and tablet cognitive maintenance.
- **Assistance Disclaimer:** Does **NOT** enforce a mandatory 20-minute session cap. The system must always preserve patient autonomy to pause, stop, or continue.

---

### Part D: Digital Assistance Models Awaiting Consented Native Telemetry

#### Models AS01â€“AS10 â€” Real-Time Digital UI Assistance Models
- **True Category:** Actual In-App Assistance Requirement Models.
- **Dataset Required:** Consented 4-Tier Native Telemetry gathered directly from the NeuroCompanion Unity application.
- **Status:** **BLOCKED AWAITING NATIVE TELEMETRY.**
- **Governance Mandate:** Zero supervised assistance models will be trained until authentic patient-initiated requests (Tier 2) and confirmed task outcomes (Tier 3) are recorded under formal patient consent. Until then, assistance logic executes via transparent deterministic fallbacks with full patient control.

---
*Catalogue certified as complete, evidence-based, and clinically governed.*
