"""
Runtime Request and Response Schemas for NeuroCompanion Phase 4.7.

Defines the stable input and output contracts for the backend assistance runtime.
Ensures clean distinction between approved assistance, patient-initiated assistance,
caregiver-controlled assistance, optional break offers, suppressed recommendations,
neutral/no-action responses, insufficient data, and runtime errors.
"""

import uuid
from dataclasses import dataclass, field
from datetime import datetime, timezone
from enum import Enum
from typing import Any, Dict, List, Optional, Union

from .carl_action import CarlAction
from .schemas import InteractionTelemetryContext


class RuntimeStatus(str, Enum):
    """Execution status of the assistance runtime cycle."""
    APPROVED = "APPROVED"                               # System-suggested assistance approved by policy
    PATIENT_INITIATED = "PATIENT_INITIATED"             # Patient explicitly requested help/repeat
    CAREGIVER_CONTROLLED = "CAREGIVER_CONTROLLED"       # Caregiver lock/preset override active
    OPTIONAL_BREAK = "OPTIONAL_BREAK"                   # Optional break offer presented (never forced)
    SUPPRESSED = "SUPPRESSED"                           # Recommendation suppressed by cooldown or patient dismissal
    NO_ADDITIONAL_ASSISTANCE = "NO_ADDITIONAL_ASSISTANCE"# Patient proceeding well; no companion cue needed
    INSUFFICIENT_DATA = "INSUFFICIENT_DATA"             # Mandatory interaction telemetry missing or empty
    ERROR = "ERROR"                                     # Unexpected internal processing error handled gracefully


@dataclass
class AssistanceRuntimeRequest:
    """
    Standard input contract for the assistance runtime.

    Attributes:
        context: Standard InteractionTelemetryContext or raw dictionary.
        seconds_since_last_prompt: Elapsed seconds since last spontaneous companion cue.
        cdr_score: Explicitly ignored / discarded per safety rule.
        request_id: Optional unique tracking ID (auto-generated if omitted).
        metadata: Optional extensible metadata dictionary.
    """
    context: Union[InteractionTelemetryContext, Dict[str, Any]]
    seconds_since_last_prompt: float = 60.0
    cdr_score: Optional[float] = None
    request_id: str = field(default_factory=lambda: str(uuid.uuid4()))
    metadata: Dict[str, Any] = field(default_factory=dict)


@dataclass
class AssistanceRuntimeResponse:
    """
    Complete, auditable output contract of the NeuroCompanion assistance runtime.
    Supplies all required decision metadata, safety traceability, and Carl action instructions.
    """
    request_id: str
    patient_id: str
    session_id: str
    runtime_status: RuntimeStatus
    decision_id: Optional[str]
    decision_status: Optional[str]
    selected_strategy_id: Optional[str]
    decision_reason: str
    prediction_reference: Optional[Dict[str, Any]]
    policy_rules_applied: List[str]
    caregiver_control_applied: bool
    patient_choice_required: bool
    patient_choice_status: str
    requires_carl_action: bool
    carl_action: Optional[Dict[str, Any]]
    created_at: datetime = field(default_factory=lambda: datetime.now(timezone.utc))

    def to_dict(self) -> Dict[str, Any]:
        """Serializes the response to a JSON-compatible dictionary."""
        return {
            "request_id": self.request_id,
            "patient_id": self.patient_id,
            "session_id": self.session_id,
            "runtime_status": self.runtime_status.value,
            "decision_id": self.decision_id,
            "decision_status": self.decision_status,
            "selected_strategy_id": self.selected_strategy_id,
            "decision_reason": self.decision_reason,
            "prediction_reference": self.prediction_reference,
            "policy_rules_applied": self.policy_rules_applied,
            "caregiver_control_applied": self.caregiver_control_applied,
            "patient_choice_required": self.patient_choice_required,
            "patient_choice_status": self.patient_choice_status,
            "requires_carl_action": self.requires_carl_action,
            "carl_action": self.carl_action,
            "created_at": self.created_at.isoformat(),
        }
