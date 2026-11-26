# Phase 4.3 — Shared Cross-Dataset Feature Schema

**Project:** NeuroCompanion
**Phase:** 4.3 — Cross-Dataset Shared Representations
**Date:** 2026-11-18
**Status:** ✅ Prepared — Ready for Phase 4.4 modelling

---

> [!IMPORTANT]
> This document describes two **shared cross-dataset feature representations** constructed in Phase 4.3. These are empirical research priors and normative reference tables — **not** supervised machine-learning models. No clinical predictions are made from these tables alone.

---

## Overview

Phase 4.3 identified pairs of prepared datasets that share a common underlying measurable construct — timing latency and text structural complexity — and built unified schema tables from them. These shared representations serve two purposes:

1. **Increase effective sample size** for feature priors used in future model design.
2. **Establish normative reference distributions** across cohort types and task domains.

Two shared representations were produced:

| ID | Name | Output File | Rows | Columns |
|---|---|---|---|---|
| SHARED_REP_01_PACING | Shared Pacing Latency Space | `shared_pacing_latencies_prepared.csv` | 481 | 9 |
| SHARED_REP_02_TEXT | Shared Text Readability and Complexity Space | `shared_text_readability_prepared.csv` | 609 | 7 |

---

## SHARED_REP_01_PACING — Shared Pacing Latency Space

### Identity

| Property | Value |
|---|---|
| **Representation ID** | SHARED_REP_01_PACING |
| **Output file** | `Backend/data/processed/phase4/shared_representations/shared_pacing_latencies_prepared.csv` |
| **Rows** | 481 |
| **Columns** | 9 |
| **Shared construct** | Human task completion timing (reaction time / task duration) |
| **Purpose** | Empirical timing prior for NeuroCompanion adaptive timeout multiplier (M5) |

### Contributing Datasets

| Dataset ID | Dataset Name | Domain | N Participants | Key Variable |
|---|---|---|---|---|
| DS10_MSL_LIFESPAN | Motor Sequence Learning — Lifespan | Motor / Tapping RT | 130 | Tapping reaction time (ms), 4 age groups |
| DS11_NBACK_DEMENTIA | N-Back Dementia Working Memory | Cognitive / Working Memory RT | 24 | N-Back response time (ms), Severity Group |
| DS02_CASAS | CASAS Smart Home Task Duration | Physical IADL / Task Duration | 327 | Activity duration (ms), completion score |

**Age groups in DS10_MSL_LIFESPAN:** Child · Adolescent · Young Adult · Older Adult

### Column Schema

| Column | Type | Description |
|---|---|---|
| `source_dataset` | string | Source dataset identifier: `MSL`, `NBACK`, or `CASAS` |
| `participant_id` | string | Original participant identifier from the source dataset |
| `task_domain` | string | `Tapping` / `Working_Memory` / `IADL_Physical` |
| `cohort_type` | string | `Healthy_Child` / `Healthy_Adolescent` / `Healthy_Young_Adult` / `Healthy_Older_Adult` / `Dementia_MCI` / `Dementia_AD` |
| `observed_latency_ms` | float | Raw observed reaction time or task duration in milliseconds |
| `log_latency_ms` | float | Natural log of `observed_latency_ms` — used for normalisation |
| `normative_z_score` | float | Z-score computed within each `cohort_type × task_domain` group |
| `is_dementia_cohort` | int | Binary flag: `0` = healthy/normative cohort; `1` = dementia or MCI cohort |
| `empirical_pacing_multiplier_prior` | float | Ratio of dementia cohort median latency to healthy older adult median latency within the same task domain — used as prior for timeout multiplier |

### Scientific Justification for Pooling

The three datasets were pooled because they share a **single underlying measurable construct: the time taken by a human to complete a cognitive or motor task**. The shared abstraction is **TIMING** — not diagnosis, not activity type, not measurement instrument.

- Tapping RT (DS10_MSL_LIFESPAN), working memory RT (DS11_NBACK_DEMENTIA), and smart home IADL duration (DS02_CASAS) are all measured in milliseconds as a time interval between stimulus/task onset and task completion.
- **Task domain tags** (`task_domain`) and **cohort type tags** (`cohort_type`) are preserved in the schema to prevent inappropriate cross-domain pooling in downstream analyses.
- **GroupKFold** validation is maintained on `participant_id` within each `source_dataset`, preserving dataset-level independence.
- The `empirical_pacing_multiplier_prior` column quantifies the ratio between dementia cohort latency and healthy older adult latency — providing an **empirical timing prior** for the M5 timeout multiplier in NeuroCompanion adaptive pacing logic.

> [!NOTE]
> The `empirical_pacing_multiplier_prior` is an **empirical reference ratio derived from research data** — it is NOT a supervised ML model output, NOT a clinical threshold, and NOT validated against NeuroCompanion interaction outcomes. It serves as a research-informed starting point for timeout multiplier calibration until native telemetry is available.

> [!WARNING]
> The `normative_z_score` is computed **within** `cohort_type × task_domain` groups only. Cross-domain z-score comparisons (e.g., comparing tapping RT z-scores to IADL duration z-scores) are scientifically invalid and must not be performed.

### What Was NOT Pooled and Why

| Excluded Data Type | Datasets Affected | Reason for Exclusion |
|---|---|---|
| eGeMAPS acoustic feature vectors (88-dim) | DS14_CHINESE1_EGEMAPS, DS15_NCMMSC2021_AUDIO, DS12_MDVR_KCL | Same 88-feature space but different languages, different pathologies, and different recording equipment. Direct pooling would confound language, equipment, and pathology effects. Domain adaptation required before pooling. **Deferred to Phase 4.4.** |
| Clinical tabular features | DS01_OASIS1, DS13_PITT_META, DS02_CASAS scores | Incompatible label schemas across datasets. Different measurement instruments for cognitive status. Must remain in separate prepared tables. |

---

## SHARED_REP_02_TEXT — Shared Text Readability and Complexity Space

### Identity

| Property | Value |
|---|---|
| **Representation ID** | SHARED_REP_02_TEXT |
| **Output file** | `Backend/data/processed/phase4/shared_representations/shared_text_readability_prepared.csv` |
| **Rows** | 609 |
| **Columns** | 7 |
| **Shared construct** | Structural syntactic complexity of human-produced or AI-produced text |
| **Purpose** | Readability and complexity prior for NeuroCompanion AS02 (Simplify Instruction) |

### Contributing Datasets

| Dataset ID | Dataset Name | Domain | N Units | Key Variable |
|---|---|---|---|---|
| DS04_AI4HOPE | AI4HOPE Conversational AI Responses | English AI responses | 600 responses (60 user IDs) | Expert clarity, relevance, and actionability ratings |
| DS14_CHINESE1_TRANSCRIPTS | Chinese AD Cookie Theft Transcripts | Chinese clinical transcripts | 9 session transcripts | Transcript-level linguistic features, TTR |

### Column Schema

| Column | Type | Description |
|---|---|---|
| `source_dataset` | string | Source dataset identifier: `AI4HOPE` or `Chinese_AD_Transcripts` |
| `text_unit_id` | string | Original row or session identifier from the source dataset |
| `language` | string | `English` or `Chinese` |
| `mean_sentence_length_words` | float | Average number of words per sentence in the text unit |
| `lexical_diversity_ttr` | float | Type-Token Ratio — unique tokens divided by total tokens |
| `reading_grade_level` | float | Flesch-Kincaid grade level estimate |
| `is_accessible_grade_6` | int | Binary flag: `1` if `reading_grade_level` ≤ 6.0 (accessible language); `0` otherwise |

### Scientific Justification for Pooling

Both datasets contain **text units** — sequences of natural language produced either by an AI system or by a human speaker — and both admit computation of **language-agnostic structural properties**.

- **Mean sentence length** and **lexical diversity (TTR)** are structural syntactic properties that do not depend on vocabulary, semantics, or language identity. They measure **how complex the structure of a sentence is**, not what it means.
- The `language` tag is preserved in the schema to prevent cross-lingual vocabulary analyses.
- The `is_accessible_grade_6` binary flag directly supports NeuroCompanion's **AS02 (Simplify Instruction)** design requirement: responses intended for dementia patients should target Grade 6 or below readability to maximise comprehension.
- Flesch-Kincaid grade level estimation is applied as a structural approximation; for Chinese text, the grade level estimate is a cross-linguistic structural analogue and is tagged accordingly via the `language` column.

> [!NOTE]
> The shared abstraction is **STRUCTURAL SYNTACTIC COMPLEXITY** — not meaning, not vocabulary, not language-specific grammar. This is what justifies pooling English AI responses (DS04_AI4HOPE) with Chinese clinical transcripts (DS14_CHINESE1_TRANSCRIPTS) in a single schema table.

> [!TIP]
> The `is_accessible_grade_6` column is the primary feature linking this table to AS02 (Simplify Instruction) design logic. When future native telemetry provides labels for "patient needed simplified language," this table's features are ready as model inputs.

### What Was NOT Pooled and Why

| Excluded Data Type | Reason for Exclusion |
|---|---|
| Raw vocabulary tokens | English and Chinese vocabulary are entirely incompatible. Cross-lingual token pooling would be semantically invalid. Vocabulary features remain in separate dataset-specific tables. |
| Semantic content / meaning | Semantic features were not extracted for this shared representation. Different languages and task domains (AI response generation vs. clinical picture description) make semantic pooling invalid without cross-lingual embeddings. |
| Speaker-level clinical labels | OASIS CDR scores and clinical severity labels are **not** merged into this table. SHARED_REP_02_TEXT is a text-structure table only. Clinical labels remain in DS01_OASIS1 and DS13_PITT_META. |

---

## Summary of Shared Representations

```
Phase4/shared_representations/
├── shared_pacing_latencies_prepared.csv      ← SHARED_REP_01_PACING  (481 rows × 9 cols)
└── shared_text_readability_prepared.csv      ← SHARED_REP_02_TEXT    (609 rows × 7 cols)
```

| Property | SHARED_REP_01_PACING | SHARED_REP_02_TEXT |
|---|---|---|
| Shared construct | Timing latency (ms) | Structural text complexity |
| Contributing datasets | DS10, DS11, DS02 | DS04, DS14 (transcripts) |
| Rows | 481 | 609 |
| Columns | 9 | 7 |
| AS categories informed | AS06, AS07 | AS02, AS09 |
| Is a supervised ML model? | ❌ No — empirical prior | ❌ No — research feature table |
| Ready for Phase 4.4? | ✅ Yes | ✅ Yes |

> [!CAUTION]
> Neither shared representation constitutes a supervised prediction model. Both are **research feature priors and normative reference tables**. Supervised prediction of any AS01–AS10 assistance category requires future native NeuroCompanion interaction telemetry with verified outcome labels.

---

## What is Deferred to Phase 4.4

| Deferred Item | Reason |
|---|---|
| eGeMAPS cross-dataset audio pooling (DS12, DS14_EGEMAPS, DS15) | Requires domain adaptation across languages and recording equipment before pooling is scientifically valid |
| Cross-lingual semantic text embeddings | Requires a multilingual embedding model; out of scope for Phase 4.3 schema preparation |
| Supervised feature-to-AS-label linkage | Requires native telemetry labels; none available in Phase 4.3 |

---

*Document prepared: 2026-11-18 | NeuroCompanion Phase 4.3 | Backend/docs/Phase4_3_SHARED_FEATURE_SCHEMA.md*
