# NeuroCompanion Phase 4: Modular AI/ML Technical Architecture
**Document:** Phase 4 System Architecture, Pipeline Specifications & API Contracts  
**File Location:** `Backend/research_archive/Phase4_Initial_Audit/Phase4_Modular_Architecture.md`  
**Auditor:** NeuroCompanion Phase 4 AI/ML Verification & Architecture Team  
**Date:** November 17, 2026  
**Status:** Certified Architecture Specification  

---

## 1. Architectural Principles & Isolation Boundaries

To ensure absolute system stability and scientific reproducibility, the Phase 4 Machine Learning Architecture is strictly decoupled from the completed, frozen Phase 1, Phase 2A, and Phase 3 components.

```mermaid
flowchart TD
    subgraph Frozen_Core["Frozen Foundations (Read-Only)"]
        P1["Phase 1: OASIS Clinical CDR Category Estimation"]
        P2A["Phase 2A: FastAPI Core Backend & SQLite (neurocompanion.db)\nEndpoints: /api/v1/patients, /api/v1/telemetry"]
        P3["Phase 3: Unity 3D Windows Client (Cognitive Environments)"]
    end

    subgraph Phase4_ML_Subsystem["Phase 4 Modular AI/ML Subsystem"]
        subgraph Data_Layer["Data & Registry Layer"]
            DR["Dataset Registry & Provenance Catalog"]
            DP["Isolated Data Preparation Pipelines (Brain-IT, AI4HOPE, VASE)"]
            FE["Feature Engineering & Representation Layer"]
        end

        subgraph Model_Layer["Model Management & Training"]
            MR["Model Registry & Versioning Engine"]
            TV["User-Grouped Training & Cross-Validation"]
            TE["Independent Test Evaluation & Uncertainty Quantification"]
        end

        subgraph Serving_Layer["Inference & Monitoring Service"]
            PA["Phase 4 Prediction API Router (/api/v1/ml/...)"]
            EX["Explainability Engine (SHAP / Feature Attribution)"]
            MO["Model Performance & Data Drift Monitor"]
        end
    end

    P2A -.->|Read Telemetry Schema via Read-Only DB Consumer| DP
    P2A <==>|Stable REST API Contracts (JSON)| PA
    P3 -.->|Sends Telemetry to Phase 2A| P2A
```

### Inviolable Architectural Rules
1. **Zero Database Schema Mutations:** Phase 4 never modifies `neurocompanion.db` tables, column names, or constraints.
2. **Read-Only Data Consumers:** Feature extraction pipelines ingest telemetry via dedicated read-only SQLite connections or read-only REST endpoints.
3. **Decoupled Prediction Services:** All ML inference is served through independent router endpoints under `/api/v1/ml/`, returning standardized JSON schemas with explicit confidence and uncertainty scores.
4. **Independent Pipelines:** Each external dataset (`Brain-IT`, `AI4HOPE`, `VASE`) has its own encapsulated extraction, transformation, and validation module. No shared code couples the pipelines.

---

## 2. Component Specifications

### 2.1 Dataset Registry & Provenance Catalog
- **Location:** `Backend/ml/registry/`
- **Purpose:** Tracks dataset physical files, cryptographic checksums, data origins, collection dates, and licensing constraints.
- **Manifest File:** `datasets_manifest.json`
  ```json
  {
    "datasets": {
      "brain_it": {
        "version": "1.0.0",
        "format": "xlsx",
        "relative_path": "research_archive/Data_Brain-IT_RCT_for-publication.xlsx",
        "sha256": "4b92...",
        "provenance": "ETH Zurich (NCT05387057)",
        "license": "CC-BY-4.0",
        "stakeholder": "patient_mNCD",
        "legitimate_scope": "longitudinal_adherence"
      },
      "ai4hope": {
        "version": "1.0.0",
        "format": "csv_collection",
        "relative_path": "research_archive/",
        "files": ["expert_prompts.csv", "participants_users.csv", "response_ratings.csv", "session_logs.csv", "codebook.csv"],
        "provenance": "EU Horizon Europe (AI4HOPE)",
        "license": "Open Access / EU Open Science",
        "stakeholder": "clinician_experts_and_caregivers",
        "legitimate_scope": "nlp_conversational_quality"
      },
      "vase": {
        "version": "1.0.0",
        "format": "xlsx",
        "relative_path": "research_archive/Complete data set, Lai et al..xlsx",
        "provenance": "Wellcome Trust / UCL (Lai et al.)",
        "license": "CC-BY-4.0",
        "stakeholder": "patient_advanced_dementia_observational",
        "legitimate_scope": "observational_engagement_dynamics"
      }
    }
  }
  ```

---

### 2.2 Independent Data Preparation Pipelines
Each pipeline runs as a standalone Python module in `Backend/ml/pipelines/`:
- `prepare_brainit.py`:
  - Validates row integrity and header rows in `Data_Brain-IT_RCT_for-publication.xlsx`.
  - Parses `Attendance Adherence` (12 weeks) and `Duration Adherence` into clean longitudinal long-format tables: `(participant_id, week, planned_sessions, attended_sessions, attendance_rate, missed_reason)`.
  - Masks missing value sentinel codes (`-99`, `-96`) as standard `NaN`.
- `prepare_ai4hope.py`:
  - Ingests `expert_prompts.csv`, `participants_users.csv`, and `response_ratings.csv`.
  - Performs string sanitization (UTF-8 encoding, stripping whitespace, normalized persona labels).
  - Verifies foreign-key integrity between `user_id` and `interaction_id`.
  - Segregates the 600 expert evaluations from the 601 caregiver evaluations into clean target tables.
- `prepare_vase.py`:
  - Parses sheet `MAINDATA 6-30 (SC)` (44,400 rows).
  - Enforces data type casting on numeric columns (`Seconds`, `VidSubject`, `Rater`, `Rating`, `Passive`).
  - Generates clip-level summary statistics without mutating raw time-series matrices.

---

### 2.3 Feature Engineering Layer
- **Location:** `Backend/ml/features/`
- **Text Feature Extractors (`text_features.py`):**
  - Prompt and response text representation for AI4HOPE NLP models.
  - Combines classical stylistic features (character length, token count, sentence complexity, question mark density, persona one-hot encoding) with dense semantic embeddings (`all-MiniLM-L6-v2` or `TF-IDF` bi-grams).
- **Adherence & Engagement Extractors (`timeseries_features.py`):**
  - Brain-IT: Cumulative adherence percentage, slope of attendance decline over weeks 1–6, frequency of somatic non-adherence events.
  - VASE: Empirical transition probabilities between state clusters (States 1–2: Disengaged vs States 5–7: Actively Engaged).
- **Strict Leakage Prevention:**
  - Feature transformations (e.g., standard scalers, TF-IDF vectorizers) are fitted **strictly inside training folds** during cross-validation. No dataset-wide fitting is permitted.

---

### 2.4 Model Registry & Versioning
- **Location:** `Backend/ml/models/` and `Backend/saved_model/phase4/`
- **Model Versioning Schema:** Semantic versioning `v{MAJOR}.{MINOR}.{PATCH}`:
  - `MAJOR`: Architecture change (e.g., linear baseline to transformer).
  - `MINOR`: New training dataset or feature representation.
  - `PATCH`: Hyperparameter re-tuning or bug fix.
- **Model Artifact Packaging:** Each registered model stores:
  - `model.pkl` / `weights.bin`: Trained serializable artifact.
  - `metadata.json`: Feature list, hyperparameter dictionary, training git commit hash, dataset registry version, and validation metrics.
  - `preprocessor.pkl`: Fitted feature transformation pipeline.

---

### 2.5 Training, Validation & Independent Evaluation
- **Location:** `Backend/ml/training/`
- **Validation Protocols:**
  - **User-Grouped Splitting (`GroupKFold`):** For AI4HOPE, splitting is strictly grouped by `user_id` ($k=5$ folds, 12 experts per fold, ensuring that all 10 interactions from any single expert remain exclusively in train or test).
  - **Stratified Partitioning:** Stratified by primary language and persona style to maintain demographic representation.
- **Independent Test Holdout:**
  - A fixed $20\%$ holdout set ($n=12$ experts, 120 QA pairs) is sequestered prior to any hyperparameter optimization and evaluated only once for the final model report.
- **Evaluation Metrics:**
  - Ordinal Targets (Empathy, Actionability, Information Support): **Quadratic Weighted Kappa (QWK)**, **Mean Absolute Error (MAE)**, and **Spearman Rank Correlation ($\rho$)**.
  - Binary Quality Indicators: **Balanced Accuracy**, **Macro F1**, and **Area Under ROC (AUROC)**.

---

### 2.6 Prediction APIs & Inference Contracts
- **Location:** `Backend/ml/api/router.py` (mounted into FastAPI application under prefix `/api/v1/ml/` without touching Phase 2A core routes).
- **Endpoint 1: Caregiver Conversational Quality Scorer**
  - **Route:** `POST /api/v1/ml/nlp/evaluate-response`
  - **Request Schema:**
    ```json
    {
      "prompt_text": "How do I manage evening agitation in Alzheimer's disease?",
      "response_text": "Evening agitation, or sundowning, can often be reduced by establishing a calm routine, minimizing ambient noise, and maintaining gentle lighting.",
      "persona_style": "Professional caregiver",
      "model_version": "v1.0.0"
    }
    ```
  - **Response Schema:**
    ```json
    {
      "model_version": "v1.0.0",
      "predicted_scores": {
        "empathy_score": 4.15,
        "actionability_score": 4.42,
        "perceived_support_score": 4.05
      },
      "uncertainty": {
        "confidence_interval_95": [3.85, 4.45],
        "prediction_variance": 0.04
      },
      "quality_flags": {
        "high_actionability": true,
        "empathy_threshold_met": true,
        "review_recommended": false
      },
      "inference_latency_ms": 14.2
    }
    ```

---

### 2.7 Explainability & Uncertainty Quantification
- **Feature Attribution:** Global and local feature attributions generated via SHAP (SHapley Additive exPlanations) or TreeSHAP for tree-based models, identifying the key linguistic tokens driving empathy and actionability scores.
- **Uncertainty Quantification:**
  - For ensemble models (e.g., Random Forest / LightGBM): Jackknife variance across individual estimator trees.
  - For linear models: Predictive variance based on parameter covariance matrices.
  - High-uncertainty predictions (variance $> \tau$) trigger an automated flag: `"review_recommended": true`.

---

### 2.8 Monitoring, Telemetry Audit & Model Drift
- **Location:** `Backend/ml/monitoring/`
- **Data Drift Tracking:** Population Stability Index (PSI) computed weekly on incoming prompt/response text length, vocabulary overlap, and score distributions against baseline training distributions.
- **Fallback Guarantees:** If the ML service experiences an exception, timeout, or extreme data drift, the API falls back gracefully to deterministic rule-based heuristic checks without breaking client interaction.

---

## 3. Directory Layout in Backend

```text
Backend/
├── main.py                          # Phase 2A Core Entry Point (Frozen)
├── data/
│   └── neurocompanion.db            # Phase 2A SQLite Database (Frozen, Read-Only)
├── research_archive/                # Source Datasets & Forensic Reports (Read-Only)
└── ml/                              # Phase 4 Modular AI/ML Root
    ├── registry/
    │   ├── datasets_manifest.json   # Cryptographic provenance catalog
    │   └── registry_manager.py      # Manifest loader and integrity validator
    ├── pipelines/
    │   ├── prepare_brainit.py       # Brain-IT data cleaning & extraction
    │   ├── prepare_ai4hope.py       # AI4HOPE relational data extraction
    │   └── prepare_vase.py          # VASE time-series parser
    ├── features/
    │   ├── text_features.py         # NLP vectorizers & stylistic extractors
    │   └── timeseries_features.py   # Adherence & transition extractors
    ├── training/
    │   ├── train_ai4hope_quality.py # GroupKFold training & evaluation pipeline
    │   └── hyperopt_config.py       # Cross-validation & grid search specs
    ├── models/
    │   └── v1_0_0/                  # Versioned serialized artifacts
    │       ├── model.pkl
    │       ├── preprocessor.pkl
    │       └── metadata.json
    ├── api/
    │   ├── schemas.py               # Pydantic request/response schemas
    │   └── router.py                # Isolated FastAPI router (/api/v1/ml/...)
    └── monitoring/
        ├── drift_detector.py        # PSI and distribution drift tracker
        └── fallback_rules.py        # Deterministic fallback heuristics
```

---
*Certified by NeuroCompanion Phase 4 Architecture System.*
