# NeuroCompanion Phase 1 – Practical Model Backend

> **⚠️ Educational Machine-Learning Prototype**
> This prototype is trained on the cross-sectional OASIS-1 dataset ($N = 235$ assessed records).
> The model **does not** perform medical or clinical diagnosis and **must not** be used for clinical decision-making.

---

## 1. Practical Model Overview

- **Dataset:** OASIS-1 (`Backend/data/raw/oasis_cross-sectional_utf8.csv`)
- **Assessed Cohort:** 235 participants with clinically recorded CDR ratings (unassessed younger controls excluded).
- **Target:** 3 Experimental CDR Categories:
  - **Class 0:** CDR = 0.0 (No dementia pattern)
  - **Class 1:** CDR = 0.5 (Very mild dementia pattern)
  - **Class 2:** CDR ≥ 1.0 (Mild to moderate dementia pattern)
- **Features Used (Experiment A - Practical Screener Model):**
  - `Age` (Demographic, years: 30–105)
  - `M/F` (Biological sex: 'M' or 'F')
  - `Educ` (Education code: 1–5)
  - `MMSE` (Mini-Mental State Examination: 0–30, administered cognitive assessment)
- **Features Excluded:**
  - `ID`, `Hand`, `Delay` (identifiers / zero variance / non-informative)
  - `SES` (excluded from practical model due to real-world collection complexity)
  - `eTIV`, `nWBV`, `ASF` (MRI neuroimaging measurements excluded from practical companion model)

---

## 2. Experimental Findings (Ablation of MMSE)

Using Repeated Stratified 5-Fold Cross-Validation (50 folds) on the training cohort ($N = 188$):

| Model | Experiment A (with MMSE) Macro-F1 | Experiment B (without MMSE) Macro-F1 | Delta (MMSE Contribution) |
| :--- | :---: | :---: | :---: |
| **DummyClassifier (Baseline)** | 0.2432 | 0.2432 | 0.0000 |
| **Logistic Regression** | 0.6583 | 0.4481 | **+0.2102** |
| **Random Forest (Constrained Depth)** | **0.6662** | 0.4492 | **+0.2170** |

*Conclusion:* Demographic features alone (`Age`, `M/F`, `Educ`) provide weak predictive signal (~49% accuracy, ~0.449 Macro-F1). Incorporating an administered cognitive screener (`MMSE`) increases Macro-F1 by **+21.7 percentage points** and Balanced Accuracy to **70.29%**.

---

## 3. Held-Out Test Evaluation ($N = 47$)

Evaluated once on the untouched 20% stratified test split:

- **Overall Accuracy:** `76.60%`
- **Balanced Accuracy:** `68.87%`
- **Macro-Averaged F1:** `0.6917`

### Class-Specific Breakdown:
- **Class 0 (CDR 0):** Precision: `0.9200`, Recall: `0.8519`, F1: `0.8846` ($N=27$)
- **Class 1 (CDR 0.5):** Precision: `0.5882`, Recall: `0.7143`, F1: `0.6452` ($N=14$)
- **Class 2 (CDR 1+):** Precision: `0.6000`, Recall: `0.5000`, F1: `0.5455` ($N=6$)

---

## 4. Quick Start Commands

```bash
cd Backend
venv\Scripts\activate

# Step 1: Preprocess OASIS dataset into stratified splits
python model/preprocess.py

# Step 2: Compare experiments & train the winning pipeline
python model/train_model.py

# Step 3: Run final evaluation on held-out test fold
python model/evaluate_model.py

# Step 4: Launch FastAPI service
uvicorn main:app --reload
```

---

## 5. API Endpoints

- **Root:** `http://127.0.0.1:8000/`
- **Health:** `http://127.0.0.1:8000/health`
- **Interactive Documentation:** `http://127.0.0.1:8000/docs`
- **Predict Endpoint:** `POST http://127.0.0.1:8000/predict`

### Example Request Body:
```json
{
  "Age": 74.0,
  "M/F": "F",
  "Educ": 2.0,
  "MMSE": 22.0
}
```

---

## 6. Known Limitations

1. **Small Minority Class ($N = 30$ in Class 2):** With only 6 test instances in Class 2, metric estimates carry substantial statistical variance.
2. **Clinical Screener Requirement:** The model relies heavily on `MMSE`. If MMSE is missing or estimated casually, prediction quality drops to baseline levels.
3. **No Clinical Generalization:** Results represent technical feasibility within the OASIS-1 research sample and must not be interpreted as diagnostic efficacy.
