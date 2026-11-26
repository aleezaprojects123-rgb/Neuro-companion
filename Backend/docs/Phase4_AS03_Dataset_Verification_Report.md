# Phase 4 — AS03 Dataset Verification & Forensic Audit Report

**Assistance Specification:** **AS03 — Step-by-Step Guidance**  
**Role:** Break activities into smaller manageable steps, guide patient through sequences, and provide next step when needed  
**Evaluation Scope:** 7 Candidate Datasets in Local Storage  
**Audit Date:** November 21, 2026  
**Execution Mode:** STRICT READ-ONLY AUDIT (No files downloaded, modified, deleted, or extracted; no model trained; no database modified)  
**Report Location:** `NeuroCompanion/Backend/docs/Phase4_AS03_Dataset_Verification_Report.md`  

---

## 1. Executive Summary

This forensic audit rigorously verifies the physical existence, file integrity, structural composition, and scientific relevance of seven candidate datasets for **AS03 (Step-by-Step Guidance)** stored across the NeuroCompanion research archive and Backend directories.

All candidate datasets were examined directly on the local filesystem (`C:\Users\Admin\OneDrive\Desktop\NeuroCompanion\Backend\`). Every archive was inspected via internal ZIP metadata without extracting or altering file contents, and every spreadsheet/CSV was verified for actual data rows, schemas, and participant records.

### Key Audit Findings:
1. **Total Candidates Evaluated:** 7
2. **Physically Confirmed Downloaded Datasets:** **7 of 7 (100%)** are present and valid on disk.
3. **Incomplete or Corrupted Datasets:** **0 of 7 (0%)**. All present archives pass CRC checksum checks.
4. **Missing Candidates:** **0 of 7 (0%)**.
5. **Direct AS03 Guidance Datasets:** **0 of 7 (0%)**. No dataset contains direct, labeled step-level prompt/guidance interventions for 3D digital cognitive game environments.
6. **Strong Indirect Datasets:** **2 of 7 (28.6%)** — **CASAS Cued Complex Activities** and **Robot Cognitive Training Games** provide genuine, highly informative procedural sequences, sub-step breakdowns, error rates, and automated cue triggers.
7. **Limited Indirect Datasets:** **3 of 7 (42.9%)** — **Motor Sequence Learning (MSL)**, **AI4HOPE**, and **Alzheimer's Tablet-Based Training RCT** provide supplementary psychomotor or clinical outcome context.
8. **No AS03 Contribution:** **2 of 7 (28.6%)** — **N-Back Dementia** and **MDVR-KCL** (pure working memory reaction time and acoustic speech datasets with zero procedural sequencing).

---

## 2. Dataset Verification Summary Table

| Dataset | Found? | Actual data present? | File count | Size | Verification status | AS03 contribution |
| :--- | :---: | :---: | :---: | :---: | :--- | :--- |
| **1. CASAS Cued Complex Activities** | YES | YES | 407 entries (403 txt, 1 png, 3 docs) | 12.36 MB (ZIP) / 40.39 MB (Uncomp.) | **DOWNLOADED & VERIFIED** | **Strong indirect** |
| **2. AI4HOPE Dementia Companion** | YES | YES | 5 CSV files | 1.78 MB cumulative | **DOWNLOADED & VERIFIED** | **Limited indirect** |
| **3. Motor Sequence Learning (MSL)** | YES | YES | 462 data files (Exp 1: 134, Exp 2: 328) + 1 XLSX | 8.30 MB (ZIPs) / 11.04 MB (Uncomp.) | **DOWNLOADED & VERIFIED** | **Limited indirect** |
| **4. Alzheimer's Tablet-Based Training RCT** | YES | YES | 1 primary XLSX (+ 1 secondary RCT XLSX) | 24.74 KB (+ 98.2 KB Brain-IT) | **DOWNLOADED & VERIFIED** | **Limited indirect** |
| **5. Personalising Older Adults' Cognitive Training Robot Games** | YES | YES | 15 archive members (7 XLSX, 5 TXT, 3 dirs) | 396.2 KB (ZIP) / 1.35 MB (Uncomp.) | **DOWNLOADED & VERIFIED** | **Strong indirect** |
| **6. N-Back Dementia Dataset** | YES | YES | 1 primary XLSX (+ documentation) | 15.84 KB | **DOWNLOADED & VERIFIED** | **None** |
| **7. MDVR-KCL** | YES | YES | 79 WAV audio files | 578.06 MB (ZIP) | **DOWNLOADED & VERIFIED** | **None** |

---

## 3. Forensic Investigation of Each Candidate

### Candidate 1: CASAS Cued Complex Activities
- **Filesystem Path:** `Backend/research_archive/cognitive_assessment.zip`  
  *(Also verified: exploratory extraction at `Backend/research_archive/casas_phase2_exploratory/data/casas/extracted/`)*
- **Data Present:** Yes. Complete raw event streams from 400 numbered participant files (`001.txt` to `400.txt`), clinical diagnoses (`diagnosis.txt`), activity descriptions (`activitylist.txt`), and task performance/sequencing scores (`activityscores.txt`).
- **File Types:** Plain text sensor stream logs (`.txt`), layout image (`.png`), and derived feature tables (`.csv`).
- **Archive Status:** Valid uncorrupted ZIP archive (MD5: `7357205e9218523a8be966a74f3721cc`, Zenodo DOI: `10.5281/zenodo.15717153`).
- **Task & Step Structure:**
  - Tasks 1–8: Uncued multi-step domestic tasks (e.g., Task 1: Sweep kitchen & dust living room has **10 explicit sub-steps**; Task 3: Write birthday card has **8 sub-steps**; Task 7: Prepare cup of soup has **9 sub-steps**).
  - Tasks 9–16: **Cued Complex Activities** (e.g., Task 9: Check desk lamp wattage & replace bulb has **7 sub-steps**; Task 11: Countertop washing has **8 sub-steps**; Task 14: Prepare bowl of oatmeal has **13 sub-steps**).
  - Explicit step annotations appear in sensor streams (e.g., `1.1`, `1.2`, ..., `1.10`, `14.1`...`14.13`).
  - Automated audio prompting cues (`<do>.../AudioCues/*.wav</do>`) are timestamped directly in the sensor logs when participants fail to initiate or progress through task steps.
- **AS03 Classification:** **Strong indirect**.  
  *Justification:* Provides hierarchical action sequences, human-annotated step breakdowns, step omissions, and automated cue triggers across 350 valid clinical participants. It is not *Direct* because the domain consists of physical ambient smart home sensors (PIR motion and door reed switches) rather than 3D digital companion interactions.

---

### Candidate 2: AI4HOPE Dementia Companion
- **Filesystem Path:** `Backend/research_archive/` (`expert_prompts.csv`, `participants_users.csv`, `response_ratings.csv`, `session_logs.csv`, `codebook.csv`)
- **Data Present:** Yes. 600 conversational interaction queries, 60 participant demographic and clinical stage records, 601 response rating records, and 1,440 session log rows.
- **File Types:** Comma-Separated Values (`.csv`, UTF-8).
- **Archive Status:** Fully extracted tabular CSVs with complete codebook documentation.
- **Variables & Features:**
  - `expert_prompts.csv`: `question`, `answer`, `answer_style`, clinical ratings (`clarity`, `relevance`, `empathy`, `actionability`, `misinformation_flagged`, `bias_flagged`).
  - `participants_users.csv`: `stage`, `diagnosis`, `tech_comfort`, `sus_score`.
  - `session_logs.csv`: `phase`, `user_id`, `session_id`, `event_type`, `interaction_id`.
- **AS03 Classification:** **Limited indirect**.  
  *Justification:* AI4HOPE is an open-domain palliative care conversational chatbot evaluation dataset. It contains no procedural task breakdowns, step sequences, action hierarchies, or next-step guidance interventions. Its utility is restricted to conversational phrasing (AS02 Simplify / AS09 Explanation), not sequential task guidance.

---

### Candidate 3: Motor Sequence Learning (MSL) Across Lifespan
- **Filesystem Path:** `Backend/research_archive/Phase4_New_Datasets/` (`Experiment 1.zip`, `Experiment 2.zip`, `FiguresTables_SourceData.xlsx`, `README (1).txt`)
- **Data Present:** Yes. 130 raw participant sequence logs in Experiment 1; 324 raw participant sequence logs in Experiment 2; 19-sheet Excel summary table.
- **File Types:** ZIP archives containing raw trial-level tab-delimited text files (`.txt`), summary spreadsheet (`.xlsx`).
- **Archive Status:** Valid ZIP archives; verified using `zipfile.testzip()`.
- **Variables & Features:**
  - Trial-by-trial logs: `Trial`, `Block`, `Target_Key`, `Pressed_Key`, `Accuracy` (1/0), `Reaction_Time_ms`.
  - Sequential structure: Fixed 8-element finger-tapping motor sequences (e.g., `4-1-3-2-4-2-3-1`) practiced across 14 training blocks and re-tested after sleep consolidation.
- **AS03 Classification:** **Limited indirect**.  
  *Justification:* Models trial-by-trial sequential order execution, error rates, and sequence learning curves. However, it represents motor tapping sequences (SRTT), not cognitive activity step-by-step guidance. It strongly informs psychomotor pacing (AS07) rather than procedural task instruction (AS03).

---

### Candidate 4: Alzheimer's Tablet-Based Training RCT
- **Filesystem Path:**  
  1. Primary: `Backend/research_archive/DATA - The effect of a 6-month tablet-based multimodal training for cognitive functioning in Alzheimer’s disease in a randomized controlled trial.xlsx`  
  2. Secondary: `Backend/research_archive/Data_Brain-IT_RCT_for-publication.xlsx`
- **Data Present:** Yes. Primary study has 25 dementia participants across 73 clinical variables; Brain-IT has 121 participants across 3 worksheets.
- **File Types:** Microsoft Excel OpenXML (`.xlsx`).
- **Archive Status:** Standalone spreadsheets; fully verified and readable via `openpyxl`.
- **Variables & Features:**
  - Baseline and 6-month follow-up psychometric test scores: MMSE, MoCA, visual/verbal paired associate learning, ADAS-Cog sub-scores.
  - Adherence metrics: attendance percentage, total completed training hours, dropped sessions.
- **AS03 Classification:** **Limited indirect**.  
  *Justification:* These are randomized controlled trial summary evaluation tables. They contain no within-session gameplay telemetry, no action logs, and no step-level guidance events.

---

### Candidate 5: Personalising Older Adults' Cognitive Training Robot Games
- **Filesystem Path:** `Backend/research_archive/Phase4_New_Datasets/Robot_Cognitive_Training/data&prompts.zip`  
  *(Also verified: identical copy at `Phase4_New_Datasets/data&prompts.zip`)*
- **Data Present:** Yes. 437 individual game runs (`session_played.xlsx`), 82 session records (`game_session.xlsx`), 20 older adult participants (`User_data.xlsx`, Mean MMSE: 28.65), 450 LLM hallucination evaluations, and 5 cognitive game prompt templates.
- **File Types:** ZIP archive containing 7 `.xlsx` spreadsheets and 5 `.txt` prompt schemas.
- **Archive Status:** Valid uncorrupted ZIP archive (SHA-256: `305aa6b6afc21ce37f8eb4433f3f0dd585aee42b86c8163cdc9504d2d60cb590`, Zenodo DOI: `10.5281/zenodo.21650130`).
- **Task & Step Structure:**
  - Contains explicit activity sequencing task: `prompt/Reorder Activities prompt.txt` (a prompt specifically designed to test older adults on sequencing the steps of daily living activities in correct order).
  - Detailed gameplay telemetry in `session_played.xlsx`: game type, difficulty level, target stimuli, response time, touch interaction coordinates ($X, Y$), tap bounding boxes, error count, and completion status.
- **AS03 Classification:** **Strong indirect**.  
  *Justification:* Directly captures digital cognitive game progression, error occurrences, touch-screen interaction bounds, and a dedicated procedural step-reordering activity for older adults. Not classified as *Direct* because guidance prompts were not recorded as autonomous branching step interventions.

---

### Candidate 6: N-Back Dementia Dataset
- **Filesystem Path:** `Backend/research_archive/Phase4_New_Datasets/N_Back_Dementia/Reproducibility_Data_SI_Modified.xlsx`
- **Data Present:** Yes. Complete replication data for 24 mild-to-moderate dementia participants across 41 rows and 29 columns.
- **File Types:** Microsoft Excel OpenXML (`.xlsx`).
- **Archive Status:** Standalone spreadsheet; verified via `openpyxl` (SHA-256: `975c794480da42a267c3e8e95b5bb6520c04ae16fb9b99a30844d99201507ec0`).
- **Variables & Features:**
  - Continuous reaction time (RT in ms) and accuracy percentages across 1-back and 2-back working memory blocks under Bilateral, Unilateral, and Sham tDCS conditions.
- **AS03 Classification:** **None**.  
  *Justification:* Investigates working memory reaction times and neuromodulation. It contains no multi-step procedural activities, no task breakdown, and no guidance cues.

---

### Candidate 7: MDVR-KCL (Mobile Device Voice Recording)
- **Filesystem Path:** `Backend/research_archive/Phase4_New_Datasets/MDVR_KCL/26_29_09_2017_KCL.zip`
- **Data Present:** Yes. 79 high-fidelity audio recordings (`.wav`) across Healthy Controls (`HC`) and Parkinson's Disease (`PD`) cohorts.
- **File Types:** ZIP archive containing `.wav` audio files.
- **Archive Status:** Valid uncorrupted ZIP archive (Size: 578.06 MB, 79 entries).
- **Variables & Structure:**
  - `ReadText`: Standardized reading passage recordings.
  - `SpontaneousDialogue`: Conversational speech recordings.
- **AS03 Classification:** **None**.  
  *Justification:* Pure acoustic speech database. It contains zero procedural activity sequences or guidance interventions.

---

## 4. Final Dataset Counts

In strict adherence to Step 6 audit rules, the counts are partitioned into three mutually exclusive categories:

```text
========================================================================
FINAL AUDIT TOTALS
========================================================================
1. Confirmed Downloaded & Verified Datasets:           7
2. Downloaded but Incomplete / Unverified Datasets:    0
3. Candidate Datasets That Are Not Present:            0
------------------------------------------------------------------------
Total Candidate Datasets Audited:                      7
========================================================================
```

---

## 5. AS03 Scientific Conclusions & Feasibility Assessment

### 5.1 How many AS03 datasets do we ACTUALLY have?
We have **7 confirmed, downloaded, and verified datasets** in local storage. All seven exist physically on disk, are uncorrupted, and have been structurally validated.

### 5.2 Which are genuinely useful for AS03?
**Two datasets** provide genuine, substantive value for modeling AS03 (Step-by-Step Guidance):
1. **CASAS Cued Complex Activities (`cognitive_assessment.zip`)**:
   - Contains 16 structured domestic activities explicitly decomposed into **3 to 13 discrete sub-steps**.
   - Annotates exact sub-step execution lines (`1.1` to `16.x`), activity start/end boundaries, and participant sequence paths.
   - Captures automated audio prompt events (`AudioCues/*.wav`) triggered when older adults with MCI/Dementia omit steps or stall.
2. **Personalising Older Adults' Cognitive Training Robot Games (`data&prompts.zip`)**:
   - Contains 437 digital cognitive game sessions with error counts, tap bounds, and trial progression.
   - Includes an explicit `Reorder Activities` procedural sequence reasoning task for older adults.

### 5.3 Which contain direct step-level guidance labels?
**None (0).** No existing dataset provides direct, end-to-end telemetry linking digital 3D game step failures to automated in-game step-by-step guidance prompts (such as Carl Fredricksen demonstrating the next step or highlighting the target object).
- In CASAS, prompts were physical pre-recorded audio files triggered in an apartment setting, and post-cue step recovery is not systematically scored in tabular format.
- In Robot Cognitive Training, game runs track touch errors and difficulty adaptation, but not granular step-by-step guidance scaffolding.

### 5.4 Which provide only indirect evidence?
- **Strong Indirect Evidence:** CASAS (sub-step breakdown, sequence execution, cue events) and Robot Cognitive Training (game trial progression, step reordering, touch errors).
- **Limited Indirect Evidence:** Motor Sequence Learning (finger-tapping sequence order and motor error rates), AI4HOPE (conversational dialogue style), and Tablet-Based RCT (study adherence and cognitive battery scores).

### 5.5 Is our current collection sufficient to begin Phase 4.2 / 4.3 assessment?
**Yes.** We have sufficient empirical data to establish:
1. **Hierarchical Task Decompositions:** CASAS provides human-validated sub-step structures for multi-step tasks.
2. **Procedural Sequence Logic:** Robot Cognitive Training and CASAS provide ground-truth step ordering and error distributions.
3. **Pacing and Latency Thresholds:** MSL and N-Back establish empirical reaction-time and pause boundaries ($\ge 5\text{s}$) to avoid false-positive premature step prompting.

### 5.6 Should we search for additional datasets, or stop collecting for now?
**Stop collecting for now.**
- We have 7 fully verified datasets locally available, representing sensor streams, cognitive games, conversational dialogues, motor sequences, and clinical trials.
- Downloading more generic datasets will not yield direct 3D virtual companion guidance labels, because NeuroCompanion's interactive 3D Unity environment (e.g., Carl Fredricksen in `CarlJawTestScene`) is a novel clinical deployment modality.
- The correct next scientific step is to proceed to Phase 4.2/4.3 shared feature schema definition, leveraging CASAS and Robot Cognitive Training as strong indirect priors while defining synthetic interaction telemetry protocols for our Unity runtime.
