"""
PolicyDecision -- Output contract for the NeuroCompanion Phase 4.6 Safe Assistance Policy.

Each PolicyDecision is the final, auditable output of the full decision flow:
    InteractionTelemetryContext
        -> AssistancePredictionEngine  (Phase 4.5 candidate recommendations)
        -> AssistancePolicyInterface   (Phase 4.5 safety arbitration)
        -> SafeAssistancePolicy        (Phase 4.6 decision integration)
        -> PolicyDecision              (this contract)

No CDR scores, clinical diagnoses, or fabricated ML labels are used at any point.
"""

import uuid
from dataclasses import dataclass, field
from datetime import datetime, timezone
from enum import Enum
from typing import Any, Dict, List, Optional


class DecisionStatus(str, Enum):
    """The final disposition of an assistance decision cycle."""
    APPROVED = "APPROVED"
    NO_ADDITIONAL_ASSISTANCE = "NO_ADDITIONAL_ASSISTANCE"
    SUPPRESSED_COOLDOWN = "SUPPRESSED_COOLDOWN"
    SUPPRESSED_BY_PATIENT = "SUPPRESSED_BY_PATIENT"
    CAREGIVER_OVERRIDE = "CAREGIVER_OVERRIDE"
    PATIENT_HELP_REQUEST = "PATIENT_HELP_REQUEST"
    INSUFFICIENT_DATA = "INSUFFICIENT_DATA"


class PatientChoiceStatus(str, Enum):
    """Whether patient consent/choice is pending, accepted, rejected, or not applicable."""
    NOT_REQUIRED = "NOT_REQUIRED"
    PENDING = "PENDING"
    ACCEPTED = "ACCEPTED"
    REJECTED = "REJECTED"


@dataclass
class PolicyDecision:
    """
    Auditable, traceable record of a single Phase 4.6 safe assistance decision.

    Fields
    ------
    decision_id : str
        Unique UUID for this decision (auto-generated).
    patient_id : str
        Anonymised patient identifier from the interaction context.
    session_id : str
        Session identifier from the interaction context.
    task_name : str
        Task or activity name at the time of the decision.
    selected_strategy_id : Optional[str]
        The StrategyID value approved for delivery, or None when no assistance is offered.
    decision_status : DecisionStatus
        The final disposition (APPROVED, NO_ADDITIONAL_ASSISTANCE, etc.).
    decision_reason : str
        Human-readable rationale for the decision, suitable for audit logs.
    prediction_reference : Optional[Dict[str, Any]]
        The raw AssistancePredictionRecord.to_dict() for the selected candidate, or None.
    policy_rules_applied : List[str]
        Ordered list of policy rule names evaluated to reach this decision.
    caregiver_control_applied : bool
        True when a caregiver lock or explicit override determined the outcome.
    patient_choice_required : bool
        True when the delivered strategy requires patient acceptance (e.g. AS08 break).
    patient_choice_status : PatientChoiceStatus
        Current state of the patient's choice (defaults to PENDING when choice is required).
    created_at : datetime
        UTC timestamp when this decision was produced.
    """

    patient_id: str
    session_id: str
    task_name: str
    selected_strategy_id: Optional[str]
    decision_status: DecisionStatus
    decision_reason: str
    prediction_reference: Optional[Dict[str, Any]]
    policy_rules_applied: List[str]
    caregiver_control_applied: bool
    patient_choice_required: bool
    patient_choice_status: PatientChoiceStatus

    # Auto-populated
    decision_id: str = field(default_factory=lambda: str(uuid.uuid4()))
    created_at: datetime = field(default_factory=lambda: datetime.now(timezone.utc))

    def to_dict(self) -> Dict[str, Any]:
        """Serialise to a JSON-compatible dict for API responses and audit storage."""
        return {
            "decision_id": self.decision_id,
            "patient_id": self.patient_id,
            "session_id": self.session_id,
            "task_name": self.task_name,
            "selected_strategy_id": self.selected_strategy_id,
            "decision_status": self.decision_status.value,
            "decision_reason": self.decision_reason,
            "prediction_reference": self.prediction_reference,
            "policy_rules_applied": self.policy_rules_applied,
            "caregiver_control_applied": self.caregiver_control_applied,
            "patient_choice_required": self.patient_choice_required,
            "patient_choice_status": self.patient_choice_status.value,
            "created_at": self.created_at.isoformat(),
        }
