# CASAS Exploratory Research Archive

**Status:** ARCHIVED (Exploratory Experiment Only)  
**Date Archived:** 2026-11-08  
**Notice:** This folder contains the exploratory smart-home dataset (CASAS) and binary task-quality classification models investigated during Phase 2 exploration.

## Important Project Declarations
1. **Decoupled from Active Pipeline:** CASAS is **no longer part of the active NeuroCompanion AI architecture or roadmap**.
2. **Not Used for Patient Adaptation:** Neither the CASAS dataset, features, models, nor labels are used to estimate patient support levels or drive Carl's assistance in the Unity application.
3. **Reason for Archival:** Sensor telemetry in a physical smart apartment (passive PIR motion and door switches) cannot represent fine-grained digital interactions (touch latency, hint requests, audio repetitions, micro-step prompts) inside NeuroCompanion's three digital therapy areas (Orientation Room, Reminiscence Corner, Cognitive Garden).
4. **Active Direction:** The active missing-MMSE pathway is being developed as **Unity Interaction-Based Adaptive Intelligence**, utilizing native Unity digital interaction events and real empirical telemetry.

## Archived Contents
- `data/casas/`: Raw CASAS dataset files and task audit observations (`task_audit_data.csv`).
- `model_phase2/`: Training pipeline script (`train_phase2.py`).
- `saved_model_phase2/`: Saved model pipelines (`pipeline.joblib`, `pipeline_expA.joblib`, `pipeline_expB.joblib`, and `schema.json`).
- `reports_phase2/`: Cross-validation metrics (`cv_results.json`, `model_comparison.json`, and `confusion_matrix_comparison.png`).
