"""
NeuroCompanion - Extended Interaction Model FastAPI Routes (Phase 3→4).

Provides versioned /api/v2/ REST endpoints for:
1. Patient Preferences (likes, dislikes, avoid requests, modalities)
2. Memory Support Presentations (content presented, repetition tracking)
3. Observation Opportunities (linking to presentations, response status, latency)
4. Assistance Episodes (trigger, offer, delivery, observable subsequent outcomes)
5. Dynamic UI Configuration History (active snapshot, timeline, change sources)
6. Event Interpretations (decoupled CD01-CD09 and AS01-AS10 analytical labels)
7. Consent Governance (caregiver consent tracking and active validity checks)

Decoupled from Phase 2A routes (/api/v1/). Preserves complete backward compatibility.
"""

from datetime import datetime, timezone
from typing import Any, Dict, List, Optional
import uuid

from fastapi import APIRouter, HTTPException, Query, status
from pydantic import BaseModel, Field

from telemetry.schemas import TherapyArea
from telemetry.extended_schemas import (
    PreferenceCategory,
    PreferenceSource,
    PatientPreference,
    PresentationMethod,
    MemorySupportPresentation,
    OpportunityType,
    ResponseStatus,
    ObservationOpportunity,
    AssistanceType,
    AssistanceInitiation,
    SubsequentOutcome,
    AssistanceEpisode,
    ConfigChangeSource,
    UIConfigurationHistory,
    EventInterpretation,
    ConsentType,
    ConsentRecord,
)
from telemetry.extended_database import extended_db

extended_router = APIRouter(prefix="/api/v2", tags=["Extended Interaction Model (Phase 3/4)"])


def _utc_now() -> datetime:
    return datetime.now(timezone.utc)


# =====================================================================
# 1. Patient Preferences
# =====================================================================

class PreferenceCreateRequest(BaseModel):
    preference_category: PreferenceCategory = Field(..., description="Category (ACTIVITY, MUSIC, CONTENT, etc.)")
    preference_key: str = Field(..., min_length=1, description="Key, e.g. 'favorite_genre'")
    preference_value: str = Field(..., min_length=1, description="Value, e.g. 'Classical - 1950s'")
    source: PreferenceSource = Field(default=PreferenceSource.PATIENT_EXPLICIT)
    is_current: bool = Field(default=True)
    notes: Optional[str] = None


class PreferencesResponse(BaseModel):
    status: str = "success"
    patient_id: str
    count: int
    preferences: List[PatientPreference]


@extended_router.post(
    "/preferences/{patient_id}",
    response_model=PatientPreference,
    status_code=status.HTTP_201_CREATED,
    summary="Record or update a patient preference"
)
async def create_patient_preference(patient_id: str, request: PreferenceCreateRequest):
    pref = PatientPreference(
        preference_id=str(uuid.uuid4()),
        patient_id=patient_id,
        preference_category=request.preference_category,
        preference_key=request.preference_key,
        preference_value=request.preference_value,
        source=request.source,
        is_current=request.is_current,
        recorded_at=_utc_now(),
        notes=request.notes,
    )
    extended_db.save_preference(pref)
    return pref


@extended_router.get(
    "/preferences/{patient_id}",
    response_model=PreferencesResponse,
    summary="Retrieve preferences for a patient"
)
async def get_patient_preferences(
    patient_id: str,
    current_only: bool = Query(True, description="Return only active (current) preferences")
):
    prefs = extended_db.get_preferences_for_patient(patient_id, current_only=current_only)
    return PreferencesResponse(
        patient_id=patient_id,
        count=len(prefs),
        preferences=prefs,
    )


# =====================================================================
# 2. Memory Support Presentations
# =====================================================================

class PresentationCreateRequest(BaseModel):
    patient_id: str = Field(..., min_length=1)
    session_id: str = Field(..., min_length=1)
    room: TherapyArea
    activity_id: Optional[str] = None
    content_id: str = Field(..., min_length=1)
    content_version: str = Field(default="1.0")
    presentation_method: PresentationMethod
    repetition_count: int = Field(default=0, ge=0)
    triggering_event_id: Optional[str] = None


class PresentationsResponse(BaseModel):
    status: str = "success"
    count: int
    presentations: List[MemorySupportPresentation]


@extended_router.post(
    "/presentations",
    response_model=MemorySupportPresentation,
    status_code=status.HTTP_201_CREATED,
    summary="Record a memory support presentation instance"
)
async def create_presentation(request: PresentationCreateRequest):
    pres = MemorySupportPresentation(
        information_id=str(uuid.uuid4()),
        patient_id=request.patient_id,
        session_id=request.session_id,
        room=request.room,
        activity_id=request.activity_id,
        content_id=request.content_id,
        content_version=request.content_version,
        presentation_method=request.presentation_method,
        presented_at=_utc_now(),
        repetition_count=request.repetition_count,
        triggering_event_id=request.triggering_event_id,
    )
    extended_db.save_presentation(pres)
    return pres


@extended_router.get(
    "/presentations/{patient_id}",
    response_model=PresentationsResponse,
    summary="Retrieve presentations for a patient"
)
async def get_presentations(patient_id: str):
    pres_list = extended_db.get_presentations_for_patient(patient_id)
    return PresentationsResponse(count=len(pres_list), presentations=pres_list)


@extended_router.get(
    "/presentations/{patient_id}/session/{session_id}",
    response_model=PresentationsResponse,
    summary="Retrieve presentations in a specific session"
)
async def get_presentations_by_session(patient_id: str, session_id: str):
    pres_list = extended_db.get_presentations_for_session(session_id)
    # Filter by patient_id for verification safety
    matching = [p for p in pres_list if p.patient_id == patient_id]
    return PresentationsResponse(count=len(matching), presentations=matching)


# =====================================================================
# 3. Observation Opportunities
# =====================================================================

class OpportunityCreateRequest(BaseModel):
    patient_id: str = Field(..., min_length=1)
    session_id: str = Field(..., min_length=1)
    room: TherapyArea
    activity_id: Optional[str] = None
    linked_presentation_id: Optional[str] = None
    opportunity_type: OpportunityType
    response_status: ResponseStatus = Field(default=ResponseStatus.UNKNOWN)
    response_latency_seconds: Optional[float] = Field(None, ge=0.0)
    assistance_requested: bool = Field(default=False)
    assistance_received: bool = Field(default=False)
    related_event_id: Optional[str] = None


class OpportunityResponseUpdateRequest(BaseModel):
    response_status: ResponseStatus
    response_latency_seconds: Optional[float] = Field(None, ge=0.0)
    related_event_id: Optional[str] = None
    assistance_requested: Optional[bool] = None
    assistance_received: Optional[bool] = None


class OpportunitiesResponse(BaseModel):
    status: str = "success"
    session_id: str
    count: int
    opportunities: List[ObservationOpportunity]


@extended_router.post(
    "/opportunities",
    response_model=ObservationOpportunity,
    status_code=status.HTTP_201_CREATED,
    summary="Record an observation opportunity"
)
async def create_opportunity(request: OpportunityCreateRequest):
    opp = ObservationOpportunity(
        opportunity_id=str(uuid.uuid4()),
        linked_presentation_id=request.linked_presentation_id,
        patient_id=request.patient_id,
        session_id=request.session_id,
        room=request.room,
        activity_id=request.activity_id,
        opportunity_type=request.opportunity_type,
        presented_at=_utc_now(),
        response_status=request.response_status,
        response_latency_seconds=request.response_latency_seconds,
        assistance_requested=request.assistance_requested,
        assistance_received=request.assistance_received,
        related_event_id=request.related_event_id,
    )
    extended_db.save_observation_opportunity(opp)
    return opp


@extended_router.get(
    "/opportunities/{patient_id}/session/{session_id}",
    response_model=OpportunitiesResponse,
    summary="Retrieve observation opportunities in a session"
)
async def get_opportunities(patient_id: str, session_id: str):
    opps = extended_db.get_opportunities_for_session(session_id)
    matching = [o for o in opps if o.patient_id == patient_id]
    return OpportunitiesResponse(
        session_id=session_id,
        count=len(matching),
        opportunities=matching,
    )


@extended_router.patch(
    "/opportunities/{opportunity_id}/response",
    status_code=status.HTTP_200_OK,
    summary="Update response status and latency on an observation opportunity"
)
async def update_opportunity_response(
    opportunity_id: str,
    update: OpportunityResponseUpdateRequest
):
    extended_db.update_opportunity_response(
        opportunity_id=opportunity_id,
        response_status=update.response_status,
        response_latency_seconds=update.response_latency_seconds,
        related_event_id=update.related_event_id,
        assistance_requested=update.assistance_requested,
        assistance_received=update.assistance_received,
    )
    return {"status": "success", "opportunity_id": opportunity_id, "updated": True}


# =====================================================================
# 4. Assistance Episodes
# =====================================================================

class AssistanceEpisodeCreateRequest(BaseModel):
    patient_id: str = Field(..., min_length=1)
    session_id: str = Field(..., min_length=1)
    room: TherapyArea
    activity_id: Optional[str] = None
    activity_step_id: Optional[str] = None
    triggering_event_id: Optional[str] = None
    assistance_type: AssistanceType
    initiation: AssistanceInitiation
    accepted: Optional[bool] = None
    delivered: bool = Field(default=False)
    delivered_at: Optional[datetime] = None
    subsequent_outcome: SubsequentOutcome = Field(default=SubsequentOutcome.UNKNOWN)
    outcome_event_id: Optional[str] = None
    notes: Optional[str] = None


class AssistanceEpisodeOutcomeUpdateRequest(BaseModel):
    delivered: bool = True
    delivered_at: Optional[datetime] = None
    subsequent_outcome: SubsequentOutcome
    outcome_event_id: Optional[str] = None
    accepted: Optional[bool] = None
    notes: Optional[str] = None


class AssistanceEpisodesResponse(BaseModel):
    status: str = "success"
    session_id: str
    count: int
    episodes: List[AssistanceEpisode]


@extended_router.post(
    "/assistance/episodes",
    response_model=AssistanceEpisode,
    status_code=status.HTTP_201_CREATED,
    summary="Record an assistance episode"
)
async def create_assistance_episode(request: AssistanceEpisodeCreateRequest):
    ep = AssistanceEpisode(
        episode_id=str(uuid.uuid4()),
        patient_id=request.patient_id,
        session_id=request.session_id,
        room=request.room,
        activity_id=request.activity_id,
        activity_step_id=request.activity_step_id,
        triggering_event_id=request.triggering_event_id,
        assistance_type=request.assistance_type,
        initiation=request.initiation,
        offered_at=_utc_now(),
        accepted=request.accepted,
        delivered=request.delivered,
        delivered_at=request.delivered_at,
        subsequent_outcome=request.subsequent_outcome,
        outcome_event_id=request.outcome_event_id,
        notes=request.notes,
    )
    extended_db.save_assistance_episode(ep)
    return ep


@extended_router.get(
    "/assistance/episodes/{patient_id}/session/{session_id}",
    response_model=AssistanceEpisodesResponse,
    summary="Retrieve assistance episodes in a session"
)
async def get_assistance_episodes(patient_id: str, session_id: str):
    episodes = extended_db.get_episodes_for_session(session_id)
    matching = [e for e in episodes if e.patient_id == patient_id]
    return AssistanceEpisodesResponse(
        session_id=session_id,
        count=len(matching),
        episodes=matching,
    )


@extended_router.patch(
    "/assistance/episodes/{episode_id}/outcome",
    status_code=status.HTTP_200_OK,
    summary="Update delivery status and subsequent outcome on an assistance episode"
)
async def update_assistance_episode_outcome(
    episode_id: str,
    update: AssistanceEpisodeOutcomeUpdateRequest
):
    deliv_time = update.delivered_at or (_utc_now() if update.delivered else None)
    extended_db.update_episode_outcome(
        episode_id=episode_id,
        delivered=update.delivered,
        delivered_at=deliv_time,
        subsequent_outcome=update.subsequent_outcome,
        outcome_event_id=update.outcome_event_id,
        accepted=update.accepted,
        notes=update.notes,
    )
    return {"status": "success", "episode_id": episode_id, "updated": True}


# =====================================================================
# 5. Dynamic UI Configuration History
# =====================================================================

class UIConfigCreateRequest(BaseModel):
    text_size: str = Field(default="medium")
    text_contrast: str = Field(default="standard")
    max_visible_choices: int = Field(default=4, ge=1, le=10)
    subtitles_enabled: bool = Field(default=True)
    speech_enabled: bool = Field(default=True)
    speech_speed: float = Field(default=1.0, ge=0.5, le=2.0)
    instruction_format: str = Field(default="concise")
    visual_hints_available: bool = Field(default=True)
    demonstrations_available: bool = Field(default=False)
    step_by_step_guidance: bool = Field(default=False)
    activity_complexity: str = Field(default="standard")
    navigation_assistance: bool = Field(default=True)
    change_source: ConfigChangeSource = Field(default=ConfigChangeSource.CAREGIVER)
    change_reason: Optional[str] = None
    related_recommendation_id: Optional[str] = None
    related_assistance_episode_id: Optional[str] = None


class UIConfigHistoryResponse(BaseModel):
    status: str = "success"
    patient_id: str
    count: int
    history: List[UIConfigurationHistory]


@extended_router.post(
    "/ui/config/{patient_id}",
    response_model=UIConfigurationHistory,
    status_code=status.HTTP_201_CREATED,
    summary="Apply new UI configuration snapshot (closes active previous config)"
)
async def create_ui_configuration(patient_id: str, request: UIConfigCreateRequest):
    config = UIConfigurationHistory(
        config_id=str(uuid.uuid4()),
        patient_id=patient_id,
        effective_from=_utc_now(),
        text_size=request.text_size,
        text_contrast=request.text_contrast,
        max_visible_choices=request.max_visible_choices,
        subtitles_enabled=request.subtitles_enabled,
        speech_enabled=request.speech_enabled,
        speech_speed=request.speech_speed,
        instruction_format=request.instruction_format,
        visual_hints_available=request.visual_hints_available,
        demonstrations_available=request.demonstrations_available,
        step_by_step_guidance=request.step_by_step_guidance,
        activity_complexity=request.activity_complexity,
        navigation_assistance=request.navigation_assistance,
        change_source=request.change_source,
        change_reason=request.change_reason,
        related_recommendation_id=request.related_recommendation_id,
        related_assistance_episode_id=request.related_assistance_episode_id,
    )
    extended_db.save_ui_config(config)
    return config


@extended_router.get(
    "/ui/config/{patient_id}/active",
    response_model=Optional[UIConfigurationHistory],
    summary="Retrieve current effective UI configuration for a patient"
)
async def get_active_ui_configuration(patient_id: str):
    active = extended_db.get_active_ui_config(patient_id)
    if not active:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"No active UI configuration found for patient '{patient_id}'."
        )
    return active


@extended_router.get(
    "/ui/config/{patient_id}/history",
    response_model=UIConfigHistoryResponse,
    summary="Retrieve complete UI configuration change timeline"
)
async def get_ui_configuration_history(patient_id: str):
    hist = extended_db.get_ui_config_history(patient_id)
    return UIConfigHistoryResponse(
        patient_id=patient_id,
        count=len(hist),
        history=hist,
    )


# =====================================================================
# 6. Event Interpretations (Decoupled Analytical Tags)
# =====================================================================

class EventInterpretationCreateRequest(BaseModel):
    event_id: str = Field(..., min_length=1)
    patient_id: str = Field(..., min_length=1)
    cognitive_domains: List[str] = Field(default_factory=list, description="CD01-CD09 tags")
    assistance_labels: List[str] = Field(default_factory=list, description="AS01-AS10 labels")
    analyst_note: Optional[str] = None
    model_version: Optional[str] = None


class EventInterpretationsResponse(BaseModel):
    status: str = "success"
    event_id: str
    count: int
    interpretations: List[EventInterpretation]


@extended_router.post(
    "/interpretations",
    response_model=EventInterpretation,
    status_code=status.HTTP_201_CREATED,
    summary="Record an analytical interpretation for an interaction event"
)
async def create_event_interpretation(request: EventInterpretationCreateRequest):
    interp = EventInterpretation(
        interpretation_id=str(uuid.uuid4()),
        event_id=request.event_id,
        patient_id=request.patient_id,
        cognitive_domains=request.cognitive_domains,
        assistance_labels=request.assistance_labels,
        analyst_note=request.analyst_note,
        generated_at=_utc_now(),
        model_version=request.model_version,
    )
    extended_db.save_event_interpretation(interp)
    return interp


@extended_router.get(
    "/interpretations/event/{event_id}",
    response_model=EventInterpretationsResponse,
    summary="Retrieve interpretations for a specific event"
)
async def get_event_interpretations(event_id: str):
    interps = extended_db.get_interpretations_for_event(event_id)
    return EventInterpretationsResponse(
        event_id=event_id,
        count=len(interps),
        interpretations=interps,
    )


# =====================================================================
# 7. Consent Governance
# =====================================================================

class ConsentCreateRequest(BaseModel):
    consent_type: ConsentType
    granted: bool = Field(default=True)
    granted_by: str = Field(..., min_length=1)
    expires_at: Optional[datetime] = None
    notes: Optional[str] = None


class ConsentRecordsResponse(BaseModel):
    status: str = "success"
    patient_id: str
    count: int
    records: List[ConsentRecord]


class ConsentCheckResponse(BaseModel):
    patient_id: str
    consent_type: ConsentType
    has_valid_consent: bool


@extended_router.post(
    "/consent/{patient_id}",
    response_model=ConsentRecord,
    status_code=status.HTTP_201_CREATED,
    summary="Record a caregiver consent grant or revocation"
)
async def record_consent(patient_id: str, request: ConsentCreateRequest):
    record = ConsentRecord(
        consent_id=str(uuid.uuid4()),
        patient_id=patient_id,
        consent_type=request.consent_type,
        granted=request.granted,
        granted_by=request.granted_by,
        granted_at=_utc_now(),
        expires_at=request.expires_at,
        notes=request.notes,
    )
    extended_db.save_consent_record(record)
    return record


@extended_router.get(
    "/consent/{patient_id}",
    response_model=ConsentRecordsResponse,
    summary="Retrieve all consent records for a patient"
)
async def get_consent_records(patient_id: str):
    records = extended_db.get_consent_records(patient_id)
    return ConsentRecordsResponse(
        patient_id=patient_id,
        count=len(records),
        records=records,
    )


@extended_router.get(
    "/consent/{patient_id}/{consent_type}/check",
    response_model=ConsentCheckResponse,
    summary="Check if active non-expired consent exists for a patient"
)
async def check_consent(patient_id: str, consent_type: ConsentType):
    valid = extended_db.check_consent(patient_id, consent_type)
    return ConsentCheckResponse(
        patient_id=patient_id,
        consent_type=consent_type,
        has_valid_consent=valid,
    )
