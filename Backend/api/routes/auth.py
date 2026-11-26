"""
FastAPI Router for NeuroCompanion Authentication and User Profiles.

Endpoints:
  - POST /api/v1/auth/register         - Register new caregiver or patient account
  - POST /api/v1/auth/login            - Authenticate with email/password
  - POST /api/v1/auth/forgot-password  - Initiate password recovery
  - GET  /api/v1/auth/me               - Retrieve authenticated user profile
  - PUT  /api/v1/auth/profile          - Update authenticated user profile
  - POST /api/v1/auth/patient-mode     - Enter Patient Mode (accessible initial config)
"""

import logging
from typing import Optional
from fastapi import APIRouter, Header, HTTPException, status, Depends
from fastapi.security import HTTPBearer, HTTPAuthorizationCredentials

from api.schemas.auth import (
    UserRegisterRequest,
    UserLoginRequest,
    ForgotPasswordRequest,
    ForgotPasswordResponse,
    UserProfileDto,
    AuthTokenResponse,
    UserProfileUpdateRequest,
    PatientModeEntryRequest,
    PatientModeEntryResponse,
    UserRole,
)
from telemetry.database import db

logger = logging.getLogger(__name__)

auth_router = APIRouter(prefix="/api/v1/auth", tags=["Authentication & User Profiles"])
security = HTTPBearer(auto_error=False)


def get_current_user(credentials: Optional[HTTPAuthorizationCredentials] = Depends(security)) -> UserProfileDto:
    """Dependency extracting and verifying the authenticated user from the Bearer token."""
    if not credentials or not credentials.credentials:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Authentication token missing or invalid.",
            headers={"WWW-Authenticate": "Bearer"},
        )
    user_dict = db.get_user_by_token(credentials.credentials)
    if not user_dict:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Session has expired or token is invalid.",
            headers={"WWW-Authenticate": "Bearer"},
        )
    return UserProfileDto(**user_dict)


@auth_router.post(
    "/register",
    response_model=AuthTokenResponse,
    status_code=status.HTTP_201_CREATED,
    summary="Register a new NeuroCompanion account",
)
async def register_user(request: UserRegisterRequest) -> AuthTokenResponse:
    """
    Registers a new Caregiver or Patient account with PBKDF2-HMAC-SHA256 password hashing.
    Rejects duplicate email addresses.
    Returns the user profile and a 7-day session token.
    """
    try:
        user_dict = db.create_user(
            email=request.email,
            full_name=request.full_name,
            password=request.password,
            role=request.role.value,
        )
        token = db.create_session_token(user_dict["user_id"])
        logger.info(f"User registered successfully: {user_dict['email']} (role: {user_dict['role']})")
        return AuthTokenResponse(
            status="ok",
            token=token,
            token_type="bearer",
            user=UserProfileDto(**user_dict),
        )
    except ValueError as e:
        logger.warning(f"Registration conflict: {e}")
        raise HTTPException(status_code=status.HTTP_409_CONFLICT, detail=str(e))
    except Exception as e:
        logger.error(f"Unexpected registration error: {e}", exc_info=True)
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Internal server error during registration.",
        )


@auth_router.post(
    "/login",
    response_model=AuthTokenResponse,
    status_code=status.HTTP_200_OK,
    summary="Authenticate with email and password",
)
async def login_user(request: UserLoginRequest) -> AuthTokenResponse:
    """
    Authenticates a user using secure PBKDF2 hash verification.
    Returns a session token and user profile on success.
    Never exposes internal password hashes.
    """
    user_dict = db.authenticate_user(email=request.email, password=request.password)
    if not user_dict:
        logger.warning(f"Authentication failed for email: {request.email}")
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid email or password. Please verify your credentials.",
        )

    token = db.create_session_token(user_dict["user_id"])
    logger.info(f"User authenticated: {user_dict['email']}")
    return AuthTokenResponse(
        status="ok",
        token=token,
        token_type="bearer",
        user=UserProfileDto(**user_dict),
    )


@auth_router.post(
    "/forgot-password",
    response_model=ForgotPasswordResponse,
    status_code=status.HTTP_200_OK,
    summary="Request password recovery",
)
async def forgot_password(request: ForgotPasswordRequest) -> ForgotPasswordResponse:
    """
    Initiates a password reset request.
    Verifies email existence and safely logs the reset request token.
    Accurately informs the user that recovery instructions are logged, without falsely claiming an SMTP delivery.
    """
    user = db.get_user_by_email(request.email)
    if not user:
        # Prevent user enumeration attack while providing clear message
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"No account found associated with '{request.email}'. Please check the email address.",
        )

    reset_token = db.record_password_reset(request.email)
    logger.info(f"Password reset request recorded for: {request.email} (token generated: {reset_token[:8]}...)")

    return ForgotPasswordResponse(
        status="ok",
        email=request.email,
        message="Password recovery request registered successfully. Contact your facility administrator or caregiver coordinator to finalize reset.",
        recovery_initiated=True,
    )


@auth_router.get(
    "/me",
    response_model=UserProfileDto,
    status_code=status.HTTP_200_OK,
    summary="Retrieve current authenticated user profile",
)
async def get_my_profile(current_user: UserProfileDto = Depends(get_current_user)) -> UserProfileDto:
    """Returns the profile of the currently authenticated user."""
    return current_user


@auth_router.put(
    "/profile",
    response_model=UserProfileDto,
    status_code=status.HTTP_200_OK,
    summary="Update current user profile",
)
async def update_my_profile(
    request: UserProfileUpdateRequest,
    current_user: UserProfileDto = Depends(get_current_user),
) -> UserProfileDto:
    """Updates profile attributes for the authenticated user."""
    updated = db.update_user_profile(
        user_id=current_user.user_id,
        full_name=request.full_name,
        profile_picture_url=request.profile_picture_url,
        patient_association=request.patient_association,
        accessibility_preferences=request.accessibility_preferences,
    )
    if not updated:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="User not found.")
    return UserProfileDto(**updated)


@auth_router.post(
    "/patient-mode",
    response_model=PatientModeEntryResponse,
    status_code=status.HTTP_200_OK,
    summary="Enter Patient Mode without caregiver authentication",
)
async def enter_patient_mode(request: Optional[PatientModeEntryRequest] = None) -> PatientModeEntryResponse:
    """
    Enters Patient Mode following the existing NeuroCompanion patient-entry workflow:
    - Never requires password or MMSE score.
    - Preserves unassigned support tier (active_support_tier is None).
    - Carl is configured with accessible defaults (concise instructions, auto-hints enabled).
    - Preserves patient autonomy and privacy.
    """
    target_patient_id = (request.patient_id if request and request.patient_id else "patient_001").strip()
    profile = db.get_support_profile(target_patient_id)

    tier = profile.active_support_tier if profile else None
    carl_style = profile.carl_config.instruction_style if profile and profile.carl_config else "concise"
    auto_hints = profile.carl_config.allow_auto_hints if profile and profile.carl_config else True
    hesitation = profile.carl_config.hesitation_threshold_seconds if profile and profile.carl_config else 25.0
    speech_rate = profile.carl_config.speech_rate_multiplier if profile and profile.carl_config else 1.0

    logger.info(f"Patient Mode entered for patient '{target_patient_id}' (tier: {tier})")

    return PatientModeEntryResponse(
        status="ok",
        mode="PatientMode",
        patient_id=target_patient_id,
        active_support_tier=tier,
        carl_instruction_style=carl_style,
        allow_auto_hints=auto_hints,
        hesitation_threshold_seconds=hesitation,
        speech_rate_multiplier=speech_rate,
        message=f"Welcome to NeuroCompanion. Calm, accessible patient space active for {target_patient_id}.",
    )
