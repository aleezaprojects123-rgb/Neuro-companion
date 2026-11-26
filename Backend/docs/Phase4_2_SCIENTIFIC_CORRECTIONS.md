# NEUROCOMPANION PHASE 4.2 â€” SCIENTIFIC CORRECTIONS AND GOVERNANCE AUDIT
**Document Status:** Formal Scientific Correction & Clinical Governance Audit  
**System Module:** `Backend/ml/` (Phase 4 Intelligence Subsystem)  
**Verification Date:** November 2026  
**Auditor:** Antigravity AI Engineering & Research Verification Agent  

---

## 1. Executive Summary: The Inviolable Distinction Between Research Models and Assistance Models

A rigorous scientific audit of the preliminary Phase 4.2 documentation revealed several critical conceptual conflations that, if left uncorrected, would compromise clinical safety and scientific integrity:
1. **Conflating Research Outcomes with Digital Assistance Requirements:** External datasets record clinical diagnoses, observer ratings, laboratory response times, or physical smart-home sensor events. **None of these datasets record digital assistance requirements (AS01â€“AS10) inside NeuroCompanion's Unity virtual environment.**
2. **Conflating Heuristic Engineering Formulas with Supervised Targets:** A dynamic timeout multiplier (e.g., $\alpha_{pacing} = 1.0 + 0.35 \times z$) is an engineered heuristic formula, **NOT** an observed target label in any dataset.
3. **Imposing Unsupported Thresholds:** Treating observational Likert ratings (VASE) as "proof" of a 60-second software inactivity threshold, or clinical trial adherence (Brain-IT) as "proof" of a mandatory 20-minute session cap, violates empirical evidence and removes patient autonomy.

This document formally details all necessary scientific corrections applied across the Phase 4.2 documentation suite.

---

## 2. Model-by-Model Forensic Corrections

### 2.1 Model M2 â€” CASAS Cognitive Assessment
- **Previous Unscientific Claim:** Claimed that CASAS could predict binary "task failure / assistance needed" (`score != 1`) and directly inform Unity AS03 (Step-by-Step Guidance) and AS10 (Independent Completion).
- **Scientific Corrections Applied:**
  1. **Preserve Original Score Categories:** In CASAS, `score` contains four distinct observed clinical categories:
     - `score = 1`: Independent, accurate physical completion ($n=1,447$).
     - `score = 2`: In-person verbal prompt required from human experimenter ($n=492$).
     - `score = 3`: Incomplete, omitted, or erroneous step performance ($n=84$).
     - `score = 5`: Severe cognitive/task breakdown ($n=51$).
     *Mandate:* Scores 2, 3, and 5 must **NEVER** be automatically collapsed into a single artificial "assistance needed" label. A participant who needs a verbal reminder (`score = 2`) is clinically distinct from someone who commits an uncorrected omission (`score = 3`) or experiences severe confusion (`score = 5`).
  2. **Disclaim Unity Digital UI Predictions:** CASAS evaluates physical instrumental activities of daily living (cooking, telephone use, medication management) in an ambient smart apartment instrumented with environmental motion sensors. **CASAS does NOT predict digital UI step guidance (AS03) or digital task independence (AS10) inside a 3D Unity software application.**
  3. **Reclassified Role:** Model M2 is strictly an **Exploratory Physical-IADL Task-Performance Research Model**. It investigates how ambient sensor transition latencies correlate with physical task outcomes, not digital UI prompt selection.
  4. **Domain Limitations:** Population is community-dwelling older adults performing scripted physical chores. Sensor patterns do not translate to touch-screen gestures or avatar interactions.

---

### 2.2 Model M3 â€” AI4HOPE Conversational AI
- **Previous Unscientific Claim:** Claimed that AI4HOPE optimizes patient digital assistance requirements (AS02 Simplify Instruction, AS09 Verbal Explanation).
- **Scientific Corrections Applied:**
  1. **Preserve Original Conversational-Quality Targets:** AI4HOPE contains continuous ratings evaluated by clinical experts and family caregivers:
     - `answer_rating_empathy` (Mean: $3.65 \pm 0.82$, Range: $1.0\text{--}5.0$).
     - `answer_rating_clarity` (Mean: $4.25 \pm 0.69$, Range: $1.0\text{--}5.0$).
     - `answer_rating_actionability` (Mean: $4.08 \pm 0.74$, Range: $1.0\text{--}5.0$).
     - `answer_rating_relevance` (Mean: $4.33 \pm 0.65$, Range: $1.0\text{--}5.0$).
     - `answer_rating_misinformation_flagged` (4 instances flagged as clinical misinformation).
  2. **Disclaim Patient Assistance Claims:** AI4HOPE evaluates the quality of generated palliative conversational responses. It does **NOT** predict whether a patient requires digital assistance, when a prompt should be simplified, or whether a verbal explanation is warranted.
  3. **Evaluator-Grouped Validation:** The dataset comprises 600 rated dialogue turns evaluated by 120 distinct individuals (60 healthcare professionals, 60 family carers). **Validation must strictly enforce grouping on `user_id` / evaluator identity (`GroupKFold`)** to prevent evaluator subjective bias from leaking between training and validation folds.
  4. **Reclassified Role:** Model M3 is strictly a **Conversational Response Quality Evaluation Model**. It evaluates text generation readability and compassionate phrasing, not user assistance needs.

---

### 2.3 Model M4 â€” Speech Corpora (NCMMSC2021 + Chinese Dataset 1)
- **Previous Unscientific Claim:** Claimed that combining NCMMSC2021 and Chinese Dataset 1 acoustic features predicts anomic pause duration and establishes a need for AS01 (Repeat Instruction) or AS07 (Pacing).
- **Scientific Corrections Applied:**
  1. **Separate Diagnostic Classification from Assistance Selection:** Speech acoustic analysis can classify clinical diagnostic groups (Alzheimer's Disease vs. Mild Cognitive Impairment vs. Healthy Controls). **Diagnostic classification does NOT establish an assistance requirement.**
  2. **Speech Pauses Do NOT Equal Repetition Need:** Acoustic silence, vocal hesitations, and prolonged pauses during spontaneous speech reflect lexical retrieval deficits (anomia). **They do NOT indicate that the patient failed to hear the instruction or wants it repeated (AS01).** Treating speech pauses as a prompt repetition trigger patronizes the patient and degrades interaction quality.
  3. **Disentangle Incompatible Datasets:**
     - *NCMMSC2021:* 806 WAV audio recordings (4.93 GB), 280 raw participants / 123 merged, 3-class diagnosis (AD, MCI, HC), mixed sampling rates (8 kHz, 16 kHz, 48 kHz).
     - *Chinese AD Dataset 1:* 401 sessions, binary diagnosis (AD vs. HC), Cookie Theft description task only, pre-extracted 88-dim eGeMAPS vectors + dialogue TSVs.
     - *Correction:* NCMMSC2021 and Chinese Dataset 1 must **NOT** be naively pooled into a single tabular training matrix. They involve different acoustic tasks, recording channels, and diagnostic criteria.
  4. **Reclassified Role:** Model M4 is strictly a **Speech Acoustic Cognitive Classification Research Model**. It serves as an exploratory diagnostic research tool, not an in-app assistance controller.

---

### 2.4 Model M5 â€” Latency Distributions (N-Back Dementia & MSL)
- **Previous Unscientific Claim:** Claimed that Model M5 is a machine learning model that predicts dynamic timeout multipliers ($\alpha_{pacing} \in [1.0, 2.5]$) as a supervised target.
- **Scientific Corrections Applied:**
  1. **Timeout Multipliers are NOT Supervised Targets:** Neither N-Back nor MSL contains a label called "timeout multiplier", "pacing dilation", or "assistance decision". The timeout multiplier is an **engineering heuristic formula**, not an observed ground-truth dataset label.
  2. **Separate Observed Response Times from Assistance Decisions:**
     - *MSL ($N=130$, 32 older adults):* Measures normative healthy psychomotor finger-tapping latencies ($450\text{--}650\text{ ms}$).
     - *N-Back Dementia ($N=24$):* Measures computerized working memory decision latency under experimental tDCS brain stimulation ($777.6\text{ ms} \to 1,303.9\text{ ms}$).
     - *Correction:* These two cohorts cannot be combined into a single ML classification model. They represent completely different task paradigms (motor tapping vs. 2-back working memory).
  3. **Reclassified Role:** M5 is **NOT a machine learning model**. It is an **Empirical Response-Time Distribution Baseline**. It provides observational reference distributions to guide user interface timeout engineering, not trained ML predictions.

---

### 2.5 Benchmark B2 â€” VASE Dementia Video Engagement
- **Previous Unscientific Claim:** Claimed that VASE data "validates a 60-second inactivity pause threshold" for AS08.
- **Scientific Corrections Applied:**
  1. **Remove Unsupported 60-Second Inactivity Claim:** VASE consists of 44,400 second-by-second observer ratings on a 1â€“7 Likert scale evaluating emotional/behavioral engagement among 11 nursing home residents with severe dementia listening to live music or viewing art. **VASE never evaluated software inactivity, screen timeouts, or digital assistance.**
  2. **No Automatic Assistance Triggering:** VASE observational engagement measurements must **NEVER** be used to trigger automatic breaks or system interventions.
  3. **Reclassified Role:** Benchmark B2 is strictly a **Descriptive Observational Engagement Research Reference**.

---

### 2.6 Benchmark B3 â€” Session Duration (Brain-IT & Tablet RCT)
- **Previous Unscientific Claim:** Claimed that Brain-IT and Tablet RCT adherence data "proves and enforces a hard 20-minute session cap".
- **Scientific Corrections Applied:**
  1. **Remove Mandatory 20-Minute Session Cap:** In clinical trials, session duration and attendance drop-offs are descriptive behavioral outcomes. They do **NOT** prove that every session must be terminated at 20 minutes.
  2. **Preserve Patient Autonomy:** The digital companion must **NEVER** forcefully lock out or shut down on a patient who wishes to continue. It may offer a gentle break suggestion, but **patient agency to pause, stop, or continue must be 100% preserved**.
  3. **Reclassified Role:** Benchmark B3 is strictly a **Descriptive Longitudinal Adherence Research Reference**.

---

## 3. Summary of Document Updates

The five Phase 4.2 documents have been thoroughly audited and aligned with these corrections:
1. `Phase4_2_DATASET_SUITABILITY_REPORT.md`: Updated to distinguish between datasets suitable for specific research models vs. datasets unsuitable for AS01â€“AS10 assistance.
2. `Phase4_2_AS01_AS10_MODEL_FEASIBILITY.md`: Explicitly decoupled supporting intelligence models from assistance-selection models; clarified that zero external models predict UI assistance.
3. `Phase4_2_DATASET_COMBINATION_PLAN.md`: Prohibited naive pooling of NCMMSC2021 with Chinese Dataset 1; prohibited pooling N-Back with MSL into a single ML model.
4. `Phase4_2_ML_TRAINING_PRIORITY_AND_ARCHITECTURE.md`: Separated genuine exploratory research models from assistance controllers; removed the hard 20-minute cap.
5. `Phase4_2_FUTURE_TELEMETRY_REQUIREMENTS.md`: Reaffirmed that authentic AS01â€“AS10 assistance prediction requires consented native telemetry in Unity.

---
*Report certified as scientifically corrected, evidence-based, and clinically governed.*
