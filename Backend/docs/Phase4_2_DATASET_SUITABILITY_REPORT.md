# NEUROCOMPANION PHASE 4.2 â€” DATASET SUITABILITY REPORT
**Document Status:** Complete Forensic Evaluation & Machine Learning Suitability Audit  
**System Module:** `Backend/ml/` (Phase 4 Intelligence Subsystem)  
**Verification Date:** November 2026  
**Auditor:** Antigravity AI Engineering & Research Verification Agent  

---

## 1. Executive Summary & Evaluation Framework

In Phase 4.1, a rigorous forensic inventory audited 20 distinct research dataset collections across canonical and secondary storage locations, verifying their file integrity, sheet dimensions, participant demographics, variables, and licensing restrictions.

In this report, Phase 4.2 systematically evaluates the **machine learning suitability** of each audited collection to support the NeuroCompanion unified AI intelligence architecture.

### Suitability Classification Criteria
To ensure scientific rigor, avoid data hallucination, and respect clinical governance boundaries, each dataset is assigned one of four definitive training suitability classifications:

- **Class A â€” Suitable for a Specific ML Training Task:**  
  The dataset possesses a sufficiently large, validated sample ($N \ge 50$), authentic ground-truth target labels, well-defined input features, and an appropriate open license permitting local model training. It can directly train a specialized machine learning model for a specific clinical or interactional sub-task.
- **Class B â€” Suitable for Supplementary Features or Empirical Research:**  
  The dataset cannot or should not train an independent supervised classifier (due to small participant sample size $N < 50$, class imbalance, healthy-only cohort, or absence of digital UI trigger labels), but provides vital empirical priors, physiological latency distributions, normative feature baselines, or heuristic pacing thresholds.
- **Class C â€” Suitable Only for Methodological Reference or Background:**  
  The dataset evaluates an out-of-domain protocol (e.g., non-interactive museum object handling, ambient art therapy) that cannot be linked to digital companion interaction, but informs clinical qualitative understanding.
- **Class D â€” Not Currently Suitable:**  
  The dataset is access-restricted behind institutional consortium data-sharing agreements (TalkBank SLA), is incomplete on local disk (audio external), or represents an uncollected candidate wishlist item (0 bytes downloaded).

---

## 2. Summary Suitability Classification Matrix

| # | Dataset Collection | Primary Location | Population & Verified Sample | Key Verified Variables | Suitability Class | Specific Role in Phase 4 ML Architecture |
|:---:|:---|:---|:---|:---|:---:|:---|
| **1** | **OASIS-1** | `Backend/data/raw/` | $N=436$ raw; **$N=235$ assessed** (135 CDR 0, 70 CDR 0.5, 30 CDR 1+) | Age, Sex, Educ, MMSE, CDR | **Class A** | **Cognitive Impairment Screening Model** (Trained & frozen in Phase 1). |
| **2** | **CASAS Cognitive Assessment** | `research_archive/CASAS/` | $N=327$ older adults, **2,074 task runs** across 8 IADLs | Sensor events, duration, pauses, `score` (1=indep, 2=prompted, 3/5=error) | **Class A** | **Task Performance & Prompting Predictor** (Supervised prediction of task failure/assistance need in sequential tasks). |
| **3** | **TIHM for Dementia** | `research_archive/TIHM/` | $N=54$ dementia households, longitudinal IoT | Environmental motion, bed sensors, sleep patterns, clinical alerts | **Class B** | **Longitudinal Circadian & Distress Priors** (Empirical baseline for session scheduling and distress escalation). |
| **4** | **AI4HOPE Palliative Care** | `research_archive/AI4HOPE/` | $N=120$ evaluators, **600 rated dialogue turns** | Question/answer text, readability, empathy (1â€“5), clarity, actionability | **Class A** | **Conversational Quality & Readability Scorer** (NLP regression to score prompt empathy, clarity, and reading ease $\le 6$th grade). |
| **5** | **Brain-IT Exergame RCT** | `research_archive/Brain-IT RCT/` | $N=41$ older adults (21 intervention, 20 control), 12 weeks | Weekly attendance, minutes per session, cognitive battery scores | **Class B** | **Longitudinal Adherence & Fatigue Distribution** (Empirical calibration of 20-minute session fatigue cap for AS08). |
| **6** | **VASE Dementia Engagement** | `research_archive/VASE/` | $N=11$ late-stage dementia patients, 48 video clips | **44,400 second-by-second** 1â€“7 Likert observational engagement ratings | **Class B** | **In-Session Disengagement Trajectory Prior** (Fine-grained trajectory calibration for interaction stagnation; $N=11$ restricts deep ML). |
| **7** | **Alzheimer's Tablet RCT** | `research_archive/` | $N=25$ mild/moderate AD (13 intervention, 12 control), 6 months | Pre/post neuropsychological scores, tablet adherence | **Class B** | **Longitudinal Intervention Trajectory Calibration** (Empirical rate of cognitive maintenance in tablet games). |
| **8** | **Canterbury Wellbeing Scales** | `research_archive/Phase4_New_Datasets/` | 201 session evaluations (92 AD, 37 VaD, 35 Mixed) | Visual Analogue Scale wellbeing before/after handling museum objects | **Class C** | **Methodological Reference** (Qualitative baseline for wellbeing dynamics in dementia care). |
| **9** | **Robot Cognitive Training** | `research_archive/Phase4_New_Datasets/` | **$N=20$ Healthy Older Adults** (MMSE $28.65$; **ZERO dementia**) | 437 game runs, touch coordinates ($X, Y$), prompt audio WAVs | **Class B** | **Normative Healthy Interaction Baseline** (Healthy psychomotor benchmarks; forbidden to pool into dementia diagnostic models). |
| **10** | **Motor Sequence Learning (MSL)** | `research_archive/Phase4_New_Datasets/` | $N=130$ lifespan (**$N=32$ healthy older adults**, ages 60â€“80) | 50,000+ key-press latencies ($450\text{--}650\text{ ms}$) | **Class B** | **Psychomotor Pacing Prior** (Normative motor speed prior for the `LatencyFeatureExtractor`). |
| **11** | **N-Back Dementia** | `research_archive/Phase4_New_Datasets/` | **$N=24$ mild/moderate dementia patients** | Working memory RT ($777.6\text{ ms} \to 1,303.9\text{ ms}$), tDCS conditions | **Class B** | **Cognitive Response Pacing Prior** (Empirical prior for AS07 dynamic timeout dilation; $N=24$ prevents high-capacity neural training). |
| **12** | **MDVR-KCL Audio** | `digital life analyzer/data/mdvr_kcl/` | **$N=73$ (31 Parkinson's, 42 Controls; ZERO dementia)** | 73 audio recordings (read text & spontaneous speech) | **Class B** | **Out-of-Domain Acoustic Specificity Benchmark** (Used strictly to test that acoustic models do not misclassify Parkinson's dysarthria as AD). |
| **13** | **DementiaBank Pitt Metadata** | `research_archive/Phase4_New_Datasets/` | ~300 patients, **512 clinical visits** | Longitudinal MMSE, Blessed, CERAD, CDR stage | **Class B** | **Longitudinal Cognitive Decline Mapping Prior** (Links clinical cognitive stage to expected instructional comprehension limits). |
| **14** | **Chinese AD Dataset 1** | `digital life analyzer/data/chinese_dataset_1/` | **401 sessions** (AD and Healthy Controls) | 88 openSMILE eGeMAPS acoustic features + 401 dialogue TSVs | **Class A** | **Acoustic & Linguistic Impairment Classifier** (Supervised binary classification of cognitive decline from acoustic/dialogue features). |
| **15** | **NCMMSC2021 AD Audio** | `digital life analyzer/data/ncmmsc2021/` | **280 participants, 806 verified WAVs** (4.93 GB) | Continuous spontaneous speech, hesitations, silence ratios, pitch | **Class A** | **Acoustic Cognitive Impairment Screening Model** (Supervised 3-class classifier: AD vs MCI vs HC from openSMILE eGeMAPS vectors). |
| **16** | **DementiaNet** | `digital life analyzer/data/dementianet/` | Partial (Code & MIT license on disk; audio on Google Drive) | Public figure longitudinal metadata | **Class D** | **Not Currently Suitable** (Local audio files absent; requires external Google Drive download). |
| **17** | **DementiaBank Pitt Corpora** | TalkBank (`sla2.talkbank.org`) | ~500 sessions (AD, MCI, Control) | Audio WAVs & CHAT transcripts of Cookie Theft task | **Class D** | **Not Currently Suitable** (Gated behind TalkBank Consortium HTTP Basic Authentication; access agreement required). |
| **18** | **ADReSSo 2021** | Interspeech Challenge | 237 audio recordings | Acoustic speech diagnosis, progression, and MMSE regression | **Class D** | **Not Currently Suitable** (Gated behind Interspeech Challenge DUA). |
| **19** | **ADReSS-M** | Interspeech Challenge | Multilingual audio (English + Greek) | Cross-lingual acoustic Alzheimer's classification | **Class D** | **Not Currently Suitable** (Gated behind Interspeech Challenge DUA). |
| **20** | **TAUKADIAL 2024** | Interspeech Challenge | Bilingual audio (English + Mandarin) | Spontaneous dialogue, clinical risk assessment | **Class D** | **Not Currently Suitable** (Gated behind Interspeech Challenge DUA). |
| *21* | *PAAL ADL Accelerometry v2* | *Uncollected Wishlist Benchmark* | *0 participants on disk (0 bytes)* | *None* | **Class D** | *Uncollected Wishlist Benchmark (0 bytes downloaded).* |
| *22* | *Dem@Care Fine-Grained ADL* | *Uncollected Wishlist Benchmark* | *0 participants on disk (0 bytes)* | *None* | **Class D** | *Uncollected Wishlist Benchmark (0 bytes downloaded).* |
| *23* | *ALMOND Benchmark* | *Uncollected Wishlist Benchmark* | *0 participants on disk (0 bytes)* | *None* | **Class D** | *Uncollected Wishlist Benchmark (0 bytes downloaded).* |

---

## 3. Detailed Forensic Suitability Evaluation by Dataset

### 3.1 Class A Datasets: Directly Suitable for Supervised ML Training

#### 1. OASIS-1 Cross-Sectional MRI & Clinical
- **Supports Genuine ML Training?** YES (Already trained, calibrated, and frozen in Phase 1).
- **Actual Prediction Target:** 3-class Clinical Dementia Rating (`CDR`): Class 0 ($0.0$, Non-Demented), Class 1 ($0.5$, Very Mild Dementia), Class 2 ($\ge 1.0$, Mild-to-Moderate Dementia).
- **Available Features:** Practical screener inputs: `Age`, `M/F` (Sex), `Educ` (Education code 1â€“5), `MMSE` (Mini-Mental State Exam score 0â€“30). (MRI volumetric features `eTIV`, `nWBV`, `ASF` excluded from practical model).
- **Actual Target Labels:** `CDR` column, verified complete across all 235 eligible assessed records.
- **Population:** Older adults evaluated at Washington University Alzheimer's Disease Research Center.
- **Sample Size:** 235 assessed records (188 train / 47 test).
- **Limitations:** Cross-sectional only; single clinical visit; does not capture real-time interaction dynamics.
- **Licence & Permitted Use:** OASIS Open Access Data Use Agreement / CC-BY 4.0; commercial and non-commercial research permitted with attribution.
- **Training Suitability Verdict:** **CLASS A (COMPLETED & FROZEN)**.

---

#### 2. CASAS Cognitive Assessment Smart Home
- **Supports Genuine ML Training?** YES.
- **Actual Prediction Target:** Task-level prompting and failure prediction:
  - *Four Observed Clinical Categories:* Score 1 = Independent ($n=1,447$), Score 2 = Verbal prompt required ($n=492$), Score 3 = Incomplete/error ($n=84$), Score 5 = Severe breakdown ($n=51$).
  - *Scientific Mandate:* Scores 2, 3, and 5 must remain separate; never collapse into an artificial binary label.
- **Available Features:** Activity duration (`duration`), pause frequency (`pause_cnt_5_int`), maximum pause length (`max_pause_int`), activity type index (`act` 1â€“8), inter-sensor transition latencies.
- **Actual Target Labels:** `score` column (1, 2, 3, 5).
- **Population:** 327 community-dwelling older adults performing 8 multi-step IADLs in an instrumented smart apartment.
- **Sample Size:** 2,074 task runs ($N=327$ unique participants).
- **Limitations:** Captures physical smart-home ambient interactions (cooking, medicine dispensing), NOT screen-based virtual avatar interactions. `tier` is a post-hoc recoding.
- **Licence & Permitted Use:** Creative Commons Attribution (CC-BY 4.0); free local training and deployment.
- **Training Suitability Verdict:** **CLASS A FOR EXPLORATORY PHYSICAL-IADL RESEARCH (UNSUITABLE FOR UNITY AS01â€“AS10 ASSISTANCE)**.

---

#### 3. AI4HOPE Palliative Care Conversational AI
- **Supports Genuine ML Training?** YES.
- **Actual Prediction Target:** Continuous regression of conversational quality and empathetic phrasing:
  - `answer_rating_empathy` (1.0â€“5.0 scale).
  - `answer_rating_clarity` (1.0â€“5.0 scale).
  - `answer_rating_actionability` (1.0â€“5.0 scale).
  - Readability Grade Level constraint (predicting Flesch-Kincaid Grade Level $\le 6.0$).
- **Available Features:** Text features: word count, sentence length, lexical diversity (Type-Token Ratio), sentiment polarity, readability scores (FKGL, Flesch Reading Ease, Coleman-Liau), TF-IDF vectors / local sentence embeddings.
- **Actual Target Labels:** Expert clinical ratings and family caregiver ratings across 600 multi-turn exchanges.
- **Population:** 120 professional palliative clinicians and family caregivers.
- **Sample Size:** 600 verified interaction evaluations.
- **Limitations:** Focuses on palliative caregiver dialogue rather than software UI navigation; evaluates generated text quality, not real-time in-app trigger states.
- **Licence & Permitted Use:** Creative Commons Attribution (CC-BY 4.0); open research and commercial use with attribution.
- **Training Suitability Verdict:** **CLASS A (CONVERSATIONAL QUALITY & READABILITY NLP MODEL)**.

---

#### 4. Chinese AD Dataset 1
- **Supports Genuine ML Training?** YES.
- **Actual Prediction Target:** Binary cognitive decline screening: Alzheimer's Disease (AD) vs. Healthy Controls (HC).
- **Available Features:** Standardized 88-dimensional openSMILE extended Geneva Minimalistic Acoustic Parameter Set (eGeMAPS) feature vectors (pitch, jitter, shimmer, formant frequencies, harmonic-to-noise ratio, spectral flux) and transcript linguistic metrics (Mean Length of Utterance, vocabulary diversity).
- **Actual Target Labels:** Binary diagnosis flag recorded per session.
- **Population:** Older adults performing the standardized Cookie Theft picture description task.
- **Sample Size:** 401 session records $\times$ 88 acoustic features + 401 dialogue transcripts in TSV format.
- **Limitations:** Picture description task in Mandarin Chinese; acoustic features transfer cross-lingually (eGeMAPS acoustic prosody is largely language-independent), but surface vocabulary requires translation for NLP transfer.
- **Licence & Permitted Use:** Academic Research License; non-commercial local training permitted with citation.
- **Training Suitability Verdict:** **CLASS A (ACOUSTIC COGNITIVE DECLINE SCREENER)**.

---

#### 5. NCMMSC2021 AD Audio Competition
- **Supports Genuine ML Training?** YES.
- **Actual Prediction Target:** 3-class acoustic cognitive screening: Alzheimer's Disease (AD, $n=79$) vs. Mild Cognitive Impairment (MCI, $n=93$) vs. Healthy Controls (HC, $n=108$).
- **Available Features:** 806 high-resolution WAV audio files (4.93 GB). Standardized 88-dimensional openSMILE eGeMAPS acoustic vectors can be extracted locally from all 806 recordings.
- **Actual Target Labels:** 3 diagnostic classes encoded in official challenge labels and directory structures.
- **Population:** 280 raw Chinese older adults performing spontaneous and picture-prompted speech tasks.
- **Sample Size:** 806 WAV recordings (123 merged participant sets + 280 individual task files).
- **Limitations:** Audio files recorded at mixed sampling rates (8 kHz, 16 kHz, 48 kHz); must be resampled to 16 kHz mono prior to openSMILE feature extraction. Participant overlapping between raw and merged subsets must be strictly grouped.
- **Licence & Permitted Use:** NCMMSC Challenge Evaluation Terms; permitted for academic research and local model training; redistribution strictly forbidden.
- **Training Suitability Verdict:** **CLASS A (ACOUSTIC SPEECH CLASSIFICATION MODEL)**.

---

### 3.2 Class B Datasets: Suitable for Empirical Priors & Heuristic Calibration

#### 6. N-Back Working Memory in Dementia ($N=24$)
- **Why Not Class A:** Extremely small participant sample size ($N=24$ mild-to-moderate dementia patients). Training a supervised deep neural network on 24 individuals risks extreme overfitting and memorization of patient-specific noise.
- **Valuable Role (Class B):** Roncero et al. established that working memory reaction times in dementia exhibit a bimodal distribution: Group 1 fast responders average $777.6\text{ ms}$, while Group 2 slow responders average $1,303.9\text{ ms}$, with standard deviations up to $2,500\text{ ms}$. This empirical distribution directly parameterizes the Bayesian pacing model (`Backend/ml/features/latency_features.py`) to dynamically expand UI timeout windows (AS07) by $+50\%\text{--}100\%$ without causing artificial task failure.
- **Training Suitability:** **CLASS B (EMPIRICAL PACING DISTRIBUTION PRIOR)**.

---

#### 7. Motor Sequence Learning (MSL, $N=130$)
- **Why Not Class A:** The older cohort ($N=32$, ages 60â€“80) comprises strictly cognitively healthy individuals performing motor finger-tapping. It contains zero dementia diagnoses or software assistance labels.
- **Valuable Role (Class B):** Provides over 50,000 normative motor tapping latencies, proving that healthy older adults execute rapid motor responses within $450\text{--}650\text{ ms}$. This establishes the physiological floor for normative motor latency in the `LatencyFeatureExtractor`.
- **Training Suitability:** **CLASS B (NORMATIVE MOTOR LATENCY PRIOR)**.

---

#### 8. Robot Cognitive Training ($N=20$)
- **Why Not Class A:** $N=20$ participants, and all participants were strictly healthy older adults (MMSE $28.65 \pm 1.65$). As mandated by clinical governance, pooling healthy older adults into dementia diagnostic classifiers corrupts the clinical boundary.
- **Valuable Role (Class B):** Contains 437 game runs with millisecond touch coordinates ($X, Y$) and prompt durations. It provides the healthy benchmark for tablet cognitive game navigation and informs the spatial touch stagnation threshold ($>8.0\text{ s}$) for visual cueing (AS04).
- **Training Suitability:** **CLASS B (HEALTHY TABLET INTERACTION BENCHMARK)**.

---

#### 9. Brain-IT Exergame RCT ($N=41$)
- **Why Not Class A:** Small sample size ($N=41$ enrolled; 21 intervention, 20 control) tracking 12-week adherence to an exergame protocol.
- **Valuable Role (Class B):** Adherence and attendance records demonstrate that older adults with cognitive impairment exhibit sharp fatigue and dropout when single exercise sessions exceed $20\text{--}25\text{ minutes}$. This provides descriptive longitudinal adherence research on exercise attendance. It does NOT prove a mandatory session cap. Patient choice and autonomy to pause, stop, or continue must be 100% preserved.
- **Training Suitability:** **CLASS B (LONGITUDINAL ADHERENCE & FATIGUE CAP PRIOR)**.

---

#### 10. VASE Dementia Video Engagement ($N=11$)
- **Why Not Class A:** Extreme small participant sample size ($N=11$ late-stage dementia patients residing in nursing homes). Training a classifier on 11 individuals produces catastrophic patient-identity leakage.
- **Valuable Role (Class B):** Contains 44,400 second-by-second 1â€“7 Likert engagement ratings during music therapy. Analysis shows that moments of deep disengagement (Likert $<2.0$) persist for prolonged blocks ($>60\text{ s}$). VASE provides valuable observational engagement distributions during sensory music/art therapy. It did NOT evaluate software inactivity or screen timeouts, and must never be used to trigger automatic UI breaks or interventions.
- **Training Suitability:** **CLASS B (IN-SESSION DISENGAGEMENT DYNAMICS PRIOR)**.

---

#### 11. Alzheimer's Tablet Multimodal RCT ($N=25$)
- **Why Not Class A:** $N=25$ participants evaluated pre/post a 6-month clinical trial. Too small for supervised classification.
- **Valuable Role (Class B):** Documents cognitive maintenance trajectories over 6 months of tablet-based cognitive exercises, validating the clinical efficacy of multi-domain cognitive training in AD.
- **Training Suitability:** **CLASS B (LONGITUDINAL COGNITIVE MAINTENANCE BENCHMARK)**.

---

#### 12. TIHM for Dementia IoT Telemonitoring ($N=54$)
- **Why Not Class A:** Ambient environmental motion and bed sensors tracking dementia patients in private homes. Lacks software interaction telemetry or UI assistance labels.
- **Valuable Role (Class B):** Provides longitudinal sleep disruption and restlessness distributions, calibrating the clinical risk escalation module (AS08/caregiver notification) when nocturnal agitation is detected.
- **Training Suitability:** **CLASS B (CIRCADIAN DISRUPTION & CLINICAL RISK PRIOR)**.

---

#### 13. DementiaBank Pitt Clinical Metadata ($N=512$ Visits)
- **Why Not Class A:** Tabular clinical records containing neuropsychological battery scores (MMSE, Blessed, CERAD, CDR), but zero real-time audio or interaction telemetry (raw audio and transcripts are access-gated behind TalkBank SLA).
- **Valuable Role (Class B):** Establishes longitudinal disease progression rates across 512 patient visits, providing the clinical mapping between MMSE decline and expected conversational complexity tolerance.
- **Training Suitability:** **CLASS B (LONGITUDINAL COGNITIVE DECLINE MAPPING PRIOR)**.

---

#### 14. MDVR-KCL Audio ($N=73$)
- **Why Not Class A:** Comprises 31 Parkinson's Disease (PD) patients and 42 Healthy Controls. Contains ZERO dementia patients.
- **Valuable Role (Class B):** Serves as a vital **out-of-domain negative control benchmark**. In Phase 4 evaluation, our acoustic cognitive decline models (trained on NCMMSC2021 and Chinese Dataset 1) will be evaluated on MDVR-KCL to verify that the model does NOT erroneously classify motor speech dysarthria (hypophonia, vocal tremor) as Alzheimer's cognitive decline.
- **Training Suitability:** **CLASS B (OUT-OF-DOMAIN ACOUSTIC SPECIFICITY BENCHMARK)**.

---

### 3.3 Class C Datasets: Suitable Only for Methodological Reference

#### 15. Canterbury Wellbeing Scales (201 Session Records)
- **Why Class C:** Evaluates pre/post Visual Analogue Scale (VAS) wellbeing among individuals with dementia handling museum objects and heritage artifacts. It contains zero digital user interface interactions, cognitive training exercises, or latency telemetry.
- **Role:** Qualitative clinical evidence that tactile, non-judgmental engagement improves patient affect in dementia care.
- **Training Suitability:** **CLASS C (QUALITATIVE METHODOLOGICAL REFERENCE)**.

---

### 3.4 Class D Datasets: Not Currently Suitable (Incomplete or Gated)

#### 16. DementiaNet
- **Why Class D:** Only the GitHub code repository and documentation exist on local disk. Raw speech audio files reside on an external Google Drive folder and were never retrieved locally.
- **Training Suitability:** **CLASS D (LOCAL DATA INCOMPLETE)**.

#### 17â€“20. TalkBank Restricted Corpora (Pitt Audio/Transcripts, ADReSSo 2021, ADReSS-M, TAUKADIAL 2024)
- **Why Class D:** Raw audio recordings and CHAT transcripts are access-restricted behind TalkBank Consortium HTTP Basic Authentication (`sla2.talkbank.org`). Registration, faculty verification, and an executed Data-Sharing Agreement with Carnegie Mellon University are required.
- **Training Suitability:** **CLASS D (ACCESS RESTRICTED / CONSORTIUM APPROVAL REQUIRED)**.

#### 21â€“23. Uncollected Wishlist Benchmarks (PAAL v2, Dem@Care Fine-Grained ADL, ALMOND)
- **Why Class D:** Zero files downloaded to disk (0 bytes). Identified conceptually during early literature scoping.
- **Training Suitability:** **CLASS D (UNCOLLECTED BENCHMARK)**.

---
*Report certified as complete, evidence-based, and clinically governed.*
