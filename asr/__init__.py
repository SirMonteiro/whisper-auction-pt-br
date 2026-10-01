"""ASR module for audio transcription."""

from .base import ITranscriber
from .whisper_transcriber import WhisperTranscriber

__all__ = ["ITranscriber", "WhisperTranscriber"]
