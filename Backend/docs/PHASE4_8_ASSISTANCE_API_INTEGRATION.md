# NeuroCompanion — Phase 4.8 Assistance API Integration
## FastAPI Transport Layer & Endpoint Documentation

**Version:** 1.0.0  
**Date:** 2026-11-21  
**Status:** COMPLETE — Assistance API Router Implemented; 20/20 Phase 4.8 Tests Pass.  
**Subsystem Location:** `Backend/api/routes/assistance.py` & `Backend/api/schemas/assistance.py`  

---

## 1. Executive Summary

Phase 4.8 exposes the NeuroCompanion assistance prediction and safe policy runtime (Phases 4.5, 4.6, 4.7) via the application's primary FastAPI service.

The endpoint serves as the transport and validation boundary:
- **Canonical Endpoint:** `POST /api/v1/assistance/evaluate`
- **Transport Role:** Performs strict input validation, rejects missing patient/session identifiers with HTTP 422, enforces CDR exclusion, passes telemetry into `AssistanceRuntime`, and returns the structured decision.
- **Safety Boundary:** Contains zero decision-making logic itself. All clinical governance, rate limiting, and action authorization remain encapsulated in `SafeAssistancePolicy` and `AssistanceRuntime`.
- **Unity Readiness:** Provides a stable JSON contract ready for HTTP client integration from Unity C# scripts in Phase 5.

---

## 2. API Contract Specification

### 2.1 Route Details
- **HTTP Method:** `POST`
- **Path:** `/api/v1/assistance/evaluate`
- **Tags:** `AS01-AS10 Assistance Runtime`
- **Success Status Code:** `200 OK`
- **Validation Failure Code:** `422 Unprocessable Content`

### 2.2 Request Schema (`AssistanceEvaluateRequest`)

Supports both nested context formatting and flat JSON payloads:

| Field | Type | Required | Description |
|---|---|:---:|---|
| `context` | `InteractionTelemetrySchema` | Optional | Nested context object (preferred). |
| `patient_id` | `str` | Required* | Anonymized patient identifier (*required if context omitted). |
| `session_id` | `str` | Required* | Active therapy session UUID (*required if context omitted). |
| `task_name` | `str` | Optional | Unity activity / minigame name (default `"DefaultTask"`). |
| `task_domain` | `str` | Optional | Task classification (`cognitive_selection`, `domestic_adl`, etc.). |
| `step_index` | `int` | Optional | 1-based current step index ($\ge 1$). |
| `step_total_steps` | `int` | Optional | Total steps in activity ($\ge 1$). |
| `response_latency_ms` | `float` | Optional | Response timing in milliseconds ($\ge 0$). |
| `idle_duration_ms` | `float` | Optional | Inactivity time preceding event in ms ($\ge 0$). |
| `target_miss_count` | `int` | Optional | Target miss count ($\ge 0$). |
| `step_error_count` | `int` | Optional | Errors committed on current step ($\ge 0$). |
| `consecutive_error_count`| `int` | Optional | Consecutive error burst ($\ge 0$). |
| `session_duration_minutes`| `float` | Optional | Elapsed session minutes ($\ge 0$). |
| `patient_requested_help` | `bool` | Optional | Set to `true` when patient taps "Help". |
| `patient_requested_repeat`| `bool` | Optional | Set to `true` when patient requests instruction repeat. |
| `patient_requested_break` | `bool` | Optional | Set to `true` when patient requests rest/pause. |
| `patient_dismissed_prompt`| `bool` | Optional | Set to `true` when patient dismissed or muted companion audio. |
| `caregiver_locked_strategy`| `str` | Optional | Authoritative caregiver preset lock (e.g. `"AS06_SLOWER_SPEECH"`). |
| `seconds_since_last_prompt`| `float` | Optional | Seconds elapsed since last companion cue (default `60.0`). |
| `cdr_score` | `float` | Optional | **Prohibited.** Discarded and audited if supplied. |
| `request_id` | `str` | Optional | Optional client-supplied UUID for idempotency / trace tracking. |

### 2.3 Response Schema (`AssistanceEvaluateResponse`)

| Field | Type | Description |
|---|---|---|
| `request_id` | `str` | UUID tracking this specific request. |
| `patient_id` | `str` | Anonymized patient identifier. |
| `session_id` | `str` | Session identifier. |
| `runtime_status` | `str` | Outcome: `APPROVED`, `PATIENT_INITIATED`, `CAREGIVER_CONTROLLED`, `OPTIONAL_BREAK`, `SUPPRESSED`, `NO_ADDITIONAL_ASSISTANCE`, `INSUFFICIENT_DATA`, `ERROR`. |
| `decision_id` | `Optional[str]` | UUID of the persisted policy decision record. |
| `decision_status` | `Optional[str]` | Underlying policy status (`APPROVED`, `CAREGIVER_OVERRIDE`, `PATIENT_HELP_REQUEST`, etc.). |
| `selected_strategy_id` | `Optional[str]` | The selected StrategyID (e.g. `"AS01_REPEAT_INSTRUCTION"`), or `null`. |
| `decision_reason` | `str` | Clinical & safety justification for the decision. |
| `prediction_reference` | `Optional[Dict]` | Raw Phase 4.5 candidate predictor outputs and evidence features. |
| `policy_rules_applied` | `List[str]` | Ordered list of safety rules applied. |
| `caregiver_control_applied`| `bool` | True if a caregiver preset lock superseded automated prediction. |
| `patient_choice_required` | `bool` | True if action requires patient confirmation before Carl speaks/acts. |
| `patient_choice_status` | `str` | `NOT_REQUIRED`, `PENDING`, `ACCEPTED`, `REJECTED`. |
| `requires_carl_action` | `bool` | **Authoritative flag:** `true` ONLY when Carl is authorized to intervene. |
| `carl_action` | `Optional[Dict]` | Abstract companion action instruction (null if `requires_carl_action=false`). |
| `created_at` | `str` | ISO-8601 UTC timestamp. |

---

## 3. Example Request & Response Payloads

### 3.1 Example 1: Approved Spontaneous Visual Hint
**Request:**
```http
POST /api/v1/assistance/evaluate HTTP/1.1
Host: 127.0.0.1:8000
Content-Type: application/json

{
  "patient_id": "patient-garden-42",
  "session_id": "session-live-001",
  "task_name": "CognitiveGarden",
  "target_miss_count": 2,
  "idle_duration_ms": 1600.0,
  "seconds_since_last_prompt": 65.0
}
```

**Response (200 OK):**
```json
{
  "request_id": "c1f7a37e-61ba-478a-92e1-4bb2f63f5e92",
  "patient_id": "patient-garden-42",
  "session_id": "session-live-001",
  "runtime_status": "APPROVED",
  "decision_id": "0d2c3848-18e9-4e78-9e5c-023a1a67a840",
  "decision_status": "APPROVED",
  "selected_strategy_id": "AS04_VISUAL_HINT",
  "decision_reason": "Target miss count (2) or idle latency (1600.0ms) exceeded threshold. Spatial highlight visual cue recommended.",
  "prediction_reference": {
    "strategy_id": "AS04_VISUAL_HINT",
    "recommendation": "Target miss count (2) exceeded threshold.",
    "recommendation_type": "DETERMINISTIC_RECOMMENDATION",
    "is_genuine_ml": false,
    "confidence_score": 0.82
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
    "action_type": "SHOW_VISUAL_HINT",
    "strategy_id": "AS04_VISUAL_HINT",
    "is_optional": false,
    "parameters": {
      "hint_type": "spatial_highlight",
      "duration_ms": 3000
    },
    "description": "Carl executes SHOW_VISUAL_HINT based on evaluated safe policy recommendation"
  },
  "created_at": "2026-11-21T18:10:00.000000+00:00"
}
```

### 3.2 Example 2: Suppressed by Rate Limiting Cooldown
**Request:**
```json
{
  "patient_id": "patient-garden-42",
  "session_id": "session-live-001",
  "idle_duration_ms": 2000.0,
  "seconds_since_last_prompt": 12.0
}
```

**Response (200 OK):**
```json
{
  "request_id": "67f8a11e-91ba-478a-92e1-4bb2f63f5e93",
  "patient_id": "patient-garden-42",
  "session_id": "session-live-001",
  "runtime_status": "SUPPRESSED",
  "decision_id": "8d2c3848-18e9-4e78-9e5c-023a1a67a841",
  "decision_status": "SUPPRESSED_COOLDOWN",
  "selected_strategy_id": null,
  "decision_reason": "Cooldown active (12.0s < 45.0s). Spontaneous prompts suppressed to avoid cognitive overload.",
  "prediction_reference": null,
  "policy_rules_applied": [
    "CDR_PROHIBITION",
    "DATA_SUFFICIENCY_CHECK",
    "SPONTANEOUS_PROMPT_COOLDOWN_45S"
  ],
  "caregiver_control_applied": false,
  "patient_choice_required": false,
  "patient_choice_status": "NOT_REQUIRED",
  "requires_carl_action": false,
  "carl_action": null,
  "created_at": "2026-11-21T18:10:15.000000+00:00"
}
```

---

## 4. Architecture & Security Status

1. **Router Registration:**
   The router is registered in `main.py` via `app.include_router(assistance_router)`. It is registered once, avoiding circular imports, and preserves all existing Phase 1, Phase 2A, and Phase 3 routes.
2. **Dependency Injection:**
   `get_assistance_runtime()` lazily initializes and shares a single thread-safe `AssistanceRuntime` instance connected to `neurocompanion.db`. Testing harnesses can seamlessly override this dependency via `app.dependency_overrides`.
3. **Authentication & Transport Security:**
   Consistent with the existing Phase 2A and Phase 3 local development backend, endpoints currently operate in an unauthenticated local environment. In future multi-tenant production deployments, an authentication middleware (e.g. JWT / API key) will be attached to the router.
4. **Privacy & Data Minimization:**
   No audio bytes, biometric signals, or unneeded patient identifiers are accepted or stored. Decision logs record only structured metadata.

---

## 5. Verification & Test Suite

The test suite in `Backend/tests/test_phase4_8_assistance_api.py` uses ASGI in-process dispatching (`_asgi_request`) and validates all 14 required areas:

- `test_01_valid_assistance_request`: Successful 200 OK evaluation.
- `test_02_correct_runtime_invocation`: Request and patient ID propagation.
- `test_03_valid_structured_response`: Response schema field completeness.
- `test_04a_missing_patient_id_returns_422`: Rejection of missing patient identifier.
- `test_04b_empty_patient_id_returns_422`: Rejection of whitespace-only patient identifier.
- `test_04c_missing_session_id_returns_422`: Rejection of missing session identifier.
- `test_05a_invalid_latency_type_returns_422`: Rejection of non-numeric latency.
- `test_05b_negative_step_index_returns_422`: Rejection of negative step index.
- `test_06_no_additional_assistance`: Clean session returns non-interfering response.
- `test_07a_cooldown_suppression`: 45-second cooldown suppression.
- `test_07b_patient_dismissal_suppression`: Prompt dismissal suppresses spontaneous cues.
- `test_08_caregiver_control`: Caregiver locked strategy overrides predictions.
- `test_09a_patient_requested_help`: Explicit help request bypasses cooldown.
- `test_09b_patient_requested_repeat`: Explicit repeat request triggers verbatim prompt replay.
- `test_10_optional_as08_break`: Break recommendation generates optional pause offer with no lockout.
- `test_11_runtime_error_handling`: Runtime exceptions return safe structured error response without crash.
- `test_12_router_registration`: Verified endpoint registration in OpenAPI schema.
- `test_13a_health_endpoint_still_functional`: Backward compatibility check for `/api/v1/health`.
- `test_13b_root_endpoint_still_functional`: Backward compatibility check for `/`.
- `test_14_no_unauthorized_executable_carl_actions`: Strictly verifies that non-approved statuses never return an executable Carl action.

**Result:** `Ran 20 tests — OK`
