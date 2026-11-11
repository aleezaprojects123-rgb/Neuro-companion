"""
main.py - FastAPI application entry-point for NeuroCompanion.

API Contract Version: v1  (P2A-30)
API version prefix:   /api/v1/

Practical OASIS-1 3-Class CDR Classification Model:
  - Class 0: CDR 0 (No dementia pattern)
  - Class 1: CDR 0.5 (Very mild dementia pattern)
  - Class 2: CDR 1+ (Mild to moderate dementia pattern)

Versioned Endpoints (/api/v1/):
  - GET  /api/v1/            – Root project info
  - GET  /api/v1/health      – Health and model status check
  - POST /api/v1/predict     – Run prediction on practical input features
  - POST /api/v1/telemetry/events               – Ingest Unity telemetry
  - POST /api/v1/profiles/onboard               – Onboard new patient
  - POST /api/v1/profiles/support               – Create/update support profile
  - GET  /api/v1/profiles/support/{patient_id}  – Retrieve support profile
  - GET  /api/v1/profiles/interaction/{patient_id} – Retrieve interaction profile
  - GET  /api/v1/profiles/recommendations/{patient_id} – Retrieve recommendations
  - POST /api/v1/carl/assistance                – Immediate Carl assistance
  - POST /api/v1/profiles/override              – Caregiver override and lock

Legacy Endpoints (preserved for backward compatibility):
  - GET  /        – Same as /api/v1/
  - GET  /health  – Same as /api/v1/health
  - POST /predict – Same as /api/v1/predict
  - All telemetry/profiles/carl routes remain at their original paths

Run:
    cd Backend
    uvicorn main:app --reload
"""

from __future__ import annotations

import logging
import sys
from contextlib import asynccontextmanager
from pathlib import Path
from typing import Any

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, ConfigDict, Field

# Ensure Backend/ is in sys.path
sys.path.insert(0, str(Path(__file__).resolve().parent))

from model.config import MODEL_VERSION
from model.predict import _load_artefacts, is_model_loaded, predict
from telemetry.routes import router as telemetry_router

# ---------------------------------------------------------------------------
# API Contract constants (P2A-30)
# ---------------------------------------------------------------------------
API_VERSION: str = "v1"
API_PREFIX: str = f"/api/{API_VERSION}"

logging.basicConfig(level=logging.INFO, format="%(levelname)s: %(message)s")
logger = logging.getLogger(__name__)


# ---------------------------------------------------------------------------
# Lifespan – Load model pipeline once at startup
# ---------------------------------------------------------------------------

@asynccontextmanager
async def lifespan(app: FastAPI):
    """Load the trained pipeline and schema into memory at startup."""
    logger.info("Initializing NeuroCompanion practical model pipeline...")
    try:
        _load_artefacts()
        logger.info("Pipeline loaded successfully (version: %s).", MODEL_VERSION)
    except Exception as exc:
        logger.error("Pipeline loading failed at startup: %s", exc)
    yield
    logger.info("NeuroCompanion backend shutting down.")


# ---------------------------------------------------------------------------
# FastAPI Application
# ---------------------------------------------------------------------------

app = FastAPI(
    title="NeuroCompanion API",
    description=(
        "Educational machine-learning backend trained on OASIS-1 practical features. "
        "Predicts 3 experimental CDR categories. "
        "**NOT a medical diagnostic tool.**\n\n"
        "**API Contract Version:** v1 (P2A-30)\n"
        "**Versioned prefix:** `/api/v1/`\n"
        "**Legacy routes:** preserved at original paths for backward compatibility."
    ),
    version=f"{API_VERSION}/{MODEL_VERSION}",
    lifespan=lifespan,
)

# Include telemetry routes at BOTH legacy and versioned prefixes
# Legacy prefix (backward-compatible – no prefix, as originally mounted)
app.include_router(telemetry_router)
# Versioned prefix (new Unity integration target)
app.include_router(telemetry_router, prefix=API_PREFIX)


# ---------------------------------------------------------------------------
# Pydantic Schemas
# ---------------------------------------------------------------------------

class PredictRequest(BaseModel):
    """
    Input schema for practical demographic and cognitive screener features.

    Features:
      - Age: Participant age in years (30 to 105)
      - M/F: Biological sex ('M' or 'F')
      - Educ: Education code (1 to 5)
      - MMSE: Mini-Mental State Examination score (0 to 30)
    """
    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    Age: float = Field(..., ge=30.0, le=105.0, description="Participant age in years (30-105)")
    M_F: str = Field(..., alias="M/F", pattern="^[MF]$", description="Biological sex: 'M' or 'F'")
    Educ: float = Field(..., ge=1.0, le=5.0, description="Education code (1 to 5)")
    MMSE: float = Field(..., ge=0.0, le=30.0, description="Mini-Mental State Examination score (0-30)")


class PredictResponse(BaseModel):
    model_version: str
    predicted_class: int
    predicted_label: str
    class_probabilities: dict[str, float]
    disclaimer: str


class HealthResponse(BaseModel):
    status: str
    model_loaded: bool
    model_version: str


# ---------------------------------------------------------------------------
# Endpoints
# ---------------------------------------------------------------------------

@app.get("/", summary="Root info")
async def root() -> dict[str, str]:
    """Returns project identity and entry-point URLs."""
    return {
        "project": "NeuroCompanion",
        "api_version": API_VERSION,
        "task": "3-class CDR experimental classification from OASIS-1 practical features",
        "status": "running",
        "docs": "/docs",
        "health": "/health",
        "versioned_health": f"{API_PREFIX}/health",
    }


@app.get(f"{API_PREFIX}/", summary="Root info (v1)", include_in_schema=False)
async def root_v1() -> dict[str, str]:
    """Versioned alias for root info."""
    return await root()


@app.get("/health", response_model=HealthResponse, summary="Health check")
async def health() -> HealthResponse:
    """Returns application status and indicates whether the ML model is ready."""
    return HealthResponse(
        status="ok",
        model_loaded=is_model_loaded(),
        model_version=MODEL_VERSION,
    )


@app.get(f"{API_PREFIX}/health", response_model=HealthResponse, summary="Health check (v1)")
async def health_v1() -> HealthResponse:
    """Versioned alias: GET /api/v1/health — model readiness check."""
    return await health()


@app.post("/predict", response_model=PredictResponse, summary="Run 3-class prediction")
async def predict_endpoint(request: PredictRequest) -> PredictResponse:
    """
    Predict 3 experimental CDR categories:
      - 0: CDR 0 (No dementia pattern)
      - 1: CDR 0.5 (Very mild dementia pattern)
      - 2: CDR 1+ (Mild to moderate dementia pattern)

    Requires a valid MMSE score from an administered assessment.
    Result is NOT automatically mapped to any support tier.
    """
    if not is_model_loaded():
        raise HTTPException(
            status_code=503,
            detail="Model pipeline is not loaded. Please ensure training completed successfully.",
        )

    # Convert request to dictionary matching feature names (with 'M/F')
    input_dict = {
        "Age": request.Age,
        "M/F": request.M_F,
        "Educ": request.Educ,
        "MMSE": request.MMSE,
    }

    try:
        result = predict(input_dict)
    except ValueError as exc:
        raise HTTPException(status_code=422, detail=str(exc)) from exc
    except Exception as exc:
        logger.error("Prediction error: %s", exc)
        raise HTTPException(status_code=500, detail="Internal inference failure.") from exc

    return PredictResponse(**result)


@app.post(
    f"{API_PREFIX}/predict",
    response_model=PredictResponse,
    summary="Run 3-class prediction (v1)",
)
async def predict_endpoint_v1(request: PredictRequest) -> PredictResponse:
    """Versioned alias: POST /api/v1/predict — same behaviour as /predict."""
    return await predict_endpoint(request)


# ---------------------------------------------------------------------------
# Dev runner
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    import uvicorn
    uvicorn.run("main:app", host="127.0.0.1", port=8000, reload=True)
