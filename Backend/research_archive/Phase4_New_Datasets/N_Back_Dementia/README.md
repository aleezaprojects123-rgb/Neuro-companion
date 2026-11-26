# N-Back Working Memory and tDCS in Dementia — Dataset README

## Overview

This dataset contains the minimal replication data for the clinical study investigating computerized N-back working memory practice and transcranial direct current stimulation (tDCS) in individuals living with dementia, stratified by baseline reaction time.

## Research Publication & Source

- **Title:** *Baseline task performance predicts mini-mental state examination improvement after individuals with dementia practice an N-back task with bilateral transcranial direct current stimulation*
- **Authors:** Carlos Roncero, Anton Popov, Bárbara Seixas-Lima, Debanjan Lahiri, Farinaz Abnavi, Howard Chertkow
- **Journal:** *PLOS ONE* (2025)
- **Zenodo Record:** https://zenodo.org/records/17047614
- **DOI:** `10.5281/zenodo.17047614`

## Licence

Creative Commons Attribution 4.0 International (CC-BY 4.0).

## File Details

- **File Name:** `Reproducibility_Data_SI_Modified.xlsx`
- **File Format:** Microsoft Excel OpenXML Spreadsheet (`.xlsx`)
- **File Size:** 16,220 bytes
- **SHA-256 Checksum:** `975c794480da42a267c3e8e95b5bb6520c04ae16fb9b99a30844d99201507ec0`

## Participant Population & Cohort

- **Total Participants:** $N = 24$ individuals diagnosed with mild-to-moderate dementia.
- **Stratification:**
  - **Group 1 ($n=12$, IDs 1–12):** Normal baseline response times on N-back (Mean RT: $777.6\text{ ms}$; baseline MMSE: $23.42 \pm 3.12$).
  - **Group 2 ($n=12$, IDs 13–24):** Slower-than-normal baseline response times (Mean RT: $1,303.9\text{ ms}$; baseline MMSE: $20.42 \pm 5.76$).
- **Study Protocol:** Within-subject crossover design across 3 stimulation conditions:
  - Bilateral tDCS (`BT1`, `BT2`, `BT3`)
  - Unilateral tDCS (`UT1`, `UT2`, `UT3`)
  - Sham stimulation (`ST1`, `ST2`, `ST3`)
  - Evaluated on trained (`T`) and untrained (`UT`) working memory tasks.

## NeuroCompanion Phase 4 Role

Directly calibrates the empirical psychomotor latency bounds for **AS07 (Additional Response Time)**, establishing that individuals with dementia require on average $+67.7\%$ longer response windows ($777.6\text{ ms} \to 1,303.9\text{ ms}$) without being penalized by false-negative timeouts.
