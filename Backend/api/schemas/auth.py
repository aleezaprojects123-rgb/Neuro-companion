"""
Authentication and User Profile Schemas for NeuroCompanion.
Supports Caregiver and Patient accounts, secure credential validation,
profile retrieval, and password recovery.
"""

from datetime import datetime, timezone
from enum import Enum
import re
from typing import Any, Dict, List, Optional
from pydantic import BaseModel, ConfigDict, Field, field_validator

EMAIL_REGEX = re.compile(r"^[^@\s]+@[^@\s]+\.[^@\s]+$")


class UserRole(str, Enum):
    """Account roles supported in NeuroCompanion."""
    CAREGIVER = "caregiver"
    PATIENT = "patient"


class UserRegisterRequest(BaseModel):
    """Request payload for user registration."""
    model_config = ConfigDict(str_strip_whitespace=True)

    full_name: str = Field(..., min_length=2, max_length=100, description="Full legal or chosen name.")
    email: str = Field(..., description="Unique email address for the account.")
    password: str = Field(..., min_length=6, max_length=128, description="Account password (min 6 chars).")
    role: UserRole = Field(default=UserRole.CAREGIVER, description="Account role: caregiver or patient.")

    @field_validator("email")
    @classmethod
    def validate_email_format(cls, v: str) -> str:
        v_clean = v.strip().lower()
        if not EMAIL_REGEX.match(v_clean):
            raise ValueError(f"'{v}' is not a valid email address.")
        return v_clean


class UserLoginRequest(BaseModel):
    """Request payload for user login."""
    model_config = ConfigDict(str_strip_whitespace=True)

    email: str = Field(..., description="Registered user email.")
    password: str = Field(..., min_length=1, max_length=128, description="Account password.")

    @field_validator("email")
    @classmethod
    def validate_email_format(cls, v: str) -> str:
        v_clean = v.strip().lower()
        if not EMAIL_REGEX.match(v_clean):
            raise ValueError(f"'{v}' is not a valid email address.")
        return v_clean


class ForgotPasswordRequest(BaseModel):
    """Request payload for password recovery."""
    model_config = ConfigDict(str_strip_whitespace=True)

    email: str = Field(..., description="Registered account email for recovery.")

    @field_validator("email")
    @classmethod
    def validate_email_format(cls, v: str) -> str:
        v_clean = v.strip().lower()
        if not EMAIL_REGEX.match(v_clean):
            raise ValueError(f"'{v}' is not a valid email address.")
        return v_clean


class ForgotPasswordResponse(BaseModel):
    """Response payload for password recovery."""
    status: str = Field(default="ok")
    email: str
    message: str
    recovery_initiated: bool = True
    support_contact: str = "support@neurocompanion.local"


class UserProfileDto(BaseModel):
    """Structured user account profile."""
    user_id: str
    email: str
    full_name: str
    role: UserRole
    profile_picture_url: Optional[str] = None
    patient_association: Optional[str] = None
    accessibility_preferences: Dict[str, Any] = Field(default_factory=dict)
    created_at: str
    last_login: Optional[str] = None


class AuthTokenResponse(BaseModel):
    """Authentication token and session response upon successful sign-in or registration."""
    status: str = "ok"
    token: str
    token_type: str = "bearer"
    expires_in_seconds: int = 86400 * 7  # 7-day session
    user: UserProfileDto


class UserProfileUpdateRequest(BaseModel):
    """Request payload to update account profile."""
    model_config = ConfigDict(str_strip_whitespace=True)

    full_name: Optional[str] = Field(None, min_length=2, max_length=100)
    profile_picture_url: Optional[str] = None
    patient_association: Optional[str] = None
    accessibility_preferences: Optional[Dict[str, Any]] = None


class PatientModeEntryRequest(BaseModel):
    """Request payload for direct Patient Mode entry."""
    model_config = ConfigDict(str_strip_whitespace=True)

    patient_id: Optional[str] = Field(default="patient_001", description="Patient identifier.")


class PatientModeEntryResponse(BaseModel):
    """Response returned when entering Patient Mode."""
    status: str = "ok"
    mode: str = "PatientMode"
    patient_id: str
    active_support_tier: Optional[str] = None
    carl_instruction_style: str = "concise"
    allow_auto_hints: bool = True
    hesitation_threshold_seconds: float = 25.0
    speech_rate_multiplier: float = 1.0
    message: str = "Patient Mode active. Initial accessible configuration loaded."
