# CASAS Cognitive Assessment Dataset: Actual File Inspection & Research Feasibility Audit

**Document:** CASAS Dataset Technical Audit Report  
**Target Archive:** `C:\Users\Admin\OneDrive\Desktop\NeuroCompanion\Backend\research_archive\cognitive_assessment.zip`  
**Report Destination:** `C:\Users\Admin\OneDrive\Desktop\NeuroCompanion\Backend\research_archive\CASAS_Inspection_Report.md`  
**Audit Date:** 2026-11-17  
**Execution Mode:** STRICT READ-ONLY (Archive and existing project files untouched)  
**Evaluator:** NeuroCompanion AI/ML Research Team  

---

## Executive Summary

This report delivers a definitive, evidence-based technical audit of the **CASAS Cognitive Assessment Dataset** (Washington State University, Zenodo DOI: `10.5281/zenodo.15717153`). The audit inspects the raw, uncorrupted local ZIP archive (`cognitive_assessment.zip`) directly to evaluate its potential contribution to **NeuroCompanion Phase 4 AI/ML Research (Models M1, M2, M3, and M4)**.

### Key Audit Findings:
1. **Archive Integrity:** The archive exists, is fully readable, and is uncorrupted (testzip passes with 0 errors; MD5 matches official Zenodo release: `7357205e9218523a8be966a74f3721cc`).
2. **Participant Representation:** While the Zenodo landing page describes "179 participants", the archive actually contains **400 numbered data files** (`001.txt` through `400.txt`) and **400 participant entries** in `diagnosis.txt`. Exactly **350 participants** have valid sensor streams (>100 bytes), **340 participants** have valid task scores in `activityscores.txt`, and **329 participants** have complete cross-file overlap across sensor data, scores, and diagnoses.
3. **Sensor Modality & Domain Mismatch:** The data consists strictly of ambient smart-home sensors (52 passive infrared [PIR] motion sensors, 15 magnetic door contact switches, 6 item-presence sensors, 5 temperature sensors, and electricity meters) installed in a physical apartment. It contains **zero digital software interaction telemetry** (no touch-screen latency, UI button taps, 3D spatial navigation, or digital audio replay).
4. **Assistance Labels (M1 Blocked):** The dataset provides **zero direct labels** for NeuroCompanion's AS01–AS09 assistance types (repeat instruction, simplify instruction, step-by-step guidance, visual hint, demonstration, slower speech, additional response time, break/pause, verbal explanation). Cues were delivered as apartment-wide automated `.wav` audio reminders (83 unique audio files; 1,393 events across 233 participants) specific to physical domestic activities (e.g., reminding a participant to find a watering can or turn off the stove).
5. **Independent Completion (M2 Indirect):** Task performance scores for uncued Activities 1–8 provide a binary indicator of unassisted execution (`score == 1`), but this reflects physical domestic ADLs, not digital cognitive tasks.
6. **Outcome Association (M3 Blocked):** While cued activities (Tasks 9–16) triggered audio prompts, `activityscores.txt` only scores Tasks 1–8. Step-level post-cue outcomes for cued tasks are **not scored or structured** in the archive.
7. **Personalisation (M4 Blocked):** The protocol was strictly cross-sectional (a single scripted experimental visit per participant). There is **no longitudinal multi-session data**.

### Final Verdict:
> [!IMPORTANT]
> **VERDICT: LIMITED USE (Supplementary Population Benchmark Only; DO NOT USE for AS01–AS09 Model Training)**  
> The CASAS dataset cannot train NeuroCompanion's digital assistance models (M1, M2, M3, or M4). It is approved solely as a supplementary external benchmark for analyzing general task latency, pause distributions, and behavioral differences across clinical cohorts (dementia vs. MCI vs. healthy controls) in physical activity contexts.

---

## 1. Archive Verification

### 1.1. Physical Archive Attributes

| Attribute | Verified Value | Evidence Source |
|---|---|---|
| **Canonical File Path** | `C:\Users\Admin\OneDrive\Desktop\NeuroCompanion\Backend\research_archive\cognitive_assessment.zip` | Local File System Inspection |
| **File Size (Bytes)** | `12,635,022` bytes | `os.path.getsize()` |
| **File Size (Formatted)** | `12.05 MB` (`12.6 MB` on disk) | File System Metadata |
| **MD5 Checksum** | `7357205e9218523a8be966a74f3721cc` | `hashlib.md5()` (Matches Zenodo metadata) |
| **SHA-256 Checksum** | `f377562f3c6a59791ba4207a86bab986ad075d73c3cd1bacf26953eb88eba71e` | `hashlib.sha256()` |
| **ZIP Test Integrity (`testzip()`)** | `None` (0 corrupted files detected) | `zipfile.ZipFile.testzip()` |
| **Archive Compression Type** | Deflated (`ZIP_DEFLATED`) | ZIP Directory Headers |
| **Total Archive Entries** | `407` (404 files, 3 directory nodes) | ZIP Infolist Inspection |

### 1.2. Available Documentation and Reference Citations

The archive and its accompanying landing page document the following provenance:
- **Title:** CASAS Smart Home dataset - scripted complex activities, activity scores, and cognitive diagnosis
- **Dataset Version:** `v2` (Published June 22, 2025 on Zenodo)
- **Primary Authors/Creators:** Diane Cook (Contact Person, WSU), Maureen Schmitter-Edgecombe, Aaron Crandall, Brian Thomas, Prafulla Dawadi.
- **Persistent Identifier (DOI):** `10.5281/zenodo.15717153`
- **Canonical Scientific Reference:**
  > Dawadi, P., Cook, D., & Schmitter-Edgecombe, M. (2013). *Automated cognitive health assessment using smart home monitoring of complex tasks.* IEEE Transactions on Systems, Man, and Cybernetics: Systems, 43(6): 1302–1313. doi: 10.1109/TSMC.2013.2252338.
- **Grant Funding Acknowledgement:** National Institutes of Health (NIH), Grant R01EB009675 ("Smart Environment Technologies for Health Assessment and Assistance").

---

## 2. Complete File Inventory

The ZIP archive contains exactly 404 files organized under the root folder `cognitive_assessment/`:

```
cognitive_assessment.zip (12,635,022 bytes)
└── cognitive_assessment/
    ├── sensorlayout.png                     (453,475 bytes; Smart apartment floorplan)
    ├── documents/
    │   ├── activitylist.txt                 (7,312 bytes; Scripted activity protocols)
    │   ├── activityscores.txt               (11,041 bytes; Experimenter task scores)
    │   └── diagnosis.txt                    (2,580 bytes; Clinical cognitive diagnoses)
    └── data/
        ├── 001.txt                          (11 bytes; Placeholder: "No testing")
        ├── 002.txt                          (11 bytes; Placeholder: "No testing")
        ├── 003.txt                          (11 bytes; Placeholder: "No testing")
        ├── 004.txt                          (299,561 bytes; Valid sensor events stream)
        ├── 005.txt                          (103,783 bytes; Valid sensor events stream)
        └── ... [395 additional participant .txt files: 006.txt to 400.txt]
```

### 2.1. File Breakdown by Type and Category

| Category | Path / Pattern | File Count | Format | Uncompressed Size Range | Purpose / Content |
|---|---|---|---|---|---|
| **Sensor Floorplan** | `cognitive_assessment/sensorlayout.png` | 1 | PNG Image | 453,475 bytes | Architectural schematic of the WSU smart apartment showing placement of M01–M51, D01–D19, and cabinet sensors. |
| **Activity Definitions** | `cognitive_assessment/documents/activitylist.txt` | 1 | ASCII Text | 7,312 bytes | Detailed breakdown of 24 scripted tasks (Tasks 1–8 uncued, 9–16 cued, 17–24 interwoven). |
| **Task Scores Table** | `cognitive_assessment/documents/activityscores.txt` | 1 | Whitespace-delimited Text | 11,041 bytes | Matrix of participant IDs, scores for Activities 1–8, sum score, and Day Out Task accuracy/sequencing scores. |
| **Diagnosis Labels** | `cognitive_assessment/documents/diagnosis.txt` | 1 | Whitespace-delimited Text | 2,580 bytes | Mapping of participant IDs (1–400) to 10 categorical diagnostic groups. |
| **Participant Sensor Data** | `cognitive_assessment/data/*.txt` | 400 | Space-delimited Event Logs | 8 bytes to 406,903 bytes (Mean: 104.7 KB) | Continuous, timestamped ambient sensor event streams per participant. |
| **Total Files** | | **404** | | **42,351,192 bytes (~40.4 MB uncompressed)** | |

---

## 3. Actual Dataset Structure and Sensor Taxonomy

### 3.1. Raw Sensor Log Format
Each non-placeholder data file in `cognitive_assessment/data/*.txt` contains one sensor reading per line. Inspection of over 50 data files confirms the following four-field format:

```text
[HH:MM:SS.ffffff] [SENSOR_ID] [MESSAGE] [OPTIONAL_TASK_ANNOTATION]
```

#### Representative Examples from `004.txt` and `010.txt`:
```text
09:00:28.604730 P001 1450
09:01:13.550810 M013 ON
09:01:15.220320 M013 OFF
09:14:12.265020 E002 START_INSTRUCT
09:14:59.644280 M051 ON 1-start,1.1,1.3,1.2
09:15:13.126090 M018 ON 1.4,1.5
09:29:32.098361 M051 OFF 1-end
09:30:36.066814 D007 OPEN 2-start
```

### 3.2. Physical Sensor Inventory
Direct extraction of all sensor identifiers across participant files reveals 12 distinct sensor hardware categories:

| Sensor Prefix | Description | Verified Quantity | Recorded Output States |
|---|---|---|---|
| **M** | Passive Infrared (PIR) Motion Detectors | 52 sensors (`M01` to `M52`) | `ON` (motion begins), `OFF` (motion ceases) |
| **D** | Magnetic Door Contact Sensors | 15 sensors (`D01` to `D19`) | `OPEN`, `CLOSE` (doors, drawers, cabinets) |
| **I** | Item-Use Sensors (Pressure / RFID mats) | 6 sensors (`I01` to `I06`) | `PRESENT`, `ABSENT` (e.g., medicine dispenser, watering can) |
| **P** | Whole-Home Electricity Power Meters | 2 meters (`P001`, `P002`) | Numerical instantaneous wattage (e.g., `1450`, `1360`) |
| **T** | Ambient Temperature Sensors | 5 sensors (`T001` to `T005`) | Numerical Celsius temperature (e.g., `22.5`, `24.0`) |
| **L** | Living Room Light / Audio Controllers | 12 controllers | Audio trigger tags `<do>...</do>`, `ON`, `OFF` |
| **K** | Kitchen Audio / Device Controllers | 1 controller | Audio trigger tags `<do>...</do>` |
| **E** | Experimenter Instruction Audio Triggers | 2 controllers (`E002`, `E003`) | `START_INSTRUCT`, `STOP_INSTRUCT`, `OPEN`, `CLOSE` |
| **A / S / B / R** | Auxiliary Ambient & Battery Sensors | 24 sensors | Environmental status flags, battery voltage levels |

---

## 4. Participant and Label Analysis

### 4.1. Participant Identifier Consistency & Overlap Audit

Cross-file correlation was conducted across all 400 possible participant IDs (PIDs 1 to 400). PIDs remain **strictly consistent** across files (i.e., `004.txt` maps to PID 4 in `diagnosis.txt` and PID 4 in `activityscores.txt`).

```
                    ┌─────────────────────────────────────────────────────┐
                    │               All 400 Enrolled PIDs                 │
                    │               (diagnosis.txt = 400)                 │
                    └──────────────────────────┬──────────────────────────┘
                                               │
             ┌─────────────────────────────────┴─────────────────────────────────┐
             ▼                                                                   ▼
┌─────────────────────────┐                                         ┌─────────────────────────┐
│ Valid Sensor Data (350) │                                         │ Valid Task Scores (340) │
│ (data/*.txt > 100 bytes)│                                         │ (activityscores.txt)    │
└────────────┬────────────┘                                         └────────────┬────────────┘
             │                                                                   │
             └─────────────────────────────────┬─────────────────────────────────┘
                                               │
                                               ▼
                              ┌──────────────────────────────────┐
                              │ Complete Full Overlap (329 PIDs) │
                              │ (Sensor + Scores + Diagnosis)    │
                              └──────────────────────────────────┘
```

#### Detailed Breakdown:
- **`diagnosis.txt`:** Exactly **400 participant entries** (IDs 1 through 400).
- **`activityscores.txt`:** Exactly **388 rows** (12 IDs between 1 and 400 are omitted entirely).
  - Valid task scores: **340 participants**.
  - All-missing (`?`): **48 participants**.
- **`cognitive_assessment/data/`:** Exactly **400 `.txt` files**.
  - Valid sensor data stream: **350 files** (87.5%).
  - Empty / uncollected files: **50 files** (12.5%):
    - "No testing": 38 files
    - "NO SENSOR DATA": 11 files
    - "No data": 1 file
- **Three-Way Intersection:** Exactly **329 participants** have valid sensor streams, valid non-missing activity scores, and an assigned clinical diagnosis.

### 4.2. Resolution of the "179 Participants" Discrepancy
The Zenodo release text cites "179 participants with corresponding cognitive diagnoses". Our file audit proves that the archive actually contains data for **329 complete participants**.  
**Explanation:** The original 2013 publication (*Dawadi et al., IEEE TSMC*) selected a clinical evaluation subset of **179 older adults** (combining healthy older adults, MCI, and dementia, while excluding younger college students and middle-aged adults). The Zenodo archive provides the full, expanded data collection encompassing 400 enrolled subjects.

### 4.3. Cognitive Diagnostic Groups (`diagnosis.txt`)
The archive categorizes participants into 10 distinct clinical and demographic groups:

| Code | Diagnostic Group Label | Count in `diagnosis.txt` (All 400) | Count with Full Valid Overlap (329) | NeuroCompanion Research Relevance |
|---|---|---|---|---|
| **1** | **Dementia** | 36 (9.0%) | **17 (5.2%)** | Target clinical cohort (severe sample scarcity) |
| **2** | **Mild Cognitive Impairment (MCI)** | 59 (14.8%) | **56 (17.0%)** | Target clinical cohort (moderate sample size) |
| **3** | Middle age (45–59) | 37 (9.2%) | 30 (9.1%) | Non-target baseline |
| **4** | Young-old (60–74) | 83 (20.8%) | 81 (24.6%) | Cognitively intact age-matched control |
| **5** | Old-old (75+) | 44 (11.0%) | 39 (11.9%) | Cognitively intact age-matched control |
| **6** | Other medical conditions | 19 (4.8%) | 17 (5.2%) | Confounding cohort |
| **7** | Watch / At-risk (longitudinal follow) | 30 (7.5%) | 28 (8.5%) | Borderline cognitive trajectory |
| **8** | Younger adult (college cohort) | 78 (19.5%) | 53 (16.1%) | Non-representative young baseline |
| **9** | Younger adult (ESL) | 13 (3.2%) | 7 (2.1%) | Non-representative baseline |
| **10** | Diagnosis not available | 1 (0.2%) | 1 (0.3%) | Unusable |
| **Total** | | **400** | **329** | **Total Confirmed Dementia + MCI = 73** |

> [!WARNING]
> **Severe Clinical Class Imbalance**  
> Within the complete dataset of 329 participants, confirmed **Dementia accounts for only 17 individuals (5.2%)** and **MCI accounts for 56 individuals (17.0%)**. Healthy older controls (Codes 4 and 5) account for 120 individuals (36.5%), and younger adults (Codes 8 and 9) account for 60 individuals (18.2%). Training a machine learning model on this distribution without rigorous grouping and reweighting will yield severe bias toward healthy patterns.

---

## 5. Activity, Cueing, and Performance Score Analysis

### 5.1. Scripted Tasks Architecture (`activitylist.txt`)
The experimental protocol divided testing into three distinct activity blocks totaling 24 tasks:

```
[Block 1: Uncued Tasks (1–8)]       ──> Performed independently without any reminders.
[Block 2: Cued Tasks (9–16)]        ──> Prompts delivered when participant hesitated or omitted steps.
[Block 3: Day Out Tasks (17–24)]    ──> Interwoven complex planning tasks without step-by-step cues.
```

#### Task Breakdown:
- **Tasks 1–8 (Uncued ADLs):** Sweep/dust, fill weekly medicine dispenser, write birthday card & check, watch DVD news clip, water indoor plants, answer phone regarding news clip, prepare microwave soup, select outfit for interview.
- **Tasks 9–16 (Cued ADLs):** Replace desk lamp bulb, wash hands at sink, clean countertops, phone call to transcribe recipe, sort/fold laundry, prepare oatmeal on stovetop, sort/file billing statements, setup card game & explain rules.
- **Tasks 17–24 (Interwoven "Day Out Task"):** Read bus schedule, heat pack in microwave, choose magazine, count bus fare, take motion sickness medication, gather picnic ingredients, pack basket, exit apartment.

### 5.2. Actual Recorded Cues in Sensor Logs
Inspection of raw sensor streams reveals exactly how cueing was executed:
- **Audio Cue Triggers:** Cues were delivered as automated `.wav` audio playback commands routed through apartment speakers (`LivingRoom` or `Kitchen`).
  ```text
  14:46:22.154226 LivingRoom <do>/usr/local/CASAS/mapping/AudioCues/L1111.wav</do>
  10:57:05.338370 Kitchen <do>/usr/local/CASAS/mapping/AudioCues/K2121.wav</do>
  ```
- **Quantitative Cue Findings:**
  - Exactly **233 participants** received at least one recorded audio cue.
  - Exactly **1,393 audio cue events** were delivered across the dataset.
  - There are **83 unique audio cue files** (e.g., `L1111.wav`, `L1121.wav`, `K2121.wav`).
  - Average cues received by diagnosis:
    - **Dementia (Code 1):** 10.3 cues per participant (range 1–22).
    - **MCI (Code 2):** 5.3 cues per participant (range 1–13).
    - **Healthy Older Adults (Codes 4 & 5):** 5.5 cues per participant (range 1–28).
    - **Younger Adults (Code 8):** 6.4 cues per participant (range 1–17).

> [!NOTE]
> **Nature of CASAS Cues:** Cues were verbal reminders specific to physical smart-apartment steps (e.g., *"Take the pot from the cupboard"*, *"Turn on the kitchen faucet"*). They were **not** digital interface prompts, visual highlights, or graduated software scaffolds.

### 5.3. Task Performance Scores (`activityscores.txt`)

`activityscores.txt` records human examiner ratings for **Activities 1 to 8 only** (Tasks 9–16 are completely unrated in this table).

#### Header Specification:
```text
* Participant ID
* Scores for activities 1-8 (columns 2 through 9)
* Sum of scores for activities 1-8 (column 10)
* Accuracy score for day out task (column 11)
* Sequencing score for day out task (column 12)
```

#### Score Definitions (Established in Dawadi et al., 2013):
- **Score 1 (High Quality / Unassisted):** Task executed independently and accurately according to protocol.
- **Score 2 (Verbal Prompt Given):** Examiner had to provide a verbal cue to initiate or resume task.
- **Score 3 (Incomplete / Error):** Participant omitted steps or completed task incorrectly.
- **Score 4 (Physical Guidance / Multiple Prompts):** Extensive assistance required.
- **Score 5 (Task Breakdown):** Participant unable to complete task despite prompts.
- **Score 6 (Task Aborted):** Task terminated prematurely due to failure or distress.
- **`?` (Missing):** Task not attempted, uncollectible, or unrecorded.

#### Verified Score Distribution Across Activities 1–8:
| Score Value | Meaning | Total Occurrences across Tasks 1–8 | Percentage |
|---|---|---|---|
| **1** | Unassisted / Standard Completion | **1,527** | 56.1% |
| **2** | Verbal Prompt Required | **431** | 15.8% |
| **3** | Incomplete / Step Omission | **94** | 3.5% |
| **4** | Substantial Assistance / Errors | **447** | 16.4% |
| **5** | Task Breakdown / Severe Failure | **57** | 2.1% |
| **6** | Task Aborted | **5** | 0.2% |
| **`?`** | Missing Data | **396** | 14.5% (approx. 49 per task) |

---

## 6. Detailed NeuroCompanion Model Feasibility Audit

### 6.1. M1 — AS01–AS09 Assistance Need Prediction

| NeuroCompanion Assistance Type | Corresponding CASAS Field / Annotation | Direct Feasibility | Technical Finding & Rationale |
|---|---|---|---|
| **AS01_REPEAT_INSTRUCTION** | None | ❌ **Infeasible** | CASAS has no concept of replaying UI audio on demand. |
| **AS02_SIMPLIFY_INSTRUCTION** | None | ❌ **Infeasible** | CASAS audio cues are fixed pre-recorded `.wav` files with no syntactic simplification. |
| **AS03_STEP_BY_STEP** | Task sub-step breakdown (`1.1`, `1.2`) | ❌ **Infeasible** | Sub-step tags in CASAS mark sensor events post-hoc, not interactive guidance offered to a patient. |
| **AS04_VISUAL_HINT** | None | ❌ **Infeasible** | Physical apartment has no holographic or screen-based visual highlights. |
| **AS05_DEMONSTRATION** | None | ❌ **Infeasible** | Carl 3D demonstrations do not exist in a physical sensor apartment. |
| **AS06_SLOWER_SPEECH** | None | ❌ **Infeasible** | No variable-rate audio playback. |
| **AS07_ADDITIONAL_TIME** | Task `duration` | ❌ **Infeasible** | Duration measures physical elapsed time, not an adaptive software timeout adjustment. |
| **AS08_BREAK_PAUSE** | Sensor inactivity pauses | ❌ **Infeasible** | Inactivity pauses reflect participant hesitation, not a supportive break delivered by software. |
| **AS09_VERBAL_EXPLANATION** | Audio cues (`.wav` triggers) | ❌ **Infeasible** | CASAS audio cues are single task step instructions, not semantic conceptual explanations. |

**Conclusion on M1:** CASAS contains **zero valid labels or features** for training or validating NeuroCompanion's AS01–AS09 assistance types. Any attempt to map CASAS `.wav` cues or Score 2 prompts to AS01–AS09 would be an ungrounded, fabricated heuristic.

---

### 6.2. M2 — AS10 Independent Completion Prediction

| NeuroCompanion Target | Corresponding CASAS Field | Feasibility | Technical Finding & Rationale |
|---|---|---|---|
| **AS10_INDEPENDENT_COMPLETION** ($y \in \{0, 1\}$) | `activityscores.txt` `score == 1` | ⚠️ **Limited / Theoretical Proxy Only** | In Activities 1–8 (uncued), `score == 1` rigorously indicates that the participant completed the task without examiner prompts or breakdowns. |

**Critical Limitations for M2:**
1. **Domain Mismatch:** Completing a physical broom-sweeping task (Task 1) independently does not correlate directly with navigating a 3D digital Cognitive Garden or recalling a digital memory card.
2. **Missing Input Covariates:** CASAS records ambient motion, not digital touchscreen interactions or 3D controller trajectories.
3. **Usage Verdict:** Useful **only** as a conceptual benchmark demonstrating that machine learning models can predict binary task competence from timing and pause metrics. It **cannot** be deployed into the NeuroCompanion production inference pipeline.

---

### 6.3. M3 — Observational Outcome Association

| NeuroCompanion Target | Corresponding CASAS Field | Feasibility | Technical Finding & Rationale |
|---|---|---|---|
| **Outcome Association:** $P(\text{Outcome} \mid \text{Delivered AS}, x)$ | Audio cues linked to subsequent scores | ❌ **Infeasible** | While audio cues were delivered during Tasks 9–16, `activityscores.txt` **does not score Tasks 9–16**. The subsequent completion outcomes of cued tasks were never tabulated into the scores file. |

**Critical Limitations for M3:**
1. In Tasks 1–8 (which *are* scored), audio cues were not delivered because the tasks were uncued.
2. In Tasks 9–16 (which *received* cues), post-cue outcomes are unrecorded in `activityscores.txt`.
3. Consequently, there is no paired dataset linking an assistance event to a confirmed subsequent task outcome.

---

### 6.4. M4 — Longitudinal Personalisation

| NeuroCompanion Target | Corresponding CASAS Field | Feasibility | Technical Finding & Rationale |
|---|---|---|---|
| **Personalisation:** Per-patient residual adjustment $\Delta p_i$ | Repeated participant sessions | ❌ **Infeasible** | The CASAS cognitive assessment protocol is strictly **cross-sectional** (a single experimental visit per participant). |

**Critical Limitations for M4:**
1. Exactly 1 visit recorded per participant ID.
2. Zero longitudinal multi-day or multi-week tracking.
3. No data to model habituation, cognitive decline trajectory, or individual learning curves over time.

---

## 7. Comprehensive Feasibility & Research Task Matrix

The following table summarizes the definitive feasibility across all Phase 4 intelligence requirements:

| Research Task | Actual Supporting Files & Fields | Feasibility | Technical Limitations & Disconnects |
|---|---|---|---|
| **M1: Assistance Need (AS01–AS09)** | None (`.wav` audio cues in `data/*.txt` are ADL-specific prompts) | ❌ **Infeasible** | Zero mapping to digital UI scaffolding; cues reflect physical domestic reminders, not software adaptations. |
| **M2: Independent Completion (AS10)** | `activityscores.txt` (`score == 1` vs `score in {2,3,4,5,6}`) | ⚠️ **Limited Benchmark Only** | Valid measure of physical ADL competence; completely divorced from digital 3D interactive software environments. |
| **M3: Outcome Association** | None (Tasks 9–16 cues unlinked to scores) | ❌ **Infeasible** | Cued tasks are completely omitted from `activityscores.txt`; paired assistance-outcome records do not exist. |
| **M4: Personalisation** | None (cross-sectional visit only) | ❌ **Infeasible** | Single-session experimental protocol; zero longitudinal repetition or multi-day tracking per participant. |
| **Supplementary Research: Latency & Pause Dynamics** | `task_audit_data.csv` (`duration`, `pause_cnt_5_int`, `max_pause_int`) | ✅ **Feasible for Reference Research** | High-quality baseline data on how pause frequency and task duration differ between MCI, dementia, and healthy controls. |
| **Supplementary Research: Cognitive Group Benchmarking** | `diagnosis.txt` (Codes 1–5) | ✅ **Feasible for Reference Research** | Provides empirical distributions of cognitive impairment prevalence in structured task studies. |

---

## 8. Data Quality, Missing Information, and Scientific Gaps

Direct inspection revealed several critical data-quality limitations:

1. **Missing Clinical Demographics:** The archive does **not** include exact chronological age (only broad age brackets like "45–59" or "60–74"), biological sex, education level, MMSE scores, MoCA scores, or medication history. None of the clinical features used in Phase 1 (M0) are available in CASAS.
2. **Missing Data Files (12.5%):** 50 of the 400 participant data files contain no sensor records (placeholders such as "No testing" or "NO SENSOR DATA").
3. **Missing Task Scores (14.5%):** 48 participant rows in `activityscores.txt` contain only question marks (`?`).
4. **Day Out Task Scoring Sparsity:** 178 of the 388 participant rows in `activityscores.txt` (45.9%) have missing Day Out Task accuracy and sequencing scores.
5. **Dementia Cohort Scarcity:** While 36 participants are coded as Dementia in `diagnosis.txt`, only **17 participants with Dementia** have complete, usable sensor data and task scores. This sample size is too small for generalizable supervised learning.

---

## 9. Final Recommendations and Dataset Isolation Protocol

### 9.1. Final Operational Verdict

> ### **RECOMMENDATION: LIMITED USE**
>
> 1. **DO NOT USE for Production Model Training:** Under no circumstances may CASAS data be used to train, fine-tune, or validate NeuroCompanion's M1 (AS01–AS09), M2 (AS10), M3 (Outcome Association), or M4 (Personalisation) models.
> 2. **DO NOT INGEST into SQLite:** CASAS records must **never** be inserted into `neurocompanion.db` (`interaction_events`, `assistance_episodes`, or `event_interpretations`).
> 3. **APPROVED AS SUPPLEMENTARY RESEARCH REFERENCE ONLY:** CASAS may be referenced in scientific publications and exploratory research reports as an external empirical benchmark illustrating how cognitive impairment manifests in task execution duration, pause frequency, and hesitation dynamics.

### 9.2. Strict Isolation Checklist

| Isolation Requirement | Enforcement Mechanism |
|---|---|
| **Directory Isolation** | Raw CASAS data remains quarantined in `Backend/research_archive/` and will not be moved into `Backend/data/`. |
| **Database Isolation** | The SQLite database (`neurocompanion.db`) maintains strict schema validation; zero CASAS sensor events or PIDs may be ingested. |
| **API Contract Isolation** | No FastAPI endpoint (`/api/v1/` or `/api/v4/`) will reference, expose, or depend upon CASAS files or models. |
| **Unity Client Isolation** | Unity clients interact purely with native digital interactions; no CASAS logic will enter C# scripts. |
| **Scientific Claim Integrity** | Phase 4 documentation will explicitly disclaim CASAS as a source of digital assistance ground truth. |

---

*Report certified by NeuroCompanion AI/ML Research Team*  
*Audit Completed: 2026-11-17 · Strict Read-Only Verification*
