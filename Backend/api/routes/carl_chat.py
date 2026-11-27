"""
carl_chat.py - Conversational Companion Router for Carl Fredricksen (Phase 6.5)

Endpoints:
    POST /api/v1/carl/chat
    POST /api/v1/carl/voice-stt
    GET  /api/v1/carl/chat/quick-prompts

Provides supportive, calming, and empathetic conversational replies tailored
for Alzheimer's and dementia patients in the NeuroCompanion Cognitive Garden.
Now includes automatic voice Speech-to-Text (STT) processing via SpeechRecognition.
"""

from __future__ import annotations

import io
import logging
import re
from typing import List, Optional
from fastapi import APIRouter, File, HTTPException, Request, UploadFile, status
from pydantic import BaseModel, Field
import speech_recognition as sr

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


class VoiceSTTResponse(BaseModel):
    transcription: str = Field(..., description="Transcribed speech from patient's microphone")
    reply: str = Field(..., description="Carl's response")
    emotion: str = Field("warm", description="Emotional tone")
    action: str = Field("talk", description="Gesture/animation to play")
    voice_clip: Optional[str] = Field(None, description="Audio clip to play")


class QuickPrompt(BaseModel):
    id: str
    label: str
    prompt: str


# Empathetic companion response rules for dementia care & open-ended conversation
CONVERSATION_RULES = [
    {
        "patterns": [r"how are (you|u)", r"how do you do", r"how('s| is) it going", r"how are things", r"how have you been"],
        "reply": "I'm doing just fine, thank you! It's a wonderful day in our garden. How are you feeling today?",
        "emotion": "warm",
        "action": "nod",
        "voice_clip": "carl_im_fine",
    },
    {
        "patterns": [r"^hi\b", r"^hello\b", r"^hey\b", r"good morning", r"good afternoon", r"good evening", r"greetings"],
        "reply": "Hello there! It is so good to see you. Take a deep breath of the fresh garden air with me.",
        "emotion": "happy",
        "action": "wave",
        "voice_clip": "carl_hello",
    },
    {
        "patterns": [r"who are you", r"what('s| is) your name", r"tell me about yourself", r"who made you"],
        "reply": "I'm Carl Fredricksen! I'm here to keep you company and enjoy this peaceful garden together.",
        "emotion": "warm",
        "action": "bow",
        "voice_clip": "carl_joyful_greeting",
    },
    {
        "patterns": [r"what (are we|should we) (do|doing|plant)", r"what next", r"what do i do", r"help me", r"instructions"],
        "reply": "We are nurturing our little garden! You can water the seedlings at our planting station or simply watch the sparrows in the sky.",
        "emotion": "encouraging",
        "action": "talk",
        "voice_clip": "carl_gardening",
    },
    {
        "patterns": [r"bird", r"sparrow", r"goose", r"geese", r"fly", r"flying", r"feeder"],
        "reply": "Aren't the sparrows lovely? They take restful breaks on the cherry tree and flutter across the sky every few minutes.",
        "emotion": "happy",
        "action": "talk",
        "voice_clip": "carl_garden_peace",
    },
    {
        "patterns": [r"flower", r"plant", r"seedling", r"garden", r"tree", r"blossom", r"sakura", r"cherry"],
        "reply": "The garden is coming along wonderfully. The cherry blossoms and seedlings bring so much peace and color here.",
        "emotion": "thoughtful",
        "action": "talk",
        "voice_clip": "carl_gardening",
    },
    {
        "patterns": [r"weather", r"sunny", r"sunshine", r"rain", r"cloud", r"sky", r"breeze", r"wind", r"warm"],
        "reply": "The weather is so gentle today. The sunlight through the cherry branches feels calm and comforting.",
        "emotion": "warm",
        "action": "nod",
        "voice_clip": "carl_garden_peace",
    },
    {
        "patterns": [r"ellie", r"balloon", r"adventure", r"paradise", r"house", r"russell", r"kevin"],
        "reply": "Ah, Ellie and I always believed that adventure is out there. But today, sharing this peaceful garden with you is the sweetest adventure of all.",
        "emotion": "warm",
        "action": "bow",
        "voice_clip": "carl_speech_sample",
    },
    {
        "patterns": [r"thank", r"thanks", r"appreciate", r"grateful", r"kind"],
        "reply": "You're very welcome, my dear friend. Being here with you is the highlight of my day.",
        "emotion": "warm",
        "action": "bow",
        "voice_clip": "carl_im_fine",
    },
    {
        "patterns": [r"tired", r"sad", r"confused", r"worried", r"scared", r"lost", r"anxious", r"forgot", r"forget"],
        "reply": "You are completely safe here in the garden with me. Take all the time you need, and let's just listen to the gentle breeze together.",
        "emotion": "reassuring",
        "action": "nod",
        "voice_clip": "carl_garden_peace",
    },
    {
        "patterns": [r"good job", r"great", r"beautiful", r"nice", r"love", r"like this", r"peaceful"],
        "reply": "Thank you! Everything looks brighter when we take our time and appreciate the simple moments together.",
        "emotion": "happy",
        "action": "nod",
        "voice_clip": "carl_hello",
    },
]

DEFAULT_REPLIES = [
    "That is wonderful. I'm right here with you, enjoying every moment in our peaceful garden.",
    "I hear you, my friend. It's so nice to talk with you while we watch the garden grow.",
    "Indeed! Take all the time you'd like. There's no rush in our garden today.",
    "That sounds lovely. Every day spent in good company is a good day.",
]


def determine_carl_response(message: str) -> tuple[str, str, str, str]:
    """Returns (reply, emotion, action, voice_clip) matching the message."""
    msg_clean = message.strip().lower()
    
    for rule in CONVERSATION_RULES:
        for pat in rule["patterns"]:
            if re.search(pat, msg_clean):
                return rule["reply"], rule["emotion"], rule["action"], rule.get("voice_clip", "carl_speech_sample")

    # Dynamic fallback tailored to message length
    import random
    reply = random.choice(DEFAULT_REPLIES)
    return reply, "warm", "talk", "carl_speech_sample"


@carl_chat_router.post(
    "/chat",
    response_model=CarlChatResponse,
    status_code=status.HTTP_200_OK,
    summary="Send a text message to Carl and get an empathetic companion response",
)
async def carl_chat(req: CarlChatRequest) -> CarlChatResponse:
    """Processes user input and returns Carl Fredricksen's conversational reply."""
    logger.info("Carl received text message: %s from patient: %s", req.message, req.patient_id)
    reply, emotion, action, voice_clip = determine_carl_response(req.message)
    return CarlChatResponse(
        reply=reply,
        emotion=emotion,
        action=action,
        voice_clip=voice_clip,
    )


@carl_chat_router.post(
    "/voice-stt",
    response_model=VoiceSTTResponse,
    status_code=status.HTTP_200_OK,
    summary="Receive audio WAV from Unity microphone, transcribe via STT, and respond as Carl",
)
async def carl_voice_stt(
    request: Request,
    file: Optional[UploadFile] = File(None),
) -> VoiceSTTResponse:
    """
    Accepts raw WAV bytes in body or multipart/form-data audio file.
    Transcribes open speech via SpeechRecognition (Google STT) and generates Carl's response.
    """
    audio_bytes: bytes = b""
    if file is not None:
        audio_bytes = await file.read()
    else:
        audio_bytes = await request.body()

    if not audio_bytes or len(audio_bytes) < 44:  # standard WAV header is 44 bytes
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="No valid audio WAV data received",
        )

    logger.info("Received audio for STT (%d bytes)", len(audio_bytes))

    # Transcribe using SpeechRecognition
    recognizer = sr.Recognizer()
    transcription = ""

    try:
        audio_buffer = io.BytesIO(audio_bytes)
        with sr.AudioFile(audio_buffer) as source:
            # Adjust for ambient noise and record
            audio_data = recognizer.record(source)
            transcription = recognizer.recognize_google(audio_data, language="en-US")
            logger.info("Transcribed user voice: '%s'", transcription)
    except sr.UnknownValueError:
        logger.info("Speech was unintelligible or silent")
        transcription = ""
    except sr.RequestError as e:
        logger.error("Could not request results from Speech Recognition service; %s", e)
        transcription = ""
    except Exception as e:
        logger.error("Error processing audio WAV: %s", e)
        transcription = ""

    if not transcription:
        # Graceful polite response when audio was too soft or unintelligible
        return VoiceSTTResponse(
            transcription="(Unclear speech or background sound)",
            reply="I'm right here with you, my friend. Tell me again, what were you thinking?",
            emotion="warm",
            action="nod",
            voice_clip="carl_im_fine",
        )

    # Generate response
    reply, emotion, action, voice_clip = determine_carl_response(transcription)

    return VoiceSTTResponse(
        transcription=transcription,
        reply=reply,
        emotion=emotion,
        action=action,
        voice_clip=voice_clip,
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
