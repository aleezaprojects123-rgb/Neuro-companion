# Raw Data Directory

## ⚠️ Dataset Not Included

The dataset CSV is **not** committed to this repository. You must download it
manually from Kaggle and place it here.

---

## Dataset Details

| Field       | Value |
|-------------|-------|
| **Name**    | Alzheimer's Disease Dataset |
| **Author**  | Rabie El Kharoua |
| **Kaggle URL** | <https://www.kaggle.com/datasets/rabieelkharoua/alzheimers-disease-dataset> |
| **License** | See Kaggle dataset page |

---

## Download Instructions

1. Create a free Kaggle account at <https://www.kaggle.com> if you don't have one.
2. Navigate to the dataset URL above.
3. Click the **Download** button (you may need to accept the dataset rules).
4. Extract the downloaded archive.
5. Rename the main CSV file to exactly:

```
alzheimers_disease_data.csv
```

6. Place it in this directory:

```
Backend/data/raw/alzheimers_disease_data.csv
```

---

## ⚠️ Important Warnings

> **Synthetic Data** – This dataset was generated synthetically for
> machine-learning education. It does **not** represent real patient records.

> **Privacy** – Do **not** commit the CSV to version control. It is excluded
> via `.gitignore`.

> **No Medical Claims** – Models trained on this data cannot and must not be
> used to diagnose Alzheimer's disease or any other medical condition.

---

## Expected File After Download

```
Backend/
└── data/
    └── raw/
        ├── README.md                      ← this file
        └── alzheimers_disease_data.csv    ← place downloaded CSV here
```
