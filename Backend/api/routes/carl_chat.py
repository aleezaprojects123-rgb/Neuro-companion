"""
carl_chat.py - Conversational Companion Router for Carl Fredricksen (Phase 6.5)

Endpoints:
    POST /api/v1/carl/chat
    GET  /api/v1/carl/chat/quick-prompts

Provides supportive, calming, and empathetic conversational replies tailored
for Alzheimer's and dementia patients in the NeuroCompanion Cognitive Garden.
"""

from __future__ import annotations

import logging
import re
from typing import List, Optional
from fastapi import APIRouter, status
from pydantic import BaseModel, Field

logger = logging.getLogger(__name__)

carl_chat_router = APIRouter(prefix="/api/v1/carl", tags=["Carl Companion Dialogue"])


class CarlChatRequest(BaseModel):
    message: str = Field(..., description="User's message or question to Carl")
    patient_id: Optional[str] = Field("P001", description="Identifier of the active patient")
    session_id: Optional[str] = Field("S001", description="Active session identifier")


class CarlChatResponse(BaseModel):
    reply: str = Field(..., description="Carl's speech response")
    emotion: str = Field("warm", description="Carl's emotional state (warm, happy, encouraging, thoughtful)")
    action: str = Field("talk", description="Animation gesture (talk, bow, nod, wave)")
    voice_clip: Optional[str] = Field(None, description="Pre-recorded or synthesized audio clip name")


class QuickPrompt(BaseModel):
    id: str
    label: str
    prompt: str


# Empathetic companion response rules for dementia care
CONVERSATION_RULES = [
    {
        "patterns": [r"how are (you|u)", r"how do you do", r"how('s| is) it going", r"how are things"],
        "reply": "I'm doing just fine, thank you! It's a wonderful day in our garden. How are you feeling today?",
        "emotion": "warm",
        "action": "nod",
    },
    {
        "patterns": [r"^hi\b", r"^hello\b", r"^hey\b", r"good morning", r"good afternoon", r"greetings"],
        "reply": "Hello there! It is so good to see you. Take a deep breath of the fresh garden air with me.",
        "emotion": "happy",
        "action": "wave",
    },
    {
        "patterns": [r"who are you", r"what('s| is) your name", r"tell me about yourself"],
        "reply": "I'm Carl Fredricksen! I'm here to keep you company and enjoy this peaceful garden together.",
        "emotion": "warm",
        "action": "bow",
    },
    {
        "patterns": [r"what (are we|should we) (do|doing|plant)", r"what next", r"what do i do", r"help"],
        "reply": "We are nurturing our little garden! You can water the seedlings at our planting station or simply watch the sparrows in the sky.",
        "emotion": "encouraging",
        "action": "talk",
    },
    {
        "patterns": [r"bird", r"sparrow", r"goose", r"geese", r"fly", r"flying"],
        "reply": "Aren't the sparrows lovely? They take restful breaks on the cherry tree and flutter across the sky every few minutes.",
        "emotion": "happy",
        "action": "talk",
    },
    {
        "patterns": [r"flower", r"plant", r"seedling", r"garden", r"tree"],
        "reply": "The garden is coming along wonderfully. The cherry blossoms and seedlings bring so much peace and color here.",
        "emotion": "thoughtful",
        "action": "talk",
    },
    {
        "patterns": [r"thank", r"thanks", r"appreciate"],
        "reply": "You're very welcome, my dear friend. Being here with you is the highlight of my day.",
        "emotion": "warm",
        "action": "bow",
    },
    {
        "patterns": [r"tired", r"sad", r"confused", r"worried", r"scared", r"lost"],
        "reply": "You are completely safe here in the garden with me. Take all the time you need, and let's just listen to the gentle breeze together.",
        "emotion": "reassuring",
        "action": "nod",
    },
]

DEFAULT_REPLY = (
    "That is wonderful. I'm right here with you, enjoying every moment in our peaceful garden."
)


@carl_chat_router.post(
    "/chat",
    response_model=CarlChatResponse,
    status_code=status.HTTP_200_OK,
    summary="Send a message to Carl and get an empathetic companion response",
)
async def carl_chat(req: CarlChatRequest) -> CarlChatResponse:
    """Processes user input and returns Carl Fredricksen's conversational reply."""
    msg_clean = req.message.strip().lower()
    logger.info("Carl received message: %s from patient: %s", req.message, req.patient_id)

    # Match rules
    for rule in CONVERSATION_RULES:
        for pat in rule["patterns"]:
            if re.search(pat, msg_clean):
                return CarlChatResponse(
                    reply=rule["reply"],
                    emotion=rule["emotion"],
                    action=rule["action"],
                    voice_clip="carl_joyful_greeting",
                )

    # Friendly default fallback
    return CarlChatResponse(
        reply=DEFAULT_REPLY,
        emotion="warm",
        action="talk",
        voice_clip="carl_speech_sample",
    )


@carl_chat_router.get(
    "/chat/quick-prompts",
    response_model=List[QuickPrompt],
    status_code=status.HTTP_200_OK,
    summary="Get recommended one-click quick dialogue prompts for the patient HUD",
)
async def get_quick_prompts() -> List[QuickPrompt]:
    """Returns accessible one-click prompts for elderly and cognitive patients."""
    return [
        QuickPrompt(id="p1", label="💬 Hi Carl, how are you?", prompt="Hi Carl, how are you?"),
        QuickPrompt(id="p2", label="🌱 What are we doing today?", prompt="What are we doing today?"),
        QuickPrompt(id="p3", label="🌸 Tell me about the garden", prompt="Tell me about the garden"),
    ]
