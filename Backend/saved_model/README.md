# Saved Model Directory

## Contents

After running the training pipeline, this directory will contain:

| File | Description |
|------|-------------|
| `pipeline.joblib` | Fitted sklearn Pipeline (preprocessor + classifier) |
| `schema.json` | Feature schema used for input validation at inference |

---

## How to Generate These Files

```bash
cd Backend

# Step 1: Preprocess the raw CSV
python model/preprocess.py

# Step 2: Train and select the best model
python model/train_model.py
```

---

## ⚠️ Notes

- Do **not** commit `pipeline.joblib` or `schema.json` to version control if
  the model was trained on sensitive data.
- This dataset is synthetic; the model is for educational use only.
- The pipeline includes preprocessing — do **not** pre-process inputs before
  calling `predict.py`; the pipeline handles it internally.
