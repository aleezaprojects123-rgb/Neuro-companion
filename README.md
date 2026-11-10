# NeuroCompanion

> **Phase 1 – ML Backend Only**
>
> ⚠️ Unity, Carl, animations, voice, Vosk, and UI are **NOT included** in Phase 1.

---

## What This Is

NeuroCompanion is an educational prototype demonstrating a binary
machine-learning classification backend trained on a **synthetically generated**
Alzheimer's Disease dataset (Rabie El Kharoua, Kaggle).

**This project does NOT diagnose Alzheimer's disease or any medical condition.**

---

## Phase 1 Scope

| Included | Not Included |
|----------|-------------|
| ✅ Python ML pipeline | ❌ Unity engine |
| ✅ FastAPI REST backend | ❌ Carl companion character |
| ✅ Binary classification | ❌ Animations |
| ✅ Joblib model persistence | ❌ Voice / Vosk / speech |
| ✅ Swagger docs at /docs | ❌ Frontend / UI |
| ✅ Synthetic dataset EDA | ❌ Database / auth |
| ✅ Confusion matrix report | ❌ Cloud services |

---

## Quick Start

```bash
cd Backend
python -m venv venv
venv\Scripts\activate
pip install -r requirements.txt

# Place CSV at: Backend/data/raw/alzheimers_disease_data.csv

python model/data_loader.py      # inspect data
python model/preprocess.py       # preprocess
python model/train_model.py      # train
python model/evaluate_model.py   # evaluate
uvicorn main:app --reload        # serve API
```

---

## Test the API

| URL | Method | Purpose |
|-----|--------|---------|
| http://127.0.0.1:8000/ | GET | Root info |
| http://127.0.0.1:8000/health | GET | Model status |
| http://127.0.0.1:8000/docs | GET | Swagger UI |
| http://127.0.0.1:8000/predict | POST | Run prediction |

---

## Dataset

Download from Kaggle:
<https://www.kaggle.com/datasets/rabieelkharoua/alzheimers-disease-dataset>

Place the CSV at `Backend/data/raw/alzheimers_disease_data.csv`.

---

## What Phase 2 Will Add

Phase 2 begins **only after all Phase 1 acceptance tests pass.**

Planned additions:
- **Carl** – conversational companion interface
- **Voice I/O** – speech recognition and text-to-speech
- **Frontend** – dashboard UI
- **Persistent sessions** – anonymised session logging
- **Unity integration** – 3D companion animations

---

## Limitations

- Synthetic dataset — not real patient data.
- Binary classification only (0 or 1).
- Not clinically validated.
- Educational use only.
