"""
Latency & Cognitive Pacing Feature Estimator.
Informed by empirical psychomotor benchmarks from Motor Sequence Learning
(Van Roy et al.) and N-Back Dementia (Roncero et al., 2025).
"""

from typing import Dict, Any


class LatencyPacingEstimator:
    """Calculates pacing categories and relative delay metrics without diagnostic claims."""

    # Empirical baselines (ms)
    HEALTHY_OLDER_ADULT_MEAN_RT = 525.0       # From Van Roy et al. lifespan older adults
    HEALTHY_OLDER_ADULT_STD_RT = 110.0
    DEMENTIA_PRESERVED_SPEED_MEAN_RT = 777.6  # From Roncero Group 1 (BT1)
    DEMENTIA_SLOWER_SPEED_MEAN_RT = 1303.9    # From Roncero Group 2 (BT1)
    UPPER_WORKING_MEMORY_TIMEOUT_MS = 2500.0  # Max deliberation threshold

    @classmethod
    def evaluate_latency(cls, response_latency_ms: float) -> Dict[str, Any]:
        """Evaluates trial latency against empirical benchmarks."""
        if response_latency_ms < 0:
            raise ValueError("Response latency cannot be negative.")

        # Z-score relative to healthy older adult normative baseline
        normative_z = (response_latency_ms - cls.HEALTHY_OLDER_ADULT_MEAN_RT) / cls.HEALTHY_OLDER_ADULT_STD_RT

        # Pacing classification (behavioral, not diagnostic)
        if response_latency_ms <= 650.0:
            pacing_bracket = "preserved_speed"
            guidance = "Normal pacing; no additional deliberation time required."
        elif response_latency_ms <= 1100.0:
            pacing_bracket = "mildly_delayed_speed"
            guidance = "Allow standard working memory buffer (up to 1,500 ms) before prompting."
        elif response_latency_ms <= cls.UPPER_WORKING_MEMORY_TIMEOUT_MS:
            pacing_bracket = "substantially_slowed_speed"
            guidance = "Accommodate slower deliberation; offer AS07 (additional response time)."
        else:
            pacing_bracket = "deliberation_timeout"
            guidance = "Timeout exceeded (>2,500 ms); evaluate whether patient requires assistance."

        return {
            "response_latency_ms": float(response_latency_ms),
            "normative_z_score": float(round(normative_z, 2)),
            "pacing_bracket": pacing_bracket,
            "pacing_guidance": guidance,
            "timeout_exceeded": bool(response_latency_ms > cls.UPPER_WORKING_MEMORY_TIMEOUT_MS)
        }
