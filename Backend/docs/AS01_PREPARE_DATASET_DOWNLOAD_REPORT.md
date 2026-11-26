# AS01 PREPARE Challenge Phase 2 (Acoustic Track) Dataset Download & Inspection Report

**Date:** 2026-11-21  
**Target:** PREPARE Challenge Phase 2 — Acoustic Track (NIH Alzheimer's Acoustic Challenge 2 / DementiaBank)  
**Task ID:** AS01 Dataset Acquisition & Feasibility Evaluation  
**Output Path:** `Backend\docs\AS01_PREPARE_DATASET_DOWNLOAD_REPORT.md`  
**Target Archive Folder:** `Backend\research_archive\Phase4_New_Datasets\PREPARE_DementiaBank\`  

---

## 1. Executive Summary & Download Status

| Metric | Status / Value |
| :--- | :--- |
| **Download Status** | **BLOCKED / AUTH_REQUIRED (Halted at Access Barrier)** |
| **Files Downloaded** | `0` files (`0` bytes) |
| **Automated Bypass Attempted** | **No** (Strict compliance with safety restrictions and ethics) |
| **Primary Sources Inspected** | DrivenData Competition Portal & TalkBank DementiaBank Repository |
| **Access Restriction Level** | **NIH ODSS / TalkBank Controlled Access** (Institutional DUA Required) |

The automated download was safely halted upon encountering active authentication walls and controlled clinical research data agreements on both DrivenData and TalkBank. In accordance with safety policies and ethical guidelines for human subjects data, no credentials or bypass mechanisms were forged.

---

## 2. Source Repositories Inspected

1. **DrivenData Competition 299 (NIH Alzheimer's Acoustic Challenge 2)**:
   - *Problem Description & Data Page*: `https://www.drivendata.org/competitions/299/competition-nih-alzheimers-acoustic-2/page/929/#data`
   - *Data Download Endpoint*: `https://www.drivendata.org/competitions/299/competition-nih-alzheimers-acoustic-2/data/`
   - *Competition Rules*: `https://www.drivendata.org/competitions/299/competition-nih-alzheimers-acoustic-2/rules/`
2. **TalkBank / DementiaBank Dedicated Challenge Portal**:
   - *Portal Page*: `https://talkbank.org/dementia/DrivenData/`
   - *Media Server*: `https://media.talkbank.org/dementia/English/0extra/DrivenData/`
   - *DementiaBank Access Specifications*: `https://talkbank.org/dementia/access.html`

---

## 3. Catalog of Dataset Files & Specifications

Based on the verified inspection of the data specification and repository directory, the PREPARE Acoustic Track comprises the following components:

### A. Tabular & Acoustic Feature Files (DrivenData)
| Filename | Expected Size / Volume | Format | Description |
| :--- | :--- | :--- | :--- |
| `metadata.csv` | ~2,058 rows | CSV | Participant metadata: `uid`, `age`, `gender`, `split` (`train`/`test`), `hash`, `filesize_kb`. |
| `train_labels.csv` | 1,646 rows | CSV | Ground truth diagnostic labels (one-hot: `diagnosis_control`, `diagnosis_mci`, `diagnosis_adrd`). |
| `submission_format.csv` | 412 rows | CSV | Template for prediction submission. |
| `train_features.csv` | ~1,646 subjects | CSV | Pre-extracted 88 eGeMAPS acoustic features (openSMILE v02 standard set) over 0.2s windows. |
| `test_features.csv` | ~412 subjects | CSV | Pre-extracted 88 eGeMAPS acoustic features for evaluation subjects. |

### B. Audio Data (TalkBank Media Repository)
| Filename | Size | Format | Description |
| :--- | :--- | :--- | :--- |
| `train_audios.zip` | 391 MB | ZIP / MP3 | 1,646 diarized and spliced ~30-second speech recordings (1 per training individual). |
| `test_audios.zip` | 98 MB | ZIP / MP3 | 412 diarized and spliced ~30-second speech recordings (1 per test individual). |
| `train_audios_sample.zip`| 3.5 MB | ZIP / MP3 | 15 sample audio clips for inspection. |

### C. Checksum Verification
- **SHA-256 Checksums:** `N/A` (Files could not be downloaded without authenticated credentials; checksum calculation deferred until authorized researcher download).

---

## 4. Access Barriers & Governance Requirements

Both hosting platforms enforce strict access gates:

### Barrier 1: DrivenData Gate
- Navigating directly to `https://www.drivendata.org/competitions/299/competition-nih-alzheimers-acoustic-2/data/` triggers an immediate HTTP 302 redirect to `https://www.drivendata.org/accounts/login/?next=...`.
- Requires an active DrivenData account, competition enrollment, and formal agreement to the DrivenData Competition Rules and Terms of Use.

### Barrier 2: TalkBank / DementiaBank Controlled Access
- The media endpoint `https://media.talkbank.org/dementia/English/0extra/DrivenData/` responds with an authentication gate (HTTP 401 / credential modal).
- DementiaBank classifies all primary patient multimedia as **Controlled Access**:
  > *"Access to the data in DementiaBank is password protected and restricted to members of the DementiaBank consortium group."*
- **Requirements for consortium membership:**
  1. Primary applicant must be an established researcher or clinical investigator with institutional affiliation (.edu / hospital / research institute).
  2. Applicant must send a formal request to `talkbank@cmu.edu` or `talkbank@andrew.cmu.edu` detailing institutional status and specific research objectives.
  3. Acceptance of the **TalkBank Ground Rules** (citing repository contributions, no re-distribution, non-commercial research use, protection of participant privacy).
  4. Students and postgraduates cannot apply directly; a faculty advisor must submit the request, sponsor the researcher, specify an access term (< 1 year), and assume legal responsibility.

---

## 5. Scientific Suitability Analysis for AS01 (Repeat Instruction)

### 5.1 Dataset Population & Target Constructs
- **Cohort:** 2,058 older adults categorized into:
  - `Control`: Cognitively unimpaired individuals.
  - `MCI`: Mild Cognitive Impairment.
  - `ADRD`: Alzheimer's Disease and Related Dementias.
- **Task Modality:** Short (~30s) continuous speech elicited through standardized cognitive assessments (e.g., Cookie Theft picture description, storytelling, or verbal interviews).

### 5.2 Relevance to AS01: Indirect Acoustic Evidence Only
- **AS01 Definition:** Repeat Instruction is a behavioral indicator where a patient requires instructions or conversational prompts to be repeated due to working memory degradation, receptive language impairment, or attention lapses.
- **Potential Utility:**
  - Provides a benchmark for acoustic degradation in cognitive decline (prosodic flattening, prolonged pause durations, reduced speech rate, altered pitch variability via eGeMAPS).
  - Can serve as a negative/positive acoustic background model to differentiate speech hesitation caused by cognitive impairment from acoustic background noise.

### 5.3 Critical Limitations (Construct Non-Equivalence)
> [!IMPORTANT]
> **PREPARE Phase 2 is NOT a direct AS01 dataset.**
> 
> 1. **Absence of AS01 Labels:** The dataset does not contain labels for repeated instructions, conversational breakdowns, prompt-response latencies, or caregiver prompts.
> 2. **Spliced Monologues vs. Dialogic Interaction:** The audio clips are isolated 30-second monologues from cognitive tests, not interactive dialogues where instructions are given and repeated.
> 3. **Construct Mismatch:** Training an AS01 classifier directly on this dataset would result in a model that predicts clinical dementia diagnosis from acoustic features, **not** whether a patient requires an instruction to be repeated.
> 4. **Scientific Requirement:** This dataset can only serve as secondary pre-training or acoustic feature calibration, not as supervised ground truth for AS01 detector models.

---

## 6. Action Items & Next Steps for Authorized Access

To proceed with acquiring this dataset for secondary acoustic baseline modeling:

1. **Faculty / PI Application:** Have the designated institutional Principal Investigator (PI) register at [TalkBank](https://talkbank.org/dementia/) and email `talkbank@cmu.edu` requesting DementiaBank consortium access for the PREPARE DrivenData acoustic track.
2. **Manual Download:** Once credentials are confirmed, log in via a browser session and save the verified archives into:
   `Backend\research_archive\Phase4_New_Datasets\PREPARE_DementiaBank\`
3. **Verification:** Generate and record SHA-256 hashes for `train_audios.zip`, `test_audios.zip`, and feature tables.
4. **Safety Compliance:** Continue adhering strictly to NeuroCompanion Phase 4 safeguards (no direct ML code modifications, no synthetic clinical data generation, no unauthorized cloud processing).
