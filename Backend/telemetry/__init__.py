"""
NeuroCompanion - Telemetry Module Package
Exposes core Pydantic schemas for Unity interaction telemetry, onboarding,
interaction profiling, and caregiver governance.
Also exports the interaction aggregation engine.
"""

from .schemas import (
    TherapyArea,
    EventType,
    SupportTier,
    AssistanceSource,
    CompletionStatus,
    InteractionPayload,
    InteractionEvent,
    CarlConfiguration,
    CaregiverPreferences,
    InitialSupportProfile,
    AreaSummary,
    InteractionProfile,
    SupportRecommendation,
    CaregiverOverride,
)
from .aggregation import compute_interaction_profile_from_events
from .baseline_adaptation import (
    AssistanceActionRequest,
    AssistanceActionResponse,
    generate_immediate_assistance,
    evaluate_baseline_recommendation,
)

__all__ = [
    "TherapyArea",
    "EventType",
    "SupportTier",
    "AssistanceSource",
    "CompletionStatus",
    "InteractionPayload",
    "InteractionEvent",
    "CarlConfiguration",
    "CaregiverPreferences",
    "InitialSupportProfile",
    "AreaSummary",
    "InteractionProfile",
    "SupportRecommendation",
    "CaregiverOverride",
    "compute_interaction_profile_from_events",
    "AssistanceActionRequest",
    "AssistanceActionResponse",
    "generate_immediate_assistance",
    "evaluate_baseline_recommendation",
]
