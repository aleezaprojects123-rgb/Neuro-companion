# NeuroCompanion Phase 4 — AS01–AS10 Assistance Evidence Matrix & Native Telemetry Strategy

**Document Status:** Approved Architecture & Clinical Evidence Specification  
**System Module:** `Backend/ml/` (Phase 4 Intelligence Subsystem)  
**Parent Framework:** NeuroCompanion AI/ML Research Subsystem  
**Date:** November 2026  

---

## 1. Executive Summary & Clinical Governance Statement

A central objective of the NeuroCompanion assistance engine is to adaptively support individuals living with Mild Cognitive Impairment (MCI) and mild-to-moderate Alzheimer's Disease (AD) during cognitive training and daily interactions. The NeuroCompanion taxonomy categorizes assistance into ten distinct operational modalities (**AS01–AS10**).

### Fundamental Scientific Finding
Following comprehensive forensic audits of eleven external cognitive, clinical, sensor, and behavioral datasets (comprising OASIS-1, CASAS, TIHM, AI4HOPE, Brain-IT, VASE, Canterbury Wellbeing, Robot Cognitive Training, Motor Sequence Learning, N-Back Dementia, and Alzheimer's Tablet RCT):

> **Critical Empirical Finding:**  
> **Zero external datasets contain direct, granular labels for software-based digital assistance selection (AS01–AS09) or task-level digital independence (AS10).**  
> External datasets contain clinical dementia diagnoses, laboratory psychomotor reaction times, observational art-therapy video ratings, ambient home sensor alerts, or conversational empathy scores. None record a digital user interface dynamically selecting between a visual hint, verbal simplification, or pacing adjustment in response to real-time cognitive interaction.

### Governance Mandate
1. **Supervised ML for Prompt/Cue Selection is Currently Not Defensible Using External Data Alone.** Attempting to train a classifier to predict AS01–AS09 using synthetic target mappings or proxy clinical scores (e.g., mapping slow reaction times directly to "AS04 Visual Hint") represents pseudo-scientific data hallucination and poses patient safety risks.
2. **Current Operational Modality:** NeuroCompanion Phase 4 operates in a **Deterministic Rule-Based & Heuristic Pacing Engine** (`Backend/ml/api/fallbacks.py`) with strict patient agency (explicit requests always take precedence) and caregiver oversight.
3. **Future ML Foundation:** Supervised assistance prediction will be enabled exclusively through an authentic, patient-consented **4-Tier Native Telemetry Collection Pipeline** built natively into the NeuroCompanion platform.

---

## 2. Category-by-Category Forensic Evidence Matrix (AS01–AS10)

| Code | Assistance Modality | External Dataset Relevance | Actual Available Variables | Label Status | Supervised ML Defensible Today? | Operational Fallback & Heuristic Signal |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **AS01** | **Repeat Instruction** | Robot Cognitive, AI4HOPE | Robot vocal prompt templates (`data&prompts.zip`), AI4HOPE user question complexity | **None (Indirect)** | **No** (No in-app trigger labels) | Triggered immediately upon patient verbal request ("What?", "Repeat") or acoustic silence exceeding empirical pacing threshold ($>2.5\times$ baseline). |
| **AS02** | **Simplify Instruction** | AI4HOPE | Lexical readability metrics (FKGL, Flesch Reading Ease), text lengths, expert empathy/actionability scores | **Indirect (Text only)** | **No** (Predicts quality, not trigger state) | Activated if patient experiences repeated errors ($k \ge 2$) or explicit confusion; AI4HOPE-trained NLP filters response to $\le 6$th-grade reading level. |
| **AS03** | **Step-by-Step Guidance** | CASAS Cognitive Assessment | Task step sequences (`activityscores.txt`), task tier (1: independent, 2: assisted, 3: degraded) | **Indirect (Physical ADL)** | **No** (Physical smart-home cues $\neq$ UI cues) | Triggered when multi-step cognitive tasks exhibit step-omission or pause duration $>15.0\text{ s}$ during state transitions. |
| **AS04** | **Visual Hint** | Robot Cognitive, VASE | Touch coordinates, icon bounding boxes, visual engagement fixation drops | **None** | **No** (No visual cue interaction logs) | Triggered if patient gaze or touch interaction stagnates on a single screen area for $>8.0\text{ s}$ without input. High-contrast pulsing boundary. |
| **AS05** | **Demonstration** | Robot Cognitive Training | Video/animation demonstration logs in tablet exercises | **None** | **No** (No demonstration need labels) | Triggered only after 2 consecutive incorrect task attempts, offering a 5-second simulated interaction walk-through. |
| **AS06** | **Slower Speech** | AI4HOPE, Robot Cognitive | Audio prompt durations, audio sampling rates | **None** | **No** (Speech rate is fixed in datasets) | Modulates TTS engine speed multiplier from $1.0\times \to 0.8\times$ upon explicit patient command or elevated age/impairment severity profile. |
| **AS07** | **Additional Response Time** | N-Back Dementia, MSL, CASAS | Reaction time distributions ($777.6\text{ ms} \to 1,303.9\text{ ms}$ in N-Back; $400\text{--}650\text{ ms}$ in MSL; pause durations in CASAS) | **Indirect (Empirical distributions)** | **Pacing Heuristic Only** (Distribution prior) | Dynamically relaxes UI timeout thresholds by $+50\%\text{--}100\%$ based on observed psychomotor latency distributions without failing the task. |
| **AS08** | **Break or Pause** | VASE, Brain-IT | Second-by-second disengagement dips (VASE); 12-week drop-off & session fatigue (Brain-IT) | **Indirect (Session dynamics)** | **Heuristic Threshold Only** | Offered if continuous session duration $>20\text{ minutes}$ or if negative engagement trends persist over $>60\text{ seconds}$ of observation. |
| **AS09** | **Verbal Explanation** | AI4HOPE | Expert caregiver conversational explanations, empathy annotations ($N=600$) | **Indirect (Text quality)** | **No** (Evaluates output text, not UI trigger) | Provides contextual "why" scaffolding using AI4HOPE validated compassionate phrasing when patient hesitates on task purpose. |
| **AS10** | **Independent Completion** | CASAS Cognitive Assessment | `score` column (5 = independent unprompted), `tier` = 1 | **Direct in Physical ADL; None in UI** | **Task Level Only** (Not real-time UI) | Baseline state. Engine observes silently with zero intervention as long as interaction latency and accuracy remain within patient envelope. |

---

## 3. Deep-Dive Analytical Audit by Modality

### AS01 — Repeat Instruction
- **Clinical Role:** Restates the current instruction verbatim without penalizing patient score or breaking conversational rapport.
- **Available Evidence:** Robot Cognitive Training contains audio prompts (`Audio/` subdirectories) and prompt strings across 4 game conditions. AI4HOPE contains 600 dialog turns.
- **Data Gap:** Neither records whether a prompt repetition was necessary, requested, or effective.
- **Implementation Strategy:** Deterministic fallback triggered by speech recognition of repetition intent ("repeat", "again", "say it once more") or persistent silence after instruction delivery.

### AS02 — Simplify Instruction
- **Clinical Role:** Reduces cognitive load by decomposing syntactic complexity, shortening sentence length, and utilizing high-frequency familiar vocabulary.
- **Available Evidence:** AI4HOPE provides 600 multi-turn responses with verified expert caregiver quality ratings. Text feature extractors (`Backend/ml/features/text_features.py`) compute lexical density, syllables per word, and Flesch-Kincaid grade level.
- **Data Gap:** AI4HOPE annotates *general conversational quality*, not a patient's momentary cognitive failure requiring syntactic simplification.
- **Implementation Strategy:** When error counters trip or user indicates non-comprehension, the system transforms instructions using validated lexical filters ($\le 8$ words per clause, active voice).

### AS03 — Step-by-Step Guidance
- **Clinical Role:** Breaks complex multi-action tasks into sequential atomic actions with explicit confirmation gates.
- **Available Evidence:** CASAS smart-home dataset records 8 multi-step cooking, cleaning, and medication activities across 339 participants. It records sequential sensor firing and step completion.
- **Data Gap:** CASAS captures ambient physical movements in an instrumented apartment; it does not reflect digital tablet/desktop interaction workflows.
- **Implementation Strategy:** Rule-based decomposition where complex memory exercises are gated sequentially upon pause intervals $>15\text{ s}$.

### AS04 — Visual Hint
- **Clinical Role:** Highlights target UI elements, dims background distractions, or provides subtle visual iconography.
- **Available Evidence:** Robot Cognitive Training records touch x/y screen coordinates and tap events across 437 game sessions.
- **Data Gap:** The dataset does not label when a visual hint was surfaced or distinguish spontaneous discovery from prompted discovery.
- **Implementation Strategy:** Spatial gaze/touch stagnation tracker triggers a pulsating border on target elements after 8 seconds of inactivity.

### AS05 — Demonstration
- **Clinical Role:** Displays an animated avatar or cursor showing exactly how to execute the action.
- **Available Evidence:** Video demonstration protocols mentioned in literature for Alzheimer's Tablet RCT and Robot Cognitive Training.
- **Data Gap:** Raw animation interaction logs and user success rates post-demonstration are absent.
- **Implementation Strategy:** High-tier support invoked strictly after 2 consecutive failed attempts, preserving patient autonomy.

### AS06 — Slower Speech
- **Clinical Role:** Reduces synthesized speech pacing to match declining auditory processing speed in cognitive decline.
- **Available Evidence:** Acoustic timings in speech synthesis research; healthy vs impaired listening comprehension literature.
- **Data Gap:** No dataset links dynamic TTS rate adjustment to patient comprehension outcomes in real time.
- **Implementation Strategy:** Pacing slider configuration accessible to patient and caregiver; default rate drops to $0.85\times$ upon detection of elevated patient response latency.

### AS07 — Additional Response Time
- **Clinical Role:** Prevents premature timeout failure by widening the interaction latency window.
- **Available Evidence:**
  - **N-Back Dementia:** Mean reaction time in mild cognitive impairment is $777.6\text{ ms}$; in moderate impairment, it increases to $1,303.9\text{ ms}$ (a $+67.7\%$ increase; $p < 0.001$).
  - **Motor Sequence Learning:** Healthy older adults display psychomotor tapping intervals between $400\text{--}650\text{ ms}$.
  - **CASAS:** Mean inter-step pause duration expands from $4.2\text{ s}$ in healthy elders to $14.8\text{ s}$ in dementia subjects.
- **Defensible Use:** Empirical latency bounds directly inform the `LatencyPacingEstimator` (`Backend/ml/features/latency_features.py`). This is **not** a predictive black-box model, but a mathematically calibrated empirical prior preventing frustrating false-negative timeouts.

### AS08 — Break or Pause
- **Clinical Role:** Detects cognitive fatigue, distress, or disengagement, prompting a therapeutic pause or calming exercise.
- **Available Evidence:**
  - **VASE:** 44,400 second-by-second observer engagement scores illustrate rapid engagement state shifts during art activities.
  - **Brain-IT:** 12-week longitudinal adherence demonstrates that sessions exceeding 25 minutes incur higher abandonment rates.
- **Implementation Strategy:** Session timer alerts patient at 20-minute mark; prolonged zero-interaction periods trigger a gentle "Would you like to take a short rest?" prompt.

### AS09 — Verbal Explanation
- **Clinical Role:** Offers compassionate reassurance explaining why a task is being performed, reducing agitation.
- **Available Evidence:** AI4HOPE dataset provides 600 examples of empathetic caregiver dialogue, rated for emotional validation and non-judgmental tone.
- **Implementation Strategy:** Conversational model trained on AI4HOPE provides explanatory responses when patient expresses confusion or frustration.

### AS10 — Independent Completion
- **Clinical Role:** The ideal patient operating state: successful task execution without active machine intervention.
- **Available Evidence:** CASAS dataset labels `score = 5` and `tier = 1` for 1,180 of 2,074 task instances (unassisted independent completion).
- **Implementation Strategy:** Engine maintains a silent observer state; logs telemetry for longitudinal progress tracking without interrupting flow.

---

## 4. Native 4-Tier Telemetry Collection Strategy

To bridge the gap between external research and true machine-learned adaptive assistance, NeuroCompanion Phase 4 defines a strict, 4-tier native telemetry architecture. This data pipeline is implemented in `Backend/ml/features/telemetry_schema.py` and isolates patient interaction data into secure, privacy-preserving schemas.

```
+-----------------------------------------------------------------------------------+
|                        NEUROCOMPANION NATIVE TELEMETRY PIPELINE                    |
+-----------------------------------------------------------------------------------+
                                          |
    +-------------------------------------+------------------------------------+
    |                                                                          |
    v                                                                          v
[ TIER 1: PATIENT TELEMETRY ]                                [ TIER 2: PATIENT CUE RESPONSE ]
- Millisecond Latencies (First/Total)                        - Explicit User Requests ("repeat")
- Interaction Counts (Clicks, Touches)                       - Cue Delivery Timestamps (AS01-AS10)
- Hesitation Pauses (>2000ms intervals)                      - Acceptance / Dismissal Events
- Syntactic Input Features                                   - Post-Cue Latency Delta
    |                                                                          |
    +-------------------------------------+------------------------------------+
                                          |
                                          v
                         [ TIER 3: CAREGIVER ANNOTATION ]
                         - Human-in-the-Loop Validation
                         - Session Effectiveness Rating (1-5)
                         - Assistance Appropriateness Flag
                         - Diagnostic Note & Adjustment Override
                                          |
                                          v
                          [ TIER 4: MODEL INFERENCE RECORD ]
                          - Shadow Mode Logging
                          - Heuristic Output vs ML Candidate
                          - Feature Snapshot at Decision Point
                          - Latency & Resource Overhead Metrics
```

### Tier 1: Patient Behavioral Telemetry (`PatientBehavioralTelemetry`)
Captures raw, non-invasive digital interaction signals without recording sensitive audio or PII:
- `first_interaction_latency_ms`: Time from stimulus presentation to first touch/keystroke.
- `total_completion_latency_ms`: Cumulative time to submit an answer.
- `pause_count`: Number of pauses exceeding 2.0 seconds.
- `max_pause_duration_ms`: Duration of the longest single hesitation period.
- `error_count`: Number of incorrect selections before final submission.
- `trajectory_irregularity`: Spatial jitter or erratic cursor/touch motion indicating motor hesitation.

### Tier 2: Patient Cue Response (`PatientCueResponse`)
Records the dynamic interaction between the engine's assistance and the patient's reaction:
- `cue_id`: Unique identifier for the assistance instance.
- `cue_type`: Selected assistance modality (`AS01` through `AS10`).
- `delivery_trigger`: Deterministic rule (`explicit_request`, `latency_timeout`, `error_threshold`).
- `patient_reaction`: Explicit acceptance, passive utilization, or explicit dismissal.
- `subsequent_accuracy`: Whether the immediate next action was correct ($1$) or incorrect ($0$).
- `post_cue_latency_ms`: Time required to respond following assistance delivery.

### Tier 3: Caregiver Session Annotation (`CaregiverSessionAnnotation`)
Provides high-fidelity clinical ground truth through asynchronous caregiver/clinician review:
- `session_id`: Associated session identifier.
- `caregiver_id`: Verified caregiver or healthcare provider identifier.
- `perceived_engagement_score`: 5-point Likert scale of patient immersion and comfort.
- `assistance_appropriateness_rating`: Evaluation of whether the automated assistance was timely or intrusive.
- `over_assistance_flag`: Boolean indicating whether the engine intervened too early.
- `under_assistance_flag`: Boolean indicating whether the patient was left struggling.
- `clinical_notes`: Qualitative contextual observations (e.g., "Patient was tired today due to poor sleep").

### Tier 4: Model Inference Record (`ModelInferenceRecord`)
Maintains an immutable audit log for model development, shadow testing, and algorithmic governance:
- `inference_id`: Cryptographic identifier.
- `timestamp`: UTC millisecond timestamp.
- `active_engine_mode`: `deterministic_fallback`, `shadow_ml`, or `supervised_ml`.
- `input_feature_snapshot`: Normalized feature vector fed to the inference pipeline.
- `heuristic_recommendation`: Output generated by the deterministic fallback manager.
- `ml_candidate_recommendation`: Output proposed by candidate ML models running in shadow mode.
- `decision_divergence_flag`: Boolean indicating if ML diverged from deterministic rules.
- `execution_latency_ms`: Total execution time of the inference call (must remain $<50\text{ ms}$).

---

## 5. Transition Criteria: From Deterministic Rules to Supervised ML

To safeguard patient safety and uphold clinical integrity, supervised ML models will **not** be deployed into active clinical control for AS01–AS10 until the following quantitative transition thresholds are satisfied within the native telemetry database:

1. **Sample Size Requirements:**
   - Minimum of $N \ge 100$ unique patients across MCI and mild AD cohorts.
   - Minimum of $M \ge 5,000$ labeled assistance interaction instances spanning Tier 1, Tier 2, and Tier 3 annotations.
2. **Participant Separation:**
   - Strict `GroupKFold` cross-validation grouped by `patient_id` with zero patient leakage across folds.
3. **Statistical Superiority Over Baseline:**
   - Candidate ML model must demonstrate statistically significant improvement ($p < 0.01$, Wilcoxon signed-rank test) in macro F1-score and Quadratic Weighted Kappa over the deterministic baseline.
   - False-positive intervention rate (unnecessary interruption of independent completion AS10) must be $< 5\%$.
4. **Caregiver Oversight Safety Gate:**
   - In accordance with medical device governance, caregivers retain the ability to set global sensitivity caps and override any model-recommended assistance tier at any time.

---

## 6. Verification and Implementation Mapping

The principles detailed in this evidence matrix are codified directly into the Phase 4 codebase:
- **Fallback Implementation:** `Backend/ml/api/fallbacks.py` implements the deterministic rule set.
- **Telemetry Schemas:** `Backend/ml/features/telemetry_schema.py` defines the 4-tier data structures.
- **Latency Bounds:** `Backend/ml/features/latency_features.py` enforces empirical psychomotor bounds derived from N-Back and MSL.
- **Unit Tests:** `Backend/tests/test_phase4_ml.py` verifies fallback determinism and schema validation.
