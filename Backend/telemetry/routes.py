"""
NeuroCompanion - Telemetry and Support Profile FastAPI Routes.
Exposes endpoints for Unity telemetry ingestion, profile retrieval/onboarding,
recommendation browsing, and authoritative caregiver governance.

Decoupled from Phase 1 OASIS clinical prediction endpoints.
"""

from typing import List, Optional
from fastapi import APIRouter, HTTPException, Query, status
from pydantic import BaseModel, Field

from telemetry.database import db
from telemetry.aggregation import compute_interaction_profile_from_events
from telemetry.baseline_adaptation import (
    AssistanceActionRequest,
    AssistanceActionResponse,
    generate_immediate_assistance,
    evaluate_baseline_recommendation,
)
from telemetry.schemas import (
    InitialSupportProfile,
    InteractionEvent,
    InteractionProfile,
    SupportRecommendation,
    CaregiverOverride,
    SupportTier,
    CarlConfiguration,
    CaregiverPreferences,
)

router = APIRouter(tags=["Unity Telemetry & Adaptive Intelligence"])


# ---------------------------------------------------------------------
# Response Models
# ---------------------------------------------------------------------

class TelemetryIngestResponse(BaseModel):
    status: str = Field("success", description="Status of the ingestion operation")
    received_count: int = Field(..., description="Total events received in request")
    inserted_count: int = Field(..., description="Count of successfully saved new events")
    skipped_duplicates_count: int = Field(..., description="Count of duplicate events skipped safely")


class PatientRegistrationRequest(BaseModel):
    """
    Onboarding request for a new patient in NeuroCompanion.
    - Neither MMSE nor support tier selection is required.
    - Automatically creates an accessible, supportive initial configuration.
    """
    patient_id: str = Field(..., min_length=1, description="Unique anonymized patient identifier")
    caregiver_preferences: Optional[CaregiverPreferences] = Field(
        default=None, description="Optional caregiver preferences"
    )
    onboarding_notes: Optional[str] = Field(
        default=None, description="Optional notes (e.g. preferred topics, glasses)"
    )


class SupportProfileResponse(BaseModel):
    status: str = "success"
    profile: InitialSupportProfile


class InteractionProfileResponse(BaseModel):
    status: str = "success"
    profile: InteractionProfile


class RecommendationsListResponse(BaseModel):
    status: str = "success"
    patient_id: str
    count: int
    recommendations: List[SupportRecommendation]


class CaregiverOverrideResponse(BaseModel):
    status: str = "success"
    message: str
    active_tier: SupportTier
    lock_tier: bool


# ---------------------------------------------------------------------
# 1. Telemetry Ingestion Endpoint
# ---------------------------------------------------------------------

@router.post(
    "/telemetry/events",
    response_model=TelemetryIngestResponse,
    status_code=status.HTTP_201_CREATED,
    summary="Ingest batch of Unity interaction events"
)
async def ingest_telemetry_events(events: List[InteractionEvent]):
    """
    Ingest a batch of interaction events emitted by the NeuroCompanion Unity client.
    - Validates every event strictly against InteractionEvent schema.
    - Idempotent: duplicate event_id entries are safely skipped without error.
    - Zero personal data, video, audio, or raw keystrokes accepted.
    """
    if not events:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Event list cannot be empty."
        )

    inserted, skipped = db.insert_events(events)

    return TelemetryIngestResponse(
        received_count=len(events),
        inserted_count=inserted,
        skipped_duplicates_count=skipped
    )


# ---------------------------------------------------------------------
# 2. Support Profile Endpoints (Missing-MMSE Pathway & Automatic Onboarding)
# ---------------------------------------------------------------------

@router.post(
    "/profiles/onboard",
    response_model=SupportProfileResponse,
    status_code=status.HTTP_201_CREATED,
    summary="Automatically create an accessible initial support configuration"
)
async def onboard_patient(request: PatientRegistrationRequest):
    """
    Onboard any new patient into NeuroCompanion automatically:
    - Never requires MMSE score.
    - Does NOT assign Standard, Guided, or High Support tier (active_support_tier is None).
    - Initializes Carl with accessible defaults (concise instructions, auto-hints on hesitation).
    - Accommodates optional caregiver preferences.
    """
    profile = InitialSupportProfile(
        patient_id=request.patient_id,
        active_support_tier=None,
        carl_config=CarlConfiguration(
            instruction_style="concise",
            allow_auto_hints=True,
            hesitation_threshold_seconds=25.0,
            demonstrations_enabled=False,
            speech_rate_multiplier=1.0,
        ),
        caregiver_preferences=request.caregiver_preferences or CaregiverPreferences(),
        onboarding_notes=request.onboarding_notes,
    )
    db.save_support_profile(profile)
    active = db.get_support_profile(request.patient_id)
    return SupportProfileResponse(profile=active or profile)


@router.post(
    "/profiles/support",
    response_model=SupportProfileResponse,
    status_code=status.HTTP_201_CREATED,
    summary="Create or update initial patient support profile"
)
async def create_support_profile(profile: InitialSupportProfile):
    """
    Establish or update an active support profile for a patient.
    - MMSE is explicitly NOT required.
    - Preserves historical revisions in persistent storage.
    """
    db.save_support_profile(profile)
    active_profile = db.get_support_profile(profile.patient_id)
    if not active_profile:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to retrieve saved support profile."
        )

    return SupportProfileResponse(profile=active_profile)


@router.get(
    "/profiles/support/{patient_id}",
    response_model=SupportProfileResponse,
    summary="Retrieve active support profile for a patient"
)
async def get_support_profile(patient_id: str):
    """
    Retrieve the current active support profile and Carl companion configuration.
    """
    profile = db.get_support_profile(patient_id)
    if not profile:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"No active support profile found for patient ID: '{patient_id}'."
        )

    return SupportProfileResponse(profile=profile)


# ---------------------------------------------------------------------
# 3. Aggregated Interaction Profile Endpoint
# ---------------------------------------------------------------------

@router.get(
    "/profiles/interaction/{patient_id}",
    response_model=InteractionProfileResponse,
    summary="Retrieve longitudinal interaction summary for a patient"
)
async def get_interaction_profile(patient_id: str):
    """
    Retrieve longitudinal interaction trends across the three therapy rooms.
    Metrics are always recomputed live from raw stored events so they reflect the
    latest telemetry without requiring a manual save step.

    If no events have been ingested yet for this patient, returns 404.
    Strictly decoupled from clinical diagnostic data; no cognitive inference is made.
    """
    # Always derive metrics directly from raw events (live aggregation)
    events = db.get_events_for_patient(patient_id, limit=5000)
    if not events:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"No interaction events recorded yet for patient ID: '{patient_id}'. "
                   "Ingest events via POST /telemetry/events first."
        )

    profile = compute_interaction_profile_from_events(patient_id, events)

    # Persist computed profile so the interaction_profiles table stays current
    db.save_interaction_profile(profile)

    return InteractionProfileResponse(profile=profile)


# ---------------------------------------------------------------------
# 4. Support Adaptation Recommendations Endpoint
# ---------------------------------------------------------------------

@router.get(
    "/profiles/recommendations/{patient_id}",
    response_model=RecommendationsListResponse,
    summary="Retrieve support adaptation recommendations for a patient"
)
async def get_support_recommendations(
    patient_id: str,
    limit: int = Query(50, ge=1, le=200, description="Max recommendations to retrieve")
):
    """
    Retrieve adaptation recommendations proposed by the intelligence system.
    Evaluates transparent baseline heuristics against live telemetry if no pending
    recommendations exist yet and the tier is not locked by caregiver.
    """
    recs = db.get_recommendations_for_patient(patient_id, limit=limit)

    # If no pending recommendations exist, check if baseline rules recommend one
    if not recs:
        active_prof = db.get_support_profile(patient_id)
        if active_prof:
            events = db.get_events_for_patient(patient_id, limit=5000)
            if events:
                inter_prof = compute_interaction_profile_from_events(patient_id, events)
                new_rec = evaluate_baseline_recommendation(active_prof, inter_prof)
                if new_rec:
                    db.save_recommendation(new_rec)
                    recs = [new_rec]

    return RecommendationsListResponse(
        patient_id=patient_id,
        count=len(recs),
        recommendations=recs
    )


# ---------------------------------------------------------------------
# 5. Immediate Assistance Action Endpoint (Carl Companion)
# ---------------------------------------------------------------------

@router.post(
    "/carl/assistance",
    response_model=AssistanceActionResponse,
    status_code=status.HTTP_200_OK,
    summary="Generate immediate assistance action for Carl"
)
async def carl_immediate_assistance(request: AssistanceActionRequest):
    """
    Provides on-demand, non-delay assistance actions for Carl companion during Unity sessions:
    - Repeating instructions
    - Step-by-step simplification
    - Visual demonstrations
    - Visible hints
    - Pacing adjustments
    - Permissive skip/pause handling
    """
    return generate_immediate_assistance(request)


# ---------------------------------------------------------------------
# 6. Caregiver Governance & Override Endpoint
# ---------------------------------------------------------------------

@router.post(
    "/profiles/override",
    response_model=CaregiverOverrideResponse,
    status_code=status.HTTP_200_OK,
    summary="Authoritative caregiver override of active support tier"
)
async def apply_caregiver_override(override: CaregiverOverride):
    """
    Caregiver explicitly sets, approves, or locks the active application support tier.
    - Caregiver decisions immediately update the active support profile.
    - If lock_tier=True, automated adaptation recommendations are silenced.
    - Complete audit history of overrides is preserved.
    """
    db.save_caregiver_override(override)
    active_profile = db.get_support_profile(override.patient_id)

    return CaregiverOverrideResponse(
        message=f"Caregiver successfully updated support tier to {override.selected_tier.value}.",
        active_tier=active_profile.active_support_tier if active_profile else override.selected_tier,
        lock_tier=override.lock_tier
    )
