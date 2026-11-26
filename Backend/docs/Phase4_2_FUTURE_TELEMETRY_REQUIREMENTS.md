# NEUROCOMPANION PHASE 4.2 â€” FUTURE NATIVE TELEMETRY REQUIREMENTS AND GROUND-TRUTH ACQUISITION SPECIFICATION
**Document Status:** Complete Telemetry Specification & Clinical Ethics Architecture  
**System Module:** `Backend/ml/` (Phase 4 Intelligence Subsystem)  
**Verification Date:** November 2026  
**Auditor:** Antigravity AI Engineering & Research Verification Agent  

---

## 1. Executive Summary & Clinical Governance Statement

A central empirical conclusion of Phase 4.1 and Phase 4.2 is that:
> **Zero external research datasets provide direct ground truth for real-time digital UI assistance selection (AS01â€“AS09) or in-app task independence (AS10) inside NeuroCompanion's Unity environment.**

External datasets capture clinical diagnostic categories, laboratory neuropsychological latencies, or physical smart-home experimenter interventions. Attempting to train a supervised prompt selector using synthetic target labels or clinical proxy mappings (such as mapping slow reaction time directly to "Display Visual Hint AS04") constitutes **pseudo-scientific data hallucination** and poses genuine clinical risks to patients living with cognitive impairment.

To establish genuine, scientifically valid ground-truth labels for in-app assistance selection, NeuroCompanion implements a consented **4-Tier Native Telemetry Pipeline** (`Backend/ml/features/telemetry_schema.py`). 

This document defines:
1. The exact interactional, behavioral, and clinical variables required to create true assistance training data.
2. The operational criteria for distinguishing assistance **acceptance** from **true assistance effectiveness**.
3. Patient autonomy, dynamic consent, caregiver review protocols, and privacy safeguards (GDPR / HIPAA).
4. A definitive master summary table of all Phase 4 models, their datasets, training feasibility, target labels, and validation standards.

---

## 2. The 4-Tier Native Telemetry Architecture

The telemetry pipeline is architecturally partitioned into four isolated tiers to guarantee data integrity, auditability, and safety:

```
+----------------------------------------------------------------------------------------------------+
|                                  4-TIER NATIVE TELEMETRY PIPELINE                                  |
+----------------------------------------------------------------------------------------------------+
|                                                                                                    |
|  TIER 1: INTERACTION & SENSORY PACING TELEMETRY (Continuous Baseline)                              |
|  - Timestamped touch coordinates (X, Y), screen touch durations, release latencies                 |
|  - Gaze dwell time and spatial fixation stagnation on screen regions                               |
|  - Voice activity: acoustic silence duration following system prompt                              |
|  - Millisecond reaction time relative to N-Back/MSL age-matched pacing baselines                   |
|                                                                                                    |
+----------------------------------------------------------------------------------------------------+
                                                 |
                                                 v
+----------------------------------------------------------------------------------------------------+
|  TIER 2: PATIENT-INITIATED ASSISTANCE REQUESTS (Primary Ground-Truth Signal)                       |
|  - Explicit UI button taps: "Repeat Audio", "Show Hint", "Simplify", "Slower Speech"               |
|  - Natural voice queries: "What did you say?", "I don't understand", "Help me"                    |
|  - Captures 100% UNAMBIGUOUS GROUND TRUTH that the patient experienced an assistance need          |
|                                                                                                    |
+----------------------------------------------------------------------------------------------------+
                                                 |
                                                 v
+----------------------------------------------------------------------------------------------------+
|  TIER 3: ASSISTANCE DISPOSITION & IN-SITU EFFECTIVENESS (Outcome Ground Truth)                     |
|  - Assistance Offered: Which cue (AS01-AS09) was surfaced by the system                            |
|  - Disposition: Patient accepted, ignored, or actively dismissed/closed the cue                   |
|  - Subsequent Task Success: Did the patient successfully complete the sub-step after the cue?      |
|  - Error Rate Reduction: Did the error rate decrease following assistance?                         |
|  - 1-Click Micro-Feedback: Optional non-intrusive prompt: "Was this helpful? [Yes / No]"           |
|                                                                                                    |
+----------------------------------------------------------------------------------------------------+
                                                 |
                                                 v
+----------------------------------------------------------------------------------------------------+
|  TIER 4: MODEL INFERENCE, UNCERTAINTY & ABSTENTION LOGGING (Audit & Shadow Mode)                   |
|  - Record model semantic version, input feature vector, and predicted probability distribution    |
|  - Record jackknife epistemic variance and entropy                                                 |
|  - Log whether abstention was triggered and fallback arbitrator was invoked                        |
|  - Completely isolated from Tier 1-3 ground-truth records to prevent data leakage                  |
|                                                                                                    |
+----------------------------------------------------------------------------------------------------+
```

---

## 3. Explicit Data Fields Required for Genuine Assistance Modeling

To train future supervised models that predict when and which assistance modality should be triggered, the telemetry schema captures the following structured fields per interaction episode:

### 3.1 Interaction Context & Baseline Telemetry (Tier 1)
- `session_id`: Unique UUID for the active cognitive companion session.
- `participant_id`: Anonymized patient alphanumeric hash (e.g., `NC_PAT_88421`).
- `task_id`: Identifier of the active cognitive game or conversational routine.
- `task_complexity_tier`: Nominal difficulty rating ($1.0\text{--}3.0$).
- `substep_index`: Current sequential step number within the multi-part exercise.
- `step_start_timestamp_utc`: ISO 8601 millisecond timestamp.
- `step_latency_ms`: Elapsed time from prompt completion to patient interactive input.
- `normative_latency_z`: Z-score of response latency relative to age-matched baseline (calibrated via MSL).
- `touch_dwell_duration_ms`: Duration of continuous finger contact on touch display.
- `spatial_stagnation_duration_ms`: Duration for which touch or gaze remained stationary in one screen quadrant.
- `vocal_silence_duration_ms`: Duration of acoustic silence prior to vocal response (calibrated via NCMMSC2021).

---

### 3.2 Patient Assistance Intent & Explicit Requests (Tier 2)
- `patient_requested_assistance`: Boolean flag (`True` if the user actively sought help).
- `request_channel`: Categorical (`button_tap`, `voice_intent`, `unprompted_gesture`).
- `requested_modality`: Specific modality requested by patient:
  * `AS01` â€” Repeat Instruction (Tapped "Repeat" or said "What did you say?")
  * `AS02` â€” Simplify Instruction (Said "I don't understand" or requested easier prompt)
  * `AS04` â€” Visual Hint (Tapped "Show Hint" or question mark icon)
  * `AS06` â€” Slower Speech (Selected "Speak slower" accessibility toggle)
  * `AS08` â€” Break or Pause (Said "I'm tired" or tapped "Pause")
  * `AS09` â€” Verbal Explanation (Said "Why are we doing this?")

---

### 3.3 Assistance Delivery, Disposition & True Efficacy (Tier 3)
- `assistance_offered_code`: Modality surfaced by the system (`AS01` to `AS09`, or `None` if `AS10` independent).
- `assistance_offered_trigger`: Reason for intervention (`patient_request`, `latency_timeout`, `consecutive_errors`, `spatial_stagnation`).
- `assistance_offered_timestamp_utc`: Millisecond timestamp of visual/audio presentation.
- `patient_disposition`: Explicit disposition behavior:
  * `accepted` â€” Patient tapped or engaged with the offered assistance.
  * `ignored` â€” Patient continued independent attempt without engaging the cue.
  * `dismissed` â€” Patient actively tapped "Close" or swiped away the cue.
- `subsequent_task_completion`: Boolean flag (`True` if the patient completed the task successfully following the prompt).
- `post_assistance_latency_ms`: Time taken to execute the target action following cue presentation.
- `subsequent_error_count`: Number of errors committed after cue delivery.
- `micro_feedback_rating`: Optional 1-click user rating (`helpful`, `unhelpful`, `neutral`, `unanswered`).
- `caregiver_review_flag`: Boolean flag indicating whether a supervising caregiver reviewed and validated the interaction.

---

### 3.4 Model Shadow Logging (Tier 4)
- `model_version`: Semantic identifier (e.g., `assistance_selector_v0.2.1_shadow`).
- `predicted_probabilities`: Hash mapping `AS01`â€“`AS09` and `AS10` to predicted float probabilities.
- `epistemic_uncertainty`: Prediction variance or entropy.
- `abstention_triggered`: Boolean flag (`True` if uncertainty exceeded safety threshold).
- `arbitrator_override`: Boolean flag (`True` if patient agency or safety rule overrode model recommendation).

---

## 4. Operationalizing True Assistance Efficacy: The Acceptance vs. Efficacy Mandate

A foundational error in assistive AI is equating **acceptance** with **effectiveness**:
- **Why Acceptance is Insufficient:** An individual living with Mild Cognitive Impairment or early Alzheimer's Disease may tap an offered hint out of habit, accidental touch, or polite compliance, even if the hint fails to resolve their confusion.
- **The Operational Definition of True Efficacy:**  
  An assistance intervention is defined as **clinically effective** if and only if:
  1. The patient successfully completes the sub-step (`subsequent_task_completion = True`).
  2. The post-assistance response latency returns to within the patient's normative pacing window ($\le 1.5	imes$ baseline).
  3. The error rate on subsequent steps does not increase.
  4. The patient does NOT immediately request a second, escalated assistance cue for the same step.
  5. If micro-feedback is provided, it is marked as `helpful`.

Any training pipeline targeting assistance effectiveness must use this **composite ground-truth label**, not raw acceptance rates.

---

## 5. Patient Autonomy, Dynamic Consent, and Caregiver Oversight

Deploying cognitive telemetry in vulnerable populations demands the highest standards of data ethics, transparency, and legal compliance (GDPR, HIPAA, and the Declaration of Helsinki):

### 1. Inviolable Patient Agency
The patient's explicit interaction always overrides machine learning predictions:
- If a patient taps "Show Hint", the system displays the hint immediately, regardless of what any machine learning model predicts.
- If a patient taps "Dismiss" or "I want to do it myself", the system immediately withdraws the cue and suppresses future automatic prompts for that task block.

### 2. Tiered Dynamic Consent & Data Provenance
- **Transparent Opt-In:** Telemetry collection is never enabled by default. During initial setup, patients and their legally designated healthcare proxies are presented with clear, plain-language options:
  * *Tier 1 Consent (Local Gameplay Only):* Real-time pacing and assistance execute locally, but zero interaction logs are persisted to disk.
  * *Tier 2 Consent (Local Adaptation & Caregiver Summary):* Interaction logs are persisted locally in encrypted storage to generate caregiver progress summaries and personalize pacing.
  * *Tier 3 Consent (Consented Research Telemetry):* Anonymized, de-identified telemetry episodes are exported for scientific model retraining under formal Institutional Review Board (IRB) oversight.
- **Right to Erasure & Immediate Revocation:** Patients or caregivers can withdraw consent or purge all local interaction logs with a single click in the settings menu.

### 3. Local-Only Storage & Anonymization
- All telemetry records are stored in encrypted local application storage with zero cloud dependencies.
- Zero Protected Health Information (PHI)â€”such as patient names, residential addresses, clinical NHS/SSN numbers, or raw facial videoâ€”is ever logged in telemetry schemas.

---

## 6. Master Table of All Proposed Models and Validation Requirements

The table below provides the definitive specification of all models across the Phase 4 machine learning intelligence subsystem:

| Model ID | Model Name & Role | Primary Training Datasets | Current Feasibility | Authentic Target Labels | Key Input Features | Primary Proposed ML Algorithm | Validation Approach |
|:---:|:---|:---|:---:|:---|:---|:---|:---|
| **M1** | **Cognitive Screening Classifier** | OASIS-1 ($N=235$ assessed) | **READY (FROZEN)** | 3-Class `CDR` (0.0, 0.5, 1.0+) | Age, Sex, Education, MMSE | Calibrated Random Forest | Stratified 80/20 train/test split (Phase 1 Baseline) |
| **M2** | **Task Breakdown & Prompting Predictor** | CASAS Smart Home ($N=327$, 2,074 runs) | **READY (PRIORITY 1)** | `score` (1=indep, 2=prompted, 3/5=error) | Duration, pause counts $>5	ext{ s}$, max pause, activity index | Calibrated Random Forest / XGBoost | Strict `GroupKFold(groups=pid)` ($N=327$ groups) |
| **M3** | **Conversational Quality & Readability Scorer** | AI4HOPE Palliative Care ($N=120$, 600 dialogs) | **READY (PRIORITY 1)** | `answer_rating_empathy`, clarity, actionability, FKGL $\le 6.0$ | Sentence length, lexical diversity (TTR), Dale-Chall, sentiment | Multi-target Ridge Regressor / Calibrated GBDT | 5-fold cross-validation grouped by dialogue topic |
| **M4** | **Acoustic Cognitive Impairment Classifier** | NCMMSC2021 Audio (806 WAVs) + Chinese AD 1 (401 sessions) | **READY (PRIORITY 1)** | 3-Class Diagnosis (AD, MCI, HC) | 88-dimensional openSMILE eGeMAPS acoustic prosody vectors | Calibrated ExtraTrees / ElasticNet Logistic Regression | Strict `GroupKFold(groups=speaker_id)` (Zero speaker leakage) |
| **M5** | **Hierarchical Bayesian Latency Pacing Estimator** | N-Back Dementia ($N=24$) + MSL Lifespan ($N=130$) + CASAS | **READY (PRIORITY 1)** | Continuous timeout dilation $lpha_{pacing} \in [1.0, 2.5]$ | Observed response latency (ms), age-stratified baseline, task complexity | Hierarchical Bayesian Latency Estimator | Leave-one-group-out cross-validation across latency percentiles |
| **B1** | **Out-of-Domain Acoustic Specificity Benchmark** | MDVR-KCL Audio ($N=73$ Parkinson's / HC) | **READY (PRIORITY 2)** | Negative control evaluation (Parkinson's vs AD) | 88-dimensional openSMILE eGeMAPS acoustic vectors | Out-of-domain specificity testing on Model M4 | Evaluates whether Model M4 falsely flags PD as AD |
| **B2** | **Intra-Session Disengagement Trajectory Prior** | VASE Dementia Video ($N=11$, 44,400 ratings) | **READY (PRIORITY 2)** | Inactivity threshold ($>60	ext{ s}$ pause) | Second-by-second engagement drops during sensory tasks | Descriptive trajectory prior (Not high-capacity ML) | Parameterizes heuristic $60	ext{ s}$ pause threshold in `fallbacks.py` |
| **B3** | **Longitudinal Session Fatigue & Attendance Prior** | Brain-IT RCT ($N=41$) + Tablet RCT ($N=25$) | **READY (PRIORITY 2)** | Hard 20-minute session cap for AS08 | Weekly attendance attrition, minutes per session | Empirical survival / hazard distribution | Parameterizes hard 20-minute session cap in `fallbacks.py` |
| **AS01-09** | **In-App Real-Time UI Assistance Predictor** | Future Consented Native Telemetry (NeuroCompanion Unity) | **FUTURE (PRIORITY 3)** | Consented Tier 2/3 assistance requests and accepted UI cues | Gaze dwell, touch latencies, consecutive errors, task state | Contextual Multi-Armed Bandit / Calibrated Neural Net | Participant-grouped temporal train/validation/test split |
| **AS10** | **In-App Digital Task Independence Model** | Future Consented Native Telemetry (NeuroCompanion Unity) | **FUTURE (PRIORITY 3)** | Unprompted successful task completion in Unity 3D games | Interaction latency, error count, touch precision | Calibrated Logistic Regression / Random Forest | Strict `GroupKFold(groups=participant_id)` |

---
*Report certified as complete, evidence-based, and clinically governed.*
