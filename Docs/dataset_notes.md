# Dataset Notes – Alzheimer's Disease Dataset

## Dataset Identity

| Field | Value |
|-------|-------|
| **Name** | Alzheimer's Disease Dataset |
| **Author** | Rabie El Kharoua |
| **Platform** | Kaggle |
| **URL** | https://www.kaggle.com/datasets/rabieelkharoua/alzheimers-disease-dataset |

---

## ⚠️ Synthetic Data Warning

> This dataset was **synthetically generated** for machine-learning education
> and research. It does **not** contain real patient records. Any patterns
> learned by a model on this data **cannot** be assumed to reflect real-world
> clinical relationships.
>
> **Never use a model trained on this data to make clinical decisions.**

---

## Target Definition

| Value | Label | Meaning |
|-------|-------|---------|
| `0` | No Alzheimer's-related pattern | No pattern detected in this synthetic record |
| `1` | Alzheimer's-related pattern | Pattern detected in this synthetic record |

The column name in the CSV is **`Diagnosis`**.

---

## Feature-Selection Notes

### Dropped Columns (Identifiers / Administrative)

| Column | Reason |
|--------|--------|
| `PatientID` | Unique row identifier – carries no predictive signal |
| `DoctorInCharge` | Administrative label – not a clinical feature |

These columns are listed in `config.py → DROP_COLUMNS` and removed before
any feature engineering.

### Retained Features

All other columns (demographics, lifestyle, medical history, clinical
measurements, cognitive assessments, symptoms) are retained as model inputs.

### Target Exclusion

`Diagnosis` is **explicitly excluded** from the feature matrix `X` to prevent
target leakage.

---

## Data-Cleaning Decisions

| Decision | Implementation |
|----------|---------------|
| Missing numeric values | Imputed with **median** (robust to outliers) |
| Missing categorical values | Imputed with **most frequent** value |
| Scaling | StandardScaler applied to all numeric features |
| Encoding | OneHotEncoder with `handle_unknown="ignore"` for categorical features |
| Duplicates | Flagged and reported; not automatically removed (dataset is synthetic) |

---

## Train/Test Split

| Parameter | Value |
|-----------|-------|
| Test size | 20% (`TEST_SIZE = 0.2`) |
| Strategy | **Stratified** – preserves class ratio in both folds |
| Random seed | `RANDOM_STATE = 42` |

> **Critical:** The train/test split is performed **before** fitting any
> preprocessing. The preprocessor (imputer, scaler, encoder) is fit
> **only on the training fold** and then applied to the test fold.
> This prevents data leakage.

---

## Model Evaluation

Evaluation is performed **once** on the held-out test set after model
selection is complete.

| Metric | Description |
|--------|-------------|
| Accuracy | Fraction of correct predictions |
| Precision (macro) | Average precision across both classes |
| Recall (macro) | Average recall across both classes |
| F1 (macro) | Harmonic mean of precision and recall |
| Confusion matrix | True/False positive and negative counts |

---

## Data Leakage Prevention

The following precautions are implemented:

1. **Split before fit** – `train_test_split` is called before `preprocessor.fit`.
2. **Pipeline** – preprocessing and model are saved as a single sklearn
   `Pipeline`, ensuring the same transformations are applied at inference time.
3. **Target excluded from features** – `Diagnosis` is never in the feature
   matrix `X`.
4. **No test-set tuning** – hyperparameters are selected using cross-validation
   on the training set only.
5. **Admin columns dropped** – `PatientID` and `DoctorInCharge` are removed
   before any splits to prevent spurious correlations.
