"""
Four-Tier Data Governance Schemas for Future NeuroCompanion Interactions.
Enforces strict segregation between raw client telemetry, patient choices,
caregiver human annotations, and model inference logs.
"""

from dataclasses import dataclass, field
from typing import Optional, List, Dict, Any
from datetime import datetime


@dataclass
class PatientBehavioralTelemetry:
    """Tier 1: Objective Behavioral Telemetry (Direct Client Measurement)."""
    interaction_id: str
    patient_id: str
    session_id: str
    room_id: str                          # orientation_room, reminiscence_corner, cognitive_garden
    task_name: str
    step_index: int
    step_start_timestamp: datetime
    step_end_timestamp: datetime
    pre_action_latency_ms: float
    idle_duration_ms: float
    target_miss_count: int
    repeated_click_count: int
    step_error_count: int
    client_version: str = "3.0.0"


@dataclass
class PatientCueResponse:
    """Tier 2: Explicit Patient Actions & Preferences."""
    interaction_id: str
    patient_id: str
    cue_code: Optional[str]               # AS01-AS09 or None
    cue_offered_timestamp: Optional[datetime]
    patient_requested_hint: bool
    patient_accepted_cue: bool
    patient_dismissed_audio: bool
    time_to_cue_response_ms: Optional[float]
    subsequent_completion_success: bool


@dataclass
class CaregiverSessionAnnotation:
    """Tier 3: Caregiver & Expert Human Ground Truth."""
    annotation_id: str
    session_id: str
    patient_id: str
    annotator_id: str
    annotator_role: str                   # Family caregiver, Certified nurse, Clinician
    annotation_timestamp: datetime
    observed_confusion_level: str         # None, Mild, Moderate, Severe
    assistance_helpfulness_rating: str    # Highly helpful, Appropriate, Unneeded, Distracting
    caregiver_physically_assisted: bool
    ground_truth_assistance_need: Optional[str] # Validated AS01-AS10 ground-truth label


@dataclass
class ModelInferenceRecord:
    """Tier 4: Model-Generated Predictions & Uncertainty (Isolated from Ground Truth)."""
    inference_id: str
    interaction_id: str
    patient_id: str
    model_version: str                    # e.g., v1.0.0
    timestamp: datetime
    predicted_probabilities: Dict[str, float] # Probabilities for AS01-AS09
    predicted_as10_independent: float
    prediction_variance: float            # Jackknife or epistemic uncertainty
    abstention_triggered: bool            # True if model abstains due to high uncertainty
