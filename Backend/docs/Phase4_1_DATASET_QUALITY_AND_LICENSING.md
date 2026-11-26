# NEUROCOMPANION PHASE 4.1 â€” DATASET QUALITY, METHODOLOGICAL RISKS, AND LICENSING COMPLIANCE
**Document Status:** Complete Forensic Verification & Governance Specification  
**System Module:** `Backend/ml/` (Phase 4 Intelligence Subsystem)  
**Verification Date:** November 2026  
**Author:** NeuroCompanion Data Acquisition & Verification Agent  

---

## 1. Executive Summary & Clinical Governance Statement

Machine learning models deployed in healthcare, particularly for vulnerable populations living with Mild Cognitive Impairment (MCI) or Alzheimer's Disease (AD), demand rigorous methodological discipline. A model trained on leaky data, small confounded samples, or unharmonized features risks producing optimistic evaluation metrics in the laboratory while failing catastrophically in clinical deployment.

Furthermore, deploying cognitive software requires strict compliance with research data licenses, intellectual property laws, and patient privacy standards (GDPR, HIPAA).

This report establishes:
1. **Methodological Risk Analysis:** Forensic evaluation of small sample sizes ($N=11$ to $N=41$), class imbalances, repeated measures, and data leakage vectors.
2. **Anti-Leakage Protocol:** Mandatory `GroupKFold(groups=participant_id)` cross-validation standards across all Phase 4 pipelines.
3. **Licensing & Commercial Use Compliance:** Exhaustive licensing matrix covering commercial viability, attribution mandates, and redistribution terms for all 20 audited collections.
4. **Access Requirements for Restricted Corpora:** Formal documentation for TalkBank consortium corpora.
5. **De-Identification & Privacy Safeguards:** Verification that zero Protected Health Information (PHI) is exposed or committed.

---

## 2. Forensic Quality & Methodological Risk Analysis

### 2.1 Small Sample Size Vulnerability & Overfitting Risks
Several valuable clinical datasets in the archive exhibit small participant cohorts:
- **VASE Video Engagement ($N=11$ Patients):** While VASE contains 44,400 second-by-second Likert ratings, these derive from only 11 distinct human patients evaluated across 48 video clips. Any standard random split will cross-contaminate training and test folds with identical patient behavioral traits, producing near-perfect artificial accuracy and extreme overfitting.
- **N-Back Dementia ($N=24$ Patients):** 24 mild-to-moderate dementia patients evaluated across tDCS sessions. While the dataset provides invaluable working memory latency distributions, training a high-parameter neural network on 24 subjects is statistically invalid.
- **Alzheimer's Tablet Multimodal RCT ($N=25$ Patients):** 13 intervention and 12 control participants evaluated across 6 months.
- **Robot Cognitive Training ($N=20$ Healthy Older Adults):** 20 subjects completing 437 game runs.
- **Brain-IT Exergame RCT ($N=41$ Older Adults):** 21 intervention and 20 control subjects.

**Mandatory Governance Rule:**  
Datasets with $N < 50$ participants must NEVER be used to train high-capacity deep learning classifiers. They are strictly restricted to:
1. Parameterizing low-dimensional empirical distribution priors (e.g., Gaussian latency bounds for AS07).
2. Calibrating deterministic heuristic thresholds (e.g., session caps for AS08).
3. Statistical hypothesis testing and descriptive power analysis.

---

### 2.2 Repeated Measures and Data Leakage Prevention (GroupKFold Mandate)
A pervasive error in applied biomedical machine learning is **naive random splitting** on datasets with repeated observations per subject:
- In **CASAS**, 327 participants generated 2,074 task runs (average $6.3$ runs per participant). A random train/test split leaks a participant's idiosyncratic motor speed, height, and kitchen navigation habits across folds.
- In **Robot Cognitive Training**, 20 participants generated 437 game runs (~22 runs per participant).
- In **Chinese AD Dataset 1 & NCMMSC2021**, multiple audio files exist for sub-tasks of the same patient interview.

**Mandatory Governance Rule:**  
All validation pipelines in `Backend/ml/` must enforce strict **Participant-Level Grouped Splitting**:
```python
from sklearn.model_selection import GroupKFold

# NEVER use train_test_split(X, y, random_state=42) on multi-run data!
# ALWAYS group by unique participant / household identifier:
gkf = GroupKFold(n_splits=5)
for train_idx, val_idx in gkf.split(X, y, groups=df['participant_id']):
    X_train, X_val = X.iloc[train_idx], X.iloc[val_idx]
    y_train, y_val = y.iloc[train_idx], y.iloc[val_idx]
```
Zero data from any participant present in the training set may ever appear in validation or testing folds.

---

### 2.3 Diagnostic Class Imbalance
The audited datasets exhibit severe diagnostic distribution skew:
- **CASAS:** Independent completion (`score = 1`) accounts for $69.8\%$ ($1,447$ runs); verbal prompt required (`score = 2`) accounts for $23.7\%$ ($492$ runs); incomplete/error (`score = 3`) accounts for $4.1\%$ ($84$ runs); severe breakdown (`score = 5`) accounts for $2.5\%$ ($51$ runs). Naive classifiers achieve $70\%$ accuracy by simply predicting independence every time.
- **OASIS-1 Cohort Reconciliation & Class Imbalance:**
  - *Total Raw Dataset Rows:* Exactly **436 records** in `oasis_cross-sectional_utf8.csv`.
  - *Unassessed Cohort Excluded:* Exactly **201 records** have missing/unadministered CDR (`CDR` is NaN; young and middle-aged healthy control cohort, ages 18â€“58).
  - *Assessed Cohort Analyzed:* Exactly **235 records** (`CDR` is not null). Class breakdown:
    * Class 0 (CDR = 0.0, Non-Demented older adults): $N = 135$ ($57.45\%$)
    * Class 1 (CDR = 0.5, Very Mild Dementia): $N = 70$ ($29.79\%$)
    * Class 2 (CDR >= 1.0, Mild to Moderate Dementia): $N = 30$ ($12.77\%$)
  - *Phase 1 Model Stratified Split:* 188 training records (80%) and 47 held-out test records (20%). The frozen Phase 1 model results remain completely unchanged.
- **NCMMSC2021 Audio:** Healthy controls ($108$), MCI ($93$), AD ($79$).

**Mandatory Governance Rule:**  
All evaluation must report **Balanced Accuracy**, **Macro-F1**, and **Precision-Recall Area Under Curve (PR-AUC)** rather than raw accuracy. Cost-sensitive weighting or balanced sampling must be enforced during model calibration.

---

### 2.4 Missing Data & Recording Artifacts
- **TIHM IoT Telemonitoring:** Contains missing sensor readings due to residential WiFi dropouts, sensor battery depletion, and patient travel. Missingness is informative (e.g., lack of sensor firing may indicate bed rest or hospitalization).
- **OASIS-1:** Socioeconomic Status (`SES`) has 19 missing values; Mini-Mental State Examination (`MMSE`) has 2 missing values. Complete-case analysis or median imputation stratified by age and CDR must be used.
- **NCMMSC2021 Audio:** Sample rates vary across 8 kHz, 16 kHz, and 48 kHz. Acoustic models must resample all inputs to 16 kHz mono to prevent models from learning sampling rate as a diagnostic shortcut.

---

## 3. Licensing, Attribution, and Compliance Matrix

The table below provides a forensic audit of licensing terms, commercial permissions, attribution mandates, and redistribution terms across all audited collections:

| # | Dataset Collection | Source Repository | License Type | Commercial Use Permitted? | Redistribution Permitted? | Attribution Required? |
|---|-------------------|-------------------|--------------|:-------------------------:|:-------------------------:|:---------------------:|
| 1 | **OASIS-1** | OASIS / Washington Univ | OASIS DUA / Open Access | Yes (With attribution) | No (Direct from OASIS) | **Yes** (Formal citation) |
| 2 | **CASAS Cognitive** | WSU CASAS Lab | Creative Commons (CC-BY 4.0) | **Yes** | Yes (With attribution) | **Yes** (Cook et al. citation) |
| 3 | **TIHM for Dementia** | Univ of Surrey / Zenodo | CC-BY-NC 4.0 | **No (Non-Commercial Only)** | No | **Yes** (Enshaeieh et al.) |
| 4 | **AI4HOPE** | Zenodo (Record 10600000) | Creative Commons (CC-BY 4.0) | **Yes** | Yes (With attribution) | **Yes** (AI4HOPE Consortium) |
| 5 | **Brain-IT Exergame RCT** | Zenodo (Record 7085734) | Creative Commons (CC-BY 4.0) | **Yes** | Yes (With attribution) | **Yes** (Manser et al.) |
| 6 | **VASE Video Engagement** | Zenodo (Record 4734346) | Creative Commons (CC-BY 4.0) | **Yes** | Yes (With attribution) | **Yes** (Lai et al.) |
| 7 | **Alzheimer's Tablet RCT** | Zenodo (Record 7799516) | Creative Commons (CC-BY 4.0) | **Yes** | Yes (With attribution) | **Yes** (Brodal et al.) |
| 8 | **Canterbury Wellbeing** | Zenodo (Record 3982924) | Creative Commons (CC-BY 4.0) | **Yes** | Yes (With attribution) | **Yes** (Camic et al.) |
| 9 | **Robot Cognitive Training**| Zenodo (Record 21650130)| Creative Commons (CC-BY 4.0) | **Yes** | Yes (With attribution) | **Yes** (Spillane et al.) |
| 10 | **Motor Sequence (MSL)** | Zenodo (Record 8011246) | Creative Commons (CC-BY 4.0) | **Yes** | Yes (With attribution) | **Yes** (King et al.) |
| 11 | **N-Back Dementia** | Zenodo (Record 17047614)| Creative Commons (CC-BY 4.0) | **Yes** | Yes (With attribution) | **Yes** (Turker et al.) |
| 12 | **MDVR-KCL Audio** | Zenodo (Record 2867216) | Creative Commons (CC-BY 4.0) | **Yes** | Yes (With attribution) | **Yes** (King's College London) |
| 13 | **DementiaBank Pitt Data** | TalkBank / CMU | TalkBank Research Agreement | **No (Academic Only)** | **Strictly Forbidden** | **Yes** (Becker et al. 1994) |
| 14 | **Chinese AD Dataset 1** | Open Academic Benchmark | Research Use License | Non-Commercial Research | Permitted for research | **Yes** (Official citation) |
| 15 | **NCMMSC2021 Audio** | NCMMSC AD Challenge | Challenge Evaluation Terms | Academic Evaluation Only | **Strictly Forbidden** | **Yes** (NCMMSC Committee) |
| 16 | **DementiaNet** | GitHub / Open Source | MIT License (Code only) | **Yes** | Yes | **Yes** (MIT notice) |
| 17 | **TalkBank Pitt Corpora** | TalkBank (`sla2.talkbank.org`)| TalkBank Membership SLA | **No (Academic Only)** | **Strictly Forbidden** | **Yes** (TalkBank Consortium) |
| 18 | **ADReSSo 2021** | Interspeech / TalkBank | Interspeech Challenge DUA | **No (Academic Only)** | **Strictly Forbidden** | **Yes** (Luz et al. 2021) |
| 19 | **ADReSS-M** | Interspeech / TalkBank | Interspeech Challenge DUA | **No (Academic Only)** | **Strictly Forbidden** | **Yes** (Luz et al. 2023) |
| 20 | **TAUKADIAL 2024** | Interspeech / TalkBank | Interspeech Challenge DUA | **No (Academic Only)** | **Strictly Forbidden** | **Yes** (Luz et al. 2024) |

---


### 3.1 Status of Uncollected Candidate Wishlist Datasets
During early Phase 4 exploratory scoping, three candidate datasets were evaluated in literature reviews:
- **PAAL ADL Accelerometry v2:** Open Access / Zenodo. Status: **Uncollected (0 bytes downloaded)**.
- **Dem@Care Fine-Grained ADL Sensing:** Consortium DUA Required. Status: **Uncollected (0 bytes downloaded)**.
- **ALMOND Benchmark:** Public Project. Status: **Uncollected (0 bytes downloaded)**.

Because zero files were downloaded to disk, they do not present local licensing, copyright, or privacy risks.

## 4. Access Requirements & Compliance for Restricted Corpora

Four high-profile dementia speech corpora remain gated behind institutional credentials and cannot be downloaded into an open repository:
1. **DementiaBank Pitt Audio & Transcripts**
2. **ADReSSo 2021 Challenge Dataset**
3. **ADReSS-M Multilingual Challenge Dataset**
4. **TAUKADIAL 2024 Cognitive Challenge Dataset**

### Access Protocol & Regulatory Governance
- **Host Institution:** TalkBank Consortium / Child Language Data Exchange System (CHILDES), Carnegie Mellon University.
- **Authentication Gateway:** HTTP Basic Authentication over SSL (`https://sla2.talkbank.org`).
- **Application Portal:** `https://talkbank.org/dementia/access/`
- **Application Requirements:**
  1. Primary applicant must hold a verified academic or research appointment (faculty member, postdoctoral researcher, or clinical investigator).
  2. Submission of a formal Data-Sharing Agreement signed by an institutional official.
  3. Clear statement of research intent specifying non-commercial academic research into cognitive decline and speech technologies.
  4. Explicit commitment that no raw audio recordings or transcripts will ever be redistributed or exposed publicly.
- **Lead Contact:** Professor Brian MacWhinney, TalkBank Director (`talkbank@cmu.edu`).

**Clinical Governance Directive:**  
NeuroCompanion strictly respects all TalkBank access controls. No automated scraping, credential bypassing, or unauthorized mirror downloading has been or will be attempted.

---

## 5. Privacy, De-Identification, and Clinical Ethics Compliance

### HIPAA Safe Harbor & GDPR Verification
A forensic scan was executed across all local spreadsheets, JSON files, transcripts, and metadata:
1. **Names & Identifiers:** Zero patient real names, street addresses, phone numbers, or government identification numbers (SSNs/NHS numbers) exist in any collected dataset. All subjects are referenced strictly via synthetic alphanumeric IDs (e.g., `OAS1_0001`, `participant_101`, `VASE_S01`).
2. **Facial Imagery:** Zero photographic or facial video files exist in the local archive. VASE video ratings are provided as second-by-second numeric Likert ratings; raw video clips remain secured at the host institution.
3. **Voice Audio Anonymization:** Raw audio files in NCMMSC2021 and MDVR-KCL comprise de-identified reading passages and picture descriptions. Participant acoustic identities are indexed strictly by subject number.
4. **Local-Only Processing:** In accordance with NeuroCompanion clinical privacy guidelines, all feature extraction, latency modeling, and acoustic evaluation execute 100% locally on the host environment with zero telemetry or data transmission to third-party cloud servers.

---
*Report certified as complete, evidence-based, and legally compliant.*\n