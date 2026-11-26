# TIHM Dataset Forensic Inspection & Research Feasibility Audit

**Document:** Forensic Audit of the TIHM (Technology Integrated Health Management) Dataset  
**Target Archive:** `C:\Users\Admin\OneDrive\Desktop\NeuroCompanion\Backend\research_archive\TIHM_Dataset.zip`  
**Report Destination:** `C:\Users\Admin\OneDrive\Desktop\NeuroCompanion\Backend\research_archive\TIHM_Inspection_Report.md`  
**Audit Date:** 2026-11-17  
**Execution Mode:** STRICT READ-ONLY (No models trained, no SQLite insertion, standard library verification only)  
**Evaluator:** NeuroCompanion AI/ML Research Team  

---

## Executive Summary

This report delivers an exhaustive, evidence-based forensic inspection of the **TIHM (Technology Integrated Health Management) for Dementia Dataset** (`TIHM1.5-Data`, released by Prof. Payam Barnaghi, UK Dementia Research Institute / Imperial College London / Surrey and Borders Partnership NHS Foundation Trust). The audit evaluates the physical archive integrity, inspects all tabular records, audits clinical label semantics, and evaluates its feasibility for **NeuroCompanion Phase 4 AI/ML Research (Models M1, M2, M3, and M4)**.

### Key Audit Findings:
1. **Archive Integrity Confirmed:** The ZIP archive exists, is uncorrupted (`testzip()` passes with 0 errors), has an exact file size of **6,978,042 bytes**, and matches the official expected MD5 checksum **exactly**: `ccf913f38a7808bc366bf0e799b409e4`.
2. **Dataset Scale & Longitudinal Scope:** The dataset contains continuous longitudinal data for **56 participants living with dementia or mild cognitive impairment (MCI)** tracked in their private homes over a 3-month window (April 1 to June 30, 2019; mean active tracking of **48.6 days** per participant, up to 89 days).
3. **Data Completeness:** The dataset comprises **5 primary CSV tables** totaling **1,510,326 data rows** (~60.1 MB uncompressed) with **zero missing values and zero duplicate records**.
4. **Clinical Label Reality (Health Alerts, NOT Assistance):** The 608 entries in `Labels.csv` represent **remote clinical triage alerts** flagged by NHS clinicians and monitoring algorithms (305 Blood pressure alerts, 135 Agitation events, 95 Pulse anomalies, 68 Body water flags, 4 Weight flags, 1 Body temperature alert). The dataset contains **zero assistance-need labels (AS01–AS09), zero independent-completion labels (AS10), zero task completion records, and zero assistance interaction data**.
5. **Phase 4 Feasibility Verdict:**
   - **M1 (Assistance Need Prediction):** **NOT SUITABLE.** Zero assistance labels; passive home sensors cannot map to digital UI scaffolding.
   - **M2 (Independent Completion Prediction):** **NOT SUITABLE.** Zero task-level completion outcomes.
   - **M3 (Outcome Association):** **NOT SUITABLE.** Zero assistance delivery events.
   - **M4 (Longitudinal Personalisation):** **LIMITED SUPPLEMENTARY RESEARCH ONLY.** TIHM provides high-quality longitudinal time-series data of passive home activity and sleep across ~3 months in people with dementia. It is suitable strictly as an external population benchmark for studying circadian rhythm stability and longitudinal non-stationarity, but cannot personalize NeuroCompanion's assistance delivery.
6. **Data-Use & Licensing Constraints:** Released under an **academic, non-commercial research license**. Commercial utilization of derivative models requires formal licensing agreements from the rights holders (Surrey and Borders Partnership NHS Foundation Trust / Imperial College London).

---

## 1. Archive Integrity Verification

### 1.1. Physical Archive Checksums & Metrics

| Parameter | Verified Value | Evidence Source / Method | Status |
|---|---|---|---|
| **Canonical Path** | `C:\Users\Admin\OneDrive\Desktop\NeuroCompanion\Backend\research_archive\TIHM_Dataset.zip` | Local File System Inspection | Verified Present |
| **Exact File Size** | `6,978,042` bytes (~6.65 MB) | `os.path.getsize()` | Verified Exact |
| **Official Expected MD5** | `ccf913f38a7808bc366bf0e799b409e4` | Project Audit Specification | Target Benchmark |
| **Calculated MD5** | `ccf913f38a7808bc366bf0e799b409e4` | `hashlib.md5()` | ✅ **EXACT MATCH** |
| **Calculated SHA-256** | `368d642b4cdc680d0706abfd3c8b2e5387824d9737ad33f50531bafa6a4bf5a1` | `hashlib.sha256()` | Recorded |
| **ZIP Test Integrity** | `None` (0 corrupted files detected) | `zipfile.ZipFile.testzip()` | ✅ **100% Intact** |
| **Archive Structure** | 28 total entries (21 files, 7 directories) | ZIP Directory Headers | Verified |

### 1.2. Complete Archive Member Inventory

```
TIHM_Dataset.zip (6,978,042 bytes)
├── README.md                                         (3,424 bytes; Dataset overview)
├── Dataset/                                          (Directory)
│   ├── Activity.csv                                  (36,677,421 bytes; Ambient motion logs)
│   ├── Demographics.csv                              (1,377 bytes; Age brackets & sex)
│   ├── Labels.csv                                    (23,726 bytes; Clinical alert events)
│   ├── Physiology.csv                                (986,085 bytes; Vital sign measurements)
│   └── Sleep.csv                                     (22,424,343 bytes; Bed mat sleep telemetry)
└── example_code/                                     (Directory; Baseline evaluation code)
    ├── data_loader.py                                (31,391 bytes; PyTorch data loader)
    ├── test_data_loader.py                           (1,868 bytes; Loader unit tests)
    ├── example_baseline_classfication.ipynb          (383,132 bytes; Agitation classification)
    ├── example_data_loading.ipynb                    (36,378 bytes; Data loading demo)
    ├── example_data_visualisation.ipynb              (350,466 bytes; Visualization demo)
    ├── example_LSTM.ipynb                            (53,671 bytes; PyTorch LSTM pipeline)
    ├── figs/                                         (Directory; 4 plot files)
    │   ├── all_activity_ridgeplot.svg                (105,559 bytes)
    │   ├── baselines_agitation.svg                   (34,193 bytes)
    │   ├── shap_LR.svg                               (1,102 bytes)
    │   └── c55f8/personal_multiview_day_plot.pdf     (32,505 bytes)
    └── utils/                                        (Directory; 2 helper scripts)
        ├── data_utils.py                             (2,940 bytes)
        └── vis_utils.py                              (8,120 bytes)
```

---

## 2. Actual Tabular Dataset Inspection

All five tables in the `Dataset/` directory were audited directly via stream-parsing.

### 2.1. Table Summary & Dimensions

| Table File | Uncompressed Bytes | Column Count | Data Rows | Unique Participants | Date Range Covered | Missing Cells | Duplicate Rows |
|---|---|---|---|---|---|---|---|
| **`Demographics.csv`** | 1,377 bytes | 3 | **56** | 56 | Static baseline | **0 (0.0%)** | **0** |
| **`Labels.csv`** | 23,726 bytes | 3 | **608** | 49 | 2019-04-04 to 2019-06-30 | **0 (0.0%)** | **0** |
| **`Activity.csv`** | 36,677,421 bytes | 3 | **1,030,559** | 56 | 2019-04-01 to 2019-06-30 | **0 (0.0%)** | **0** |
| **`Physiology.csv`** | 986,085 bytes | 5 | **17,679** | 55 | 2019-04-01 to 2019-06-30 | **0 (0.0%)** | **0** |
| **`Sleep.csv`** | 22,424,343 bytes | 6 | **461,423** | **17** | 2019-04-01 to 2019-06-30 | **0 (0.0%)** | **0** |
| **Total Tabular Data** | **60,113,012 bytes (~60.1 MB)** | — | **1,510,326** | **56 distinct** | **88 calendar days** | **0 (0.0%)** | **0** |

---

### 2.2. Table-by-Table Schema & Value Distributions

#### A. `Dataset/Demographics.csv` ($N=56$)
- **Schema:** `patient_id` (string), `age` (categorical bracket), `sex` (string).
- **Age Distribution:**
  - `(80, 90]`: 26 participants (46.4%)
  - `(70, 80]`: 17 participants (30.4%)
  - `(90, 110]`: 13 participants (23.2%)
- **Sex Distribution:** Exactly balanced:
  - `Female`: 28 participants (50.0%)
  - `Male`: 28 participants (50.0%)
- **Clinical Relevance:** Reflects an authentic advanced-age dementia cohort (100% aged 70+; 69.6% aged 80+). Exact chronological age is masked for privacy.

#### B. `Dataset/Labels.csv` ($N=608$)
- **Schema:** `patient_id` (string), `date` (timestamp `YYYY-MM-DD HH:MM:SS`), `type` (categorical alert type).
- **Full Value Distribution:**
  - `Blood pressure`: 305 alerts (50.16%)
  - `Agitation`: 135 alerts (22.20%)
  - `Pulse`: 95 alerts (15.62%)
  - `Body water`: 68 alerts (11.18%)
  - `Weight`: 4 alerts (0.66%)
  - `Body temperature`: 1 alert (0.16%)
- **Key Forensic Finding:** These are **clinical alert events** generated by the remote monitoring triage system. They indicate physiological thresholds exceeded or behavioral agitation symptoms observed. They contain **zero task-support or assistance-delivery records**.

#### C. `Dataset/Activity.csv` ($N=1,030,559$)
- **Schema:** `patient_id` (string), `location_name` (categorical room), `date` (timestamp `YYYY-MM-DD HH:MM:SS`).
- **Location Distribution (Passive PIR Motion & Door Contact Sensors in Private Homes):**
  - `Kitchen`: 227,312 firings (22.06%)
  - `Hallway`: 193,984 firings (18.82%)
  - `Lounge`: 180,767 firings (17.54%)
  - `Bedroom`: 131,133 firings (12.72%)
  - `Fridge Door`: 108,803 contact events (10.56%)
  - `Bathroom`: 82,027 firings (7.96%)
  - `Front Door`: 58,287 contact events (5.66%)
  - `Back Door`: 48,246 contact events (4.68%)
- **Key Forensic Finding:** Ambient room transitions and door opens in free-living domestic environments. Completely divorced from in-app digital task interactions.

#### D. `Dataset/Physiology.csv` ($N=17,679$)
- **Schema:** `patient_id` (string), `date` (timestamp), `device_type` (categorical device), `value` (float), `unit` (string).
- **Device Types & Measurement Units:**
  - `Body Temperature`: 5,617 readings (Unit: `Cel`, e.g., 36.1°C)
  - `Systolic blood pressure`: 2,305 readings (Unit: `mm[Hg]`, e.g., 128 mmHg)
  - `Diastolic blood pressure`: 2,305 readings (Unit: `mm[Hg]`, e.g., 78 mmHg)
  - `Heart rate`: 2,305 readings (Unit: `beats/min`, e.g., 72 bpm)
  - `Body weight`: 1,874 readings (Unit: `kg`, e.g., 74.2 kg)
  - `O/E - muscle mass`: 1,264 readings (Unit: `kg`)
  - `Total body water`: 1,264 readings (Unit: `%`)
  - `Skin Temperature`: 745 readings (Unit: `Cel`)

#### E. `Dataset/Sleep.csv` ($N=461,423$)
- **Schema:** `patient_id` (string), `date` (timestamp), `state` (categorical stage), `heart_rate` (float), `respiratory_rate` (float), `snoring` (boolean).
- **Sub-Cohort Scarcity:** Available for **only 17 of the 56 participants** (bed sensor mats were not installed or operational in the remaining 39 homes).
- **Sleep Stages:** `LIGHT`: 239,102 (51.8%); `AWAKE`: 93,752 (20.3%); `DEEP`: 89,398 (19.4%); `REM`: 39,171 (8.5%).
- **Snoring:** `False`: 445,404 (96.5%); `True`: 16,019 (3.5%).
- **Physiological Ranges:** Heart rate 37.0–107.0 bpm; respiratory rate 8.0–31.0 breaths/min.

---

### 2.3. Participant Overlap and Relational Integrity

```
Demographics (56 PIDs)
├── Activity:     56 / 56 (100.0% overlap)
├── Physiology:   55 / 56 (98.2% overlap - 1 missing)
├── Labels:       49 / 56 (87.5% overlap - 7 with no clinical alerts)
└── Sleep:        17 / 56 (30.4% overlap - bed mat deployed in subset only)

Intersection (Demographics ∩ Activity ∩ Physiology ∩ Labels): 48 participants
Intersection across ALL 5 tables: exactly 12 participants
```

- **Join Keys:** Primary key is `patient_id` (anonymized 5-character hexadecimal string, e.g., `'c55f8'`). Temporal synchronization key is `date`. Relational integrity is completely clean; zero orphaned participant IDs exist in the relational tables.
- **Longitudinal Tracking Span:** In `Activity.csv`, participants have continuous daily monitoring ranging from **3 to 89 days** (Mean: **48.6 days**, Median: **53.0 days**).

---

## 3. Label Verification against NeuroCompanion Requirements

A rigorous inspection of all column names, value spaces, and accompanying codebase (`example_code/data_loader.py`, `example_code/example_baseline_classfication.ipynb`) was conducted against NeuroCompanion's cognitive assistance requirements:

| NeuroCompanion Label Requirement | Present in TIHM? | Exact Supporting Evidence from TIHM Data | Technical Verdict |
|---|---|---|---|
| **AS01_REPEAT_INSTRUCTION** | ❌ **No** | Zero instruction replay records; no digital audio prompts. | Incompatible |
| **AS02_SIMPLIFY_INSTRUCTION** | ❌ **No** | Zero text simplification records. | Incompatible |
| **AS03_STEP_BY_STEP** | ❌ **No** | Zero step guidance or micro-prompting. | Incompatible |
| **AS04_VISUAL_HINT** | ❌ **No** | Zero visual highlighting; private homes contain no UI. | Incompatible |
| **AS05_DEMONSTRATION** | ❌ **No** | Zero avatar or interactive demonstrations. | Incompatible |
| **AS06_SLOWER_SPEECH** | ❌ **No** | Zero speech rate modulation. | Incompatible |
| **AS07_ADDITIONAL_TIME** | ❌ **No** | Zero timeout extension flags. | Incompatible |
| **AS08_BREAK_PAUSE** | ❌ **No** | Zero software rest recommendations. | Incompatible |
| **AS09_VERBAL_EXPLANATION** | ❌ **No** | Zero semantic explanations. | Incompatible |
| **AS10_INDEPENDENT_COMPLETION**| ❌ **No** | Zero task completion outcomes; free-living passive monitoring. | Incompatible |
| **Assistance Offered / Delivered** | ❌ **No** | Zero assistance delivery logs. | Incompatible |
| **Patient Acceptance / Rejection** | ❌ **No** | Zero user feedback or acceptance tracking. | Incompatible |
| **Clinical Health-Event Alerts** | ✅ **Yes** | `Labels.csv` records 6 triage alert categories (Agitation, BP, Pulse, Water, Weight, Temp). | Remote Clinical Triage Only |

> [!WARNING]
> **Definitive Finding on Labels**  
> The `Labels.csv` file in TIHM represents **clinical health monitoring alerts** (e.g., clinician triage for high blood pressure or behavioral agitation). It contains **zero software assistance labels, zero task performance labels, and zero intervention delivery records**. Under no circumstances may clinical triage alerts be conflated with software assistance types AS01–AS09 or AS10.

---

## 4. Phase 4 Model Feasibility Evaluation

### 4.1. M1 — Assistance-Type Prediction (AS01–AS09)
- **Feasibility Classification:** ❌ **NOT SUITABLE**
- **Evidence & Limitations:** M1 requires predicting which specific software assistance type (repeat, simplify, step-by-step, visual hint, demonstration, etc.) will support a patient during an active digital activity in Unity. TIHM contains zero digital interaction data and zero assistance labels. Passive home sensor firings (kitchen motion, fridge door opening) cannot provide ground truth for in-app cognitive assistance.

### 4.2. M2 — Independent Completion Prediction (AS10)
- **Feasibility Classification:** ❌ **NOT SUITABLE**
- **Evidence & Limitations:** M2 requires binary prediction of whether a patient will complete a structured digital task independently without assistance. TIHM is a passive free-living monitoring dataset; participants were not performing discrete, evaluated cognitive tasks with completion criteria.

### 4.3. M3 — Outcome Association After Assistance
- **Feasibility Classification:** ❌ **NOT SUITABLE**
- **Evidence & Limitations:** M3 requires estimating the outcome distribution following the delivery of a specific assistance type ($P(\text{Outcome} \mid \text{Delivered AS}, x)$). TIHM contains no assistance delivery events and no post-assistance task outcome metrics.

### 4.4. M4 — Individual Longitudinal Adaptation
- **Feasibility Classification:** ⚠️ **LIMITED SUPPLEMENTARY RESEARCH ONLY (External Benchmark)**
- **Evidence & Feasibility Scope:** TIHM provides genuine, high-quality longitudinal tracking across ~3 months ($N=56$, mean 48.6 active days) in an authentic older adult dementia cohort.
- **What TIHM CAN Support for M4 Research:**
  - Can serve as an external mathematical benchmark for studying **longitudinal non-stationarity and behavioral drift** in people with dementia.
  - Can be used to evaluate time-series modeling techniques (e.g., LSTMs, Gaussian Processes, change-point detection) for detecting subtle multi-week shifts in circadian activity distributions and room-transition entropy.
- **What TIHM CANNOT Support:**
  - Cannot adapt or personalize NeuroCompanion's assistance delivery models.
  - Cannot train patient-specific residual adapters $\Delta p_i$ for software prompts.
  - Observational sleep and activity patterns in TIHM cannot be assumed to cause or predict specific digital UI difficulties.

---

## 5. Data-Use Restrictions and Licensing Compliance

The dataset provenance and terms of use were inspected from `README.md`, `example_code/data_loader.py`, and the official GitHub repository (`https://github.com/PBarnaghi/TIHM1.5-Data`):

| Restriction Dimension | Exact Dataset Policy | Compliance Mandate for NeuroCompanion |
|---|---|---|
| **Dataset Source & Lead** | TIHM for Dementia / Imperial College London (Prof. Payam Barnaghi) & Surrey and Borders Partnership NHS Foundation Trust. | Official attribution required in all publications. |
| **Commercial Restriction** | **STRICTLY NON-COMMERCIAL (Academic & Educational Research Only).** | NeuroCompanion must **never** incorporate TIHM-derived model weights or data into commercial releases. |
| **Attribution Requirement** | Mandatory scientific citation of the TIHM project publications and the `TIHM1.5-Data` repository. | Required citation documented in Phase 4 research bibliography. |
| **Institutional Governance** | Originates from an NHS clinical trial IoT testbed (funded by NHS England and Innovate UK). Data was anonymized under clinical research ethics. | Data must remain strictly quarantined within `research_archive/`. |
| **Future Commercial Deployment** | Any commercial product utilizing algorithms trained directly on TIHM data requires formal intellectual property and data licensing agreements from the rights holders. | Precludes using TIHM for any production software weights. |

---

## 6. Strict Project Protection Audit

In compliance with strict project protection rules:
- ✅ **No Models Trained:** Zero training scripts or accuracy benchmarks were executed.
- ✅ **Zero SQLite Ingestion:** No records from `Activity.csv`, `Labels.csv`, `Physiology.csv`, `Sleep.csv`, or `Demographics.csv` were inserted into `neurocompanion.db`.
- ✅ **No Phase Interference:** Phase 1 (CDR model), Phase 2A (FastAPI/database), and Phase 3 (Unity) remain 100% frozen and untouched.
- ✅ **No Package Installation:** All forensic auditing was conducted strictly using Python standard-library modules (`zipfile`, `hashlib`, `csv`, `collections`, `io`, `os`, `json`).
- ✅ **Archive Preserved:** `TIHM_Dataset.zip` was audited strictly in read-only stream mode; its hash remains identical (`ccf913f38a7808bc366bf0e799b409e4`).
- ✅ **Strict Quarantine:** The dataset remains isolated exclusively in `research_archive/`.

---

## 7. Final Task-by-Task Decision Matrix & Recommendations

| Phase 4 Intelligence Task | Classification | Key Justification | Scientific Limitations |
|---|---|---|---|
| **M1: Assistance Need (AS01–AS09)** | ❌ **Not Suitable** | Zero assistance-need labels. | In-home ambient motion sensors cannot proxy digital touchscreen UI scaffolding. |
| **M2: Independent Completion (AS10)**| ❌ **Not Suitable** | Zero task completion outcomes. | Passive free-living domestic monitoring does not evaluate task completion. |
| **M3: Outcome Association** | ❌ **Not Suitable** | Zero assistance delivery events. | No intervention-outcome pairs exist. |
| **M4: Longitudinal Personalisation** | ⚠️ **Limited Supplementary Research Only** | Genuine ~3-month longitudinal tracking of 56 patients with dementia. | Valid only as an external time-series benchmark for circadian drift; cannot personalize digital assistance. |

### Final Operational Recommendation
> ### **FINAL VERDICT: LIMITED USE (Supplementary Longitudinal Benchmark Only; DO NOT USE for Production Model Training)**
>
> 1. **PROHIBITED FOR PRODUCTION TRAINING:** Under no circumstances may TIHM data be used to train NeuroCompanion's M1, M2, or M3 models, or to supply software assistance weights.
> 2. **PROHIBITED FROM DATABASE INGESTION:** TIHM records must **never** be inserted into `neurocompanion.db`.
> 3. **APPROVED EXCLUSIVELY AS A SUPPLEMENTARY RESEARCH BENCHMARK:** TIHM may be referenced in exploratory longitudinal modeling research to investigate how circadian activity patterns and room-transition entropy shift over multi-month windows in older adults living with dementia.

---

*Report certified by NeuroCompanion AI/ML Research Team*  
*Audit Completed: 2026-11-17 · Strict Read-Only Verification*
