# NeuroCompanion Phase 4 — Dataset Download & Collection Report

**Task Type:** READ-ONLY DATASET COLLECTION & AUDIT  
**Parent Framework:** NeuroCompanion Phase 4 AI/ML Subsystem  
**Execution Environment:** Windows (PowerShell), Python 3.14 (`Backend/venv/Scripts/python.exe`)  
**Base Directory:** `C:\Users\Admin\OneDrive\Desktop\NeuroCompanion\Backend\research_archive\Phase4_New_Datasets\`  
**Date:** November 17, 2026  

---

## 1. Executive Summary

This report documents the forensic collection, download verification, and access inspection of candidate research datasets identified in `Best_Datasets_for_All_10_Assistance_Categories.md`. In strict adherence to scientific ethics and project governance:
- No login or authentication restrictions were bypassed.
- No institutional applications were submitted or emails sent.
- Zero synthetic data was generated or substituted for inaccessible files.
- Original filenames and archive structures were preserved exactly as published.
- Existing frozen phases (Phase 1 OASIS model, Phase 2A database & APIs, Phase 3 Unity project, and Phase 4 modular ML code in `Backend/ml/`) were **100% untouched and preserved**.

---

## 2. Dataset Collection Summary Table

| Dataset | Download Status | Files Downloaded | Size | Licence / Access | Population | Relevant Research Purpose |
| :--- | :---: | :--- | :---: | :--- | :--- | :--- |
| **DementiaBank — Pitt Corpus (Data Spreadsheet)** | **DOWNLOADED_AND_VERIFIED** | `PItt-data.xlsx` | 140,661 B (137.4 KB) | Open Document (Spreadsheet) / Consortium Gated (Transcripts) | 104 Controls, 208 Dementia/AD, 85 Unknown ($N=512$ visits) | Longitudinal cognitive decline, MMSE, CERAD, CDR correlation |
| **DementiaBank — Pitt Corpus (Transcripts & Audio)** | **LOGIN_REQUIRED** | None (Gated by Auth) | 0 B | TalkBank Consortium Agreement | Dementia (AD/MCI) and Healthy Controls | Acoustic features, Cookie Theft transcripts, speech pauses |
| **Other DementiaBank Corpora (Delaware, WLS, etc.)** | **LOGIN_REQUIRED** | None (Gated by Auth) | 0 B | TalkBank Consortium Agreement | MCI, AD, and Healthy Older Adults | Conversational pacing, external memory aid interventions |
| **Dem@Care Dataset** | **ACCESS_REQUIRES_APPROVAL** | None (Application Required) | 0 B | Institutional Collaboration Agreement (Recoverable) | Alzheimer's Disease & Healthy Controls | Ambient sensor ADL monitoring (DS9–12), structured tasks (DS6–8) |
| **Gkoumas et al. Longitudinal Corpus** | **ACCESS_REQUIRES_APPROVAL** | None (NDA Required) | 0 B | Institutional NDA Agreement | Dementia Longitudinal Cohort | Acoustic & linguistic embeddings for cognitive trajectory modeling |
| **MDVR-KCL Dataset** | **DOWNLOADED_AND_VERIFIED** | `26_29_09_2017_KCL.zip` | 606,144,431 B (578.1 MB) | Creative Commons Attribution 4.0 (CC-BY 4.0) | **Parkinson's Disease (31) & Healthy Controls (42)** | Motor dysarthria, acoustic timing baselines (AS06, AS07) |
| **Cannings et al. 2024 (Home iPad Prompting)** | **NOT_PUBLICLY_AVAILABLE** | None (No Data Repo Attached) | 0 B | JMIR Open Access Paper / Closed Data | Dementia in Home ADL Activities | Direct iPad prompt modalities (visual, auditory, breakdown) |
| **CogWatch (NCT04044911)** | **NOT_APPLICABLE** | None (IPD Sharing: No) | 0 B | Closed Clinical Trial Record | **Stroke Apraxia & Action Disorganisation** | Stroke ADL tea-making training (Not Dementia / AD) |
| **Perilli et al. 2013 (Video Prompting)** | **NOT_PUBLICLY_AVAILABLE** | None (No Open Data Attached) | 0 B | Closed Journal Access / No Repo | Mild-to-Moderate Alzheimer's Disease | Video vs. verbal vs. pictorial prompting case studies |

---

## 3. Deep-Dive Inspection by Dataset

### A. DementiaBank — Pitt Corpus
- **Official URLs:**
  - Landing Page: `https://talkbank.org/dementia/access/English/Pitt.html`
  - Participant Information Spreadsheet: `https://talkbank.org/dementia/access/0docs/PItt-data.xlsx`
  - Transcript ZIP: `https://talkbank.org/data/dementia/English/Pitt?f=zip`
  - Audio Archive: `https://media.talkbank.org/dementia/English/Pitt`
- **Download Results:**
  - `PItt-data.xlsx` was **successfully downloaded and verified**:
    - Local Path: `C:\Users\Admin\OneDrive\Desktop\NeuroCompanion\Backend\research_archive\Phase4_New_Datasets\DementiaBank_Pitt\PItt-data.xlsx`
    - File Size: `140,661 bytes`
    - SHA-256: `c410e7e0e01ca1fcb69ab9711746ae8740a346893ce758412c311cd14f6a4b93`
    - Structure: 3 worksheets (`readme`, `data`, `variables`).
    - Data Sheet: 512 rows $\times$ 108 clinical variables spanning longitudinal visits across 104 healthy controls, 208 dementia/probable AD participants, and 85 unknown diagnoses. Includes MMSE, CERAD, CDR, and autopsy confirmation.
  - Transcript ZIP and Audio files returned authentication gates:
    - Server response: HTTP 200 with HTML auth redirection to `https://sla2.talkbank.org:443/authUI/authModals.js`.
    - Status: `LOGIN_REQUIRED`.
- **Licence & Access Policy:**
  - DementiaBank data is password-protected and restricted to members of the DementiaBank consortium group.
  - Researchers must register at TalkBank, review Ground Rules, and email `talkbank@cmu.edu` with institutional affiliation, faculty advisor endorsement, and a brief statement of research purpose.

---

### B. Other DementiaBank Corpora
- **Official Portal:** `https://talkbank.org/dementia/access/`
- **Corpora Evaluated:**
  - Protocol Corpora: Baycrest (`English/Protocol/Baycrest.html`), Delaware (`English/Protocol/Delaware.html`).
  - Non-Protocol Corpora: Holland, Kempler, Lanzi, Lu, VAS, WLS, Baycrest-PPA, DePaul, Hopkins.
  - Multilingual Corpora: Jalvingh/PARLO (German), Dem@Care (Greek), Kang (Korean), Chou/Lu/Ye (Mandarin), Ivanova/PerLA (Spanish).
- **Download Status:** `LOGIN_REQUIRED`.
  - Every direct transcript download endpoint (`https://talkbank.org/data/dementia/English/<Corpus>?f=zip`) triggers `initAuthModals` via `sla2.talkbank.org`.
  - Zero unauthenticated ZIP endpoints exist on the server.
- **Access Procedure:**
  - Centralized TalkBank consortium approval is required to access any corpus in this directory.

---

### C. Dem@Care Dataset
- **Official Portal:** `https://demcare.eu/datasets/`
- **Dataset Structure:**
  - `DS1–DS2`: Multi-sensor RGB/depth video recordings.
  - `DS3–DS5`: Audio recordings during clinical and home interactions.
  - `DS6–DS8`: Directed activity protocols (structured task performance).
  - `DS9–DS12`: Home environmental sensors, ambient motion detectors, and wearable trackers.
- **Download Status:** `ACCESS_REQUIRES_APPROVAL`.
- **Access Policy & Application Requirements:**
  - Access is granted upon formal email application to `info@demcare.eu` from an institutional email address.
  - Application must specify requested datasets (Recommended for NeuroCompanion: `DS6` structured protocols and `DS9–DS12` ambient home sensor streams).
  - Requires signed institutional supervisor agreement.
  - License is explicitly recoverable (Dem@Care reserves the right to revoke data access at their sole discretion).

---

### D. Gkoumas et al. Longitudinal Corpus
- **Research Publication:**
  - PMC Article: `https://pmc.ncbi.nlm.nih.gov/articles/PMC11420249/`
  - PDF: `https://www.zubiaga.org/publications/files/gkoumas2024longitudinal.pdf`
- **Data Availability Statement:**
  > *"The raw data itself cannot be made publicly available as this does not comply with our ethics. Yet, it could be made available to interested parties subject to an NDA agreement. In the future, we also aim to make publicly available pre-trained embeddings for linguistic and audio modalities."*
- **Download Status:** `ACCESS_REQUIRES_APPROVAL`.
- **Access Procedure:**
  - Requires formal institutional Non-Disclosure Agreement (NDA) with Queen Mary University of London / principal authors.

---

### E. MDVR-KCL Dataset
- **Official Zenodo Record:** `https://zenodo.org/records/2867216`
- **Direct ZIP URL:** `https://zenodo.org/records/2867216/files/26_29_09_2017_KCL.zip`
- **Download Results:** **`DOWNLOADED_AND_VERIFIED`**
  - Local Path: `C:\Users\Admin\OneDrive\Desktop\NeuroCompanion\Backend\research_archive\Phase4_New_Datasets\MDVR_KCL\26_29_09_2017_KCL.zip`
  - File Size: `606,144,431 bytes` (578.06 MB)
  - SHA-256 Checksum: `45c43cdfa5d078833a6ac27574c471c9ca34d4cd4dea5877f31ebe93b1b5edf6`
  - ZIP Integrity Test: **PASS** (`testzip()` returned `None`; zero CRC errors; 100% uncorrupted).
- **Archive Contents & Hierarchy:**
  - Total Archive Members: 79 (6 directory entries, 73 audio `.wav` files).
  - Directory Structure: `26-29_09_2017_KCL/ReadText/HC/`, `26-29_09_2017_KCL/ReadText/PD/`, and `26-29_09_2017_KCL/SpontaneousDialogue/`.
  - Audio Files Breakdown:
    - Healthy Controls (`HC`): 42 recordings (ReadText: 22, Spontaneous: 20).
    - Parkinson's Disease (`PD`): 31 recordings (ReadText: 15, Spontaneous: 16).
- **Target Population:**
  - **CRITICAL POPULATION DISTINCTION:** The MDVR-KCL dataset contains **Parkinson's Disease patients and Healthy Controls**. It does **NOT** contain Alzheimer's disease or dementia participants.
- **Licence & Permitted Use:**
  - Creative Commons Attribution 4.0 International (CC-BY 4.0). Permits academic and commercial adaptation with appropriate attribution.
- **Research Role in Phase 4:**
  - Provides objective acoustic timing, vocal pitch stability, and speech rate baselines for motor speech impairment (dysarthria), directly informing pacing calibrations for **AS06 (Slower Speech)** and **AS07 (Additional Response Time)**.

---

### F. Cannings et al. 2024 — Home iPad Prompting Study
- **Research Publication:**
  - Journal: *JMIR Aging* 2024;7:e56055
  - DOI: `10.2196/56055`
  - PubMed Central: `PMC11380066`
- **Investigation of Supplementary Data:**
  - Full XML inspection of PMC11380066 confirms `pmc-prop-has-supplement: no`.
  - The study evaluates iPad prompt modalities (auditory alerts, visual step breakdown, attentional cues) across 3 home-based experimental phases.
  - However, no public open-science data repository was linked in the text.
- **Download Status:** `NOT_PUBLICLY_AVAILABLE` / `ACCESS_REQUIRES_APPROVAL`.
- **Access Procedure:** Direct author inquiry would be required for raw log sharing.

---

### G. CogWatch
- **ClinicalTrials.gov Registry:** `https://clinicaltrials.gov/study/NCT04044911`
- **Trial Title:** "Randomised Controlled Trial of a Task Model Approach (CogWatch) to Tea Making Training in Stroke Apraxia and Action Disorganisation Syndrome"
- **Population & Data Sharing Audit:**
  - Population: **Stroke patients with apraxia and action disorganisation syndrome**. Not an Alzheimer's or MCI cohort.
  - Data Sharing Statement: Protocol confirms `IPD Sharing: NO`. Individual participant data is not shared publicly.
- **Download Status:** `NOT_APPLICABLE` / `NOT_PUBLICLY_AVAILABLE`.

---

### H. Perilli et al. 2013 — Video Prompting Study
- **Research Publication:**
  - PubMed ID: `24245381` (PMC10852674)
  - Journal: *American Journal of Alzheimer's Disease & Other Dementias* (2013)
  - Title: "Video prompting versus other instruction strategies for persons with Alzheimer's disease"
- **Investigation of Supplementary Data:**
  - EuropePMC metadata confirms `isOpenAccess: N`, `hasSupplementary: None`, `hasData: N`.
  - This was a dual single-case comparative study evaluating video prompting vs. verbal instruction (Study I) and static pictorial cues (Study II).
  - No raw machine-readable data repository exists.
- **Download Status:** `NOT_PUBLICLY_AVAILABLE`.

---

## 4. Preservation of Existing NeuroCompanion Subsystems

Throughout the execution of this collection and download task, the following project assets were strictly preserved:

1. **Phase 1 (OASIS Model Pipeline):**
   - File `Backend/saved_model/pipeline.joblib` untouched.
   - Preprocessed OASIS data untouched.
2. **Phase 2A (FastAPI Backend & SQLite Database):**
   - Live production database `Backend/data/neurocompanion.db` untouched (0 schema mutations, 0 records added).
   - Core API routes in `Backend/main.py` and `Backend/telemetry/` untouched.
3. **Phase 3 (Unity 3D Foundation):**
   - All Unity scene assets, avatars, and configuration untouched.
4. **Phase 4 Subsystem Code:**
   - Existing modules under `Backend/ml/` and test suite `Backend/tests/test_phase4_ml.py` untouched.
   - All 15 automated unit tests continue to pass 100%.

---

## 5. Next Steps for Restricted Datasets

If the project owner decides to pursue the restricted collections:
1. **DementiaBank (Pitt Transcripts & Audio, Delaware, WLS):** Register on TalkBank (`sla2.talkbank.org`) and submit the standardized consortium membership application via email to `talkbank@cmu.edu` with institutional supervisor sign-off.
2. **Dem@Care (DS6 & DS9–DS12):** Prepare and submit the formal collaboration agreement template to `info@demcare.eu` from an institutional domain.
3. **Gkoumas et al.:** Contact Queen Mary University of London to execute an institutional Non-Disclosure Agreement (NDA).
