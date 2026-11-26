"""
Deterministic Fallbacks and Uncertainty Abstention Logic for Phase 4 Services.
Ensures graceful degradation and zero crashes if ML service is unavailable.
"""

from typing import Dict, Any


class DeterministicFallbackManager:
    """Provides deterministic heuristic fallbacks for inference endpoints."""

    @staticmethod
    def get_conversational_fallback(question: str, answer: str) -> Dict[str, Any]:
        """Provides heuristic lexical fallback evaluation when ML model abstains."""
        q_len = len(question.split()) if question else 0
        a_len = len(answer.split()) if answer else 0

        # Simple heuristic markers
        has_empathy_kw = any(w in answer.lower() for w in ["understand", "support", "help", "comfort"])
        has_action_kw = any(w in answer.lower() for w in ["step", "routine", "doctor", "recommend"])

        base_empathy = 3.5 if has_empathy_kw else 3.0
        base_action = 3.8 if has_action_kw else 3.2

        return {
            "model_version": "heuristic_fallback_v1.0",
            "empathy_score": base_empathy,
            "actionability_score": base_action,
            "prediction_variance": 0.5,
            "abstention_triggered": True,
            "review_recommended": bool(a_len < 10 or q_len < 3),
            "latency_ms": 1.0
        }
