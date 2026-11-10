"""
NeuroCompanion - Interaction Event Aggregation Engine.
Calculates objective behavioral telemetry metrics from raw stored interaction events.

Principles:
1. Summarizes raw interaction counts, completion rates, hint usage, instruction repeats,
   skips, and latencies across all three therapy rooms: Orientation Room, Reminiscence Corner, Cognitive Garden.
2. Safe and neutral: strictly counts occurrences; NEVER infers cognitive difficulty or impairment from
   latencies, pauses, or incorrect attempts.
3. Decoupled from clinical diagnostic metrics and ML models.
"""

from datetime import datetime, timezone
from typing import Dict, List, Optional, Set
from telemetry.schemas import (
    TherapyArea,
    EventType,
    AssistanceSource,
    CompletionStatus,
    InteractionEvent,
    AreaSummary,
    InteractionProfile,
)


def compute_interaction_profile_from_events(
    patient_id: str,
    events: List[InteractionEvent]
) -> InteractionProfile:
    """
    Compute an aggregated InteractionProfile directly from a chronological list
    of raw InteractionEvent objects.
    """
    now_utc = datetime.now(timezone.utc)

    if not events:
        # Return an empty baseline profile if no events exist yet
        return InteractionProfile(
            patient_id=patient_id,
            last_updated=now_utc,
            total_sessions_recorded=0,
            overall_completion_rate=None,
            area_summaries={area: AreaSummary() for area in TherapyArea},
            observed_assistance_frequency={src: 0 for src in AssistanceSource},
            recent_trend_note="No interaction events recorded yet."
        )

    # 1. Track unique sessions
    unique_sessions: Set[str] = set()

    # 2. Track activity completion/attempts
    # An activity is considered attempted if at least one event is logged for that (session_id, activity_id)
    attempted_activities_by_area: Dict[TherapyArea, Set[str]] = {area: set() for area in TherapyArea}
    completed_activities_by_area: Dict[TherapyArea, Set[str]] = {area: set() for area in TherapyArea}
    skipped_activities_by_area: Dict[TherapyArea, Set[str]] = {area: set() for area in TherapyArea}

    # 3. Counts per area
    hints_by_area: Dict[TherapyArea, int] = {area: 0 for area in TherapyArea}
    repeats_by_area: Dict[TherapyArea, int] = {area: 0 for area in TherapyArea}
    demos_by_area: Dict[TherapyArea, int] = {area: 0 for area in TherapyArea}
    latencies_by_area: Dict[TherapyArea, List[float]] = {area: [] for area in TherapyArea}

    # 4. Total assistance counts by source
    assistance_by_source: Dict[AssistanceSource, int] = {src: 0 for src in AssistanceSource}

    for evt in events:
        unique_sessions.add(evt.session_id)
        area = evt.therapy_area
        task_key = f"{evt.session_id}:{evt.activity_id}"

        # Register attempt
        attempted_activities_by_area[area].add(task_key)

        # Categorize by event type
        if evt.event_type == EventType.HINT_REQUESTED:
            hints_by_area[area] += 1
            src = evt.payload.assistance_source or AssistanceSource.USER_REQUESTED
            assistance_by_source[src] += 1

        elif evt.event_type == EventType.INSTRUCTION_REPEATED:
            repeats_by_area[area] += 1
            src = evt.payload.assistance_source or AssistanceSource.USER_REQUESTED
            assistance_by_source[src] += 1

        elif evt.event_type == EventType.DEMONSTRATION_REQUESTED:
            demos_by_area[area] += 1
            src = evt.payload.assistance_source or AssistanceSource.USER_REQUESTED
            assistance_by_source[src] += 1

        elif evt.event_type == EventType.ASSISTANCE_TRIGGERED:
            src = evt.payload.assistance_source or AssistanceSource.CARL_AUTO_PROMPTED
            assistance_by_source[src] += 1

        elif evt.event_type == EventType.ACTIVITY_SKIPPED:
            skipped_activities_by_area[area].add(task_key)

        elif evt.event_type == EventType.ACTIVITY_COMPLETED:
            completed_activities_by_area[area].add(task_key)

        # Collect latency metrics if recorded (e.g. on attempts or responses)
        if evt.payload.latency_seconds is not None and evt.payload.latency_seconds >= 0.0:
            latencies_by_area[area].append(evt.payload.latency_seconds)

    # 5. Build area summaries
    area_summaries: Dict[TherapyArea, AreaSummary] = {}
    total_attempted_all = 0
    total_completed_all = 0

    for area in TherapyArea:
        n_att = len(attempted_activities_by_area[area])
        n_comp = len(completed_activities_by_area[area])
        n_skip = len(skipped_activities_by_area[area])
        n_hints = hints_by_area[area]
        n_repeats = repeats_by_area[area]
        n_demos = demos_by_area[area]

        lats = latencies_by_area[area]
        avg_lat = round(sum(lats) / len(lats), 2) if lats else None

        area_summaries[area] = AreaSummary(
            activities_attempted=n_att,
            activities_completed=n_comp,
            hints_requested_total=n_hints,
            instructions_repeated_total=n_repeats,
            demonstrations_requested_total=n_demos,
            skips_total=n_skip,
            average_response_latency_seconds=avg_lat
        )

        total_attempted_all += n_att
        total_completed_all += n_comp

    # 6. Overall completion rate
    overall_rate = (
        round(total_completed_all / total_attempted_all, 4)
        if total_attempted_all > 0
        else None
    )

    # 7. Descriptive trend note
    trend_note = (
        f"Recorded {len(events)} events across {len(unique_sessions)} session(s). "
        f"Completed {total_completed_all}/{total_attempted_all} activities."
    )

    return InteractionProfile(
        patient_id=patient_id,
        last_updated=now_utc,
        total_sessions_recorded=len(unique_sessions),
        overall_completion_rate=overall_rate,
        area_summaries=area_summaries,
        observed_assistance_frequency=assistance_by_source,
        recent_trend_note=trend_note
    )
