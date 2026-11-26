# NeuroCompanion — Phase 4.6 Safe Assistance Policy Integration
## Technical Documentation & Integration Report

**Version:** 1.0.0  
**Date:** 2026-11-21  
**Status:** COMPLETE — Integrated Policy Decision Layer Implemented; 36/36 Phase 4.6 Tests Pass.  
**Subsystem Location:** `Backend/ml/assistance/`  

---

## 1. Executive Summary

Phase 4.6 bridges the AS01–AS10 Assistance Prediction Layer (Phase 4.5) with the clinical governance and safety enforcement mechanisms necessary for safe deployment with Carl (the virtual companion).

The primary design principle is **Safety Sovereignty**:
> *An AI recommendation is strictly a candidate hypothesis, never permission to act.*

The integration flow enforces:
- Caregiver overrides and preset locks have absolute priority.
- Patient choices (help requests, dismissals, pause/break choices) are honored unconditionally.
- Rate limiting and prompt cooldowns (45-second floor) prevent cognitive overload.
- Break offers (AS08) remain strictly optional, non-blocking, and never lock the patient out.
- Zero CDR-to-assistance mapping.
- Graceful degradation on missing or sparse interaction telemetry with `NO_ADDITIONAL_ASSISTANCE` or `INSUFFICIENT_DATA`.

---

## 2. Integrated Policy Decision Architecture

```
                                [ Patient Interaction Telemetry ]
                                                │
                                                ▼
                                    ┌───────────────────────┐
                                    │ SafeAssistancePolicy  │
                                    │ (Pre-flight checks)   │
                                    └───────────┬───────────┘
                                                │
               ┌────────────────────────────────┴───────────────────────────────┐
               │                                                                │
     (Data Insufficient)                                                (Data Sufficient)
               │                                                                │
               ▼                                                                ▼
   [ INSUFFICIENT_DATA ]                                           ┌─────────────────────────┐
                                                                   │ AssistancePrediction-   │
                                                                   │ Engine (Phase 4.5)      │
                                                                   └────────────┬────────────┘
                                                                                │ 10 Strategy
                                                                                │ Candidates
                                                                                ▼
                                                                   ┌─────────────────────────┐
                                                                   │ AssistancePolicy-       │
                                                                   │ Interface (Governance)  │
                                                                   └────────────┬────────────┘
                                                                                │
                                                                                ▼
                                                                   ┌─────────────────────────┐
                                                                   │ SafeAssistancePolicy    │
                                                                   │ (Status Mapping &       │
                                                                   │ Contract Production)    │
                                                                   └────────────┬────────────┘
                                                                                │
                                                                                ▼
                                                                        [ PolicyDecision ]
                                                                        (Approved action
                                                                         delivered to Carl)
```

---

## 3. Decision Status Contract (`PolicyDecision`)

Each decision output is fully encapsulated in an auditable, immutable `PolicyDecision` object:

| Field | Type | Description |
|---|---|---|
| `decision_id` | `str` (UUIDv4) | Globally unique identifier for audit trail tracking. |
| `patient_id` | `str` | Anonymized patient identifier. |
| `session_id` | `str` | Active session identifier. |
| `task_name` | `str` | Activity or mini-game in progress. |
| `selected_strategy_id` | `Optional[str]` | The selected `StrategyID` enum value, or `None` if silence is approved. |
| `decision_status` | `DecisionStatus` | Enum: `APPROVED`, `NO_ADDITIONAL_ASSISTANCE`, `SUPPRESSED_COOLDOWN`, `SUPPRESSED_BY_PATIENT`, `CAREGIVER_OVERRIDE`, `PATIENT_HELP_REQUEST`, `INSUFFICIENT_DATA`. |
| `decision_reason` | `str` | Human-readable clinical/governance explanation for this outcome. |
| `prediction_reference` | `Optional[Dict]` | Raw Phase 4.5 predictor metadata and evidence used for the candidate. |
| `policy_rules_applied` | `List[str]` | Comprehensive list of safety rules applied in sequence. |
| `caregiver_control_applied`| `bool` | True if a caregiver lock/preset took precedence. |
| `patient_choice_required` | `bool` | True if delivery requires patient consent (e.g. AS08 break, AS05 demo). |
| `patient_choice_status` | `PatientChoiceStatus` | Enum: `NOT_REQUIRED`, `PENDING`, `ACCEPTED`, `REJECTED`. |
| `created_at` | `datetime` (UTC) | ISO-8601 timestamp with explicit UTC timezone. |

---

## 4. Safety & Governance Rules Enforced

1. **Caregiver Sovereignty (`CAREGIVER_LOCK_PRIORITY`)**
   - Caregiver locks take absolute precedence over all autonomous AI predictions, rate limits, and patient dismissals.
   - Outputs status `CAREGIVER_OVERRIDE`.

2. **Patient Agency & Immediate Assistance (`PATIENT_HELP_REQUEST_IMMEDIACY`)**
   - Explicit help requests (`patient_requested_help`), instruction repeat requests (`patient_requested_repeat`), or break requests (`patient_requested_break`) bypass cooldown and immediately trigger appropriate assistance with status `PATIENT_HELP_REQUEST`.

3. **Patient Dismissal / Mute Guard (`PATIENT_DISMISSAL_GUARD`)**
   - If the patient has dismissed or muted prompts (`patient_dismissed_prompt=True`), all spontaneous cues are suppressed (`SUPPRESSED_BY_PATIENT`).

4. **Spontaneous Prompt Cooldown (`SPONTANEOUS_PROMPT_COOLDOWN_45S`)**
   - Spontaneous suggestions require $\ge 45$ seconds since the last prompt. If this interval has not elapsed, the candidate is suppressed with status `SUPPRESSED_COOLDOWN`.

5. **Break Optionality (`AS08_BREAK_MUST_BE_OPTIONAL`)**
   - Rest/pause recommendations never block or lock out the user. The patient always retains a "Keep Playing" option (`patient_choice_required=True`).

6. **No Forced Escalation (`NO_AUTOMATIC_ESCALATION_ON_ERROR_COUNT`)**
   - Error count alone never forces high-tier scaffolding such as AS05 (Demonstration). Complex assistance requires multiple converging signals (latency, repeated cue failures, and patient consent).

7. **CDR Independence (`CDR_PROHIBITION`)**
   - Clinical Dementia Rating (CDR) scores are strictly prohibited from mapping to assistance strategies or support tiers. Any supplied score is discarded and audited.

8. **Silent Autonomy / Non-Interference (`SILENT_AUTONOMY_FALLBACK`)**
   - When a patient proceeds without difficulty or when assistance is inappropriate, the system outputs `NO_ADDITIONAL_ASSISTANCE` with `selected_strategy_id=None`.

9. **Data Sufficiency Check (`DATA_SUFFICIENCY_CHECK`)**
   - When mandatory identity telemetry (`patient_id`, `session_id`) is missing, the policy refuses to speculate and outputs `INSUFFICIENT_DATA`. Missing optional metrics degrade gracefully without triggering spurious prompts.

---

## 5. Verification & Test Suite

The dedicated Phase 4.6 test suite in `Backend/tests/test_phase4_6_safe_policy.py` covers 36 test cases across 8 functional categories:

- **PolicyDecision Output Contract (4 tests):** Schema completeness, JSON serializability, rule audit trail integrity.
- **Normal Recommendation Approval (3 tests):** Idle latency triggers, candidate evidence references, valid strategy identifiers.
- **No Additional Assistance (2 tests):** Clean session silence, clear clinical rationales.
- **Caregiver Control Precedence (4 tests):** Absolute override priority, cooldown bypass, dismissal override.
- **Patient Agency (5 tests):** Immediate help/repeat response, dismissal suppression, choice status tracking.
- **AS08 Break Behavior (4 tests):** Optionality guarantee, patient choice requirement, long session triggers.
- **Rate Limiting & Cooldowns (4 tests):** 45-second suppression window, cooldown bypass for patient help and caregiver overrides.
- **Insufficient Data & Fallbacks (4 tests):** Missing ID handling, non-spurious response to sparse metrics.
- **CDR Independence (4 tests):** Score invariant outcomes, absence of CDR terminology in decisions or rules.
- **No Automatic Escalation (2 tests):** High error count without cues does not trigger demonstration, sub-threshold latency causes no prompt.

**Result:** `Ran 36 tests — OK`
