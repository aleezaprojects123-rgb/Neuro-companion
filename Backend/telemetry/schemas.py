"""
NeuroCompanion - Interaction Telemetry Schemas
Pydantic data models for Unity interaction telemetry, initial support profiles,
interaction profiles, and caregiver governance.

Design Principles:
1. MMSE is never mandatory.
2. Supports all three therapy areas: Orientation Room, Reminiscence Corner, Cognitive Garden.
3. Extensible event payload supporting hints, instruction replays, attempts, assistance, pauses, and skips.
4. Safeguards against false cognitive interpretation: single delays, pauses, or incorrect attempts
   are never treated in isolation as evidence of cognitive impairment.
5. Strict separation between clinical screening profiles and behavioral interaction profiles.
6. Absolute caregiver oversight over active application assistance tiers.
"""

from datetime import datetime, timezone
from enum import Enum
from typing import Any, Dict, List, Optional
from pydantic import BaseModel, Field, field_validator


def _utc_now() -> datetime:
    return datetime.now(timezone.utc)


# =====================================================================
# Core Enumerations
# =====================================================================

class TherapyArea(str, Enum):
    """The three therapeutic areas within the NeuroCompanion Unity environment."""
    ORIENTATION_ROOM = "OrientationRoom"
    REMINISCENCE_CORNER = "ReminiscenceCorner"
    COGNITIVE_GARDEN = "CognitiveGarden"


class EventType(str, Enum):
    """Interaction event taxonomy emitted during Unity activity execution."""
    ACTIVITY_STARTED = "activity_started"
    INSTRUCTION_REQUESTED = "instruction_requested"
    INSTRUCTION_REPEATED = "instruction_repeated"
    HINT_REQUESTED = "hint_requested"
    ATTEMPT_SUBMITTED = "attempt_submitted"
    ASSISTANCE_TRIGGERED = "assistance_triggered"
    DEMONSTRATION_REQUESTED = "demonstration_requested"
    ACTIVITY_PAUSED = "activity_paused"
    ACTIVITY_RESUMED = "activity_resumed"
    ACTIVITY_SKIPPED = "activity_skipped"
    ACTIVITY_COMPLETED = "activity_completed"


class SupportTier(str, Enum):
    """
    Application-level assistance configurations.
    IMPORTANT: These represent software assistance preferences, NOT Alzheimer's
    disease stages or clinical cognitive severity ratings.
    """
    STANDARD = "Standard"          # Concise instructions, voluntary hints, standard pacing
    GUIDED = "Guided"              # Simplified prompts, target highlights, gentle reassurance
    HIGH_SUPPORT = "HighSupport"    # Micro-step scaffolding, visual demonstrations, no timers


class AssistanceSource(str, Enum):
    """Source that initiated the assistance or hint."""
    USER_REQUESTED = "user_requested"
    CARL_AUTO_PROMPTED = "carl_auto_prompted"
    CAREGIVER_INITIATED = "caregiver_initiated"


class CompletionStatus(str, Enum):
    """How an activity or sub-step concluded."""
    INDEPENDENT = "independent"
    WITH_GENTLE_HINTS = "with_gentle_hints"
    WITH_STEP_GUIDANCE = "with_step_guidance"
    SKIPPED_BY_USER = "skipped_by_user"
    TIMED_OUT = "timed_out"


# =====================================================================
# 1. Interaction Event Schema (Unity Telemetry Stream)
# =====================================================================

class InteractionPayload(BaseModel):
    """
    Detailed contextual measurements for a specific interaction event.
    All measurements are safe and optional to handle partial sensor/UI telemetry.
    """
    attempt_index: Optional[int] = Field(
        None, ge=1, description="Sequential attempt count within the current activity (1-indexed)"
    )
    is_correct: Optional[bool] = Field(
        None, description="Whether the attempted sub-action was correct, if evaluated"
    )
    latency_seconds: Optional[float] = Field(
        None, ge=0.0, description="Response time or pause latency preceding this event in seconds"
    )
    hint_tier_delivered: Optional[int] = Field(
        None, ge=1, le=3, description="Graduated level of hint delivered: 1=verbal cue, 2=highlight, 3=walkthrough"
    )
    assistance_source: Optional[AssistanceSource] = Field(
        None, description="Whether assistance was voluntarily requested or automatically offered"
    )
    completion_status: Optional[CompletionStatus] = Field(
        None, description="Final outcome status if this event concludes the activity"
    )
    pause_duration_seconds: Optional[float] = Field(
        None, ge=0.0, description="Duration of inactivity pause if event_type is activity_paused"
    )
    substep_identifier: Optional[str] = Field(
        None, description="Identifier of the specific sub-task or item interacted with"
    )
    notes: Optional[str] = Field(
        None, description="Optional safe telemetry note (e.g., 'user requested audio replay')"
    )


class InteractionEvent(BaseModel):
    """
    Atomic interaction record emitted by the NeuroCompanion Unity client.
    Contains zero sensitive personal data, video, audio recordings, or keystrokes.
    """
    event_id: str = Field(..., min_length=1, description="Unique event identifier (e.g., UUIDv4)")
    session_id: str = Field(..., min_length=1, description="Active session identifier")
    patient_id: str = Field(..., min_length=1, description="Anonymized patient identifier")
    timestamp_utc: datetime = Field(..., description="UTC ISO-8601 timestamp of the event")
    therapy_area: TherapyArea = Field(..., description="Target therapeutic area in Unity")
    activity_id: str = Field(..., min_length=1, description="Unique activity ID within the therapy area")
    event_type: EventType = Field(..., description="Classification of the interaction event")
    payload: InteractionPayload = Field(
        default_factory=InteractionPayload, description="Contextual event payload"
    )
    metadata: Dict[str, Any] = Field(
        default_factory=dict, description="Extensible dictionary for future Unity UI parameters"
    )

    @field_validator("event_id", "session_id", "patient_id", "activity_id")
    @classmethod
    def validate_non_empty_strings(cls, v: str) -> str:
        if not v or not v.strip():
            raise ValueError("Identifier fields cannot be empty or whitespace only.")
        return v.strip()


# =====================================================================
# 2. Initial Support Profile (Missing-MMSE Onboarding)
# =====================================================================

class CarlConfiguration(BaseModel):
    """Granular dialogue and interaction parameters applied by the Carl companion."""
    instruction_style: str = Field(
        default="concise",
        description="Instruction verbosity: 'concise', 'elaborated', or 'step_by_step'"
    )
    allow_auto_hints: bool = Field(
        default=True,
        description="Whether Carl offers proactive hints after hesitation, or waits for user request"
    )
    hesitation_threshold_seconds: float = Field(
        default=25.0,
        ge=5.0,
        le=120.0,
        description="Time without user input before Carl gently asks if assistance is needed (accessibility option, never an impairment metric)"
    )
    demonstrations_enabled: bool = Field(
        default=False,
        description="Whether Carl provides visual demonstrations before user input"
    )
    speech_rate_multiplier: float = Field(
        default=1.0,
        ge=0.7,
        le=1.3,
        description="Speech rate multiplier for Carl's TTS voice (1.0 = normal pacing)"
    )


class CaregiverPreferences(BaseModel):
    """Initial caregiver-provided application preferences."""
    preferred_therapy_areas: List[TherapyArea] = Field(
        default_factory=lambda: [TherapyArea.ORIENTATION_ROOM, TherapyArea.REMINISCENCE_CORNER],
        description="Therapy environments prioritized for this patient"
    )
    max_session_duration_minutes: int = Field(
        default=15,
        ge=5,
        le=60,
        description="Recommended maximum daily interaction session duration"
    )
    allow_adaptive_recommendations: bool = Field(
        default=True,
        description="Whether the system may suggest support tier adjustments to the caregiver"
    )


class InitialSupportProfile(BaseModel):
    """
    Onboarding profile established when a patient enters NeuroCompanion.
    Architecture Principles:
    1. MMSE is explicitly optional: absence of MMSE NEVER blocks application onboarding.
    2. Initial configuration does NOT assign a support tier (Standard, Guided, High Support).
       active_support_tier is None upon onboarding; tiers are assigned later via caregiver
       choice or explicit recommendation review.
    """
    patient_id: str = Field(..., min_length=1, description="Anonymized patient identifier")
    created_at: datetime = Field(default_factory=_utc_now, description="Creation timestamp")
    active_support_tier: Optional[SupportTier] = Field(
        default=None,
        description="Active application support tier if assigned; None during initial onboarding"
    )
    carl_config: CarlConfiguration = Field(
        default_factory=CarlConfiguration,
        description="Companion interaction parameters"
    )
    caregiver_preferences: CaregiverPreferences = Field(
        default_factory=CaregiverPreferences,
        description="Caregiver-provided initial settings"
    )
    onboarding_notes: Optional[str] = Field(
        None,
        description="Optional qualitative notes regarding sensory/mobility preferences (e.g., 'uses reading glasses')"
    )


# =====================================================================
# 3. Interaction Profile (Empirical Pattern Summaries)
# =====================================================================

class AreaSummary(BaseModel):
    """Aggregated behavioral summary for a specific therapy room."""
    activities_attempted: int = Field(default=0, ge=0)
    activities_completed: int = Field(default=0, ge=0)
    hints_requested_total: int = Field(default=0, ge=0)
    instructions_repeated_total: int = Field(default=0, ge=0)
    demonstrations_requested_total: int = Field(default=0, ge=0)
    skips_total: int = Field(default=0, ge=0)
    average_response_latency_seconds: Optional[float] = Field(None, ge=0.0)


class InteractionProfile(BaseModel):
    """
    Longitudinal summary of a patient's behavioral interactions inside Unity.
    Strictly decoupled from clinical diagnostic data. Summarizes support reliance over time.
    """
    patient_id: str = Field(..., min_length=1, description="Anonymized patient identifier")
    last_updated: datetime = Field(default_factory=_utc_now, description="Last update timestamp")
    total_sessions_recorded: int = Field(default=0, ge=0, description="Total completed Unity sessions")
    overall_completion_rate: Optional[float] = Field(
        None, ge=0.0, le=1.0, description="Proportion of attempted activities successfully completed"
    )
    area_summaries: Dict[TherapyArea, AreaSummary] = Field(
        default_factory=lambda: {area: AreaSummary() for area in TherapyArea},
        description="Breakdown of interactions per therapy environment"
    )
    observed_assistance_frequency: Dict[AssistanceSource, int] = Field(
        default_factory=lambda: {src: 0 for src in AssistanceSource},
        description="Total counts of assistance delivered by source"
    )
    recent_trend_note: Optional[str] = Field(
        None,
        description="Descriptive summary of recent interaction patterns (e.g., 'Frequent hint requests in Garden')"
    )


# =====================================================================
# 4. Support Recommendation (Adaptive Decision Governance)
# =====================================================================

class SupportRecommendation(BaseModel):
    """
    A proposed adjustment to the application support tier.
    Requires caregiver review or operational policy confirmation before applying.
    """
    recommendation_id: str = Field(..., min_length=1, description="Unique recommendation identifier")
    patient_id: str = Field(..., min_length=1, description="Anonymized patient identifier")
    timestamp_utc: datetime = Field(default_factory=_utc_now, description="Timestamp generated")
    current_tier: Optional[SupportTier] = Field(
        None, description="Existing active support configuration if assigned, None if unassigned initial"
    )
    recommended_tier: SupportTier = Field(..., description="Proposed new support configuration")
    rationale: str = Field(
        ..., min_length=1,
        description="Human-readable transparent explanation of the behavioral patterns prompting this suggestion"
    )
    triggering_area: Optional[TherapyArea] = Field(
        None, description="Specific therapy area where assistance needs were observed, if localized"
    )
    is_temporary_baseline: bool = Field(
        default=True,
        description="Flag indicating this suggestion originates from baseline rule-based heuristics, not trained ML"
    )


# =====================================================================
# 5. Caregiver Override (Governance & Authority)
# =====================================================================

class CaregiverOverride(BaseModel):
    """
    Explicit caregiver action setting, approving, or locking an application support tier.
    Caregiver decisions always supersede automated or ML recommendations.
    """
    override_id: str = Field(..., min_length=1, description="Unique override transaction ID")
    patient_id: str = Field(..., min_length=1, description="Anonymized patient identifier")
    timestamp_utc: datetime = Field(default_factory=_utc_now, description="Timestamp of override")
    selected_tier: SupportTier = Field(..., description="Support tier chosen by the caregiver")
    lock_tier: bool = Field(
        default=False,
        description="If True, automated adaptation recommendations will not be suggested"
    )
    caregiver_note: Optional[str] = Field(
        None, description="Optional reason recorded by caregiver (e.g., 'Patient feeling tired today')"
    )
