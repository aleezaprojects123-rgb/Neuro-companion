"""
Configuration parameters and default thresholds for NeuroCompanion Phase 4.5
AS01-AS10 AI Assistance Prediction Subsystem.
"""

from dataclasses import dataclass
from typing import Dict, Any


@dataclass(frozen=True)
class AssistanceConfig:
    """Configurable parameters and clinical safety thresholds for assistance predictors."""

    # AS01 Repeat Instruction
    repeat_idle_latency_ms_threshold: float = 1200.0
    repeat_target_miss_threshold: int = 1

    # AS02 Simplify Instruction
    simplify_fkgl_grade_threshold: float = 6.0
    simplify_word_count_threshold: int = 12
    simplify_repeated_hesitation_threshold: int = 2

    # AS03 Step-by-Step Guidance
    step_guidance_min_task_steps: int = 3
    step_guidance_m4_independence_cutoff: float = 0.60
    step_guidance_step_error_threshold: int = 1

    # AS04 Visual Hint
    visual_hint_target_miss_threshold: int = 2
    visual_hint_m3_error_risk_cutoff: float = 0.65
    visual_hint_idle_latency_ms: float = 1500.0

    # AS05 Demonstration
    demonstration_min_failed_cues: int = 2
    demonstration_error_threshold: int = 3
    demonstration_idle_latency_ms: float = 3000.0

    # AS06 Slower Speech
    speech_rate_default: float = 1.0
    speech_rate_slow: float = 0.85
    speech_rate_very_slow: float = 0.75

    # AS07 Additional Response Time (Pacing Reference)
    pacing_motor_p95_ms: float = 1031.27
    pacing_cognitive_p95_ms: float = 1379.91
    pacing_safety_buffer_ms: float = 500.0

    # AS08 Break / Pause
    break_continuous_minutes_threshold: float = 20.0
    break_consecutive_error_burst_threshold: int = 4

    # AS09 Verbal Explanation
    dialogue_min_empathy_score: float = 3.50
    dialogue_min_actionability_score: float = 3.50
    dialogue_low_quality_threshold: float = 3.00

    # AS10 Independent Completion
    independence_min_error_free_prob: float = 0.50
    independence_max_error_count: int = 0

    # Safety Policy Arbitrator
    prompt_cooldown_seconds: float = 45.0
    allow_forced_break_lockout: bool = False  # Strictly forbidden to lock out patient


DEFAULT_CONFIG = AssistanceConfig()
