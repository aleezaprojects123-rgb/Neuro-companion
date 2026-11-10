# Reports Directory

## Contents

After running `evaluate_model.py`, this directory will contain:

| File | Description |
|------|-------------|
| `metrics.json` | Accuracy, Precision, Recall, F1 on the held-out test set |
| `confusion_matrix.png` | Labelled confusion matrix heat-map |

---

## How to Generate These Files

```bash
cd Backend
python model/evaluate_model.py
```

---

## ⚠️ Synthetic Data Warning

> These evaluation results are based on a **synthetically generated** dataset
> created for machine-learning education. They do **not** represent real
> clinical performance and must **not** be interpreted as evidence of
> diagnostic ability.
