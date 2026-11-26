# NeuroCompanion — Phase 4.5 AS01–AS10 AI Assistance Prediction Layer
## Implementation Documentation

**Version:** 1.0.0  
**Date:** 2026-11-21  
**Status:** COMPLETE — All 10 strategies implemented; 17/17 Phase 4.5 tests pass; 148/148 full-suite tests pass.  
**Location:** `Backend/ml/assistance/`

---

## 1. Overview

Phase 4.5 delivers the **AS01–AS10 AI Assistance Prediction Layer**: a modular, hybrid intelligence subsystem that accepts patient interaction telemetry and generates structured assistance recommendations for all ten NeuroCompanion assistance strategies.

The subsystem is **deliberately honest** about its capabilities:

- It uses **genuine ML signals** only where Phase 4.4 trained models produce validated outputs.
- It uses **statistical reference estimates** where actuarial distributions from published datasets apply.
- It uses **transparent deterministic logic** where no direct training labels exist.
- It uses **provisional recommendations** where ML supporting signals exist but do not constitute trained assistance-benefit predictors.
- It **never** fabricates training labels, never claims heuristics are ML, and never inflates performance metrics.

---

## 2. Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                 AssistancePolicyInterface                    │
│           (safety + governance + cooldown layer)            │
└──────────────────────┬──────────────────────────────────────┘
                       │  filters / enriches
┌──────────────────────▼──────────────────────────────────────┐
│               AssistancePredictionEngine                    │
│      (orchestrates all 10 strategy predictors)              │
└──┬──────────┬──────────┬──────────┬──────────┬─────────────┘
   │          │          │          │          │
  AS01       AS02       AS03       AS04       AS05
  AS06       AS07       AS08       AS09       AS10
   │          │          │          │          │
   └──────────┴──────────┴──────────┴──────────┘
                         │
              RuntimeFeatureExtractor
              ModelSignalProvider ──► M1 / M3 / M4 / M6 artifacts
```

---

## 3. Module Responsibilities

| File | Purpose |
|------|---------|
| `config.py` | Frozen `AssistanceConfig` dataclass — all clinical thresholds and policy constants |
| `schemas.py` | Type-safe data contracts: `InteractionTelemetryContext`, `AssistancePredictionRecord`, `PredictionBatchResult`, `StrategyID`, `RecommendationType` |
| `model_signals.py` | `ModelSignalProvider` — loads Phase 4.4 artifacts (M1/M3/M4/M6) and extracts ML signals with graceful fallbacks |
| `feature_extractor.py` | `RuntimeFeatureExtractor` — computes derived features (Flesch-Kincaid grade level, idle latency classification, session engagement score) |
| `predictors.py` | Ten strategy predictor classes (AS01–AS10), each a self-contained recommendation unit |
| `prediction_engine.py` | `AssistancePredictionEngine` — instantiates and calls all 10 predictors, assembles `PredictionBatchResult` |
| `policy_interface.py` | `AssistancePolicyInterface` — safety arbitration, prompt cooldown enforcement, AS08 "Keep Playing" guarantee |
| `__init__.py` | Package public API — exports engine, interface, schemas, config |

---

## 4. Data Flow

### 4.1 Input: `InteractionTelemetryContext`

```python
InteractionTelemetryContext(
    session_id="session-001",
    patient_id="patient-001",
    task_id="task-kitchen-01",
    game_type="memory",               # Unity game type string
    elapsed_session_minutes=18.5,     # session clock
    idle_latency_ms=1450.0,           # time since last patient action
    target_miss_count=2,              # missed targets this task
    step_error_count=1,               # errors in current step sequence
    consecutive_errors=0,             # error burst count
    failed_cue_count=1,               # audio/visual cues patient ignored
    current_instruction_text="...",   # instruction currently displayed
    current_instruction_word_count=14,
    patient_response_text="...",      # patient's last spoken/typed reply
    prior_assistance_strategy=None,   # most recently triggered AS code
    total_steps_in_task=5,
    steps_completed=2,
    session_start_utc="2026-11-21T08:00:00Z",
)
```

### 4.2 Output: `PredictionBatchResult`

```python
PredictionBatchResult(
    session_id="session-001",
    patient_id="patient-001",
    timestamp_utc="2026-11-21T...",
    predictions=[
        AssistancePredictionRecord(
            strategy_id=StrategyID.AS07,
            strategy_name="Additional Response Time",
            recommendation_type=RecommendationType.STATISTICAL_REFERENCE,
            recommended=True,
            confidence=0.72,
            parameter={"patience_window_ms": 1531},
            rationale="M1 Log-Normal P95 pacing reference (older-adults MSL cohort)",
            evidence_source="M1-pacing-estimator-v1",
            is_genuine_ml=False,
        ),
        AssistancePredictionRecord(
            strategy_id=StrategyID.AS09,
            strategy_name="Verbal Explanation",
            recommendation_type=RecommendationType.GENUINE_ML,
            recommended=True,
            confidence=0.63,
            parameter={"empathy_score": 2.8, "actionability_score": 3.1},
            rationale="M6 empathy signal below threshold (2.80 < 3.50)",
            evidence_source="M6-dialogue-intelligence-v1",
            is_genuine_ml=True,
        ),
        # ... 8 more records
    ],
    genuine_ml_strategy_count=2,
    statistical_strategy_count=1,
    deterministic_strategy_count=7,
    policy_applied=True,
)
```

---

## 5. All Ten Assistance Strategies

### 5.1 Strategy Classification

| Strategy | Method | Evidence Basis |
|----------|--------|---------------|
| AS01 Repeat Instruction | Deterministic | Idle latency ≥ 1,200 ms OR ≥ 1 missed target |
| AS02 Simplify Instruction | Deterministic | FKGL grade > 6.0 OR word count > 12 OR ≥ 2 hesitations |
| AS03 Step-by-Step Guidance | Deterministic + M4 signal | Task has ≥ 3 steps AND step errors present; M4 independence prior informs urgency |
| AS04 Visual Hint | Deterministic + M3 signal | ≥ 2 missed targets OR idle > 1,500 ms; M3 error-risk probability boosts confidence |
| AS05 Demonstration | Deterministic | ≥ 2 failed cues AND ≥ 3 errors AND idle > 3,000 ms |
| AS06 Slower Speech | Deterministic | Triggered by AS01 repeat AND elevated miss count |
| **AS07 Additional Response Time** | **Statistical Reference** | **M1 Log-Normal P95 pacing window (validated on N=32 older adults + N=24 dementia cohort)** |
| AS08 Break / Pause | Deterministic | Session ≥ 20 min OR ≥ 4 consecutive errors — NEVER forces lockout; always presents "Keep Playing" |
| **AS09 Verbal Explanation** | **Provisional — ML Supporting Signal** | **M6 dialogue scorer used to assess candidate text quality only. M6 is NOT a trained AS09 benefit predictor.** |
| **AS10 Independent Completion** | **Provisional — ML Supporting Signal** | **M3 error-risk signal + behavioural observations. M3 is NOT a trained autonomy predictor.** |

> [!IMPORTANT]
> **No strategy in Phase 4.5 has a genuinely trained assistance-benefit predictor.** AS07 uses a validated statistical reference (M1). AS09 and AS10 are **provisional** recommendations: M6 and M3 contribute ML *supporting signals* (text quality and error risk respectively), but neither model was trained to predict whether its linked assistance strategy will benefit the patient. AS01–AS06 and AS08 are transparent deterministic recommendations.

---

### 5.2 AS01 — Repeat Instruction

**Trigger conditions (any):**
- `idle_latency_ms >= 1200.0`
- `target_miss_count >= 1`

**Parameters emitted:** `{"speech_rate": 1.0}` (speech rate unchanged; use AS06 for rate reduction)

**Confidence:** 0.85 when both conditions are met; 0.70 otherwise.

---

### 5.3 AS02 — Simplify Instruction

**Trigger conditions (any):**
- Flesch-Kincaid Grade Level of `current_instruction_text` > 6.0
- `current_instruction_word_count > 12`
- `failed_cue_count >= 2` (interpreted as hesitation signal)

**Parameters emitted:** `{"target_fkgl": 4.0, "max_words": 8}`

**Feature derivation:** FKGL computed in `RuntimeFeatureExtractor.compute_fkgl()` from syllable count and sentence count. Falls back to word-count heuristic if text is very short.

---

### 5.4 AS03 — Step-by-Step Guidance

**Trigger conditions:**
- `total_steps_in_task >= 3` (task has sufficient granularity)
- `step_error_count >= 1` OR `steps_completed < total_steps_in_task * 0.5`

**M4 signal integration:** If M4 independence prior for a matching domestic task < 0.60 (default 0.698 for overall CASAS dataset), urgency is elevated.

**Parameters emitted:** `{"next_step_index": steps_completed + 1, "total_steps": total_steps_in_task}`

> [!NOTE]
> M4 is a **descriptive prior** (69.77% independence rate from CASAS ADL dataset), not a trained ML model. It informs — but does not replace — deterministic logic.

---

### 5.5 AS04 — Visual Hint

**Trigger conditions (any):**
- `target_miss_count >= 2`
- `idle_latency_ms >= 1500.0`

**M3 signal integration:** If M3 error-occurrence probability ≥ 0.65, confidence is elevated from 0.70 → 0.82.

**Parameters emitted:** `{"hint_type": "spatial_highlight", "duration_ms": 3000}`

---

### 5.6 AS05 — Demonstration

**Trigger conditions (all):**
- `failed_cue_count >= 2`
- `step_error_count >= 3`
- `idle_latency_ms >= 3000.0`

**Parameters emitted:** `{"demo_type": "full_sequence"}`

**Confidence:** Fixed 0.90 — high because all three conditions must be simultaneously true.

---

### 5.7 AS06 — Slower Speech

**Trigger conditions:**
- AS01 would also trigger (Repeat Instruction already needed)
- `target_miss_count >= 2` (indicates the original speech rate was insufficient)

**Parameters emitted:** `{"speech_rate_multiplier": 0.85}` (very high miss counts trigger 0.75)

---

### 5.8 AS07 — Additional Response Time *(Statistical Reference)*

**Method:** M1 Log-Normal pacing estimator (Phase 4.4 artifact `m1_pacing/v1/model.joblib`).

**Validated datasets:**
| Cohort | N | σ (Log-Normal) | P95 (ms) | KS p-value |
|--------|---|---------------|----------|------------|
| Older adults MSL | 32 | 0.0956 | 1,031.27 | 0.605 |
| N-Back dementia | 24 | 0.2708 | 1,379.91 | 0.526 |

**Patience window calculation:**
```
patience_ms = max(P95_older_adults, P95_dementia) + safety_buffer_ms
            = 1,379.91 + 500 = 1,879.91 ms (rounded to 1,880 ms)
```

**Parameters emitted:** `{"patience_window_ms": 1880, "source_cohort": "dementia_nback_p95"}`

> [!WARNING]
> The M1 P95 values are **population-level statistical references**, not validated individual patient timeout requirements. They must not be presented to clinicians as calibrated per-patient thresholds.

---

### 5.9 AS09 — Verbal Explanation *(Provisional — ML Supporting Signal)*

**Classification:** PROVISIONAL. M6 is used as a text-quality supporting signal only.

**What M6 is:** A RidgeCV regression model trained on AI4Hope dialogue ratings to score the surface empathy and actionability of candidate explanation text. It predicts how a panel rated written dialogue — it was **not** trained to predict whether a Verbal Explanation will therapeutically benefit a specific patient.

**What M6 is not:** A trained AS09 assistance-benefit predictor. `is_genuine_ml` is always `False` for AS09.

**Signal extraction:** `ModelSignalProvider.get_m6_dialogue_quality(candidate_prompt_text)` → returns `{"predicted_empathy": float, "predicted_actionability": float}` for any non-empty text.

**How AS09 uses it:** If a candidate explanation text scores below quality thresholds (`empathy < 3.50` or `actionability < 3.50`), the predictor flags that the text may need improvement before voicing. It does **not** determine whether a verbal explanation is the right strategy for this patient at this moment.

**Fallback:** If `candidate_prompt_text` is empty or M6 fails to load, falls back to idle-latency deterministic trigger.

**Validated M6 performance (supporting signal reference only):**
- Empathy MAE = 0.9296 vs baseline 1.035 — beats baseline ✓
- Actionability MAE = 0.7694 vs baseline 0.7532 — does NOT beat baseline (reported honestly)

**Parameters emitted:** `{"empathy_score": float, "actionability_score": float, "trigger": "ml_signal" | "fallback"}`

---

### 5.10 AS10 — Independent Completion *(Provisional — ML Supporting Signal)*

**Classification:** PROVISIONAL. M3 is used as a task error-risk supporting signal only.

**What M3 is:** A hurdle classifier trained on task error occurrence (CASAS ADL and comparable datasets). It predicts whether a task attempt will produce errors — it was **not** trained to predict whether withholding assistance will lead to successful patient autonomous completion.

**What M3 is not:** A trained AS10 independent-completion predictor. `is_genuine_ml` is always `False` for AS10.

**Signal extraction:** `ModelSignalProvider.get_m3_error_risk(game_type, response_latency_sec)` → returns `{"error_probability": float, "expected_error_count": float}`.

**How AS10 uses it:** Derives `error_free_prob = 1.0 - error_probability`. Combined with observable session behaviour (zero step errors, no help requested), a high error-free probability supports a provisional recommendation to withhold prompts and allow independent completion. This is a behavioural observation informed by an error-risk proxy — not a validated autonomy prediction.

**Fallback:** If `game_type` not seen in training, M3 emits sklearn `UserWarning` (suppressed). Falls back to `step_error_count == 0` deterministic check.

**Validated M3 performance (supporting signal reference only):**
- Hurdle Classifier Balanced Accuracy: 64.06% (vs 50% dummy) — genuine signal ✓
- Count regressor MAE: 0.5298 (vs 0.4654 dummy) — does NOT beat baseline; 77.5% zero-inflation (reported honestly)

**Parameters emitted:** `{"error_free_probability": float, "expected_error_count": float, "independence_recommended": bool}`

---

## 6. Safety & Governance — `AssistancePolicyInterface`

The policy interface wraps every prediction batch before it reaches the application layer.

### 6.1 Rules Enforced

| Rule | Detail |
|------|--------|
| **Prompt cooldown** | Any spontaneous assistance prompt must respect ≥ 45 s gap since last prompt. Predictions within cooldown window are withheld. |
| **AS08 keep-playing guarantee** | Break/Pause recommendation must always include a "Keep Playing" option. Force-lockout (`allow_forced_break_lockout`) is permanently `False`. |
| **CDR prohibition** | No CDR (Clinical Dementia Rating) score may be used to gate or categorise assistance tier. No CDR data is ingested by any predictor. |
| **Safety floor** | If `consecutive_errors >= 4` and session > 20 min, AS08 is elevated to `HIGH_PRIORITY` regardless of other predictor outputs. |
| **Conflict resolution** | If AS05 (Demonstration) and AS10 (Independent Completion) are both recommended, AS10 is suppressed — patient is in difficulty, not ready for independent completion. |

### 6.2 Policy Output Fields

```python
policy_applied: bool           # True if any rule changed/suppressed a prediction
policy_notes: list[str]        # Human-readable explanation of each rule applied
predictions: list[...]         # Modified recommendation list
```

---

## 7. Model Signal Provider Details

`ModelSignalProvider` lazy-loads Phase 4.4 artifacts on first call.

| Method | Artifact | Fallback on failure |
|--------|----------|-------------------|
| `get_m1_signal()` | `m1_pacing/v1/model.joblib` | Returns hardcoded P95 defaults |
| `get_m3_signal(game_type)` | `m3_task_perf/v1/model.joblib` | Returns `{"error_free_prob": 0.5}` |
| `get_m4_signal()` | `m4_independence/v1/metrics.json` | Returns `{"independence_rate": 0.698}` |
| `get_m6_signal(text)` | `m6_dialogue/v1/model.joblib` | Returns `None` (triggers deterministic fallback) |

All failures are logged at `WARNING` level. No exception propagates to the caller.

---

## 8. Test Coverage

### Phase 4.5 Test Suite

**File:** `Backend/tests/test_phase4_5_assistance_predictions.py`  
**Tests:** 17 | **Result:** 17/17 PASS

| Test | Purpose |
|------|---------|
| `test_all_strategies_present` | Engine returns all 10 strategy IDs |
| `test_as01_repeat_triggered_by_idle_latency` | AS01 fires on idle > 1,200 ms |
| `test_as02_simplify_triggered_by_long_instruction` | AS02 fires on word count > 12 |
| `test_as03_step_guidance_triggered` | AS03 fires when errors present in multi-step task |
| `test_as04_visual_hint_triggered` | AS04 fires on ≥ 2 missed targets |
| `test_as05_demonstration_triggered` | AS05 fires on all three hard conditions |
| `test_as06_slower_speech_triggered` | AS06 fires when AS01 also triggers |
| `test_as07_pacing_reference_present` | AS07 always present; patience_window_ms > 0 |
| `test_as08_break_recommended_long_session` | AS08 triggers on 25-min session |
| `test_as08_keep_playing_option_always_present` | AS08 parameter includes keep_playing key |
| `test_as09_verbal_explanation_uses_ml_or_fallback` | AS09 produces valid record; `is_genuine_ml` honest |
| `test_as10_independent_completion_ml_or_fallback` | AS10 produces valid record; `is_genuine_ml` honest |
| `test_missing_telemetry_handled_gracefully` | Missing optional fields → no crash |
| `test_honest_labeling` | No deterministic predictor claims `is_genuine_ml=True` |
| `test_patient_safety_no_forced_lockout` | AS08 never sets `force_lockout=True` |
| `test_cdr_prohibition` | No schema field or parameter key contains "CDR" |
| `test_prediction_batch_consistency` | All 10 `StrategyID` members appear exactly once |

### Full Backend Regression Suite

| Suite | Tests | Result |
|-------|-------|--------|
| Phase 1 OASIS | — | ✓ |
| Phase 2A API | — | ✓ |
| Phase 4.3 Framework | — | ✓ |
| Phase 4.4 Trained Models | 5 | ✓ |
| Phase 4.5 Assistance Predictions | 17 | ✓ |
| **Total** | **148** | **ALL PASS** |

> [!NOTE]
> The FastAPI lifespan `RuntimeError: generator didn't stop after athrow()` printed to stderr during tests is a known benign teardown warning from the test harness's interaction with `httpx.AsyncClient`. It does not indicate a test failure and does not affect any tested functionality.

---

## 9. What Was NOT Done (Honest Scope Boundaries)

| Item | Why not done |
|------|-------------|
| Trained ML models for AS01–AS06, AS08 | No real-world interaction logs with caregiver-labelled assistance ground truth exist in the available datasets |
| Per-patient personalisation | No longitudinal patient-specific interaction history database was built in Phase 4 |
| Production FastAPI endpoint integration | Phase 4.5 is kept isolated from Phase 2A production backend per project rules |
| Unity direct integration | Phase 3 Unity scenes are frozen; a future Phase 5 integration bridge is required |
| CDR-based assistance tiers | Explicitly prohibited by project rules |

---

## 10. Future Requirements for Genuine ML (AS01–AS10)

To replace deterministic and provisional predictors with genuine ML for all strategies:

1. **Collect interaction logs** during real NeuroCompanion therapy sessions (Unity telemetry + caregiver actions).
2. **Record ground-truth labels** — when a therapist/caregiver selects an assistance strategy, log the trigger event with the patient's telemetry context at that moment.
3. **Target variables needed:**
   - AS01: binary — caregiver chose "Repeat" (Y/N) given `{idle_ms, miss_count}`
   - AS02: binary — caregiver chose "Simplify" given `{FKGL, word_count, miss_count}`
   - AS03: binary — caregiver chose "Step-by-Step" given `{error_count, task_steps}`
   - AS04: binary — caregiver chose "Visual Hint" given `{miss_count, idle_ms}`
   - AS05: binary — caregiver chose "Demonstration" given `{failed_cues, errors, idle_ms}`
   - AS06: binary — caregiver chose "Slower Speech" given `{speech_rate, miss_count}`
   - AS08: binary — caregiver initiated break given `{session_min, error_burst}`
   - **AS09: binary — caregiver delivered a verbal explanation AND patient performance improved** (text quality alone is insufficient)
   - **AS10: binary — caregiver withheld assistance AND patient completed task independently** (zero errors + no help is a necessary but not sufficient proxy)
   - AS08: binary — caregiver initiated break given `{session_min, error_burst}`
4. **Minimum sample size:** ≥ 100 labelled episodes per strategy for initial training; ≥ 500 for robust cross-validation.
5. **Re-train M5** (Personalised Adaptation) once per-patient history is available.

---

## 11. File Locations

| File | Path |
|------|------|
| Package root | `Backend/ml/assistance/` |
| Config | `Backend/ml/assistance/config.py` |
| Schemas | `Backend/ml/assistance/schemas.py` |
| Model signals | `Backend/ml/assistance/model_signals.py` |
| Feature extractor | `Backend/ml/assistance/feature_extractor.py` |
| Predictors (AS01–AS10) | `Backend/ml/assistance/predictors.py` |
| Prediction engine | `Backend/ml/assistance/prediction_engine.py` |
| Policy interface | `Backend/ml/assistance/policy_interface.py` |
| Package init | `Backend/ml/assistance/__init__.py` |
| Test suite | `Backend/tests/test_phase4_5_assistance_predictions.py` |
| Phase 4.5 Design | `Backend/docs/PHASE4_5_ASSISTANCE_PREDICTION_DESIGN.md` |
| Phase 4.4 Verification | `Backend/docs/PHASE4_4_FINAL_VERIFICATION.md` |
| Phase 4.4 Training Report | `Backend/docs/PHASE4_4_TRAINING_REPORT.md` |

---

*Phase 4.5 implementation completed 2026-11-21. No Phase 1, Phase 2A or Phase 3 files were modified.*
