"""
Assistance Policy Interface for NeuroCompanion Phase 4.5.
Decouples raw AI assistance predictions from clinical safety rules:
- Caregiver portal sovereignty (absolute lock priority)
- Patient agency (explicit help requests, prompt dismissibility, break optionality)
- Rate limiting and non-intrusive prompt cooldowns
- Strict prohibition of CDR-to-tier mapping
"""

from typing import Dict, Any, Optional
from datetime import datetime, timezone

from .schemas import (
    StrategyID,
    InteractionTelemetryContext,
    PredictionBatchResult,
    AssistancePredictionRecord
)
from .config import AssistanceConfig, DEFAULT_CONFIG


class AssistancePolicyInterface:
    """
    Arbitrates candidate AI predictions against clinical safety and governance rules.
    Guarantees that machine learning cannot autonomously force actions or bypass human consent.
    """

    def __init__(self, config: Optional[AssistanceConfig] = None):
        self.config = config or DEFAULT_CONFIG

    def evaluate_policy(
        self,
        batch_result: PredictionBatchResult,
        context: InteractionTelemetryContext,
        seconds_since_last_prompt: float = 60.0,
        cdr_score: Optional[float] = None
    ) -> Dict[str, Any]:
        """
        Applies governance pipeline to raw candidate predictions.
        STRICT RULE: cdr_score is never used to determine or restrict assistance tiers.
        """
        recs = batch_result.recommendations

        # ---------------------------------------------------------------------
        # 1. Caregiver Sovereignty (Absolute Override Priority)
        # ---------------------------------------------------------------------
        if context.caregiver_locked_strategy:
            locked_id = context.caregiver_locked_strategy
            target_rec = recs.get(locked_id)
            return {
                "approved_strategy": locked_id,
                "action_type": "CAREGIVER_LOCK",
                "policy_rationale": f"Caregiver portal preset lock active: '{locked_id}'. AI predictions overridden.",
                "patient_dismissible": False,
                "caregiver_override_applied": True,
                "cdr_tiering_prohibited": True,
                "selected_record": target_rec.to_dict() if target_rec else None,
                "timestamp": datetime.now(timezone.utc).isoformat()
            }

        # ---------------------------------------------------------------------
        # 2. Patient Agency & Explicit Requests
        # ---------------------------------------------------------------------
        if context.patient_requested_break:
            rec_as08 = recs[StrategyID.AS08_BREAK_PAUSE.value]
            return {
                "approved_strategy": StrategyID.AS08_BREAK_PAUSE.value,
                "action_type": "OFFER_BREAK_OPTIONAL",
                "policy_rationale": "Patient explicitly requested a break. Rest screen presented with optional 'Keep Playing' toggle.",
                "patient_dismissible": True,
                "caregiver_override_applied": False,
                "cdr_tiering_prohibited": True,
                "selected_record": rec_as08.to_dict(),
                "timestamp": datetime.now(timezone.utc).isoformat()
            }

        if context.patient_requested_repeat:
            rec_as01 = recs[StrategyID.AS01_REPEAT.value]
            return {
                "approved_strategy": StrategyID.AS01_REPEAT.value,
                "action_type": "OFFER_PROMPT",
                "policy_rationale": "Patient explicitly requested instruction repeat. Prompt re-voiced immediately.",
                "patient_dismissible": True,
                "caregiver_override_applied": False,
                "cdr_tiering_prohibited": True,
                "selected_record": rec_as01.to_dict(),
                "timestamp": datetime.now(timezone.utc).isoformat()
            }

        if context.patient_requested_help:
            # Select visual hint (AS04) or repeat (AS01)
            target_id = StrategyID.AS04_VISUAL_HINT.value if context.target_miss_count > 0 else StrategyID.AS01_REPEAT.value
            return {
                "approved_strategy": target_id,
                "action_type": "OFFER_PROMPT",
                "policy_rationale": "Patient explicitly requested help. Immediate non-intrusive cue provided.",
                "patient_dismissible": True,
                "caregiver_override_applied": False,
                "cdr_tiering_prohibited": True,
                "selected_record": recs[target_id].to_dict(),
                "timestamp": datetime.now(timezone.utc).isoformat()
            }

        # ---------------------------------------------------------------------
        # 3. Patient Dismissal & Mute Guard
        # ---------------------------------------------------------------------
        if context.patient_dismissed_prompt:
            return {
                "approved_strategy": None,
                "action_type": "SUPPRESS_DISMISSED",
                "policy_rationale": "Patient dismissed or muted companion audio. All spontaneous assistance suppressed.",
                "patient_dismissible": True,
                "caregiver_override_applied": False,
                "cdr_tiering_prohibited": True,
                "selected_record": None,
                "timestamp": datetime.now(timezone.utc).isoformat()
            }

        # ---------------------------------------------------------------------
        # 4. Spontaneous Assistance Rate Limiting Cooldown (45 seconds)
        # ---------------------------------------------------------------------
        if seconds_since_last_prompt < self.config.prompt_cooldown_seconds:
            return {
                "approved_strategy": None,
                "action_type": "SUPPRESS_COOLDOWN",
                "policy_rationale": f"Cooldown active ({seconds_since_last_prompt:.1f}s < {self.config.prompt_cooldown_seconds}s). Spontaneous prompts suppressed to avoid cognitive overload.",
                "patient_dismissible": True,
                "caregiver_override_applied": False,
                "cdr_tiering_prohibited": True,
                "selected_record": None,
                "timestamp": datetime.now(timezone.utc).isoformat()
            }

        # ---------------------------------------------------------------------
        # 5. Break Recommendation Check (Must be optional!)
        # ---------------------------------------------------------------------
        rec_as08 = recs[StrategyID.AS08_BREAK_PAUSE.value]
        if rec_as08.is_recommended:
            return {
                "approved_strategy": StrategyID.AS08_BREAK_PAUSE.value,
                "action_type": "OFFER_BREAK_OPTIONAL",
                "policy_rationale": rec_as08.recommendation + " Presented with non-blocking 'Keep Playing' option.",
                "patient_dismissible": True,
                "caregiver_override_applied": False,
                "cdr_tiering_prohibited": True,
                "selected_record": rec_as08.to_dict(),
                "timestamp": datetime.now(timezone.utc).isoformat()
            }

        # ---------------------------------------------------------------------
        # 6. Candidate Cue Selection (Multi-Tier Escalation Safeguard)
        # ---------------------------------------------------------------------
        # High-tier: AS05 Demonstration requires prior cues to have been attempted
        rec_as05 = recs[StrategyID.AS05_DEMONSTRATION.value]
        if rec_as05.is_recommended:
            return {
                "approved_strategy": StrategyID.AS05_DEMONSTRATION.value,
                "action_type": "OFFER_PROMPT",
                "policy_rationale": rec_as05.recommendation,
                "patient_dismissible": True,
                "caregiver_override_applied": False,
                "cdr_tiering_prohibited": True,
                "selected_record": rec_as05.to_dict(),
                "timestamp": datetime.now(timezone.utc).isoformat()
            }

        # Medium-tier: AS04 Visual Hint
        rec_as04 = recs[StrategyID.AS04_VISUAL_HINT.value]
        if rec_as04.is_recommended:
            return {
                "approved_strategy": StrategyID.AS04_VISUAL_HINT.value,
                "action_type": "OFFER_PROMPT",
                "policy_rationale": rec_as04.recommendation,
                "patient_dismissible": True,
                "caregiver_override_applied": False,
                "cdr_tiering_prohibited": True,
                "selected_record": rec_as04.to_dict(),
                "timestamp": datetime.now(timezone.utc).isoformat()
            }

        # Low-tier: AS01 Repeat Instruction
        rec_as01 = recs[StrategyID.AS01_REPEAT.value]
        if rec_as01.is_recommended:
            return {
                "approved_strategy": StrategyID.AS01_REPEAT.value,
                "action_type": "OFFER_PROMPT",
                "policy_rationale": rec_as01.recommendation,
                "patient_dismissible": True,
                "caregiver_override_applied": False,
                "cdr_tiering_prohibited": True,
                "selected_record": rec_as01.to_dict(),
                "timestamp": datetime.now(timezone.utc).isoformat()
            }

        # Default: AS10 Independent Completion
        rec_as10 = recs[StrategyID.AS10_INDEPENDENT.value]
        return {
            "approved_strategy": StrategyID.AS10_INDEPENDENT.value,
            "action_type": "SILENT_AUTONOMY",
            "policy_rationale": "Patient proceeding autonomously. Spontaneous cues suppressed to support independent engagement.",
            "patient_dismissible": False,
            "caregiver_override_applied": False,
            "cdr_tiering_prohibited": True,
            "selected_record": rec_as10.to_dict(),
            "timestamp": datetime.now(timezone.utc).isoformat()
        }
