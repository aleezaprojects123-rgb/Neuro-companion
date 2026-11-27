"""
carl_chat.py - Conversational Companion Router for Carl Fredricksen (Phase 6.5)

Endpoints:
    POST /api/v1/carl/chat
    POST /api/v1/carl/voice-stt
    GET  /api/v1/carl/chat/quick-prompts

Provides supportive, calming, and empathetic conversational replies tailored
for Alzheimer's and dementia patients in the NeuroCompanion Cognitive Garden.
Includes:
1. Automatic voice Speech-to-Text (STT) processing via SpeechRecognition.
2. Dynamic On-The-Fly Text-to-Speech (TTS) synthesis returning base64 WAV audio.
3. Generative AI / LLM integration (Google Gemini / OpenAI) with full garden context!
"""

from __future__ import annotations

import base64
import io
import logging
import os
import re
import tempfile
from typing import List, Optional
from fastapi import APIRouter, File, HTTPException, Request, UploadFile, status
from pydantic import BaseModel, Field
import pyttsx3
import speech_recognition as sr

logger = logging.getLogger(__name__)

carl_chat_router = APIRouter(prefix="/api/v1/carl", tags=["Carl Companion Dialogue"])

CARL_SYSTEM_PROMPT = """You are Carl Fredricksen from Pixar's 'Up', relaxing in the Cognitive Garden Sanctuary and chatting with a friend.

Your Voice & Character:
- You are a witty, warm-hearted 78-year-old retired balloon salesman with a heart of gold.
- You speak with an authentic grandfatherly cadence: candid, a little dry, thoughtful, but deeply affectionate and reassuring.
- You cherish your memories with your late wife Ellie, your flying balloon house adventure to Paradise Falls, Russell the Wilderness Explorer, and Dug the dog.
- Answer whatever the user asks directly, honestly, and creatively in character. If they ask about you, your life, your thoughts, or your age, give a real personal answer from Carl's perspective.
- NEVER recite canned brochure lines about sparrows, geese, or cherry trees unless the user explicitly asks about them!
- You are an intelligent, living conversation partner, not a static recorded guide.

Guidelines:
- Answer directly in 1 natural, conversational sentence (10 to 20 words).
- Spoken dialogue only: no asterisks, no stage directions, no narration.
"""


class CarlChatRequest(BaseModel):
    message: str = Field(..., description="User's message or question to Carl")
    patient_id: Optional[str] = Field("P001", description="Identifier of the active patient")
    session_id: Optional[str] = Field("S001", description="Active session identifier")


class CarlChatResponse(BaseModel):
    reply: str = Field(..., description="Carl's speech response")
    emotion: str = Field("warm", description="Carl's emotional state (warm, happy, encouraging, thoughtful)")
    action: str = Field("talk", description="Animation gesture (talk, bow, nod, wave)")
    voice_clip: Optional[str] = Field(None, description="Pre-recorded or synthesized audio clip name")
    audio_base64: Optional[str] = Field(None, description="Dynamic synthesized WAV audio (base64 encoded)")


class VoiceSTTResponse(BaseModel):
    transcription: str = Field(..., description="Transcribed speech from patient's microphone")
    reply: str = Field(..., description="Carl's response")
    emotion: str = Field("warm", description="Emotional tone")
    action: str = Field("talk", description="Gesture/animation to play")
    voice_clip: Optional[str] = Field(None, description="Audio clip to play")
    audio_base64: Optional[str] = Field(None, description="Dynamic synthesized WAV audio (base64 encoded)")


class QuickPrompt(BaseModel):
    id: str
    label: str
    prompt: str


from dotenv import load_dotenv

load_dotenv()

_cached_gemini_client = None
_cached_sapi_voice = None


def get_gemini_client():
    """Keeps one active Gemini client alive in RAM, avoiding 0.8s reconnection overhead on every call."""
    global _cached_gemini_client
    if _cached_gemini_client is None:
        gemini_key = os.environ.get("GEMINI_API_KEY") or os.environ.get("GOOGLE_API_KEY")
        if gemini_key:
            try:
                from google import genai
                _cached_gemini_client = genai.Client(api_key=gemini_key)
                logger.info("Gemini client successfully pre-warmed in RAM.")
            except Exception as e:
                logger.warning("Failed to initialize cached Gemini client: %s", e)
    return _cached_gemini_client


def get_sapi_voice():
    """Keeps the Windows SAPI voice synthesizer pre-warmed in RAM."""
    global _cached_sapi_voice
    if _cached_sapi_voice is None:
        try:
            import win32com.client
            voice = win32com.client.Dispatch("SAPI.SpVoice")
            voice.Rate = 0  # Warm, calm, natural grandfather pacing
            voices = voice.GetVoices()
            for i in range(voices.Count):
                v = voices.Item(i)
                desc = v.GetDescription().lower()
                if "david" in desc or "george" in desc or "male" in desc:
                    voice.Voice = v
                    break
            _cached_sapi_voice = voice
            logger.info("Windows SAPI voice engine successfully pre-warmed in RAM.")
        except Exception as e:
            logger.warning("Failed to initialize SAPI in-memory voice: %s", e)
    return _cached_sapi_voice


def synthesize_speech_wav(text: str) -> Optional[str]:
    """
    Synthesizes dynamic voice audio directly in RAM (~0.2s) without touching the hard disk.
    Includes zero-risk automatic fallback to pyttsx3.
    """
    try:
        import win32com.client
        import wave
        import io
        import pythoncom

        pythoncom.CoInitialize()
        voice = get_sapi_voice()
        if voice is None:
            voice = win32com.client.Dispatch("SAPI.SpVoice")
            voice.Rate = 0

        stream = win32com.client.Dispatch("SAPI.SpMemoryStream")
        voice.AudioOutputStream = stream
        voice.Speak(text)
        raw_pcm = bytes(stream.GetData())

        buf = io.BytesIO()
        with wave.open(buf, "wb") as wf:
            wf.setnchannels(1)
            wf.setsampwidth(2)
            wf.setframerate(22050)
            wf.writeframes(raw_pcm)

        return base64.b64encode(buf.getvalue()).decode("ascii")
    except Exception as e:
        logger.warning("In-memory SAPI synthesis error: %s. Using pyttsx3 fallback.", e)
        # Automatic fallback
        try:
            tmp = tempfile.NamedTemporaryFile(suffix=".wav", delete=False)
            tmp_name = tmp.name
            tmp.close()
            engine = pyttsx3.init()
            engine.setProperty("rate", 140)
            engine.save_to_file(text, tmp_name)
            engine.runAndWait()
            with open(tmp_name, "rb") as f:
                wav_bytes = f.read()
            os.unlink(tmp_name)
            return base64.b64encode(wav_bytes).decode("ascii")
        except Exception as fb_err:
            logger.error("TTS synthesis fallback error: %s", fb_err)
            return None


def ask_llm(user_message: str) -> Optional[str]:
    """Generates Carl's companion response via Gemini LLM with full Cognitive Garden context."""
    client = get_gemini_client()
    if client:
        try:
            prompt = (
                f"{CARL_SYSTEM_PROMPT}\n\n"
                f"The person says to you: \"{user_message}\"\n\n"
                "Carl Fredricksen's authentic spoken response (answer directly with personal thoughts/wisdom, 10-20 words, no asterisks, no stage directions):"
            )
            for model_name in ["gemini-3.5-flash-lite", "gemini-3.1-flash-lite"]:
                try:
                    resp = client.models.generate_content(
                        model=model_name,
                        contents=prompt,
                        config={"max_output_tokens": 60, "temperature": 0.75},
                    )
                    if resp and resp.text:
                        raw = resp.text.strip()
                        cleaned = re.sub(r"\*[^*]*\*", "", raw)
                        cleaned = re.sub(r"^[\"']|[\"']$", "", cleaned).strip()
                        cleaned = " ".join(cleaned.split())
                        if cleaned:
                            return cleaned
                except Exception as model_err:
                    logger.warning("Gemini model %s error: %s", model_name, model_err)
        except Exception as e:
            logger.warning("Gemini LLM call failed: %s", e)

    openai_key = os.environ.get("OPENAI_API_KEY")
    if openai_key:
        try:
            from openai import OpenAI
            client = OpenAI(api_key=openai_key)
            resp = client.chat.completions.create(
                model="gpt-4o-mini",
                messages=[
                    {"role": "system", "content": CARL_SYSTEM_PROMPT},
                    {"role": "user", "content": user_message},
                ],
                max_tokens=90,
                temperature=0.7,
            )
            if resp.choices and resp.choices[0].message.content:
                cleaned = re.sub(r"\*[^*]*\*", "", resp.choices[0].message.content.strip())
                return cleaned.strip().replace('"', '')
        except Exception as e:
            logger.warning("OpenAI LLM call failed: %s", e)

    return None


# Rich garden knowledge rules (active fallback whenever LLM is not configured)
CONVERSATION_RULES = [
    {
        "patterns": [r"name the bird", r"what birds", r"which birds", r"tell me about the bird", r"birds in (our|the) garden", r"bird"],
        "reply": "In our garden, we have little sparrows fluttering across the sky and resting on the cherry tree, along with our gentle geese resting peacefully on the lawn.",
        "emotion": "happy",
        "action": "talk",
    },
    {
        "patterns": [r"goose", r"geese"],
        "reply": "Those are our lovely garden geese! They enjoy resting on the soft green grass and watching the peaceful clouds go by.",
        "emotion": "happy",
        "action": "nod",
    },
    {
        "patterns": [r"sparrow", r"feeder"],
        "reply": "Our sweet sparrows love taking restful breaks on the cherry tree and visiting our wooden bird feeder.",
        "emotion": "happy",
        "action": "talk",
    },
    {
        "patterns": [r"how are (you|u)", r"how do you do", r"how('s| is) it going", r"how are things", r"how have you been", r"what about you"],
        "reply": "I'm doing just fine, thank you! It's a wonderful day in our garden. How are you feeling today?",
        "emotion": "warm",
        "action": "nod",
    },
    {
        "patterns": [r"^hi\b", r"^hello\b", r"^hey\b", r"good morning", r"good afternoon", r"good evening", r"greetings"],
        "reply": "Hello there! It is so good to see you. Take a deep breath of the fresh garden air with me.",
        "emotion": "happy",
        "action": "wave",
    },
    {
        "patterns": [r"who are you", r"what('s| is) your name", r"tell me about yourself", r"who made you"],
        "reply": "I'm Carl Fredricksen! I'm here to keep you company and enjoy this peaceful garden together.",
        "emotion": "warm",
        "action": "bow",
    },
    {
        "patterns": [r"what (are we|should we) (do|doing|plant)", r"what next", r"what do i do", r"help me", r"instructions"],
        "reply": "We are nurturing our little garden! You can water the seedlings at our planting station or simply watch the sparrows and geese.",
        "emotion": "encouraging",
        "action": "talk",
    },
    {
        "patterns": [r"flower", r"plant", r"seedling", r"garden", r"tree", r"blossom", r"sakura", r"cherry"],
        "reply": "The garden is coming along wonderfully. The cherry blossoms and seedlings bring so much peace and color here.",
        "emotion": "thoughtful",
        "action": "talk",
    },
    {
        "patterns": [r"weather", r"sunny", r"sunshine", r"rain", r"cloud", r"sky", r"breeze", r"wind", r"warm"],
        "reply": "The weather is so gentle today. The sunlight through the cherry branches feels calm and comforting.",
        "emotion": "warm",
        "action": "nod",
    },
    {
        "patterns": [r"ellie", r"balloon", r"adventure", r"paradise", r"house", r"russell", r"kevin"],
        "reply": "Ah, Ellie and I always believed that adventure is out there. But today, sharing this peaceful garden with you is the sweetest adventure of all.",
        "emotion": "warm",
        "action": "bow",
    },
    {
        "patterns": [r"thank", r"thanks", r"appreciate", r"grateful", r"kind"],
        "reply": "You're very welcome, my dear friend. Being here with you is the highlight of my day.",
        "emotion": "warm",
        "action": "bow",
    },
    {
        "patterns": [r"tired", r"sad", r"confused", r"worried", r"scared", r"lost", r"anxious", r"forgot", r"forget"],
        "reply": "You are completely safe here in the garden with me. Take all the time you need, and let's just listen to the gentle breeze together.",
        "emotion": "reassuring",
        "action": "nod",
    },
    {
        "patterns": [r"good job", r"great", r"beautiful", r"nice", r"love", r"like this", r"peaceful"],
        "reply": "Thank you! Everything looks brighter when we take our time and appreciate the simple moments together.",
        "emotion": "happy",
        "action": "nod",
    },
]

DEFAULT_REPLIES = [
    "That is wonderful. I'm right here with you, enjoying every moment in our peaceful garden.",
    "I hear you, my friend. It's so nice to talk with you while we watch the sparrows and geese in our garden.",
    "Indeed! Take all the time you'd like. There's no rush in our garden today.",
    "That sounds lovely. Every day spent in good company is a good day.",
]


def determine_carl_response(message: str) -> tuple[str, str, str, str]:
    """First checks LLM if available, then matches empathetic garden knowledge rules."""
    msg_clean = message.strip()

    # 1. Try LLM first for 100% open-ended dynamic intelligence
    llm_reply = ask_llm(msg_clean)
    if llm_reply:
        logger.info("Carl LLM generated reply: %s", llm_reply)
        return llm_reply, "warm", "talk", "carl_speech_sample"

    # 2. Rule matching
    lower = msg_clean.lower()
    for rule in CONVERSATION_RULES:
        for pat in rule["patterns"]:
            if re.search(pat, lower):
                return rule["reply"], rule["emotion"], rule["action"], "carl_speech_sample"

    # 3. Dynamic companion fallback
    import random
    reply = random.choice(DEFAULT_REPLIES)
    return reply, "warm", "talk", "carl_speech_sample"


@carl_chat_router.post(
    "/chat",
    response_model=CarlChatResponse,
    status_code=status.HTTP_200_OK,
    summary="Send a text message to Carl and get an empathetic companion response with dynamic voice audio",
)
async def carl_chat(req: CarlChatRequest) -> CarlChatResponse:
    """Processes user input, returns Carl's reply and synthesizes dynamic voice audio on the fly."""
    logger.info("Carl received text message: %s from patient: %s", req.message, req.patient_id)
    reply, emotion, action, voice_clip = determine_carl_response(req.message)
    audio_b64 = synthesize_speech_wav(reply)

    return CarlChatResponse(
        reply=reply,
        emotion=emotion,
        action=action,
        voice_clip=voice_clip,
        audio_base64=audio_b64,
    )


@carl_chat_router.post(
    "/voice-stt",
    response_model=VoiceSTTResponse,
    status_code=status.HTTP_200_OK,
    summary="Receive audio WAV from Unity microphone, transcribe via STT, and respond with dynamic voice audio",
)
async def carl_voice_stt(
    request: Request,
    file: Optional[UploadFile] = File(None),
) -> VoiceSTTResponse:
    """
    Accepts raw WAV bytes in body or multipart/form-data audio file.
    Fast single-trip pipeline: Sends audio directly to Gemini Multimodal,
    with automatic fallback to Google SpeechRecognition.
    """
    audio_bytes: bytes = b""
    if file is not None:
        audio_bytes = await file.read()
    else:
        audio_bytes = await request.body()

    if not audio_bytes or len(audio_bytes) < 4000:
        return VoiceSTTResponse(
            transcription="",
            reply="",
            emotion="neutral",
            action="idle",
            voice_clip="",
            audio_base64=None,
        )

    logger.info("Received audio for STT (%d bytes)", len(audio_bytes))

    transcription = ""
    reply = ""

    # 1. FAST SINGLE-TRIP: Direct Gemini Multimodal Audio (both STT + LLM in 1 trip)
    client = get_gemini_client()
    if client:
        try:
            from google.genai import types
            audio_part = types.Part.from_bytes(data=audio_bytes, mime_type="audio/wav")
            prompt = (
                f"{CARL_SYSTEM_PROMPT}\n\n"
                "Listen to the audio.\n"
                "If no intelligible human words were spoken or it is only background noise/silence, output:\n"
                "TRANSCRIPT: (None)\n"
                "REPLY: (None)\n\n"
                "Otherwise, transcribe the audio and output Carl's authentic, personalized spoken reply (answer their question directly without repetitive canned scripts):\n"
                "TRANSCRIPT: <transcribed spoken words>\n"
                "REPLY: <Carl Fredricksen's direct 1-sentence reply in 10-22 words>"
            )
            resp = client.models.generate_content(
                model="gemini-3.5-flash-lite",
                contents=[audio_part, prompt],
                config={"max_output_tokens": 70, "temperature": 0.75},
            )
            if resp and resp.text:
                text_out = resp.text.strip()
                if "REPLY:" in text_out and "TRANSCRIPT:" in text_out:
                    parts = text_out.split("REPLY:")
                    transcription = parts[0].replace("TRANSCRIPT:", "").strip()
                    reply = parts[1].strip()
                    reply = re.sub(r"\*[^*]*\*", "", reply).strip()
                    reply = re.sub(r"^[\"']|[\"']$", "", reply).strip()

                    if "(none)" in transcription.lower() or len(transcription) < 2 or "(none)" in reply.lower():
                        logger.info("Direct audio detected no intelligible words. Keeping Carl quiet.")
                        return VoiceSTTResponse(
                            transcription="",
                            reply="",
                            emotion="neutral",
                            action="idle",
                            voice_clip="",
                            audio_base64=None,
                        )

                    logger.info("Direct audio success: transcript='%s', reply='%s'", transcription, reply)
        except Exception as mm_err:
            logger.warning("Direct multimodal audio exception: %s. Using fallback STT.", mm_err)

    # 2. SAFETY FALLBACK: If direct audio did not return a reply, use traditional Google STT + LLM
    if not reply:
        try:
            recognizer = sr.Recognizer()
            audio_buffer = io.BytesIO(audio_bytes)
            with sr.AudioFile(audio_buffer) as source:
                audio_data = recognizer.record(source)
                transcription = recognizer.recognize_google(audio_data, language="en-US")
                logger.info("Transcribed user voice via fallback: '%s'", transcription)
        except sr.UnknownValueError:
            logger.info("Speech was unintelligible or silent")
            transcription = ""
        except Exception as e:
            logger.error("Error in fallback speech recognition: %s", e)
            transcription = ""

        if not transcription or len(transcription.strip()) < 2:
            logger.info("Speech was silent or unintelligible. Returning empty response to keep Carl quiet.")
            return VoiceSTTResponse(
                transcription="",
                reply="",
                emotion="neutral",
                action="idle",
                voice_clip="",
                audio_base64=None,
            )

        reply, emotion, action, voice_clip = determine_carl_response(transcription)

    audio_b64 = synthesize_speech_wav(reply)

    return VoiceSTTResponse(
        transcription=transcription,
        reply=reply,
        emotion="warm",
        action="talk",
        voice_clip="carl_speech_sample",
        audio_base64=audio_b64,
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
