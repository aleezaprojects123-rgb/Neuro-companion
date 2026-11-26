"""
Runtime Feature Extractor for NeuroCompanion Phase 4.5.
Parses, sanitizes, and enriches client telemetry contexts.
Computes deterministic linguistic readability metrics and extracts pacing deviations.
"""

import re
from typing import Dict, Any, Optional
import numpy as np

from .schemas import InteractionTelemetryContext
from .model_signals import ModelSignalProvider


def compute_flesch_kincaid_grade_level(text: Optional[str]) -> float:
    """Computes standard Flesch-Kincaid Grade Level formula for English text."""
    if not text or len(text.strip()) == 0:
        return 0.0

    sentences = re.split(r"[.!?]+", text)
    sentences = [s.strip() for s in sentences if len(s.strip()) > 0]
    num_sentences = max(1, len(sentences))

    words = re.findall(r"\b[A-Za-z]+\b", text)
    num_words = max(1, len(words))

    def count_syllables(w: str) -> int:
        w = w.lower()
        count = len(re.findall(r"[aeiouy]+", w))
        if w.endswith("e") and not w.endswith("le") and count > 1:
            count -= 1
        return max(1, count)

    num_syllables = sum(count_syllables(w) for w in words)
    fkgl = 0.39 * (num_words / num_sentences) + 11.8 * (num_syllables / num_words) - 15.59
    return round(float(np.clip(fkgl, 1.0, 18.0)), 2)


class RuntimeFeatureExtractor:
    """Sanitizes raw telemetry and enriches context with linguistic & pacing features."""

    def __init__(self, model_signals: Optional[ModelSignalProvider] = None):
        self.signals = model_signals or ModelSignalProvider()

    def sanitize_context(self, raw: Any) -> InteractionTelemetryContext:
        """Coerces dictionaries or objects into a valid InteractionTelemetryContext."""
        if isinstance(raw, InteractionTelemetryContext):
            ctx = raw
        elif isinstance(raw, dict):
            ctx = InteractionTelemetryContext(
                session_id=str(raw.get("session_id", "SESSION_DEFAULT")),
                patient_id=str(raw.get("patient_id", "PATIENT_DEFAULT")),
                task_name=str(raw.get("task_name", "DefaultTask")),
                task_domain=str(raw.get("task_domain", "cognitive_selection")),
                step_index=max(1, int(raw.get("step_index", 1))),
                step_total_steps=max(1, int(raw.get("step_total_steps", 1))),
                response_latency_ms=float(raw["response_latency_ms"]) if raw.get("response_latency_ms") is not None else None,
                idle_duration_ms=float(raw["idle_duration_ms"]) if raw.get("idle_duration_ms") is not None else None,
                target_miss_count=max(0, int(raw.get("target_miss_count", 0))),
                repeated_click_count=max(0, int(raw.get("repeated_click_count", 0))),
                step_error_count=max(0, int(raw.get("step_error_count", 0))),
                cumulative_session_errors=max(0, int(raw.get("cumulative_session_errors", 0))),
                consecutive_error_count=max(0, int(raw.get("consecutive_error_count", 0))),
                session_duration_minutes=max(0.0, float(raw.get("session_duration_minutes", 0.0))),
                patient_requested_help=bool(raw.get("patient_requested_help", False)),
                patient_requested_repeat=bool(raw.get("patient_requested_repeat", False)),
                patient_requested_break=bool(raw.get("patient_requested_break", False)),
                patient_dismissed_prompt=bool(raw.get("patient_dismissed_prompt", False)),
                caregiver_locked_strategy=raw.get("caregiver_locked_strategy"),
                caregiver_speech_rate_setting=float(raw["caregiver_speech_rate_setting"]) if raw.get("caregiver_speech_rate_setting") is not None else None,
                candidate_prompt_text=raw.get("candidate_prompt_text"),
                prior_strategy_offered=raw.get("prior_strategy_offered"),
                prior_strategy_success=bool(raw["prior_strategy_success"]) if raw.get("prior_strategy_success") is not None else None,
                client_version=str(raw.get("client_version", "3.0.0"))
            )
        else:
            ctx = InteractionTelemetryContext()

        return ctx

    def extract_linguistic_features(self, text: Optional[str]) -> Dict[str, Any]:
        """Extracts deterministic lexical readability features from candidate prompt text."""
        if not text or len(text.strip()) == 0:
            return {
                "has_text": False,
                "word_count": 0,
                "sentence_count": 0,
                "fkgl_grade_level": 0.0,
                "is_accessible_grade_6": True
            }

        words = text.strip().split()
        sentences = [s.strip() for s in re.split(r"[.!?]+", text) if len(s.strip()) > 0]
        fkgl = compute_flesch_kincaid_grade_level(text)

        return {
            "has_text": True,
            "word_count": len(words),
            "sentence_count": max(1, len(sentences)),
            "fkgl_grade_level": fkgl,
            "is_accessible_grade_6": bool(fkgl <= 6.0)
        }

    def extract_pacing_features(self, context: InteractionTelemetryContext) -> Dict[str, Any]:
        """Compares observed trial latency against M1 statistical population reference percentiles."""
        ref = self.signals.get_m1_pacing_reference(context.task_domain)
        latency = context.response_latency_ms

        if latency is None:
            return {
                "has_latency": False,
                "observed_latency_ms": None,
                "reference_domain": ref["domain"],
                "p75_ms": ref["p75_ms"],
                "p95_ms": ref["p95_ms"],
                "exceeds_p75": False,
                "exceeds_p95": False
            }

        return {
            "has_latency": True,
            "observed_latency_ms": latency,
            "reference_domain": ref["domain"],
            "p75_ms": ref["p75_ms"],
            "p95_ms": ref["p95_ms"],
            "exceeds_p75": bool(latency > ref["p75_ms"]),
            "exceeds_p95": bool(latency > ref["p95_ms"])
        }
