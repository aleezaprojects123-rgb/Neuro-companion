# NeuroCompanion API Contract — Version 1
**Contract Reference:** P2A-30  
**Frozen:** 2026-11-11  
**API Version Prefix:** `/api/v1/`  
**Legacy prefix:** _(none — original paths without prefix)_  
**Backend technology:** FastAPI + Pydantic v2 + SQLite (WAL mode)  
**Base URL (local dev):** `http://127.0.0.1:8000`

---

## Versioning Policy

All new Unity client code **MUST** use the `/api/v1/` prefix.  
Legacy routes (no prefix) are preserved indefinitely for backward compatibility and will not be removed without a new major contract version.

| Route style | Example | Unity target |
|-------------|---------|--------------|
| **Versioned (v1)** | `POST /api/v1/profiles/onboard` | ✅ Use this |
| Legacy (compat) | `POST /profiles/onboard` | ⚠️ Preserved, do not use in new code |

---

## Authentication

> **Currently unimplemented.** All endpoints are open with no token, API key, or session authentication. This is explicitly noted per endpoint. Authentication is a planned future phase and will be introduced as a non-breaking addition under `/api/v2/`.

---

## Common Response Conventions

| Field | Type | Description |
|-------|------|-------------|
| `status` | `string` | Always `"success"` on 2xx responses |
| Error body | `{"detail": "..."}` | FastAPI default error format |

**HTTP status codes used:**

| Code | Meaning |
|------|---------|
| 200 | OK (GET, POST returning computed result) |
| 201 | Created (resource stored) |
| 400 | Bad request / empty payload |
| 404 | Resource not found |
| 422 | Validation error (Pydantic) |
| 500 | Internal server error |
| 503 | Service unavailable (model not loaded) |

---

## 1. Health & Model Status

### `GET /api/v1/health`
**Legacy:** `GET /health`

**Purpose:** Check whether the FastAPI application is running and the Phase 1 ML pipeline is loaded.

**Authentication:** Not required.

**Request:** No body.

**Response — 200 OK:**
```json
{
  "status": "ok",
  "model_loaded": true,
  "model_version": "oasis-practical-v1"
}
```

**Response fields:**

| Field | Type | Description |
|-------|------|-------------|
| `status` | `string` | Always `"ok"` |
| `model_loaded` | `boolean` | `true` when `pipeline.joblib` is loaded in memory |
| `model_version` | `string` | Model artifact version identifier |

**Error responses:** None expected under normal operation.

**Limitations:** Does not verify SQLite database integrity.

---

## 2. Phase 1 — CDR Assessment (OASIS-1 Model)

### `POST /api/v1/predict`
**Legacy:** `POST /predict`

**Purpose:** Run 3-class CDR experimental classification on practical demographic and cognitive screener inputs. Returns a probability distribution across the three CDR categories.

> **IMPORTANT — Architectural constraint:** The prediction result is **never** automatically mapped to a support tier (`Standard`, `Guided`, `HighSupport`). Caregiver review is always required before any support tier is assigned. See §7 (Caregiver Override) for the tier assignment mechanism.

**Authentication:** Not required.

**Required request fields:**

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| `Age` | `float` | 30.0 – 105.0 | Participant age in years |
| `M/F` | `string` | `"M"` or `"F"` | Biological sex |
| `Educ` | `float` | 1.0 – 5.0 | Education level code (1 = less than high school, 5 = beyond college) |
| `MMSE` | `float` | 0.0 – 30.0 | Mini-Mental State Examination score from administered assessment |

**Example request:**
```json
{
  "Age": 74.0,
  "M/F": "F",
  "Educ": 2.0,
  "MMSE": 22.0
}
```

**Response — 200 OK:**
```json
{
  "model_version": "oasis-practical-v1",
  "predicted_class": 1,
  "predicted_label": "CDR 0.5: Very mild dementia pattern",
  "class_probabilities": {
    "class_0": 0.2341,
    "class_1": 0.5812,
    "class_2": 0.1847
  },
  "disclaimer": "Educational machine-learning demonstration only. Not a medical diagnosis. MMSE must be a properly administered screening assessment."
}
```

**Response fields:**

| Field | Type | Description |
|-------|------|-------------|
| `model_version` | `string` | Artifact version (`"oasis-practical-v1"`) |
| `predicted_class` | `integer` | 0, 1, or 2 |
| `predicted_label` | `string` | Human-readable CDR category name |
| `class_probabilities` | `object` | `class_0`, `class_1`, `class_2` probability floats (sum ≈ 1.0) |
| `disclaimer` | `string` | Mandatory educational disclaimer |

**Error responses:**

| Status | Condition |
|--------|-----------|
| 422 | Invalid field value or missing field |
| 503 | ML pipeline not loaded at startup |

**Limitations:**
- Trained on OASIS-1 (n=436, 20% held-out test). Class 2 metrics carry high variance (only 6 test samples).
- Not validated for clinical use.

---

## 3. Patient Onboarding

### `POST /api/v1/profiles/onboard`
**Legacy:** `POST /profiles/onboard`

**Purpose:** Register a new patient. Creates an accessible initial Carl configuration. **Never requires MMSE.** Does not assign a support tier — `active_support_tier` is always `null` immediately after onboarding.

**Authentication:** Not required. _(Caregiver auth planned for future version.)_

**Required request fields:**

| Field | Type | Description |
|-------|------|-------------|
| `patient_id` | `string` | Unique anonymised patient identifier (non-empty) |

**Optional request fields:**

| Field | Type | Description |
|-------|------|-------------|
| `caregiver_preferences` | `object` | See CaregiverPreferences schema below |
| `onboarding_notes` | `string` | Free-text accessibility notes (e.g., `"Uses reading glasses"`) |

**CaregiverPreferences schema:**

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `preferred_therapy_areas` | `array[string]` | `["OrientationRoom","ReminiscenceCorner"]` | Therapy areas to prioritise |
| `max_session_duration_minutes` | `integer` | `15` | 5 – 60 minutes |
| `allow_adaptive_recommendations` | `boolean` | `true` | Whether the system may propose tier adjustments |

**Valid `therapy_area` values:** `"OrientationRoom"`, `"ReminiscenceCorner"`, `"CognitiveGarden"`

**Example request (no-MMSE pathway):**
```json
{
  "patient_id": "P_00123",
  "caregiver_preferences": {
    "preferred_therapy_areas": ["OrientationRoom", "CognitiveGarden"],
    "max_session_duration_minutes": 20,
    "allow_adaptive_recommendations": true
  },
  "onboarding_notes": "Patient uses hearing aid"
}
```

**Example request (MMSE-available pathway — identical API call):**
```json
{
  "patient_id": "P_00456"
}
```
> When an MMSE score is available, call `POST /api/v1/predict` first to get the CDR classification for caregiver review. Then call this endpoint. The predict result is **never** passed to onboarding; both pathways use the same onboarding call.

**Response — 201 Created:**
```json
{
  "status": "success",
  "profile": {
    "patient_id": "P_00123",
    "created_at": "2026-11-11T17:00:00Z",
    "active_support_tier": null,
    "carl_config": {
      "instruction_style": "concise",
      "allow_auto_hints": true,
      "hesitation_threshold_seconds": 25.0,
      "demonstrations_enabled": false,
      "speech_rate_multiplier": 1.0
    },
    "caregiver_preferences": {
      "preferred_therapy_areas": ["OrientationRoom", "CognitiveGarden"],
      "max_session_duration_minutes": 20,
      "allow_adaptive_recommendations": true
    },
    "onboarding_notes": "Patient uses hearing aid"
  }
}
```

> **Contract guarantee:** `active_support_tier` is **always** `null` in the response from this endpoint. Any non-null value is a bug.

**Error responses:**

| Status | Condition |
|--------|-----------|
| 422 | `patient_id` is empty or missing |

---

## 4. Support Profile Management

### `POST /api/v1/profiles/support`
**Legacy:** `POST /profiles/support`

**Purpose:** Create or update a support profile explicitly (e.g., after caregiver selects a tier). Preserves full revision history — previous profiles are deactivated, not deleted.

**Authentication:** Not required. _(Caregiver auth planned.)_

**Request body:** Full `InitialSupportProfile` object (see §3 for schema). `active_support_tier` may be `null` or one of: `"Standard"`, `"Guided"`, `"HighSupport"`.

**Example request:**
```json
{
  "patient_id": "P_00123",
  "active_support_tier": "Guided",
  "carl_config": {
    "instruction_style": "step_by_step",
    "allow_auto_hints": true,
    "hesitation_threshold_seconds": 20.0,
    "demonstrations_enabled": true,
    "speech_rate_multiplier": 0.9
  },
  "caregiver_preferences": {
    "preferred_therapy_areas": ["OrientationRoom"],
    "max_session_duration_minutes": 15,
    "allow_adaptive_recommendations": true
  }
}
```

**Response — 201 Created:** Same structure as §3 response.

**Error responses:**

| Status | Condition |
|--------|-----------|
| 422 | Invalid field value |
| 500 | Profile could not be retrieved after save (rare) |

---

### `GET /api/v1/profiles/support/{patient_id}`
**Legacy:** `GET /profiles/support/{patient_id}`

**Purpose:** Retrieve the currently active support profile and Carl companion configuration for a patient.

**Authentication:** Not required.

**Path parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `patient_id` | `string` | Anonymised patient identifier |

**Response — 200 OK:** Same structure as §3 response (with actual current tier).

**Error responses:**

| Status | Condition |
|--------|-----------|
| 404 | No profile found for the given `patient_id` |

---

## 5. Telemetry Event Ingestion

### `POST /api/v1/telemetry/events`
**Legacy:** `POST /telemetry/events`

**Purpose:** Ingest a batch of interaction events emitted by the Unity client during patient sessions. Idempotent — duplicate `event_id` values are silently skipped (safe to retry).

**Authentication:** Not required.

**Request body:** JSON array of `InteractionEvent` objects. Array must not be empty.

**InteractionEvent schema:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `event_id` | `string` | ✅ | Unique event ID (use UUIDv4) |
| `session_id` | `string` | ✅ | Active session identifier |
| `patient_id` | `string` | ✅ | Anonymised patient identifier |
| `timestamp_utc` | `string` (ISO-8601) | ✅ | UTC timestamp e.g. `"2026-11-11T17:00:00Z"` |
| `therapy_area` | `string` | ✅ | One of: `"OrientationRoom"`, `"ReminiscenceCorner"`, `"CognitiveGarden"` |
| `activity_id` | `string` | ✅ | Unique activity identifier within the therapy area |
| `event_type` | `string` | ✅ | See event type table below |
| `payload` | `object` | ⬜ optional | Contextual measurements (see payload schema) |
| `metadata` | `object` | ⬜ optional | Extensible key-value dictionary |

**Valid `event_type` values:**

| Value | Meaning |
|-------|---------|
| `"activity_started"` | Activity session began |
| `"instruction_requested"` | Patient requested initial instruction |
| `"instruction_repeated"` | Patient requested instruction replay |
| `"hint_requested"` | Patient requested a hint |
| `"attempt_submitted"` | Patient submitted an answer or action |
| `"assistance_triggered"` | Carl auto-triggered assistance |
| `"demonstration_requested"` | Patient requested visual demonstration |
| `"activity_paused"` | Activity paused |
| `"activity_resumed"` | Activity resumed after pause |
| `"activity_skipped"` | Patient chose to skip activity |
| `"activity_completed"` | Activity completed |

**InteractionPayload schema (all optional):**

| Field | Type | Description |
|-------|------|-------------|
| `attempt_index` | `integer` ≥ 1 | Sequential attempt count (1-indexed) |
| `is_correct` | `boolean` | Whether the attempt was correct |
| `latency_seconds` | `float` ≥ 0.0 | Response time preceding this event |
| `hint_tier_delivered` | `integer` 1–3 | 1=verbal cue, 2=highlight, 3=walkthrough |
| `assistance_source` | `string` | `"user_requested"`, `"carl_auto_prompted"`, or `"caregiver_initiated"` |
| `completion_status` | `string` | `"independent"`, `"with_gentle_hints"`, `"with_step_guidance"`, `"skipped_by_user"`, `"timed_out"` |
| `pause_duration_seconds` | `float` ≥ 0.0 | Pause duration if event is `activity_paused` |
| `substep_identifier` | `string` | Sub-task identifier |
| `notes` | `string` | Safe telemetry annotation |

**Example request:**
```json
[
  {
    "event_id": "550e8400-e29b-41d4-a716-446655440000",
    "session_id": "sess_20261111_P001",
    "patient_id": "P_00123",
    "timestamp_utc": "2026-11-11T09:15:00Z",
    "therapy_area": "OrientationRoom",
    "activity_id": "daily_clock_reading",
    "event_type": "hint_requested",
    "payload": {
      "latency_seconds": 12.4,
      "hint_tier_delivered": 1,
      "assistance_source": "user_requested"
    }
  }
]
```

**Response — 201 Created:**
```json
{
  "status": "success",
  "received_count": 1,
  "inserted_count": 1,
  "skipped_duplicates_count": 0
}
```

**Error responses:**

| Status | Condition |
|--------|-----------|
| 400 | Empty event array |
| 422 | Invalid field values or missing required fields |

**Limitations:**
- No audio, video, biometric data, or raw keystrokes are accepted.
- Maximum practical batch size not enforced by schema; recommend ≤ 500 events per call.

---

## 6. Interaction Profile (Aggregated Longitudinal Summary)

### `GET /api/v1/profiles/interaction/{patient_id}`
**Legacy:** `GET /profiles/interaction/{patient_id}`

**Purpose:** Retrieve an aggregated longitudinal summary of a patient's behavioral interactions across all therapy areas. Metrics are recomputed live from raw stored events on every call. No cognitive inference is made.

**Authentication:** Not required.

**Path parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `patient_id` | `string` | Anonymised patient identifier |

**Response — 200 OK:**
```json
{
  "status": "success",
  "profile": {
    "patient_id": "P_00123",
    "last_updated": "2026-11-11T17:30:00Z",
    "total_sessions_recorded": 3,
    "overall_completion_rate": 0.75,
    "area_summaries": {
      "OrientationRoom": {
        "activities_attempted": 6,
        "activities_completed": 5,
        "hints_requested_total": 4,
        "instructions_repeated_total": 2,
        "demonstrations_requested_total": 0,
        "skips_total": 1,
        "average_response_latency_seconds": 8.3
      },
      "ReminiscenceCorner": {
        "activities_attempted": 2,
        "activities_completed": 2,
        "hints_requested_total": 1,
        "instructions_repeated_total": 0,
        "demonstrations_requested_total": 0,
        "skips_total": 0,
        "average_response_latency_seconds": null
      },
      "CognitiveGarden": {
        "activities_attempted": 0,
        "activities_completed": 0,
        "hints_requested_total": 0,
        "instructions_repeated_total": 0,
        "demonstrations_requested_total": 0,
        "skips_total": 0,
        "average_response_latency_seconds": null
      }
    },
    "observed_assistance_frequency": {
      "user_requested": 7,
      "carl_auto_prompted": 0,
      "caregiver_initiated": 0
    },
    "recent_trend_note": "Recorded 12 events across 3 session(s). Completed 7/8 activities."
  }
}
```

**Error responses:**

| Status | Condition |
|--------|-----------|
| 404 | No events recorded yet for `patient_id`. Ingest events first. |

---

## 7. Support Adaptation Recommendations

### `GET /api/v1/profiles/recommendations/{patient_id}`
**Legacy:** `GET /profiles/recommendations/{patient_id}`

**Purpose:** Retrieve transparent rule-based support tier adaptation recommendations. If no pending recommendations exist, the system evaluates baseline heuristics against live telemetry.

> **This is NOT real-time AI.** All recommendations are explicitly flagged `is_temporary_baseline: true` and require caregiver review before any tier change is applied.

**Authentication:** Not required.

**Path parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `patient_id` | `string` | Anonymised patient identifier |

**Query parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `limit` | `integer` | 50 | Maximum recommendations to return (1–200) |

**Recommendation trigger rules (transparent):**

1. **Unassigned/Standard → Guided:** Patient explicitly requested ≥ 5 total hints/repeats/demos across ≥ 2 sessions.
2. **Guided → High Support:** Patient explicitly requested ≥ 4 demonstrations across ≥ 2 sessions.
3. **No recommendation:** Fewer sessions than threshold, insufficient evidence, or caregiver has locked the tier.

**Explicitly excluded from triggering recommendations:**
- Activity skipping alone
- Response latency or pauses
- Incorrect attempts

**Response — 200 OK:**
```json
{
  "status": "success",
  "patient_id": "P_00123",
  "count": 1,
  "recommendations": [
    {
      "recommendation_id": "rec_a1b2c3d4e5f6",
      "patient_id": "P_00123",
      "timestamp_utc": "2026-11-11T18:00:00Z",
      "current_tier": null,
      "recommended_tier": "Guided",
      "rationale": "Temporary baseline heuristic: Patient frequently requested assistance (3 hints, 2 repeats, 0 demos) across 2 sessions. Proposing Guided tier...",
      "triggering_area": "OrientationRoom",
      "is_temporary_baseline": true
    }
  ]
}
```

**When no recommendation exists:**
```json
{
  "status": "success",
  "patient_id": "P_00123",
  "count": 0,
  "recommendations": []
}
```

> **Contract guarantee:** `count: 0` with an empty array is the correct response for insufficient evidence. It is NOT an error.

---

## 8. Immediate Carl Assistance

### `POST /api/v1/carl/assistance`
**Legacy:** `POST /carl/assistance`

**Purpose:** Generate an immediate assistance action for Carl to deliver during an active Unity session, without waiting for ML model inference. This is a request-response action, not a persistent recommendation.

> **Distinction from §7:** This endpoint provides on-demand, in-session guidance. §7 provides longitudinal tier recommendations for caregiver review. These are strictly separate systems.

**Authentication:** Not required.

**Current implementation note:** The response is a transparent rule-based template engine. Context fields (`therapy_area`, `activity_id`, `current_step`) are accepted in the schema for forward compatibility but are not yet used to vary the response content. This is explicitly documented as a temporary baseline.

**Required request fields:**

| Field | Type | Description |
|-------|------|-------------|
| `patient_id` | `string` | Anonymised patient identifier |
| `session_id` | `string` | Active session identifier |
| `therapy_area` | `string` | Current therapy area (see §5 for valid values) |
| `activity_id` | `string` | Current activity identifier |
| `action_type` | `string` | Requested assistance type (see action type table) |

**Optional request fields:**

| Field | Type | Description |
|-------|------|-------------|
| `current_step` | `integer` | Current step number within the activity |
| `step_instruction` | `string` | Instruction text for the current step (used in `repeat_instruction` response) |

**Valid `action_type` values:**

| Value | Carl behaviour | Carl state |
|-------|---------------|------------|
| `"repeat_instruction"` | Repeats current step instruction | `"repeating"` |
| `"step_by_step_breakdown"` | Isolates one element, guides gently | `"guiding_step"` |
| `"visual_demonstration"` | Demonstrates the action (ghost animation) | `"demonstrating"` |
| `"show_hint"` | Pulses the recommended item | `"hinting"` |
| `"slow_pacing"` | Slows interaction pace, reassures | `"relaxed_pace"` |
| `"pause_or_skip"` | Offers to pause or move to another room | `"offering_break"` |
| _(any other value)_ | General reassurance | `"comforting"` |

**Example request:**
```json
{
  "patient_id": "P_00123",
  "session_id": "sess_20261111_P001",
  "therapy_area": "OrientationRoom",
  "activity_id": "daily_clock_reading",
  "action_type": "repeat_instruction",
  "current_step": 2,
  "step_instruction": "Look at the big clock hand and find the hour."
}
```

**Response — 200 OK:**
```json
{
  "status": "success",
  "action_type": "repeat_instruction",
  "spoken_prompt": "Here is the instruction again: Look at the big clock hand and find the hour.",
  "visual_cue": "highlight_current_target",
  "applied_pacing_multiplier": 0.9,
  "allow_skip": true,
  "carl_state": "repeating"
}
```

**Response fields:**

| Field | Type | Description |
|-------|------|-------------|
| `status` | `string` | Always `"success"` |
| `action_type` | `string` | Echoed action type |
| `spoken_prompt` | `string` | Text for Carl to speak (via Unity TTS) |
| `visual_cue` | `string\|null` | Unity UI directive (see visual cue table below) |
| `applied_pacing_multiplier` | `float` | Speed factor (< 1.0 = slower; 1.0 = normal) |
| `allow_skip` | `boolean` | Always `true` — patient may always skip |
| `carl_state` | `string` | Animator state identifier for Carl in Unity |

**Visual cue values and their intended Unity behaviour:**

| Value | Intended Unity action |
|-------|-----------------------|
| `"highlight_current_target"` | Outline or glow the target interactive object |
| `"isolate_single_element"` | Fade non-relevant elements, focus one element |
| `"play_ghost_animation"` | Play semi-transparent demonstration animation |
| `"pulse_recommended_item"` | Pulse or bob the recommended item |
| `"show_navigation_options"` | Display room navigation overlay |
| `"gentle_companion_wave"` | Carl idle-wave animation |
| `null` | No visual cue required |

> **Unity implementation note:** Visual cue values are symbolic strings. Unity must implement a dispatcher that maps these strings to actual scene behaviours. The backend does not push to Unity; Unity must call this endpoint and act on the response.

---

## 9. Caregiver Override and Lock

### `POST /api/v1/profiles/override`
**Legacy:** `POST /profiles/override`

**Purpose:** Authoritative caregiver action — explicitly sets or locks the application support tier. Caregiver decisions always supersede automated recommendations. Full audit history is preserved.

**Authentication:** Not required. _(Caregiver role authentication is a planned future feature.)_

**Required request fields:**

| Field | Type | Description |
|-------|------|-------------|
| `override_id` | `string` | Unique transaction ID (use UUIDv4) |
| `patient_id` | `string` | Anonymised patient identifier |
| `selected_tier` | `string` | `"Standard"`, `"Guided"`, or `"HighSupport"` |
| `lock_tier` | `boolean` | If `true`, automated recommendations are suppressed |

**Optional request fields:**

| Field | Type | Description |
|-------|------|-------------|
| `timestamp_utc` | `string` (ISO-8601) | Override timestamp; defaults to server UTC |
| `caregiver_note` | `string` | Reason for override (stored in audit log) |

**Example request — set Guided tier:**
```json
{
  "override_id": "9f86d081-884c-4591-b01b-e3c9d0453e1a",
  "patient_id": "P_00123",
  "selected_tier": "Guided",
  "lock_tier": false,
  "caregiver_note": "Patient struggled during Orientation Room activities this week."
}
```

**Example request — lock at Standard:**
```json
{
  "override_id": "7d793037-a076-49f6-b0d3-e08e3d0ab428",
  "patient_id": "P_00123",
  "selected_tier": "Standard",
  "lock_tier": true,
  "caregiver_note": "Patient and family prefer minimal intervention."
}
```

**Response — 200 OK:**
```json
{
  "status": "success",
  "message": "Caregiver successfully updated support tier to Guided.",
  "active_tier": "Guided",
  "lock_tier": false
}
```

**Response fields:**

| Field | Type | Description |
|-------|------|-------------|
| `status` | `string` | Always `"success"` |
| `message` | `string` | Human-readable confirmation |
| `active_tier` | `string` | The now-active support tier |
| `lock_tier` | `boolean` | Whether adaptive recommendations are now suppressed |

**Effect of `lock_tier: true`:**
- `GET /api/v1/profiles/recommendations/{patient_id}` will always return `count: 0`
- The lock persists until a new override is submitted with `lock_tier: false`

---

## 10. Support Tier Reference

| Tier value | Description |
|------------|-------------|
| `"Standard"` | Concise instructions, voluntary hints, standard pacing |
| `"Guided"` | Simplified prompts, target highlights, gentle reassurance |
| `"HighSupport"` | Micro-step scaffolding, visual demonstrations, no timers |

> **Important:** These tiers represent **application-level assistance preferences only**. They are **not** clinical staging of Alzheimer's disease or any cognitive severity rating. They must never be presented to patients or caregivers as diagnostic labels.

---

## 11. Known Limitations and Future Work

| Limitation | Impact | Planned resolution |
|------------|--------|--------------------|
| No authentication | All endpoints publicly accessible | Add token-based auth as `/api/v2/` feature |
| Carl assistance is static lookup | Context fields (`therapy_area`, `activity_id`, `current_step`) are ignored in response selection | Phase 2B: replace with context-aware ML model |
| No push/WebSocket mechanism | Unity must poll `POST /api/v1/carl/assistance`; backend cannot proactively push to Unity | Planned WebSocket channel in future phase |
| SQLite single-file DB | Not suitable for multi-process / multi-server deployment | Planned migration to PostgreSQL for production |
| No request rate limiting | Unlimited telemetry ingestion | To be added before production deployment |
| Phase 1 model: small test set | Class 2 CDR metrics carry high variance (6 test samples) | Retrain with larger validated dataset |
| No CHANGELOG | Breaking changes not formally tracked | `CHANGELOG.md` to be maintained from this contract version forward |

---

## 12. Endpoint Summary Table

| # | Method | Versioned URL | Legacy URL | Status code | Auth |
|---|--------|---------------|------------|-------------|------|
| 1 | GET | `/api/v1/health` | `/health` | 200 | None |
| 2 | POST | `/api/v1/predict` | `/predict` | 200 | None |
| 3 | POST | `/api/v1/profiles/onboard` | `/profiles/onboard` | 201 | None |
| 4 | POST | `/api/v1/profiles/support` | `/profiles/support` | 201 | None |
| 5 | GET | `/api/v1/profiles/support/{patient_id}` | `/profiles/support/{patient_id}` | 200 | None |
| 6 | POST | `/api/v1/telemetry/events` | `/telemetry/events` | 201 | None |
| 7 | GET | `/api/v1/profiles/interaction/{patient_id}` | `/profiles/interaction/{patient_id}` | 200 | None |
| 8 | GET | `/api/v1/profiles/recommendations/{patient_id}` | `/profiles/recommendations/{patient_id}` | 200 | None |
| 9 | POST | `/api/v1/carl/assistance` | `/carl/assistance` | 200 | None |
| 10 | POST | `/api/v1/profiles/override` | `/profiles/override` | 200 | None |

---

*Contract frozen: 2026-11-11 — P2A-30. Next revision requires a new major version (`/api/v2/`).*
