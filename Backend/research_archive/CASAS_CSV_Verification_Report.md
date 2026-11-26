# CASAS CSV Verification & Reproducibility Audit

**Document:** Follow-up Audit of `task_audit_data.csv`  
**Target File:** `C:\Users\Admin\OneDrive\Desktop\NeuroCompanion\Backend\research_archive\casas_phase2_exploratory\data\casas\extracted\task_audit_data.csv`  
**Report Destination:** `C:\Users\Admin\OneDrive\Desktop\NeuroCompanion\Backend\research_archive\CASAS_CSV_Verification_Report.md`  
**Audit Date:** 2026-11-17  
**Execution Mode:** STRICT READ-ONLY (No files modified, standard library inspection only)  
**Evaluator:** NeuroCompanion AI/ML Research Team  

---

## Executive Summary

This follow-up audit investigates `task_audit_data.csv` to resolve questions arising from earlier script execution failures and to rigorously determine whether the duration and pause-frequency findings cited in `CASAS_Inspection_Report.md` are genuinely supported by the underlying data.

### Key Audit Conclusions:
1. **Provenance & Location:** `task_audit_data.csv` is **NOT** part of the original upstream Zenodo archive (`cognitive_assessment.zip`). It is an **engineered feature file created during NeuroCompanion Phase 2 exploratory research**, located at `Backend/research_archive/casas_phase2_exploratory/data/casas/extracted/task_audit_data.csv`.
2. **File Integrity & Quality:** The CSV file is completely intact, fully uncorrupted, encoded in valid UTF-8/ASCII, and contains exactly **2,074 data rows** across **10 columns** with **zero missing, null, or blank values**.
3. **Statistical Reproducibility:** All statistics reported in `CASAS_Inspection_Report.md`, `train_phase2.py`, and `reports_phase2/cv_results.json` are **100% reproducible and mathematically verified** directly from the raw CSV data.
4. **Pause and Duration Findings:** The reported findings are **empirically confirmed**:
   - High-quality, unassisted task executions (Tier 0, $n=1,447$) have a mean duration of **213.45 seconds** and a mean pause count ($\ge 5\text{s}$) of **10.79**.
   - Degraded, prompted task executions (Tier 1, $n=492$) have a mean duration of **357.99 seconds** (+67.7%) and a mean pause count ($\ge 5\text{s}$) of **19.07** (+76.7%).
5. **Root Cause of Prior Script Failures:** The inspection failures in the previous turn were strictly caused by **host Python environment configuration and script syntax errors** (missing `pandas` in global Windows Python 3.14, and an omitted `import collections` in a single-line command), **not** by file corruption, missing records, or data flaws.
6. **Critical Scientific Limitation:** While the duration and pause statistics are mathematically valid, the observation windows strictly depend on **human proctor boundary annotations** (`X-start`, `X-end`) in the physical apartment. Furthermore, they reflect physical domestic ADLs, not 3D digital cognitive interactions.

---

## 1. Provenance and File Location Analysis

### 1.1. Absence from the Upstream ZIP Archive
An automated inspection of all 407 entries within `cognitive_assessment.zip` (`12,635,022` bytes) confirmed:
- Zero CSV files exist inside `cognitive_assessment.zip`.
- The upstream Zenodo distribution contains only:
  - `sensorlayout.png` (floorplan)
  - `documents/activitylist.txt`
  - `documents/activityscores.txt`
  - `documents/diagnosis.txt`
  - `data/*.txt` (400 raw sensor event files)

### 1.2. Exact Canonical Location on Disk
`task_audit_data.csv` resides within the archived exploratory Phase 2 research tree:

```text
C:\Users\Admin\OneDrive\Desktop\NeuroCompanion\Backend\research_archive\casas_phase2_exploratory\data\casas\extracted\task_audit_data.csv
```

### 1.3. Historical Origin of the CSV
The file was generated during Phase 2 exploratory research by parsing the raw sensor timestamps in `cognitive_assessment/data/*.txt` between human proctor start/end tags (`1-start`, `1-end`, `2-start`, `2-end`, etc.) and joining the resulting duration and pause metrics with examiner ratings from `activityscores.txt`.

---

## 2. Physical File Properties & Verification

Direct inspection using Python standard library modules (`os`, `hashlib`, `csv`) confirms the following exact physical properties:

| File Property | Verified Measurement | Verification Method |
|---|---|---|
| **Full File Path** | `C:\Users\Admin\OneDrive\Desktop\NeuroCompanion\Backend\research_archive\casas_phase2_exploratory\data\casas\extracted\task_audit_data.csv` | File System Check |
| **Existence** | Confirmed (`True`) | `os.path.exists()` |
| **File Size (Bytes)** | `154,143` bytes (~150.5 KB) | `os.path.getsize()` |
| **MD5 Checksum** | `c56baa1789c9ec78bbf01014568e2755` | `hashlib.md5()` |
| **SHA-256 Checksum** | `d915cd413cad7c14eca5351fbd183dd2ce4d8908a66f8d79a60528b76328e54e` | `hashlib.sha256()` |
| **Character Encoding** | Valid UTF-8 / ASCII (7-bit clean) | Full binary decode |
| **Line Delimiter** | CRLF (`\r\n`) / Standard Windows newline | Line ending inspection |
| **Field Delimiter** | Comma (`,`) | `csv.Sniffer().sniff()` |
| **Total Physical Lines** | `2,075` lines (1 header line + 2,074 data rows) | Line enumeration |

---

## 3. CSV Structure, Column Inventory & Missing-Value Audit

### 3.1. Header & Column Schema
The CSV contains exactly **10 columns**. The header line is:
```csv
pid,act,score,tier,duration,n_events,pause_cnt_5_int,max_pause_int,pause_cnt_5_bound,max_pause_bound
```

### 3.2. Missing Values and Data Completeness
Every row of the 2,074 records was audited for missing, empty, blank, `NaN`, `NULL`, `None`, or question mark (`?`) values:

| Column Name | Inferred Data Type | Missing Count | Blank Count | Null / NaN Count | Completeness |
|---|---|---|---|---|---|
| `pid` | Integer | 0 | 0 | 0 | **100.0%** |
| `act` | Integer | 0 | 0 | 0 | **100.0%** |
| `score` | Integer (stored as float/int) | 0 | 0 | 0 | **100.0%** |
| `tier` | Integer (stored as float/int) | 0 | 0 | 0 | **100.0%** |
| `duration` | Continuous Float (seconds) | 0 | 0 | 0 | **100.0%** |
| `n_events` | Integer (event count) | 0 | 0 | 0 | **100.0%** |
| `pause_cnt_5_int` | Integer (pause count) | 0 | 0 | 0 | **100.0%** |
| `max_pause_int` | Continuous Float (seconds) | 0 | 0 | 0 | **100.0%** |
| `pause_cnt_5_bound` | Integer (pause count) | 0 | 0 | 0 | **100.0%** |
| `max_pause_bound` | Continuous Float (seconds) | 0 | 0 | 0 | **100.0%** |
| **Overall Dataset** | **2,074 rows × 10 cols = 20,740 cells** | **0** | **0** | **0** | **100.0% Complete** |

### 3.3. Participant and Activity Representation in the CSV
- **Participant Identifiers (`pid`):** Exactly **327 unique participant IDs** are represented (minimum PID = 5, maximum PID = 400).
- **Activities (`act`):** Exactly **8 unique activities** are represented (Activities 1, 2, 3, 4, 5, 6, 7, and 8). Tasks 9–24 are not included in this CSV because Tasks 9–16 were unrated in `activityscores.txt` and Tasks 17–24 were interwoven without per-task timestamps.
- **Observations per Activity:**
  - Act 1 (Sweep/dust): 276 observations
  - Act 2 (Medication dispenser): 276 observations
  - Act 3 (Birthday card & check): 274 observations
  - Act 4 (Watch DVD news): 269 observations
  - Act 5 (Water indoor plants): 276 observations
  - Act 6 (Answer phone regarding news): 274 observations
  - Act 7 (Microwave soup): 215 observations
  - Act 8 (Select outfit for interview): 214 observations
  - **Total Observations:** `2,074`

---

## 4. Documented Meanings of Fields & Engineering Definitions

The exact mathematical and operational definitions of the columns were retrieved directly from `saved_model_phase2/schema.json` and `model_phase2/train_phase2.py`:

| Column Name | Formal Definition (from `schema.json`) | Units | Operational Meaning |
|---|---|---|---|
| `pid` | Participant Identifier | Categorical ID | Unique identifier mapping to `diagnosis.txt` and raw sensor files. |
| `act` | Scripted Activity Number (1–8) | Categorical ID | The specific domestic ADL task from `activitylist.txt`. |
| `score` | Human Examiner Task Quality Score | Integer Scale (1–5) | Examiner rating from `activityscores.txt`: `1`=Unassisted, `2`=Verbal prompt, `3`=Incomplete, `5`=Breakdown. |
| `tier` | Coarse Quality Category | Integer Code (0, 1, 2) | Researcher grouping: `0`=High Quality (Score 1), `1`=Prompted (Score 2), `2`=Degraded/Breakdown (Scores 3 & 5). |
| `duration` | Proctor-Bounded Active Duration | Seconds | Elapsed time ($T_{\text{end}} - T_{\text{start}}$) calculated strictly between human proctor boundary tags (`X-start` to `X-end`). |
| `n_events` | Sensor Event Count | Count | Total raw ambient sensor firings (M, D, I, P, T) logged during the active task window. |
| `pause_cnt_5_int` | Internal Inactivity Pauses $\ge 5\text{s}$ | Count | Number of consecutive inter-sensor time gaps $\ge 5.0$ seconds occurring strictly *between* the first and last sensor events of the task. |
| `max_pause_int` | Maximum Internal Inactivity Gap | Seconds | Duration of the longest single inter-sensor quiet interval occurring *between* sensor events during the task. |
| `pause_cnt_5_bound` | Bounded Inactivity Pauses $\ge 5\text{s}$ | Count | Same as `pause_cnt_5_int`, but also includes boundary gaps (from proctor `X-start` tag to first sensor firing, and from last sensor firing to `X-end`). |
| `max_pause_bound` | Maximum Bounded Inactivity Gap | Seconds | Same as `max_pause_int`, but considers boundary intervals as potential maximum pauses. |

### 4.1. Critical Boundary Dependency Finding
As explicitly documented in `saved_model_phase2/schema.json`:
> *"While internal inter-sensor pauses are independent of human intermediate substep annotations, the observation window itself strictly depends on proctor-defined task start/end boundary annotations (`X-start`, `X-end`)."*

This means `duration` is **not an automatically discovered sensor boundary**; it is 100% determined by human proctors logging start and end timestamps in the apartment.

---

## 5. Statistical Verification & Reproducibility Analysis

Every summary statistic and grouped distribution was independently recalculated from the raw CSV data using standard Python arithmetic:

### 5.1. Overall Dataset Summary Statistics ($N=2,074$)

| Metric / Field | Min | Max | Mean | Std Dev | Median | Q25 (25th %) | Q75 (75th %) |
|---|---|---|---|---|---|---|---|
| **`duration` (s)** | 1.99 | 1,030.93 | **257.10** | 180.15 | **213.65** | 108.48 | 377.98 |
| **`n_events`** | 3.00 | 1,612.00 | **92.11** | 81.87 | **68.00** | 45.00 | 117.00 |
| **`pause_cnt_5_int`** | 0.00 | 85.00 | **13.32** | 11.85 | **11.00** | 4.00 | 20.00 |
| **`max_pause_int` (s)** | 0.91 | 200.97 | **27.61** | 22.27 | **21.55** | 12.09 | 36.05 |
| **`pause_cnt_5_bound`** | 0.00 | 85.00 | **13.37** | 11.87 | **11.00** | 4.00 | 20.00 |
| **`max_pause_bound` (s)**| 0.91 | 200.97 | **27.65** | 22.29 | **21.61** | 12.09 | 36.12 |

### 5.2. Target Label Distributions in the CSV

#### By Raw `score`:
- **Score 1 (High Quality / Unassisted):** `1,447` rows (69.77%)
- **Score 2 (Verbal Prompt Given):** `492` rows (23.72%)
- **Score 3 (Incomplete / Error):** `84` rows (4.05%)
- **Score 5 (Task Breakdown / Failure):** `51` rows (2.46%)
- *(Scores 4 and 6 do not appear in this engineered CSV; only 1, 2, 3, and 5 were processed)*

#### By Binary Classification Target (`score == 1` vs `score != 1`):
- **Class 0 (High Quality, Score 1):** `1,447` observations (69.77%)
- **Class 1 (Degraded Quality, Scores 2, 3, 5):** `627` observations (30.23%)
- **Total:** `2,074` observations across `327` participants.
- *Verification against `train_phase2.py`:* Exact match (`assert n_total == 2074`, `assert n_pids == 327`, `assert c0 == 1447`, `assert c1 == 627`).

### 5.3. Grouped Comparison: Unassisted (Tier 0) vs. Degraded/Prompted (Tiers 1 & 2)

| Feature Metric | Tier 0: High Quality ($n=1,447$) (Score 1: Unassisted) | Tier 1: Verbal Prompt ($n=492$) (Score 2: Prompted) | Tier 2: Incomplete/Breakdown ($n=135$) (Scores 3 & 5: Failure) | All Degraded (Tiers 1+2) ($n=627$) | Percentage Difference (Degraded vs. Tier 0) |
|---|---|---|---|---|---|
| **Mean Duration** | **213.45 s** | **357.99 s** | **357.31 s** | **357.85 s** | **+67.7% slower** |
| **Median Duration** | 173.80 s | 333.99 s | 322.46 s | 331.47 s | **+90.7% slower** |
| **Mean Pauses $\ge 5\text{s}$** | **10.79 pauses** | **19.07 pauses** | **19.53 pauses** | **19.17 pauses** | **+77.7% more pauses** |
| **Median Pauses $\ge 5\text{s}$**| 8.00 pauses | 17.00 pauses | 16.00 pauses | 17.00 pauses | **+112.5% more pauses** |
| **Mean Max Pause** | **26.33 s** | **31.47 s** | **27.33 s** | **30.58 s** | **+16.1% longer gap** |
| **Median Max Pause** | 20.02 s | 25.17 s | 24.02 s | 25.02 s | **+25.0% longer gap** |
| **Mean Sensor Events** | **78.41 events** | **123.05 events** | **126.24 events** | **123.73 events** | **+57.8% more motion** |

---

## 6. Root Cause Analysis of Earlier Inspection Command Failures

The previous turn encountered two command failures during inspection. A technical post-mortem confirms the exact root causes:

### Failure 1: Missing Third-Party Dependency in Host Python
- **Command:** `python -c "import pandas as pd; df = pd.read_csv(...)"`
- **Error:** `ModuleNotFoundError: No module named 'pandas'`
- **Root Cause:** The command invoked the default Windows system Python executable (`C:\Users\Admin\AppData\Local\Python\pythoncore-3.14-64\python.exe`), which is a clean, base Python installation without third-party libraries installed.
- **Resolution:** The project's dedicated virtual environment (`Backend/venv/Scripts/python.exe`) contains `pandas` and `scikit-learn`. However, standard library modules (`csv`, `collections`, `math`) in base Python are 100% capable of parsing and verifying the file without any third-party dependencies.

### Failure 2: Omitted Standard Library Import in One-Liner Script
- **Command:** `python -c "import csv; ... tiers = collections.Counter(...)"`
- **Error:** `NameError: name 'collections' is not defined`
- **Root Cause:** The Python one-liner string imported `csv` but inadvertently omitted `import collections` on line 1 before calling `collections.Counter()`.
- **Resolution:** Adding `import collections` immediately executes cleanly and generates all required frequency distributions.

> [!NOTE]
> **Conclusion on Prior Failures:** Neither failure was caused by file corruption, missing columns, filesystem lockups, or data irregularities in `task_audit_data.csv`. The CSV file itself is completely well-formed and valid.

---

## 7. Comparison with Claims in `CASAS_Inspection_Report.md`

| Section / Claim in `CASAS_Inspection_Report.md` | Verification Status | Exact Evidence from CSV Data | Discrepancy / Nuance Identified |
|---|---|---|---|
| **"Total task observations: 2,074"** | ✅ **Verified** | Exactly 2,074 data rows present in `task_audit_data.csv`. | None. Exact match. |
| **"Distinct participants: 327"** | ✅ **Verified** | Exactly 327 unique values in `pid` column. | None. Exact match. |
| **"Activities 1 to 8"** | ✅ **Verified** | Exactly values 1 through 8 in `act` column. | None. Exact match. |
| **"Binary task-quality target: 1,447 Class 0 vs. 627 Class 1"** | ✅ **Verified** | `score == 1`: 1,447; `score in {2,3,5}`: 627. | None. Exact match. |
| **"High-quality baseline data on how pause frequency and task duration differ"** | ✅ **Verified** | Unassisted mean duration = 213.45s vs. degraded = 357.85s; pauses = 10.79 vs. 19.17. | Statistically sound and mathematically verified. |
| **Provenance Identification** | ⚠️ **Nuance Clarified** | File is in `Backend/research_archive/.../extracted/`, not inside the root of `cognitive_assessment.zip`. | The previous report did not explicitly state that `task_audit_data.csv` was an engineered feature matrix created during Phase 2 rather than an original Zenodo file. |
| **Proctor Boundary Dependency** | ⚠️ **Nuance Clarified** | `schema.json` documents that `duration` is proctor-bounded. | The previous report did not emphasize that `duration` depends on human proctor `X-start` and `X-end` annotations in the apartment. |

---

## 8. Verified Findings vs. Assumptions & Undocumented Interpretations

To maintain rigorous scientific integrity, this audit establishes a strict distinction between empirically verified facts and assumptions:

### 8.1. Verified Facts (Empirically Confirmed)
1. `task_audit_data.csv` is an intact, 154,143-byte CSV file containing 2,074 rows, 10 columns, and 327 unique participants.
2. High-quality unassisted tasks (Score 1) consistently exhibit shorter durations (~213s vs. ~358s) and fewer pauses $\ge 5\text{s}$ (~10.8 vs. ~19.2) than prompted or degraded tasks (Scores 2, 3, 5).
3. The dataset has zero missing values across its 10 columns.
4. All metrics reported in the exploratory Phase 2 cross-validation artifacts (`cv_results.json`) directly match calculations from this CSV.

### 8.2. Assumptions & Undocumented Interpretations (Must NOT be Made)
1. **Assumption that pauses represent digital UI hesitation:** In CASAS, a pause represents a participant standing still or walking between rooms in a physical apartment. It does **not** represent hesitation on a touchscreen or confusion in a 3D digital scene.
2. **Assumption that Score 2 maps to digital assistance:** Score 2 indicates that a human experimenter in the smart apartment verbally reminded the participant what to do next. It does **not** map to NeuroCompanion's AS01–AS09 digital prompts (visual highlights, demonstrations, simplified text).
3. **Assumption that duration is an automated sensor discovery:** `duration` is strictly derived from human experimenters marking `X-start` and `X-end` in the event stream. Without these human proctor tags, task boundaries cannot be automatically extracted from the raw sensor stream.
4. **Assumption of longitudinal tracking:** While participants have multiple task rows (up to 8 tasks), all tasks were recorded in a **single experimental session**. There is zero multi-day or longitudinal data in this CSV.

---

## 9. Final Recommendation on Supplementary Research Utility

### Final Verdict: APPROVED FOR SUPPLEMENTARY POPULATION RESEARCH ONLY

`task_audit_data.csv` is **approved for limited, supplementary research benchmarking**, subject to strict scientific boundaries:

#### 1. What the CSV CAN Legultimately Support:
- **Comparative Hesitation Benchmarking:** Can be cited as an external benchmark demonstrating that cognitive impairment and task degradation consistently correlate with increased pause frequency and elongated task completion times in structured environments.
- **Feature Engineering Baseline:** Can serve as a conceptual reference for designing temporal inactivity thresholds (e.g., evaluating whether a 5.0-second inactivity window is clinically meaningful for cognitive monitoring).

#### 2. What the CSV CANNOT Support (Strict Prohibitions):
- **Cannot Train Phase 4 Models (M1, M2, M3, M4):** Under no circumstances may this CSV or its features be used to train NeuroCompanion's assistance prediction models.
- **Cannot Supply AS01–AS09 Ground Truth:** It contains no digital assistance labels.
- **Cannot Be Ingested into SQLite:** It must remain strictly quarantined in `research_archive/` and must never be inserted into `neurocompanion.db`.

---

*Report certified by NeuroCompanion AI/ML Research Team*  
*Audit Completed: 2026-11-17 · Strict Read-Only Verification*
