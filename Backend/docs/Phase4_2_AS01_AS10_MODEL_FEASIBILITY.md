# NEUROCOMPANION PHASE 4.2 â€” AS01â€“AS10 MODEL FEASIBILITY AND SPECIALIZED MODEL SPECIFICATION
**Document Status:** Complete Scientific Specification & Model Feasibility Analysis  
**System Module:** `Backend/ml/` (Phase 4 Intelligence Subsystem)  
**Verification Date:** November 2026  
**Auditor:** Antigravity AI Engineering & Research Verification Agent  

---

## 1. Executive Summary & Clinical Governance Statement

A primary architectural principle established in Phase 4.1 is the **strict separation between Supporting Intelligence Models and Assistance-Selection Models**:
- **Supporting Intelligence Models:** Genuine, scientifically defensible machine learning models trained on authentic target labels present in external research datasets (e.g., predicting cognitive screening stage from clinical demographics, classifying acoustic impairment from speech recordings, scoring conversational empathy from text, or predicting physical task breakdown from sensor transitions).
- **Assistance-Selection Models:** Real-time digital UI controllers that predict which of the ten NeuroCompanion assistance modalities (**AS01â€“AS10**) should be dynamically presented to a user in the Unity application.

### Fundamental Scientific Finding
> **No external dataset possesses direct ground-truth labels for real-time digital UI assistance selection (AS01â€“AS10) or in-app task independence.**  
> Supporting intelligence models provide valuable contextual signals (e.g., detecting vocal tremor, quantifying lexical hesitation, or calculating psychomotor delay), but **none of these signals directly prove which digital UI intervention is required**. For example, detecting speech hesitation indicates word-retrieval difficulty (anomia), NOT an automatic user request to repeat instructions (AS01).

Therefore, Phase 4 operates under a **Dual-Layer Architecture**:
1. **Layer 1 (Trained Supporting Models):** Specialized machine learning models trained on authentic external research labels that output calibrated probabilities, feature representations, and cognitive risk indices.
2. **Layer 2 (Clinical Safety & Pacing Arbitrator):** Deterministic rule-based fallbacks (`Backend/ml/api/fallbacks.py`) and empirical Bayesian pacing priors that map patient requests, interaction timeouts, and Layer 1 contextual signals into safe assistance actions with **mandatory epistemic abstention** whenever model uncertainty is high.

---

## 2. Specialized Candidate Machine Learning Models

Based on the forensic dataset suitability analysis in `Phase4_2_DATASET_SUITABILITY_REPORT.md`, five specialized machine learning model candidates can be defensibly trained on authentic target labels:

```
+------------------------------------------------------------------------------------------------+
|                             SPECIALIZED MODEL CANDIDATES IN PHASE 4                            |
+------------------------------------------------------------------------------------------------+
|  MODEL 1: Cognitive Screening Predictor (OASIS-1)                                              |
|  - Target: 3-Class CDR (0.0 Non-Demented, 0.5 Very Mild, 1.0+ Mild-to-Moderate)                |
|  - Inputs: Age, Sex, Education, MMSE                                                           |
|  - Status: Trained, calibrated, and frozen in Phase 1 (Baseline reference)                     |
+------------------------------------------------------------------------------------------------+
                                                 |
                                                 v
+------------------------------------------------------------------------------------------------+
|  MODEL 2: Task Breakdown & Prompting Predictor (CASAS Smart Home)                              |
|  - Target: Task Failure / Prompt Needed (`score != 1`)                                         |
|  - Inputs: Duration, pause counts, maximum pause, sensor transition latencies                  |
|  - Algorithms: Calibrated Random Forest, XGBoost with GroupKFold(groups=participant_id)        |
+------------------------------------------------------------------------------------------------+
                                                 |
                                                 v
+------------------------------------------------------------------------------------------------+
|  MODEL 3: Conversational Quality & Readability Scorer (AI4HOPE)                                |
|  - Target: Continuous Empathy (1-5), Clarity (1-5), Actionability (1-5), FKGL <= 6.0           |
|  - Inputs: Lexical diversity, mean sentence length, sentiment, Dale-Chall, TF-IDF / embeddings |
|  - Algorithms: Ridge Regression, Support Vector Regressor, Calibrated Gradient Boosted Trees   |
+------------------------------------------------------------------------------------------------+
                                                 |
                                                 v
+------------------------------------------------------------------------------------------------+
|  MODEL 4: Acoustic Cognitive Impairment Classifier (NCMMSC2021 + Chinese Dataset 1)           |
|  - Target: 3-Class Diagnosis (AD vs. MCI vs. Healthy Controls)                                 |
|  - Inputs: 88-dimensional openSMILE eGeMAPS acoustic prosody vectors                           |
|  - Algorithms: Calibrated ExtraTrees, Logistic Regression with L1/L2, GroupKFold by speaker    |
+------------------------------------------------------------------------------------------------+
                                                 |
                                                 v
+------------------------------------------------------------------------------------------------+
|  MODEL 5: Empirical Psychomotor & Cognitive Latency Pacing Estimator (N-Back + MSL)           |
|  - Target: Dynamic Timeout Multiplier alpha in [1.0, 2.5] (AS07 Pacing)                        |
|  - Inputs: Real-time touch/voice latency (ms), age-stratified baseline, task complexity       |
|  - Formulation: Hierarchical Bayesian Latency Distribution (Not a black-box neural classifier) |
+------------------------------------------------------------------------------------------------+
```

---

### Detailed Candidate Model Specifications

#### Candidate Model 1: Cognitive Screening Classifier
- **Intended Prediction Target:** 3-class Clinical Dementia Rating (`CDR` 0.0 vs. 0.5 vs. 1.0+).
- **Supporting Dataset:** OASIS-1 (`Backend/data/raw/oasis_cross-sectional_utf8.csv`).
- **Input Features:** `Age`, `M/F` (Sex), `Educ`, `MMSE`.
- **Actual Target Labels:** `CDR` column across 235 eligible assessed records.
- **Population Restrictions:** Older adults (mean age $75.3$ in assessed cohort). Excludes young control cohort.
- **Proposed ML Algorithm:** Calibrated Random Forest with `OneHotEncoder`, `StandardScaler`, and `CalibratedClassifierCV(method='sigmoid')`.
- **Validation Approach:** Stratified 80/20 train/test split (188 train / 47 test), repeated 5-fold cross-validation.
- **Evaluation Metrics:** Balanced Accuracy ($0.745$), Macro-F1 ($0.720$), Multiclass Log-Loss ($0.428$), Brier Score.
- **Expected Limitations:** Cross-sectional screener; does not reflect intra-day cognitive fluctuations or real-time digital interaction.
- **Contribution to AS01â€“AS10:** Informs the baseline patient impairment profile at session startup, setting conservative default pacing brackets.
- **Operational Status:** **COMPLETED & FROZEN IN PHASE 1**.

---

#### Candidate Model 2: Task Breakdown & Prompting Predictor
- **Intended Prediction Target:** Probability that an ongoing multi-step task will stall, fail, or require an intervention (`score \in \{2, 3, 5\}`).
- **Supporting Dataset:** CASAS Cognitive Assessment Smart Home (`task_audit_data.csv`).
- **Input Features:** `duration` (continuous seconds), `pause_cnt_5_int` (count of hesitation pauses $>5.0\text{ s}$), `max_pause_int` (longest pause duration), `act` (activity difficulty index 1â€“8), cumulative activity pace.
- **Actual Target Labels:** `score` column (Class 0: `score = 1`, unassisted accurate completion, $n=1,447$; Class 1: `score = 2`, verbal prompt needed, $n=492$; Class 2: `score \in \{3, 5\}`, failure/severe breakdown, $n=135$).
- **Population Restrictions:** Community-dwelling older adults performing physical IADLs.
- **Proposed ML Algorithm:** Calibrated Random Forest or Gradient Boosted Trees (XGBoost/LightGBM) with class-balanced weighting.
- **Validation Approach:** Strict `GroupKFold(n_splits=5)` grouped on participant identifier `pid` ($N=327$ groups). Zero participant overlap between folds.
- **Evaluation Metrics:** Macro-F1, Balanced Accuracy, Precision-Recall AUC for the failure/prompt classes.
- **Expected Limitations:** Trained on physical smart-home sensor logs. Provides structural task breakdown dynamics, but cannot be treated as a direct predictor of digital UI visual hints (AS04).
- **Contribution to AS01â€“AS10:** Directly informs **AS03 (Step-by-Step Guidance)** and **AS10 (Independent Completion)** gating logic.

---

#### Candidate Model 3: Conversational Quality & Readability Scorer
- **Intended Prediction Target:** Continuous scores for generated companion prompt empathy, clarity, and cognitive accessibility.
- **Supporting Dataset:** AI4HOPE Palliative Care Conversational AI (`expert_prompts.csv` and `response_ratings.csv`).
- **Input Features:** Mean sentence length, Flesch-Kincaid Grade Level (FKGL), Dale-Chall readability, sentiment polarity, lexical diversity (Type-Token Ratio), TF-IDF vectors / compact local sentence embeddings.
- **Actual Target Labels:** `answer_rating_empathy` (1.0â€“5.0), `answer_rating_clarity` (1.0â€“5.0), `answer_rating_actionability` (1.0â€“5.0).
- **Population Restrictions:** Caregiver and patient dialogue evaluations.
- **Proposed ML Algorithm:** Multi-target Ridge Regressor, Support Vector Regressor (SVR), or Gradient Boosted Decision Trees with isotonic calibration.
- **Validation Approach:** 5-fold cross-validation grouped by conversational topic/dialogue session.
- **Evaluation Metrics:** Mean Squared Error (MSE), Mean Absolute Error (MAE), Pearson Correlation Coefficient ($r$), $R^2$.
- **Expected Limitations:** Evaluates text quality and instructional clarity; does not predict in-app real-time trigger states.
- **Contribution to AS01â€“AS10:** Directly optimizes **AS02 (Simplify Instruction)** and **AS09 (Verbal Explanation)** prompt generation, enforcing that companion text never exceeds a 6th-grade reading level.

---

#### Candidate Model 4: Acoustic Cognitive Impairment Classifier
- **Intended Prediction Target:** 3-class acoustic cognitive screening: Alzheimer's Disease (AD) vs. Mild Cognitive Impairment (MCI) vs. Healthy Controls (HC).
- **Supporting Datasets:** NCMMSC2021 Mandarin Audio (806 WAV files, $N=280$ raw / 123 merged) + Chinese AD Dataset 1 (401 sessions, 88-dim openSMILE eGeMAPS vectors).
- **Input Features:** Standardized 88-dimensional openSMILE eGeMAPS feature space (pitch, jitter, shimmer, formant frequencies, harmonic-to-noise ratio, spectral flux, silence-to-speech ratio).
- **Actual Target Labels:** Diagnostic labels (AD, MCI, HC) encoded in official challenge splits.
- **Population Restrictions:** Older adults performing picture description and spontaneous speech tasks.
- **Proposed ML Algorithm:** Calibrated ExtraTreesClassifier, Logistic Regression with ElasticNet penalty, or LightGBM.
- **Validation Approach:** `GroupKFold(n_splits=5)` grouped strictly by unique speaker/participant identifier.
- **Evaluation Metrics:** Macro-F1, Balanced Accuracy, Multiclass Log-Loss, specificity on out-of-domain Parkinson's audio (MDVR-KCL).
- **Expected Limitations:** Mandarin speech recording tasks; acoustic prosody features generalize cross-lingually, but recording channel variance requires cepstral mean and variance normalization (CMVN).
- **Contribution to AS01â€“AS10:** Quantifies vocal hesitation and anomic pause duration, informing **AS01 (Repeat Instruction)** and **AS07 (Pacing)** to prevent interrupting patients while they are struggling for words.

---

#### Candidate Model 5: Empirical Psychomotor & Cognitive Latency Pacing Estimator
- **Intended Prediction Target:** Continuous dynamic timeout multiplier $\alpha_{pacing} \in [1.0, 2.5]$, scaling the baseline UI response window before triggering assistance.
- **Supporting Datasets:** N-Back Dementia ($N=24$) + Motor Sequence Learning ($N=130$) + CASAS inter-step pauses ($N=327$).
- **Input Features:** Observed millisecond response latency, age-stratified baseline tapping latency, task complexity index, consecutive deliberation pauses.
- **Actual Target Labels:** Empirical reaction time distribution percentiles (95th percentile working memory retrieval bounds).
- **Formulation:** Hierarchical Bayesian Latency Estimator (`Backend/ml/features/latency_features.py`).
- **Validation Approach:** Leave-one-group-out cross-validation across latency percentiles.
- **Evaluation Metrics:** Pacing coverage (percentage of successful late responses captured without timeout penalty), false-timeout rate.
- **Expected Limitations:** Small clinical sample in N-Back ($N=24$); must be conditioned on sham/unstimulated trials.
- **Contribution to AS01â€“AS10:** Directly powers **AS07 (Additional Response Time)**.

---

## 3. Modality-by-Modality AS01â€“AS10 Model Feasibility Audit

The table below summarizes the feasibility, evidence level, supporting model contribution, and abstention requirements for all ten assistance modalities:

| Code | Modality Name | Supporting Datasets | Evidence Level | Supervised Assistance Model Defensible Today? | Supporting Model Contribution | Additional Data Required for Direct Model | Abstention Policy |
|:---:|:---|:---|:---:|:---:|:---|:---|:---:|
| **AS01** | **Repeat Instruction** | Robot Training, AI4HOPE, Chinese AD 1, NCMMSC2021 | Indirect (Class B) | **NO** (No UI repetition trigger labels) | Candidate Model 4 detects anomic pauses; Model 3 filters prompt clarity. | Consented logs of user button clicks ("Repeat Audio") and verbal queries ("What?"). | **Abstain** unless explicitly requested by patient or timeout exceeds $2.5\times$ pacing baseline. |
| **AS02** | **Simplify Instruction** | AI4HOPE, Chinese AD Transcripts | Indirect (Class B) | **NO** (No UI simplification trigger labels) | Candidate Model 3 rewrites prompt to $\le 6$th-grade level upon consecutive errors. | Interaction logs linking failed comprehension to successful simplified variants. | **Abstain** until $\ge 2$ consecutive task errors occur; then invoke Model 3 text re-generation. |
| **AS03** | **Step-by-Step Guidance** | CASAS Smart Home ($N=327$) | Indirect (Class B) | **NO for UI** (Class A for physical tasks) | Candidate Model 2 predicts physical task stall/breakdown risk. | In-app interaction logs of sub-step omissions during multi-part virtual exercises. | **Abstain** during initial task attempts; activate sequential guidance when stall latency $>15.0\text{ s}$. |
| **AS04** | **Visual Hint** | Robot Training ($N=20$) | Indirect (Class B) | **NO** (Zero visual UI cue interaction logs) | Candidate Model 5 detects psychomotor hesitation; touch bounding boxes. | Gaze dwell and touch spatial coordinates linked to accepted on-screen visual highlights. | **Abstain** to deterministic rule: trigger pulsing visual cue only if touch stagnates for $>8.0\text{ s}$. |
| **AS05** | **Demonstration** | Robot Training, Tablet RCT | Indirect (Class B) | **NO** (No demonstration need labels) | Candidate Model 2 flags severe task failure probability ($>0.80$). | In-situ telemetry recording demonstration requests and subsequent task success rates. | **Abstain** strictly until 2 consecutive failed attempts, then offer 5-second animated walkthrough. |
| **AS06** | **Slower Speech** | Chinese AD 1, NCMMSC2021 | Indirect (Class B) | **NO** (Speech rate is fixed in datasets) | Candidate Model 4 identifies severe acoustic slowing in patient speech. | Consented patient TTS rate adjustments ($1.0\times \to 0.8\times$) and comprehension logs. | **Abstain** to patient accessibility setting; reduce TTS rate upon explicit command or advanced CDR stage. |
| **AS07** | **Additional Response Time** | N-Back Dementia, MSL, CASAS | Indirect (Class B) | **Empirical Pacing Heuristic Only** | Candidate Model 5 calculates Bayesian latency prior, dilating timeout by $+50\%\text{--}100\%$. | Millisecond telemetry of touch/speech completion under expanded timeout windows. | **Active Heuristic:** Automatically dilates timeout window based on Model 5 empirical distributions. |
| **AS08** | **Break or Pause** | Brain-IT, VASE, Tablet RCT, TIHM | Indirect (Class B) | **Patient-Controlled Suggestion Only** | Descriptive adherence curves inform gentle break suggestions. VASE is observational. | User-reported fatigue surveys, mid-session abandonments, and biometric fatigue signals. | **Patient Choice Preserved:** May suggest a break if session is prolonged, but never forces session termination. |
| **AS09** | **Verbal Explanation** | AI4HOPE ($N=120$) | Indirect (Class B) | **NO** (Evaluates output text, not UI trigger) | Candidate Model 3 generates compassionate explanatory phrasing. | Interaction logs of patient queries ("Why do I do this?") and subsequent engagement. | **Abstain** unless user explicitly clicks "Explain" or exhibits sustained task indecision. |
| **AS10** | **Independent Completion** | CASAS Smart Home ($N=327$) | Supplementary (Class B) | **NO for UI** (Class A for physical ADLs) | Candidate Model 2 predicts high probability of unprompted task success. | Authentic in-app task completions without hints or timeouts in NeuroCompanion Unity games. | **Active Baseline State:** System observes silently with zero prompts as long as latency is normal. |

---

## 4. Methodological Governance: Why Claims of "Predicting Assistance Effectiveness" are Prohibited

A frequent failure mode in healthcare machine learning is claiming that a model "predicts effective assistance" when the underlying dataset only recorded an observational correlation.

### Three Inviolable Clinical Governance Principles:
1. **Acceptance Does Not Equal Efficacy:** If an assistance prompt is accepted by a patient, this does **not** prove it was effective. An impaired patient may click "OK" or accept a hint out of social compliance or confusion, without that hint improving cognitive task performance. Efficacy requires measuring **subsequent task accuracy, reduced error rate, or decreased cognitive distress** relative to an unassisted baseline.
2. **Speech Pauses Do Not Prove Repetition Need:** Speech acoustic models (Candidate Model 4) can accurately detect acoustic silence and vocal hesitation. However, in neurodegenerative disorders, pauses during speech reflect lexical access deficits (anomia), NOT an auditory comprehension deficit or a desire to hear an instruction repeated (AS01). Treating pauses as an automatic trigger for AS01 patronizes the user and exacerbates conversational frustration.
3. **Mandatory Epistemic Abstention:** Any machine learning model deployed in NeuroCompanion must compute its prediction variance or entropy. If input features fall outside the training distribution (high epistemic uncertainty), the model must **abstain**, immediately reverting to safe, patient-controlled deterministic fallbacks.

---
*Report certified as complete, evidence-based, and clinically governed.*
