# NeuroCompanion — Phase 4.7 Backend Assistance Runtime
## Operational Integration & Runtime Architecture Report

**Version:** 1.0.0  
**Date:** 2026-11-21  
**Status:** COMPLETE — Unified Backend Assistance Runtime Implemented; 23/23 Phase 4.7 Tests Pass.  
**Subsystem Location:** `Backend/ml/assistance/`  

---

## 1. Executive Summary

Phase 4.7 unifies the modular components built across previous milestones into an operational, backend-ready assistance runtime. It connects patient interaction telemetry to feature extraction, candidate prediction (Phase 4.5), safe policy evaluation (Phase 4.6), companion action generation (Carl Action Adapter), and auditable decision persistence.

The runtime exposes a single, clean Python entry point: `AssistanceRuntime.process(...)`.

### Core Operational Principles
- **Separation of Concerns:** Prediction engines evaluate hypotheses; the policy arbitrates clinical safety; the runtime coordinates flow and adapts outputs to Carl without hardcoded Unity couplings.
- **Action Gating:** Only approved, patient-initiated, or caregiver-locked decisions can yield executable companion actions (`requires_carl_action=True`). Suppressed, neutral, or insufficient outcomes strictly emit `requires_carl_action=False` and `carl_action=None`.
- **Break Sovereignty:** AS08 break recommendations always translate to optional pause offers with explicit `allow_keep_playing=True` and `force_lockout=False`.
- **Fail-Safe Robustness:** Runtime errors and malformed telemetry degrade safely to structured error or insufficient-data responses rather than fabricating assistance cues.
- **Traceable Persistence:** Decisions and metadata are logged to SQLite via a thread-safe, separable repository that stores no raw audio or sensitive identifiers.

---

## 2. Runtime Architecture & Component Connections

```
              ┌─────────────────────────────────────────────────────────┐
              │           Client / Future FastAPI Layer                 │
              │         (Sends Telemetry Context or Request)            │
              └────────────────────────────┬────────────────────────────┘
                                           │
                                           ▼
              ┌─────────────────────────────────────────────────────────┐
              │                   AssistanceRuntime                     │
              │            (ml/assistance/runtime.py)                   │
              └──────┬─────────────────────┬─────────────────────┬──────┘
                     │ (1) Validate        │                     │
                     ▼                     │                     │
          ┌─────────────────────┐          │                     │
          │RuntimeFeature-      │          │                     │
          │Extractor            │          │                     │
          └──────────┬──────────┘          │                     │
                     │ (2) Sanitized Ctx   │                     │
                     ▼                     │                     │
          ┌─────────────────────┐          │                     │
          │SafeAssistancePolicy │          │                     │
          │ (ml/assistance/     │          │                     │
          │  safe_policy.py)    │          │                     │
          └──────────┬──────────┘          │                     │
                     │                     │                     │
                     │ (Calls Phase 4.5    │                     │
                     │  Prediction Engine  │                     │
                     │  & Governance)      │                     │
                     │                     │                     │
                     │ (3) PolicyDecision  │                     │
                     ▼                     │                     │
              ┌────────────────────────────┴─┐                   │
              │      CarlActionAdapter       │                   │
              │  (ml/assistance/             │                   │
              │   carl_action.py)            │                   │
              └──────────────┬───────────────┘                   │
                             │ (4) Abstract CarlAction           │
                             ▼                                   │
              ┌──────────────────────────────┐                   │
              │  AssistanceRuntimeResponse   │◄──────────────────┘
              │  (Structured JSON Contract)  │
              └──────────────┬───────────────┘
                             │ (5) Auditable Trace
                             ▼
              ┌──────────────────────────────┐
              │ AssistanceDecisionRepository │
              │ (SQLite: neurocompanion.db)  │
              └──────────────────────────────┘
```

---

## 3. Data Contracts

### 3.1 Input Contract (`AssistanceRuntimeRequest`)

Can be passed directly as an `AssistanceRuntimeRequest`, a typed `InteractionTelemetryContext`, or a raw dictionary:

```python
@dataclass
class AssistanceRuntimeRequest:
    context: Union[InteractionTelemetryContext, Dict[str, Any]]
    seconds_since_last_prompt: float = 60.0
    cdr_score: Optional[float] = None  # Prohibited; discarded & audited if supplied
    request_id: str = field(default_factory=lambda: str(uuid.uuid4()))
    metadata: Dict[str, Any] = field(default_factory=dict)
```

### 3.2 Output Contract (`AssistanceRuntimeResponse`)

```json
{
  "request_id": "4131df33-3ce6-42d4-bb06-928420e6f3df",
  "patient_id": "patient-p47-001",
  "session_id": "session-p47-001",
  "runtime_status": "APPROVED",
  "decision_id": "8bb56208-cb86-4f4c-9742-0fbb0fe3c597",
  "decision_status": "APPROVED",
  "selected_strategy_id": "AS01_REPEAT_INSTRUCTION",
  "decision_reason": "Patient idle latency (1800ms) exceeded threshold. Prompt re-voiced.",
  "prediction_reference": {
    "strategy_id": "AS01_REPEAT_INSTRUCTION",
    "recommendation": "Patient idle latency (1800ms) exceeded threshold.",
    "recommendation_type": "DETERMINISTIC_RECOMMENDATION",
    "is_genuine_ml": false,
    "confidence_score": 0.85
  },
  "policy_rules_applied": [
    "CDR_PROHIBITION",
    "DATA_SUFFICIENCY_CHECK",
    "NO_AUTOMATIC_ESCALATION_ON_ERROR_COUNT",
    "CANDIDATE_STRATEGY_SELECTION"
  ],
  "caregiver_control_applied": false,
  "patient_choice_required": false,
  "patient_choice_status": "NOT_REQUIRED",
  "requires_carl_action": true,
  "carl_action": {
    "action_type": "REPEAT_CURRENT_INSTRUCTION",
    "strategy_id": "AS01_REPEAT_INSTRUCTION",
    "is_optional": false,
    "parameters": {
      "repeat_mode": "verbatim",
      "speech_rate": 1.0
    },
    "description": "Carl executes REPEAT_CURRENT_INSTRUCTION based on evaluated safe policy recommendation"
  },
  "created_at": "2026-11-21T17:55:00.123456+00:00"
}
```

---

## 4. Carl Action Mapping

The `CarlActionAdapter` cleanly converts approved strategy IDs into platform-agnostic companion action instructions:

| Strategy ID | Canonical Carl Action Type | Default Action Parameters | `is_optional` |
|---|---|---|:---:|
| `AS01_REPEAT_INSTRUCTION` | `REPEAT_CURRENT_INSTRUCTION` | `{"repeat_mode": "verbatim", "speech_rate": 1.0}` | `False` |
| `AS02_SIMPLIFY_INSTRUCTION`| `SIMPLIFY_CURRENT_INSTRUCTION`| `{"target_fkgl": 4.0, "max_words": 8}` | `True` |
| `AS03_STEP_BY_STEP` | `PROVIDE_STEP_BY_STEP_GUIDANCE`| `{"guidance_mode": "step_by_step"}` | `True` |
| `AS04_VISUAL_HINT` | `SHOW_VISUAL_HINT` | `{"hint_type": "spatial_highlight", "duration_ms": 3000}` | `False` |
| `AS05_DEMONSTRATION` | `SHOW_DEMONSTRATION` | `{"demonstration_type": "full_sequence", "interactive": true}` | `True` |
| `AS06_SLOWER_SPEECH` | `SLOW_SPEECH` | `{"speech_rate_multiplier": 0.85}` | `False` |
| `AS07_ADDITIONAL_RESPONSE_TIME` | `ALLOW_ADDITIONAL_RESPONSE_TIME` | `{"patience_window_ms": 1880.0, "cohort_reference": "dementia_nback_p95_buffer"}` | `False` |
| `AS08_BREAK_PAUSE` | `OFFER_OPTIONAL_BREAK` | `{"break_type": "optional_pause", "allow_keep_playing": true, "force_lockout": false}` | **`True` (Strict)** |
| `AS09_VERBAL_EXPLANATION` | `PROVIDE_VERBAL_EXPLANATION` | `{"explanation_type": "verbal_reassurance"}` | `True` |
| `AS10_INDEPENDENT_COMPLETION`| `ALLOW_INDEPENDENT_COMPLETION` | `{"independent_mode": true, "suppress_cues": true}` | `False` |

---

## 5. Persistence & Traceability

Decisions are stored in the SQLite database via `AssistanceDecisionRepository`:
- **Table:** `assistance_runtime_decisions`
- **Indexed on:** `(patient_id, created_at)`
- **Traceability:** Stores `decision_id`, `request_id`, `patient_id`, `session_id`, `runtime_status`, `decision_reason`, serialized `prediction_reference`, serialized `policy_rules_applied`, and serialized `carl_action`.
- **Privacy & Hygiene:** Zero audio records, zero personal health identifiers, zero freeform dialogue stored beyond public prompt text.
- **Separability:** Accepts `:memory:` for ephemeral testing and operates cleanly if repository is set to `None`.

---

## 6. API Readiness

The runtime is packaged as a standard callable Python module (`from ml.assistance import AssistanceRuntime, AssistanceRuntimeRequest`). A future FastAPI router can integrate this with minimal scaffolding:

```python
# Future FastAPI route sketch
@router.post("/assistance/evaluate", response_model=AssistanceRuntimeResponseSchema)
async def evaluate_assistance(payload: AssistanceRuntimeRequestSchema):
    runtime = get_assistance_runtime()
    response = runtime.process(payload.to_domain())
    return response.to_dict()
```

---

## 7. Verification & Test Suite

The test suite in `Backend/tests/test_phase4_7_assistance_runtime.py` covers 23 comprehensive test cases across 12 requirement areas:

1. **Telemetry Ingestion:** Context objects and raw dictionaries validated and sanitized.
2. **Prediction Pipeline:** Candidates generated and passed to policy.
3. **Carl Action Adaptation:** Valid strategy-to-action translation across all 10 strategies.
4. **Silent Autonomy:** `NO_ADDITIONAL_ASSISTANCE` yields `requires_carl_action=False`.
5. **Suppression:** Rate-limiting cooldown (< 45s) and patient dismissal emit no action.
6. **Patient Agency:** Immediate fulfillment of help and repeat requests bypassing cooldown.
7. **Caregiver Sovereignty:** Preset locks override AI predictions and dismissals.
8. **Break Behavior:** AS08 break offer is guaranteed optional with no lockout.
9. **Telemetry Fault Tolerance:** Missing identifiers and invalid types handled safely.
10. **Error Resilience:** Exceptions in policy or prediction return safe `ERROR` status.
11. **Metadata Preservation:** Complete schema population and clean JSON serialization.
12. **Persistence:** SQLite record insertion, retrieval, and filtered querying verified.

**Result:** `Ran 23 tests — OK`
