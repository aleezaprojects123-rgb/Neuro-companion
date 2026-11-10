# Phase 1 Plan – NeuroCompanion

## Goal

Build a local, reproducible machine-learning prediction backend that:
- Trains a binary classifier on the Kaggle "Alzheimer's Disease Dataset" (synthetic).
- Exposes a FastAPI REST endpoint for predictions.
- Demonstrates a clean, modular ML pipeline with no data leakage.
- Produces evaluation artefacts (metrics, confusion matrix).

> **This is an educational prototype only. It does not diagnose any disease.**

---

## Folder Structure

```
NeuroCompanion/
├── Backend/
│   ├── data/
│   │   └── raw/
│   │       └── README.md
│   ├── model/
│   │   ├── __init__.py
│   │   ├── config.py
│   │   ├── data_loader.py
│   │   ├── preprocess.py
│   │   ├── train_model.py
│   │   ├── evaluate_model.py
│   │   └── predict.py
│   ├── saved_model/
│   │   └── README.md
│   ├── reports/
│   │   └── README.md
│   ├── main.py
│   ├── requirements.txt
│   ├── README.md
│   └── .gitignore
├── Docs/
│   ├── phase1_plan.md
│   └── dataset_notes.md
└── README.md
```

---

## Step-by-Step Workflow

### Step 0 – Set up environment

```bash
cd Backend
python -m venv venv
venv\Scripts\activate
pip install -r requirements.txt
```

### Step 1 – Download dataset

- URL: https://www.kaggle.com/datasets/rabieelkharoua/alzheimers-disease-dataset
- Save as: `Backend/data/raw/alzheimers_disease_data.csv`

### Step 2 – Inspect data

```bash
python model/data_loader.py
```

### Step 3 – Preprocess

```bash
python model/preprocess.py
```

### Step 4 – Train

```bash
python model/train_model.py
```

### Step 5 – Evaluate

```bash
python model/evaluate_model.py
```

### Step 6 – Serve API

```bash
uvicorn main:app --reload
```

---

## Commands Summary

| Step | Command |
|------|---------|
| Activate venv | `venv\Scripts\activate` |
| Install deps | `pip install -r requirements.txt` |
| Inspect data | `python model/data_loader.py` |
| Preprocess | `python model/preprocess.py` |
| Train | `python model/train_model.py` |
| Evaluate | `python model/evaluate_model.py` |
| Serve | `uvicorn main:app --reload` |

---

## Expected Outputs

| Artefact | Location |
|----------|----------|
| Processed data | `data/processed/processed_data.pkl` |
| Feature schema | `saved_model/schema.json` |
| Fitted pipeline | `saved_model/pipeline.joblib` |
| Test metrics | `reports/metrics.json` |
| Confusion matrix | `reports/confusion_matrix.png` |

---

## Acceptance Tests

| # | Test | Pass Criteria |
|---|------|---------------|
| 1 | venv activates | Prompt shows `(venv)` |
| 2 | pip install | No errors |
| 3 | data_loader.py | Prints shape, columns, class balance without error |
| 4 | preprocess.py | `schema.json` created |
| 5 | train_model.py | `pipeline.joblib` created; best model printed |
| 6 | evaluate_model.py | `metrics.json` and `confusion_matrix.png` created |
| 7 | uvicorn starts | "Application startup complete" in terminal |
| 8 | GET /health | `{"status":"ok","model_loaded":true,...}` |
| 9 | GET /docs | Swagger UI opens |
| 10 | POST /predict (valid) | Returns `predicted_class`, `predicted_label`, `disclaimer` |
| 11 | POST /predict (missing field) | HTTP 422 with field-level error |
| 12 | No banned code | No Unity, Carl, Vosk, UI, animation, voice files exist |

---

## Known Limitations

- Dataset is **synthetic** – not derived from real patient records.
- Binary classification only: 0 or 1; no stage/severity prediction.
- Model is not clinically validated.
- No authentication, rate-limiting, or production hardening.
- No persistent database; predictions are stateless.
- Phase 1 has no frontend, voice, or animation components.

---

## Phase 2 Prerequisites

Phase 2 begins **only after all Phase 1 acceptance tests pass**. It will add:
- A conversational companion interface (Carl).
- Voice input/output integration.
- A frontend dashboard.
- Persistent logging of anonymised session data.
