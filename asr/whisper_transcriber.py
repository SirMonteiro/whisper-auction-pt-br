"""Whisper ASR implementation with domain biasing and audio preprocessing."""

import io
import math
import os
import warnings
from typing import Optional

import numpy as np
import soundfile as sf
import torch
from transformers import (
    WhisperForConditionalGeneration,
    WhisperProcessor,
    logging as hf_logging,
)

from .base import ITranscriber

os.environ["PYTHONWARNINGS"] = "ignore"
warnings.filterwarnings("ignore")
hf_logging.set_verbosity_error()

DEFAULT_WHISPER_ID = "openai/whisper-small"
TARGET_SR = 16000

DEFAULT_PROMPT = (
    "Lances e pregão de leilão: dobro, dobra, dobre, dobrar, duplico, duplicar, duplicarei, "
    "triplico, triplicar, quadruplicar, mais, vezes, centavos, reais, barão, barões, "
    "pau, paus, conto, pila, onça, garoupa, lobo guará, dois patinhos na lagoa, "
    "idade de cristo, justiça de goiás, três oitão, pernas da xuxa, meia dúzia, duzentão, quinhentão."
)


class WhisperTranscriber(ITranscriber):
    """Encapsulates Whisper model loading, audio conditioning, and inference."""

    def __init__(
        self,
        model_id: str = DEFAULT_WHISPER_ID,
        device: Optional[torch.device] = None,
        prompt: str = DEFAULT_PROMPT,
        min_duration_sec: float = 0.25,
        min_rms_energy: float = 0.003,
    ) -> None:
        self.model_id = model_id
        self.min_duration_sec = min_duration_sec
        self.min_rms_energy = min_rms_energy

        if device is None:
            self.device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
        else:
            self.device = device

        print(f"Carregando Whisper ({self.model_id}) em {self.device}...")
        self.processor = WhisperProcessor.from_pretrained(self.model_id)

        dtype = torch.float16 if self.device.type == "cuda" else torch.float32
        self.model = WhisperForConditionalGeneration.from_pretrained(
            self.model_id,
            torch_dtype=dtype,
        ).to(self.device)
        self.model.eval()

        self.prompt_ids = self.processor.get_prompt_ids(prompt, return_tensors="pt").to(self.device)
        print("✓ Pipeline do Whisper inicializado com sucesso.")

    def transcribe(self, audio_data: np.ndarray, sample_rate: int = TARGET_SR) -> str:
        """Transcribes a mono float32 numpy audio array."""
        if audio_data is None or len(audio_data) == 0:
            return ""

        # Mono check
        if audio_data.ndim > 1:
            audio_data = audio_data.mean(axis=1)

        # Resample to TARGET_SR (16kHz) if needed
        if sample_rate != TARGET_SR:
            import scipy.signal
            gcd = math.gcd(int(sample_rate), int(TARGET_SR))
            up = TARGET_SR // gcd
            down = sample_rate // gcd
            audio_16k = scipy.signal.resample_poly(audio_data, up, down).astype(np.float32)
        else:
            audio_16k = audio_data.astype(np.float32)

        duration = len(audio_16k) / TARGET_SR
        if duration < self.min_duration_sec:
            return ""

        # RMS Energy check to avoid hallucinations on ambient silence or microphone pops
        rms = float(np.sqrt(np.mean(audio_16k ** 2)))
        if rms < self.min_rms_energy:
            return ""

        inputs = self.processor(
            audio_16k,
            sampling_rate=TARGET_SR,
            return_tensors="pt"
        ).input_features.to(self.device, dtype=self.model.dtype)

        with torch.no_grad():
            predicted_ids = self.model.generate(
                inputs,
                language="pt",
                task="transcribe",
                prompt_ids=self.prompt_ids,
                temperature=0.0,
                no_repeat_ngram_size=3,
                condition_on_prev_tokens=False,
            )
            raw_text = self.processor.batch_decode(
                predicted_ids,
                skip_special_tokens=True,
                clean_up_tokenization_spaces=False,
            )[0].strip()

        return raw_text

    def transcribe_bytes(self, audio_bytes: bytes) -> str:
        """Decodes WAV bytes and executes transcription."""
        data, orig_sr = sf.read(io.BytesIO(audio_bytes), dtype="float32")
        return self.transcribe(data, sample_rate=orig_sr)
