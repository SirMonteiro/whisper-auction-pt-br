"""Abstract base class and protocol for speech-to-text transcribers."""

from typing import Protocol, runtime_checkable
import numpy as np


@runtime_checkable
class ITranscriber(Protocol):
    """Interface for ASR speech transcription."""

    def transcribe(
        self, audio_data: np.ndarray, sample_rate: int, language: str | None = None
    ) -> str:
        """Transcribes a mono float32 numpy audio array to text."""
        ...

    def transcribe_bytes(
        self, audio_bytes: bytes, language: str | None = None
    ) -> str:
        """Decodes raw audio bytes (e.g. WAV) and transcribes to text."""
        ...
