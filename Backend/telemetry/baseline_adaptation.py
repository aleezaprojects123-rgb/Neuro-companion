"""
NeuroCompanion - Baseline Adaptation & Assistance Engine (Phase 2A)

Transparent, conservative, non-diagnostic baseline adaptation for Carl and
application-level assistance tiers.

Guiding Principles:
1. Temporary baseline heuristic ONLY: Clearly labelled as temporary baseline, NOT trained ML.
2. Safe & non-diagnostic: Explicitly NEVER interprets pauses, errors, or slow responses as cognitive decline.
3. Interaction-evidence driven: Focuses on patient-initiated requests (hints, instruction repeats, demos).
4. Full Caregiver Sovereignty: Caregiver overrides and locks always supersede any recommendation.
5. Immediate Assistance: Provides on-demand assistance actions for Carl without delay.
"""

from datetime import datetime, timezone
import uuid
from typing import Optional, Tuple, List, Dict
from pydantic import BaseModel, Field

from telemetry.schemas import (
    SupportTier,
    TherapyArea,
    EventType,
    AssistanceSource,
    InteractionProfile,
    SupportRecommendation,
    CarlConfiguration,
    InitialSupportProfile,
    CaregiverPreferences,
)


class AssistanceActionRequest(BaseModel):
    """Request from client/Unity for Carl to deliver immediate assistance during an activity."""
    patient_id: str
    session_id: str
    therapy_area: TherapyArea
    activity_id: str
    action_type: str = Field(
        ...,
        description="One of: 'repeat_instruction', 'step_by_step_breakdown', 'visual_demonstration', 'show_hint', 'slow_pacing', 'pause_or_skip'"
    )
    current_step: Optional[int] = None
    step_instruction: Optional[str] = None


class AssistanceActionResponse(BaseModel):
    """Immediate guidance instructions formulated for Carl companion."""
    status: str = "success"
    action_type: str
    spoken_prompt: str
    visual_cue: Optional[str] = None
    applied_pacing_multiplier: float = 1.0
    allow_skip: bool = True
    carl_state: str = "assisting"


def generate_immediate_assistance(request: AssistanceActionRequest) -> AssistanceActionResponse:
    """
    Formulates immediate actionable assistance for Carl without waiting for an ML model.
    Ensures patients are never forced to struggle.
    """
    action = request.action_type.lower()

    if action in ("repeat_instruction", "repeat"):
        prompt = (
            f"Here is the instruction again: {request.step_instruction}" 
            if request.step_instruction 
            else "Take all the time you need. Let me repeat what we're doing."
        )
        return AssistanceActionResponse(
            action_type="repeat_instruction",
            spoken_prompt=prompt,
            visual_cue="highlight_current_target",
            applied_pacing_multiplier=0.9,
            allow_skip=True,
            carl_state="repeating"
        )

    elif action in ("step_by_step_breakdown", "breakdown", "simplify"):
        return AssistanceActionResponse(
            action_type="step_by_step_breakdown",
            spoken_prompt="Let's take this one gentle step at a time. First, let's look at this part together.",
            visual_cue="isolate_single_element",
            applied_pacing_multiplier=0.85,
            allow_skip=True,
            carl_state="guiding_step"
        )

    elif action in ("visual_demonstration", "demonstrate", "show_me"):
        return AssistanceActionResponse(
            action_type="visual_demonstration",
            spoken_prompt="Watch Carl do it first! Then whenever you are ready, you can give it a try.",
            visual_cue="play_ghost_animation",
            applied_pacing_multiplier=0.8,
            allow_skip=True,
            carl_state="demonstrating"
        )

    elif action in ("show_hint", "hint"):
        return AssistanceActionResponse(
            action_type="show_hint",
            spoken_prompt="Here is a friendly hint to guide you along.",
            visual_cue="pulse_recommended_item",
            applied_pacing_multiplier=1.0,
            allow_skip=True,
            carl_state="hinting"
        )

    elif action in ("slow_pacing", "slow"):
        return AssistanceActionResponse(
            action_type="slow_pacing",
            spoken_prompt="There is no hurry at all. We are moving at your comfortable pace.",
            visual_cue=None,
            applied_pacing_multiplier=0.75,
            allow_skip=True,
            carl_state="relaxed_pace"
        )

    elif action in ("pause_or_skip", "skip", "pause", "revisit"):
        return AssistanceActionResponse(
            action_type="pause_or_skip",
            spoken_prompt="It's completely fine to pause, skip this activity, or visit another room whenever you'd like.",
            visual_cue="show_navigation_options",
            applied_pacing_multiplier=1.0,
            allow_skip=True,
            carl_state="offering_break"
        )

    else:
        return AssistanceActionResponse(
            action_type="general_reassurance",
            spoken_prompt="You're doing great. I'm right here whenever you want a hint or a demonstration.",
            visual_cue="gentle_companion_wave",
            applied_pacing_multiplier=1.0,
            allow_skip=True,
            carl_state="comforting"
        )


def evaluate_baseline_recommendation(
    profile: InitialSupportProfile,
    interaction_profile: InteractionProfile
) -> Optional[SupportRecommendation]:
    """
    Evaluates whether a transparent temporary baseline recommendation should be proposed.

    Strict Architectural Safeguards:
    1. If caregiver locked the tier (allow_adaptive_recommendations is False), return None.
    2. Does NOT interpret pauses, hesitation, response times, or incorrect attempts as impairment.
    3. Skipping activities ALONE must not trigger High Support. (A user may skip due to disinterest, fatigue, or preference).
    4. Zero assistance requests must NOT automatically reduce support. (Absence of requests does not mean high mastery).
    5. If interaction evidence is insufficient, ambiguous, or lacks clear repeated requests, return None.
    6. Recommends tier adjustment only when clear, persistent, explicit user requests occur across multiple sessions.
    7. Explicitly flags `is_temporary_baseline = True` and remains non-diagnostic.
    """
    # Safeguard 1: Caregiver Lock Sovereignty
    if not profile.caregiver_preferences.allow_adaptive_recommendations:
        return None

    current_tier = profile.active_support_tier

    # Need at least 2 recorded sessions before evaluating longitudinal assistance trends
    if interaction_profile.total_sessions_recorded < 2:
        return None

    # Calculate explicit assistance requests across all therapy areas
    total_hints = 0
    total_repeats = 0
    total_demos = 0
    total_skips = 0

    area_with_most_requests: Optional[TherapyArea] = None
    max_area_requests = -1

    for area, summary in interaction_profile.area_summaries.items():
        area_reqs = (
            summary.hints_requested_total +
            summary.instructions_repeated_total +
            summary.demonstrations_requested_total
        )
        total_hints += summary.hints_requested_total
        total_repeats += summary.instructions_repeated_total
        total_demos += summary.demonstrations_requested_total
        total_skips += summary.skips_total

        if area_reqs > max_area_requests:
            max_area_requests = area_reqs
            area_with_most_requests = area

    # Total explicit assistance inquiries (excluding passive metrics or neutral skips)
    explicit_assistance_total = total_hints + total_repeats + total_demos

    # -------------------------------------------------------------
    # Transparent Baseline Escalation Rules (Non-diagnostic)
    # -------------------------------------------------------------

    # Case 1: Initial unassigned profile or Standard tier with persistent explicit requests for guidance
    # Patient explicitly requested hints/repeats/demos at least 5 times across >= 2 sessions
    if (current_tier is None or current_tier == SupportTier.STANDARD) and explicit_assistance_total >= 5:
        return SupportRecommendation(
            recommendation_id=f"rec_{uuid.uuid4().hex[:12]}",
            patient_id=profile.patient_id,
            timestamp_utc=datetime.now(timezone.utc),
            current_tier=current_tier,
            recommended_tier=SupportTier.GUIDED,
            rationale=(
                f"Temporary baseline heuristic: Patient frequently requested assistance "
                f"({total_hints} hints, {total_repeats} repeats, {total_demos} demos) across "
                f"{interaction_profile.total_sessions_recorded} sessions. Proposing Guided tier "
                f"to proactively offer visual prompts and gentle reassurance. (Not an ML diagnosis)."
            ),
            triggering_area=area_with_most_requests,
            is_temporary_baseline=True
        )

    # Case 2: Patient on Guided tier who explicitly asks for demonstrations frequently (>= 4 demos)
    # Note: Skipping alone does NOT trigger High Support. Only explicit demonstration requests.
    if current_tier == SupportTier.GUIDED and total_demos >= 4:
        return SupportRecommendation(
            recommendation_id=f"rec_{uuid.uuid4().hex[:12]}",
            patient_id=profile.patient_id,
            timestamp_utc=datetime.now(timezone.utc),
            current_tier=current_tier,
            recommended_tier=SupportTier.HIGH_SUPPORT,
            rationale=(
                f"Temporary baseline heuristic: Patient frequently requested visual demonstrations ({total_demos} demos) "
                f"across {interaction_profile.total_sessions_recorded} sessions. Proposing High Support tier to provide "
                f"proactive step-by-step demonstrations. (Not an ML diagnosis; skips are not used as impairment evidence)."
            ),
            triggering_area=area_with_most_requests,
            is_temporary_baseline=True
        )

    # Ambiguous or insufficient evidence: return None rather than forcing a recommendation
    return None
