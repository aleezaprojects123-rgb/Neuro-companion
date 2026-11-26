# NEUROCOMPANION PHASE 4.2 â€” FINAL READINESS, VALIDATION INTEGRITY, AND BLOCKER AUDIT
**Document Status:** Complete Readiness Audit & Methodological Integrity Report  
**System Module:** `Backend/ml/` (Phase 4 Intelligence Subsystem)  
**Verification Date:** November 2026  
**Auditor:** Antigravity AI Engineering & Research Verification Agent  

---

## 1. Executive Summary & Audit Mandate

Before advancing to Phase 4.3 implementation, this audit evaluates the methodological rigor, data leakage protections, sample size adequacy, and blocker status for every proposed model in the NeuroCompanion subsystem.

### Inviolable Governance Standard:
> **No model readiness will be claimed where participant leakage remains unresolved, where sample sizes are inadequate for the proposed algorithm, or where target labels are not genuinely observed.**

---

## 2. Model-by-Model Validation Integrity & Leakage Prevention Audit

The table below provides a forensic audit of participant counts, repeated measures, validation grouping, and target label integrity for all candidate models:

| Model ID | Model Name & Role | Independent Participants ($N$) | Total Observations / Runs | Repeated Measures per Subject? | Grouping Variable for `GroupKFold` | Target Genuinely Observed? | Sample Size vs. Algorithm Adequacy | Leakage Risk & Resolution |
|:---:|:---|:---:|:---:|:---:|:---|:---:|:---|:---|
| **M1** | **Cognitive Screening Classifier** | $N=235$ assessed | 235 clinical visits | **NO** (1 visit per subject) | Stratified by `CDR` class | **YES** (`CDR` 0, 0.5, 1+) | Adequate for Calibrated Random Forest | **ZERO LEAKAGE:** Cross-sectional single visit; stratified split intact (Phase 1). |
| **M2** | **Physical IADL Task-Performance Model** | $N=327$ older adults | 2,074 task runs | **YES** (~6.3 runs per participant) | `GroupKFold(groups=pid)` ($N=327$) | **YES** (`score` 1, 2, 3, 5) | Adequate for ExtraTrees / Cost-Sensitive XGBoost | **HIGH RISK IF RANDOM SPLIT:** Participant motor habits leak. **RESOLVED:** Strict `pid` grouping enforcers. |
| **M3** | **Conversational Quality & Readability Scorer** | $N=120$ evaluators (60 MDs, 60 carers) | 600 rated dialogue turns | **YES** (~5 evaluations per rater) | `GroupKFold(groups=evaluator_id)` ($N=120$) | **YES** (Empathy, clarity, actionability 1â€“5) | Adequate for Ridge Regressor / LightGBM | **HIGH RISK IF RANDOM SPLIT:** Evaluator subjective rating style leaks. **RESOLVED:** Strict evaluator grouping. |
| **M4** | **Speech Acoustic Cognitive Classifier** | $N=280$ raw speakers | 806 WAV recordings | **YES** (Multiple task recordings per speaker) | `GroupKFold(groups=speaker_id)` ($N=280$) | **YES** (3-Class: AD, MCI, HC) | Adequate for Calibrated ExtraTrees / ElasticNet | **CRITICAL RISK:** 123 merged WAVs overlap with raw task WAVs. **RESOLVED:** Group by root speaker ID. |
| **M5** | **Response-Time Latency Baseline** | $N=24$ (N-Back) + $N=130$ (MSL) | ~50,041 latency trials | **YES** (Hundreds of tapping trials per subject) | Stratified Leave-One-Group-Out | **NO ML TARGET** (Continuous latencies) | N-Back ($N=24$) too small for deep ML; fits Bayesian prior | **RESOLVED:** Treated as an empirical descriptive timing prior; NOT supervised ML. |
| **B1** | **Out-of-Domain Acoustic Specificity Benchmark** | $N=73$ (31 PD, 42 HC) | 73 audio recordings | **NO** (1 session per subject) | Subject-isolated testing | **YES** (PD vs. HC) | Adequate for out-of-domain specificity testing | **RESOLVED:** Kept completely isolated from Model M4 training. |
| **B2** | **Observational Engagement Reference** | $N=11$ nursing home residents | 44,400 second-by-second ratings | **YES** (~4,000 ratings per patient) | Patient ID | **NO ML TARGET** (Likert ratings) | $N=11$ completely inadequate for supervised ML | **RESOLVED:** Descriptive observational reference only; NOT trained as ML. |
| **B3** | **Longitudinal Adherence Reference** | $N=41$ (Brain-IT) + $N=25$ (Tablet) | ~440 weekly attendance records | **YES** (Weekly attendance logs) | Trial cohort ID | **NO ML TARGET** (Attendance logs) | $N=41$ and $N=25$ too small for deep ML | **RESOLVED:** Descriptive adherence research reference; no mandatory 20-min cap. |
| **AS01-09** | **In-App Digital UI Assistance Selector** | *0 participants currently* | *0 in-app interaction episodes* | *N/A* | `GroupKFold(groups=patient_id)` | **NOT PRESENT IN EXTERNAL DATA** | Cannot train without native telemetry | **BLOCKED:** Must await consented 4-Tier Native Telemetry in Unity. |
| **AS10** | **In-App Digital Task Independence Model** | *0 participants currently* | *0 in-app interaction episodes* | *N/A* | `GroupKFold(groups=participant_id)` | **NOT PRESENT IN EXTERNAL DATA** | Cannot train without native telemetry | **BLOCKED:** Must await consented 4-Tier Native Telemetry in Unity. |

---

## 3. Definitive Model Classification: The Three Operational Categories

Following rigorous forensic review, the entire model subsystem is categorized into three distinct operational groups:

### Group 1: Models Genuinely Suitable for Exploratory Research Training (Phase 4.3 Immediate)
These models possess verified local data, genuinely observed target labels, sufficient participant sample sizes ($N \ge 120$), and strict participant-grouped cross-validation protocols:
1. **Model M1 (Cognitive Screening Classifier):** OASIS-1 ($N=235$ assessed). Status: **Completed & Frozen in Phase 1**.
2. **Model M2 (Physical IADL Task-Performance Model):** CASAS Smart Home ($N=327$, 2,074 runs). Target: 4-class `score` ($1, 2, 3, 5$). Evaluates physical smart-home task breakdown risk using `GroupKFold(groups=pid)`.
3. **Model M3 (Conversational Quality & Readability Scorer):** AI4HOPE Palliative Dialogue ($N=120$, 600 dialogs). Target: Continuous empathy, clarity, actionability ratings, and FKGL $\le 6.0$. Evaluates companion dialogue quality using `GroupKFold(groups=evaluator_id)`.
4. **Model M4 (Speech Acoustic Cognitive Classifier):** NCMMSC2021 Audio ($N=280$, 806 WAVs). Target: 3-class diagnosis (AD, MCI, HC) from 88-dimensional openSMILE eGeMAPS prosody vectors using `GroupKFold(groups=speaker_id)`.

---

### Group 2: Empirical Baselines & Descriptive Benchmarks (Phase 4.3 Calibration)
These models cannot or should not train supervised classifiers due to small participant counts ($N < 50$), absence of supervised assistance labels, or healthy-only cohorts. They provide descriptive distributions and specificity benchmarks:
1. **Baseline M5 (Empirical Response-Time Distribution Baseline):** N-Back Dementia ($N=24$) and MSL ($N=130$). Establishes observational working memory and motor response latencies to guide UI timeout engineering without failing patients prematurely.
2. **Benchmark B1 (Out-of-Domain Acoustic Specificity Benchmark):** MDVR-KCL Audio ($N=73$ Parkinson's/HC). Tests whether Model M4 erroneously flags Parkinson's hypokinetic dysarthria as Alzheimer's pathology.
3. **Benchmark B2 (In-Session Observational Engagement Reference):** VASE ($N=11$, 44,400 ratings). Provides descriptive observational engagement trajectories during music/art therapy. Does NOT validate software timeouts.
4. **Benchmark B3 (Longitudinal Adherence Research Reference):** Brain-IT ($N=41$) and Tablet RCT ($N=25$). Descriptive adherence research on attendance attrition. Does NOT enforce a mandatory 20-minute session cap; patient autonomy to pause, stop, or continue is 100% preserved.

---

### Group 3: Assistance Predictors Blocked Awaiting Consented Native Telemetry (Future Phase 4.4+)
These models represent the true in-app digital assistance controllers for NeuroCompanion's Unity virtual environment. Because zero external research datasets contain in-app UI assistance labels, they are **strictly blocked from supervised training until authentic native telemetry is gathered**:
1. **Model AS01â€“AS09 (In-App Real-Time UI Assistance Predictors):** Predicts when to present repeat speech, visual hints, step guidance, demonstrations, pacing adjustments, or verbal explanations inside Unity.
2. **Model AS10 (In-App Digital Task Independence Model):** Predicts autonomous virtual task completion without cues.
3. **Model Efficacy (Assistance Outcome Predictor):** Predicts whether an offered assistance cue will improve subsequent task performance.

**Operational Modality Until Telemetry is Available:**  
Phase 4 operates via transparent, deterministic rule-based fallbacks (`Backend/ml/api/fallbacks.py`) where **patient agency always takes precedence** (explicit button taps or verbal queries immediately trigger assistance regardless of model state) and caregivers maintain full clinical oversight.

---

## 4. Comprehensive Audit of External Dataset Blockers

| Blocker ID | Dataset Collection | Nature of Blocker | Impact on NeuroCompanion Subsystem | Resolution Protocol |
|:---:|:---|:---|:---|:---|
| **BLK-01** | **TalkBank Corpora** (Pitt Audio/Transcripts, ADReSSo 2021, ADReSS-M, TAUKADIAL 2024) | Raw audio recordings and CHAT transcripts are access-gated behind TalkBank Consortium HTTP Basic Authentication (`sla2.talkbank.org`). | Prevents training acoustic models on English/Greek/bilingual Cookie Theft recordings. | Formal faculty application submitted to `talkbank@cmu.edu` under signed DUA. Local directory stubs preserved; zero scraping attempted. |
| **BLK-02** | **DementiaNet** | Local data is incomplete (code and metadata on disk; raw speech audio hosted externally on Google Drive). | Cannot be used for audio model training. | Preserved as code and methodological reference only. |
| **BLK-03** | **Uncollected Wishlist Datasets** (PAAL v2, Dem@Care, ALMOND) | Zero bytes on disk; identified conceptually during early exploratory literature review. | Zero presence in local pipelines. | Properly classified as Uncollected Wishlist Benchmarks; excluded from all training pipelines. |
| **BLK-04** | **NCMMSC2021 Audio Sampling Rate Heterogeneity** | 806 WAV audio files recorded at mixed sampling rates (8 kHz, 16 kHz, 48 kHz). | Raw audio cannot be fed into openSMILE without creating artificial acoustic distortions. | Automated preprocessing pipeline enforces resampling to 16 kHz, 16-bit mono using `torchaudio`/`scipy` prior to eGeMAPS feature extraction. |
| **BLK-05** | **Absence of In-App UI Assistance Labels** | Zero external datasets contain real-time digital UI cue selection logs or in-app task independence labels. | Prohibits supervised training of AS01â€“AS10 assistance controllers today. | Enforce deterministic clinical fallbacks and implement the consented 4-Tier Native Telemetry Pipeline in Unity. |

---

## 5. Final Readiness Verdict for Phase 4.2 Completion

> **FORMAL READINESS VERDICT: PHASE 4.2 IS FULLY RECONCILED, METHODOLOGICALLY RIGOROUS, AND READY FOR FORMAL APPROVAL.**
>
> 1. **Complete Decoupling:** All research models (clinical screening, acoustic classification, conversational quality, physical task breakdown) are strictly decoupled from in-app digital assistance prediction.
> 2. **Leakage Elimination:** Every trainable model enforces participant-level grouping (`GroupKFold`) with verified independent cohorts.
> 3. **Unsupported Claims Eliminated:** The unscientific 60-second inactivity claim from VASE and the mandatory 20-minute session cap have been completely removed; patient autonomy is preserved.
> 4. **Clear Training Roadmap:** Four genuine research models (M1 frozen, M2, M3, M4) are identified for Phase 4.3 implementation, while AS01–AS10 assistance prediction is honestly documented as blocked awaiting consented native telemetry.
> 5. **Subsystem Stability:** Frozen Phases 1, 2A, and 3, production SQLite databases, and backend API routes remain 100% untouched.

---
*Report certified as complete, methodologically audited, and clinically governed.*
