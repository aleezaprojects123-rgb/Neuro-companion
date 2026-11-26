# Personalising Older Adults' Cognitive Training Robot Games — Dataset README

## Overview

This repository contains interaction logs, game session performance, questionnaire assessments, and Large Language Model (LLM) prompt engineering templates from a human-robot interaction (HRI) study evaluating personalized cognitive training games for older adults.

## Research Publication & Source

- **Title:** *Personalizing Older Adults' Cognitive Training Robot Games with LLM Support*
- **Journal:** *International Journal of Human–Computer Interaction* (IJHCI, 2026)
- **Zenodo Record:** https://zenodo.org/records/21650130
- **DOI:** `10.5281/zenodo.21650130`

## Licence

Creative Commons Attribution 4.0 International (CC-BY 4.0).

## File Details

- **File Name:** `data&prompts.zip`
- **File Format:** ZIP Archive containing Excel spreadsheets (`.xlsx`) and text prompts (`.txt`)
- **File Size:** 405,700 bytes
- **SHA-256 Checksum:** `305aa6b6afc21ce37f8eb4433f3f0dd585aee42b86c8163cdc9504d2d60cb590`
- **ZIP Integrity:** Certified valid (PASS, 0 CRC errors, 15 member entries)

## Archive Members (15 entries)

```
data&prompts.zip
├── data/
│   ├── database/
│   │   ├── game_session.xlsx             (82 session records)
│   │   └── session_played.xlsx           (437 individual game runs)
│   ├── User_data.xlsx                    (20 older adult participants, Mean MMSE 28.65)
│   ├── Hallucinations.xlsx               (450 LLM hallucination validation checks)
│   ├── Questionnaire_end_of_session.xlsx (Godspeed + UEQ-S usability evaluations)
│   ├── SAM_questionnaire.xlsx            (160 Self-Assessment Manikin emotional ratings)
│   └── Short_UEQ_Data_Analysis_Tool.xlsx (User Experience Questionnaire template)
└── prompt/
    ├── User profile prompt.txt           (JSON schema for reminiscence profile extraction)
    ├── Real Event prompt.txt             (Multi-choice historical event quiz prompt)
    ├── Memory Story Quiz prompt.txt      (JSON quiz generator prompt)
    ├── Memory Completion prompt.txt      (Sentence completion prompt)
    └── Reorder Activities prompt.txt     (Activity sequence reordering prompt)
```

## Participant Population

- **Cohort:** $N = 20$ older adult participants (Age: $73.6 \pm 4.5$ years, range 65–81; 7 males, 13 females).
- **Cognitive Status:** Cognitively healthy older adults (Mean MMSE: $28.65 \pm 1.65$, range 26–30). Zero dementia diagnoses.

## NeuroCompanion Phase 4 Role

Directly informs:
- **AS01 (Repeat Instruction):** Naturalistic vocal prompt templates.
- **AS04 (Visual Hint):** Spatial touch coordinates ($X, Y$), tap bounds, and error counts.
- **AS05 (Demonstration):** Multi-modal touch vs. voice interactive exercise protocols.
