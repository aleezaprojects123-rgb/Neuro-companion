"""
NeuroCompanion - Extended Interaction Telemetry Schemas (Phase 3→4)
New Pydantic models for patient preferences, memory support presentations,
observation opportunities, assistance episodes, UI configuration history,
event interpretations, consent records, and an extended interaction event.

IMPORTANT: Do NOT modify schemas.py. This file imports from it and extends it.
"""

from datetime import datetime, timezone
from enum import Enum
from typing import Any, Dict, List, Optional
from pydantic import BaseModel, Field

from telemetry.schemas import TherapyArea, InteractionEvent


def _utc_now() -> datetime:
    return datetime.now(timezone.utc)


# =====================================================================
# A. Patient Preferences
# =====================================================================

class PreferenceCategory(str, Enum):
    """Broad category of a patient preference."""
    ACTIVITY = "ACTIVITY"
    MUSIC = "MUSIC"
    CONTENT = "CONTENT"
    COMMUNICATION = "COMMUNICATION"
    ACCESSIBILITY = "ACCESSIBILITY"
    ASSISTANCE_TYPE = "ASSISTANCE_TYPE"
    AVOID_CONTENT = "AVOID_CONTENT"


class PreferenceSource(str, Enum):
    """Who/what established this preference."""
    PATIENT_EXPLICIT = "PATIENT_EXPLICIT"
    CAREGIVER_PROVIDED = "CAREGIVER_PROVIDED"
    AI_INFERRED = "AI_INFERRED"


class PatientPreference(BaseModel):
    """
    A single patient preference entry. Multiple preferences per patient are supported;
    is_current=False records superseded historical values.
    """
    preference_id: str = Field(..., min_length=1, description="UUID primary key")
    patient_id: str = Field(..., min_length=1, description="Anonymized patient identifier")
    preference_category: PreferenceCategory = Field(..., description="Broad category of the preference")
    preference_key: str = Field(..., min_length=1, description="Specific preference key, e.g. 'music_genre'")
    preference_value: str = Field(..., min_length=1, description="Preference value, e.g. 'jazz'")
    source: PreferenceSource = Field(..., description="Who/what established this preference")
    is_current: bool = Field(default=True, description="Whether this is the active preference value")
    recorded_at: datetime = Field(default_factory=_utc_now, description="When the preference was recorded")
    superseded_at: Optional[datetime] = Field(None, description="When this preference was superseded")
    notes: Optional[str] = Field(None, description="Optional free-text notes")


# =====================================================================
# B. Memory Support Presentations
# =====================================================================

class PresentationMethod(str, Enum):
    """How memory-support content was presented to the patient."""
    VISUAL = "VISUAL"
    SPOKEN = "SPOKEN"
    DEMONSTRATION = "DEMONSTRATION"
    COMBINED = "COMBINED"


class MemorySupportPresentation(BaseModel):
    """
    Records each instance of memory-support content being presented to a patient
    during a therapy session. Tracks repetition and links to triggering events.
    """
    information_id: str = Field(..., min_length=1, description="UUID primary key")
    patient_id: str = Field(..., min_length=1, description="Anonymized patient identifier")
    session_id: str = Field(..., min_length=1, description="Active session identifier")
    room: TherapyArea = Field(..., description="Therapy area in which content was presented")
    activity_id: Optional[str] = Field(None, description="Activity within the room, if applicable")
    content_id: str = Field(..., min_length=1, description="Identifier of the memory-support content item")
    content_version: str = Field(default="1.0", description="Version string for the content")
    presentation_method: PresentationMethod = Field(..., description="Modality used to present the content")
    presented_at: datetime = Field(..., description="UTC timestamp when content was presented")
    repetition_count: int = Field(default=0, ge=0, description="Number of times this content has been repeated")
    triggering_event_id: Optional[str] = Field(
        None,
        description="FK-ish reference to interaction_events.event_id that triggered this presentation"
    )


# =====================================================================
# C. Observation Opportunities
# =====================================================================

class OpportunityType(str, Enum):
    """The type of cognitive observation opportunity presented to the patient."""
    RECALL = "RECALL"
    RECOGNITION = "RECOGNITION"
    TASK_CONTINUATION = "TASK_CONTINUATION"
    FAMILIAR_OBJECT = "FAMILIAR_OBJECT"
    ORIENTATION_PROMPT = "ORIENTATION_PROMPT"


class ResponseStatus(str, Enum):
    """Patient's response to an observation opportunity."""
    RESPONDED = "RESPONDED"
    SKIPPED = "SKIPPED"
    DECLINED = "DECLINED"
    INTERRUPTED = "INTERRUPTED"
    NO_OPPORTUNITY_TAKEN = "NO_OPPORTUNITY_TAKEN"
    UNKNOWN = "UNKNOWN"


class ObservationOpportunity(BaseModel):
    """
    Captures an observation opportunity presented to the patient —
    a moment where patient cognition/recall can be gently noted without
    pressure. Never used in isolation for clinical inference.
    """
    opportunity_id: str = Field(..., min_length=1, description="UUID primary key")
    linked_presentation_id: Optional[str] = Field(
        None,
        description="FK-ish to memory_support_presentations.information_id"
    )
    patient_id: str = Field(..., min_length=1, description="Anonymized patient identifier")
    session_id: str = Field(..., min_length=1, description="Active session identifier")
    room: TherapyArea = Field(..., description="Therapy area where the opportunity occurred")
    activity_id: Optional[str] = Field(None, description="Activity within the room, if applicable")
    opportunity_type: OpportunityType = Field(..., description="Type of observation opportunity")
    presented_at: datetime = Field(..., description="UTC timestamp when the opportunity was presented")
    response_status: ResponseStatus = Field(
        default=ResponseStatus.UNKNOWN, description="Patient's response outcome"
    )
    response_latency_seconds: Optional[float] = Field(
        None, ge=0.0, description="Seconds between presentation and patient response"
    )
    assistance_requested: bool = Field(default=False, description="Whether the patient requested assistance")
    assistance_received: bool = Field(default=False, description="Whether assistance was delivered")
    related_event_id: Optional[str] = Field(
        None, description="FK-ish to interaction_events.event_id"
    )


# =====================================================================
# D. Assistance Episodes
# =====================================================================

class AssistanceType(str, Enum):
    """Specific form of assistance offered or delivered."""
    AS01_REPEAT_INSTRUCTION = "AS01_REPEAT_INSTRUCTION"
    AS02_SIMPLIFY_INSTRUCTION = "AS02_SIMPLIFY_INSTRUCTION"
    AS03_STEP_BY_STEP = "AS03_STEP_BY_STEP"
    AS04_VISUAL_HINT = "AS04_VISUAL_HINT"
    AS05_DEMONSTRATION = "AS05_DEMONSTRATION"
    AS06_SLOWER_SPEECH = "AS06_SLOWER_SPEECH"
    AS07_ADDITIONAL_TIME = "AS07_ADDITIONAL_TIME"
    AS08_BREAK_PAUSE = "AS08_BREAK_PAUSE"
    AS09_VERBAL_EXPLANATION = "AS09_VERBAL_EXPLANATION"


class AssistanceInitiation(str, Enum):
    """Who/what initiated the assistance offer."""
    PATIENT_REQUESTED = "PATIENT_REQUESTED"
    CARL_OFFERED = "CARL_OFFERED"
    CAREGIVER_INITIATED = "CAREGIVER_INITIATED"


class SubsequentOutcome(str, Enum):
    """Patient outcome following delivery of assistance."""
    COMPLETED_INDEPENDENTLY = "COMPLETED_INDEPENDENTLY"
    COMPLETED_WITH_ASSISTANCE = "COMPLETED_WITH_ASSISTANCE"
    PARTIALLY_COMPLETED = "PARTIALLY_COMPLETED"
    NOT_COMPLETED = "NOT_COMPLETED"
    SKIPPED = "SKIPPED"
    INTERRUPTED = "INTERRUPTED"
    UNKNOWN = "UNKNOWN"


class AssistanceEpisode(BaseModel):
    """
    Records a full lifecycle of an assistance offer: initiation → acceptance
    → delivery → outcome. Enables granular analysis of what help works.
    """
    episode_id: str = Field(..., min_length=1, description="UUID primary key")
    patient_id: str = Field(..., min_length=1, description="Anonymized patient identifier")
    session_id: str = Field(..., min_length=1, description="Active session identifier")
    room: TherapyArea = Field(..., description="Therapy area where the episode occurred")
    activity_id: Optional[str] = Field(None, description="Activity within the room, if applicable")
    activity_step_id: Optional[str] = Field(None, description="Specific step within the activity, if applicable")
    triggering_event_id: Optional[str] = Field(
        None, description="FK-ish to interaction_events.event_id that triggered assistance"
    )
    assistance_type: AssistanceType = Field(..., description="Type of assistance offered")
    initiation: AssistanceInitiation = Field(..., description="Who initiated the assistance")
    offered_at: datetime = Field(..., description="UTC timestamp when assistance was offered")
    accepted: Optional[bool] = Field(None, description="Whether the patient accepted the offer (None = unknown)")
    delivered: bool = Field(default=False, description="Whether assistance was actually delivered")
    delivered_at: Optional[datetime] = Field(None, description="UTC timestamp when assistance was delivered")
    subsequent_outcome: SubsequentOutcome = Field(
        default=SubsequentOutcome.UNKNOWN,
        description="Patient outcome following assistance delivery"
    )
    outcome_event_id: Optional[str] = Field(
        None, description="FK-ish to interaction_events.event_id capturing the outcome"
    )
    notes: Optional[str] = Field(None, description="Optional free-text notes")


# =====================================================================
# E. UI Configuration History
# =====================================================================

class ConfigChangeSource(str, Enum):
    """Who/what triggered a UI configuration change."""
    PATIENT = "PATIENT"
    CAREGIVER = "CAREGIVER"
    SYSTEM_RULE = "SYSTEM_RULE"
    AI_RECOMMENDATION = "AI_RECOMMENDATION"


class UIConfigurationHistory(BaseModel):
    """
    Immutable snapshot of the patient's UI/UX configuration at a point in time.
    effective_to=None means this is the currently active configuration.
    """
    config_id: str = Field(..., min_length=1, description="UUID primary key")
    patient_id: str = Field(..., min_length=1, description="Anonymized patient identifier")
    effective_from: datetime = Field(..., description="UTC timestamp when this config became active")
    effective_to: Optional[datetime] = Field(None, description="UTC timestamp when superseded; None = current")
    # Display settings
    text_size: str = Field(default="medium", description="Text size: small/medium/large/extra_large")
    text_contrast: str = Field(default="standard", description="Text contrast: standard/high")
    max_visible_choices: int = Field(default=4, ge=1, le=10, description="Maximum choices shown at once")
    # Audio settings
    subtitles_enabled: bool = Field(default=True, description="Whether subtitles are displayed")
    speech_enabled: bool = Field(default=True, description="Whether Carl's speech is enabled")
    speech_speed: float = Field(default=1.0, ge=0.5, le=2.0, description="Speech speed multiplier")
    # Instruction settings
    instruction_format: str = Field(
        default="concise", description="Instruction verbosity: concise/elaborated/step_by_step"
    )
    visual_hints_available: bool = Field(default=True, description="Whether visual hints can be shown")
    demonstrations_available: bool = Field(default=False, description="Whether demonstrations are available")
    step_by_step_guidance: bool = Field(default=False, description="Whether step-by-step guidance is active")
    # Activity settings
    activity_complexity: str = Field(
        default="standard", description="Activity complexity: simplified/standard/enriched"
    )
    navigation_assistance: bool = Field(default=True, description="Whether navigation prompts are shown")
    # Change metadata
    change_source: ConfigChangeSource = Field(..., description="What triggered this configuration change")
    change_reason: Optional[str] = Field(None, description="Human-readable reason for the change")
    related_recommendation_id: Optional[str] = Field(
        None, description="FK-ish to support_recommendations.recommendation_id"
    )
    related_assistance_episode_id: Optional[str] = Field(
        None, description="FK-ish to assistance_episodes.episode_id"
    )


# =====================================================================
# F. Event Interpretations
# =====================================================================

class EventInterpretation(BaseModel):
    """
    Post-hoc semantic annotation of a raw interaction event.
    Cognitive domain and assistance labels are stored as JSON arrays in the DB.
    model_version=None indicates rule-based interpretation; a version string
    indicates an ML model was used.
    """
    interpretation_id: str = Field(..., min_length=1, description="UUID primary key")
    event_id: str = Field(..., min_length=1, description="FK to interaction_events.event_id")
    patient_id: str = Field(..., min_length=1, description="Anonymized patient identifier")
    cognitive_domains: List[str] = Field(
        default_factory=list,
        description="CD01–CD09 cognitive domain tags applicable to this event"
    )
    assistance_labels: List[str] = Field(
        default_factory=list,
        description="AS01–AS10 assistance type labels applicable to this event"
    )
    analyst_note: Optional[str] = Field(None, description="Optional free-text analyst annotation")
    generated_at: datetime = Field(default_factory=_utc_now, description="When this interpretation was generated")
    model_version: Optional[str] = Field(
        default=None,
        description="ML model version if ML-generated; None for rule-based interpretation"
    )


# =====================================================================
# G. Consent Records
# =====================================================================

class ConsentType(str, Enum):
    """Category of data consent."""
    DATA_COLLECTION = "DATA_COLLECTION"
    PHOTO_USE = "PHOTO_USE"
    PERSONAL_CONTENT = "PERSONAL_CONTENT"
    AI_ANALYSIS = "AI_ANALYSIS"
    CAREGIVER_ACCESS = "CAREGIVER_ACCESS"


class ConsentRecord(BaseModel):
    """
    Immutable record of a consent decision. Each change to consent status
    creates a new record; existing records are never modified.
    """
    consent_id: str = Field(..., min_length=1, description="UUID primary key")
    patient_id: str = Field(..., min_length=1, description="Anonymized patient identifier")
    consent_type: ConsentType = Field(..., description="Category of consent being recorded")
    granted: bool = Field(..., description="True if consent granted, False if withheld/withdrawn")
    granted_by: str = Field(
        ..., min_length=1,
        description="Role of the person providing consent, e.g. 'caregiver', 'legal_guardian'"
    )
    granted_at: datetime = Field(..., description="UTC timestamp of the consent decision")
    expires_at: Optional[datetime] = Field(None, description="UTC timestamp when consent expires, if time-limited")
    notes: Optional[str] = Field(None, description="Optional free-text notes regarding the consent decision")


# =====================================================================
# Extended Interaction Event (superset of InteractionEvent)
# =====================================================================

class ExtendedInteractionEvent(InteractionEvent):
    """
    Superset of InteractionEvent that adds Phase 3/4 annotation fields.
    All new fields are optional so existing InteractionEvent instances remain
    compatible. Stored via the extended columns on interaction_events table.
    """
    event_category: Optional[str] = Field(
        None,
        description=(
            "High-level event category. One of: session, navigation, activity, "
            "object_interaction, instruction, prompt_response, attempt, assistance, "
            "demonstration, speech_communication, patient_choice, accessibility, "
            "reminiscence, memory_support, system"
        )
    )
    activity_step_id: Optional[str] = Field(
        None, description="Identifier of the specific step within the activity"
    )
    sequence_number: Optional[int] = Field(
        None, ge=0, description="Ordinal position of this event within its session"
    )
    prompt_id: Optional[str] = Field(
        None, description="Reference to the specific prompt presented, if applicable"
    )
    observation_opportunity_id: Optional[str] = Field(
        None, description="FK-ish to observation_opportunities.opportunity_id"
    )
    assistance_episode_id: Optional[str] = Field(
        None, description="FK-ish to assistance_episodes.episode_id"
    )
    correlation_id: Optional[str] = Field(
        None,
        description="Groups causally related events across a short window (e.g. prompt → response → outcome)"
    )
    event_schema_version: str = Field(
        default="2.0", description="Schema version; '1.0' for legacy events, '2.0' for Phase 3+ events"
    )
    cognitive_domain_tags: Optional[List[str]] = Field(
        None, description="CD01–CD09 cognitive domain tags applicable to this event"
    )
    assistance_labels: Optional[List[str]] = Field(
        None, description="AS01–AS10 assistance type labels applicable to this event"
    )
