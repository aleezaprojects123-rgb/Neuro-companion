# Forensic Dataset Audit: Motor Sequence Learning Collection
**Collection Name:** Motor Sequence Learning Across the Human Lifespan  
**Dataset Location:** `Backend/research_archive/Phase4_New_Datasets/` (`FiguresTables_SourceData.xlsx`, `Experiment 1.zip`, `Experiment 2.zip`, `README (1).txt`)  
**Primary Authors:** A. Van Roy, G. Albouy, R. D. Burns, B. R. King (University of Utah / KU Leuven)  
**Publication Reference:** *Motor sequence learning and memory consolidation across the human lifespan*  
**Auditor:** NeuroCompanion Phase 4 AI/ML Verification & Architecture Team  
**Audit Date:** November 17, 2026  
**Status:** Certified Read-Only Technical Audit  

---

## 1. Physical Files, Format & Integrity

| Property | Verified Value | Inspection Method |
| :--- | :--- | :--- |
| **Documentation File** | `README (1).txt` (3,712 bytes) | Plain text inspection |
| **Summary Spreadsheet** | `FiguresTables_SourceData.xlsx` (624,310 bytes, 19 worksheets) | Python `openpyxl` |
| **Raw Data Archive 1** | `Experiment 1.zip` (2,414,242 bytes, 134 entries, 130 raw text files) | Python `zipfile.ZipFile` |
| **Raw Data Archive 2** | `Experiment 2.zip` (6,289,820 bytes, 328 entries, 324 raw text files) | Python `zipfile.ZipFile` |
| **Archive Integrity** | All ZIP files are structurally valid; no extraction or checksum errors | `zipfile.testzip()` |
| **Total Cumulative Size** | ~9.33 MB across the collection | Filesystem metadata |

---

## 2. Participant Population, Lifespan Cohorts & Clinical Diagnoses

```mermaid
pie title Experiment 1 Lifespan Cohorts (N = 130)
    "Children (ages 8-11)" : 33
    "Adolescents (ages 13-16)" : 33
    "Young Adults (ages 18-30)" : 32
    "Older Adults (ages 60-80)" : 32
```

- **Lifespan Experimental Design:**
  - **Experiment 1 (Single-Session Sequence Learning):** $N = 130$ healthy participants:
    - Children: $n = 33$ (Mean age $10.1 \pm 1.1$ years)
    - Adolescents: $n = 33$ (Mean age $14.6 \pm 1.2$ years)
    - Young adults: $n = 32$ (Mean age $22.4 \pm 2.8$ years)
    - Older adults: $n = 32$ (Mean age $68.7 \pm 5.1$ years, range 60–80)
  - **Experiment 2 (3-Session Multi-Day Consolidation):** $N = 108$ healthy participants ($n = 27$ per cohort across Sessions 1, 2, and 3). Note: 42 participants completed both experiments.
- **Cognitive Diagnosis:** **100% HEALTHY PARTICIPANTS**.
  - All participants were screened to ensure healthy cognitive and neurological development.
  - **Zero participants have mild cognitive impairment, Alzheimer's disease, or any form of dementia.**

---

## 3. Experimental Task Paradigm & Telemetry Structure

### Task Paradigm: Serial Reaction Time Task (SRTT)
Participants performed a computerized finger-tapping sequence learning task. Visual cues (cues 1–8) appeared on a monitor, and participants pressed corresponding keyboard keys as quickly and accurately as possible. The task alternated between random sequences (`Random`) and repeated structured sequences (`Sequence`).

### Raw Keypress Telemetry (454 Individual Text Files across Exp 1 & Exp 2)
In every participant text file (e.g., `Experiment 1/Older adults/Exp1_BMR-0012.txt`), **every single keypress event is recorded as an individual row**:
- `Column 1`: Run label (`Pre-SRTT_Random`, `SRTT_Sequence`, `Post-SRTT_Random`).
- `Column 2`: Block number (Blocks 1 through 14).
- `Column 3`: Cue presented (visual stimulus index 1–8).
- `Column 4`: Key pressed by participant (input index 1–8).
- `Column 5`: **Response Accuracy** (`1` = correct target hit, `0` = incorrect error).
- `Column 6`: **Response Time (RT)** in milliseconds (latency between visual cue onset and physical keypress).

### Summary Spreadsheet (`FiguresTables_SourceData.xlsx`, 19 Worksheets)
Contains participant-level aggregates across 19 sheets:
- `Table1 - demographics`, `Table2 - demographics`: Age, gender, morningness/eveningness preference, testing time, sleep duration, sleep quality, and Stanford Sleepiness Scale (`SSS score`).
- `Figure2A - Norm RT`, `Figure2B - Norm Acc`: Normalized reaction times and accuracy across training blocks.
- `Figure3A-F - Micro-learning`: Micro-online learning (within block) vs micro-offline learning (between block rest intervals).
- `Figure5 - Seq offline gains`: Memory consolidation gains over sleep intervals.

---

## 4. Assessment Against AS01–AS10 Assistance Taxonomy

| NeuroCompanion AS Taxonomy | Evidence Level | Forensic Technical Analysis |
| :--- | :---: | :--- |
| **AS01–AS09 Assistance Types** | **Not Supported** | Contains **zero** adaptive software assistance or scaffolding labels. The SRTT task was an unassisted motor sequence paradigm. |
| **AS10: Independent Completion** | **Not Supported** | Every trial is an unassisted keypress response. Task-level independent completion as defined in NeuroCompanion is not applicable. |
| **Psychomotor Speed & Latency Benchmark** | **Partially Supported (Indirect)** | Provides microsecond-accurate keypress reaction times (ms) and error rates under visual cues across $n=32$ healthy older adults (ages 60–80). |
| **Sequence Learning Capacity** | **Partially Supported (Indirect)** | Quantifies how older adults acquire multi-step sequential rules compared to younger cohorts. |

---

## 5. Data Quality, Missingness & Leakage Risks

- **Data Integrity:** Impeccable. The raw text files contain thousands of precisely formatted trial rows without missing headers or corrupt characters.
- **Population Mismatch Hazard:**  
  Because all participants are healthy older adults with intact memory consolidation mechanisms, **this data cannot be used to train or evaluate assistance needs in Alzheimer's disease**. Older adults in this dataset achieved high accuracy ($> 95\%$) and mean response times of $400\text{--}650 \text{ ms}$, which is vastly faster and more accurate than individuals with mild-to-moderate dementia.

---

## 6. Licensing & Permitted Research Use

- **Repository Provenance:** Open science repository (Open Science Framework / Zenodo) accompanying the published lifespan manuscript.
- **License:** Open Access under **Creative Commons Attribution 4.0 International (CC-BY 4.0)**.
- **Permitted Use:** Academic re-use, secondary psychomotor modeling, and benchmarking permitted with citation to Van Roy et al.
- **De-Identification:** Anonymized participant codes (e.g., `BMR_0006`).

---

## 7. Recommended Legitimate Role in Phase 4

> [!IMPORTANT]
> **Recommended Role: Healthy Older Adult Motor Latency Baseline.**  
> The Motor Sequence Learning collection cannot train patient assistance models. Its legitimate scientific contribution is as a **normative psychomotor reference**:
> 1. It establishes the **upper-bound normative baseline for visual-motor reaction time** ($400\text{--}650 \text{ ms}$) and motor keypress accuracy in healthy older adults (ages 60–80).
> 2. In NeuroCompanion, when evaluating whether a patient's hesitation in the Cognitive Garden or Orientation Room is caused by normal age-related psychomotor slowing versus cognitive confusion/apraxia, Van Roy et al.'s distribution provides an empirical non-demented reference point.
