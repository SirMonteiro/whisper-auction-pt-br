"""Unit tests for the Web test environment and HTTP endpoints using a mock transcriber."""

import io
import pytest
import soundfile as sf
import numpy as np
from web.app import create_app
from auction_engine.parser import AuctionBidEvaluator


class MockTranscriber:
    """Mock transcriber simulating Whisper transcriptions without GPU or model loading."""

    def __init__(self, transcript: str = "cinquenta reais"):
        self.transcript = transcript

    def transcribe(self, audio_data: np.ndarray, sample_rate: int, language: str | None = None) -> str:
        return self.transcript

    def transcribe_bytes(self, audio_bytes: bytes, language: str | None = None) -> str:
        return self.transcript


def create_dummy_wav_bytes(duration_sec: float = 1.0, sr: int = 16000) -> bytes:
    samples = int(duration_sec * sr)
    data = (np.sin(2 * np.pi * 440 * np.linspace(0, duration_sec, samples))).astype(np.float32)
    buf = io.BytesIO()
    sf.write(buf, data, sr, format="WAV", subtype="PCM_16")
    buf.seek(0)
    return buf.read()


@pytest.fixture
def client():
    mock_transcriber = MockTranscriber("cinquenta reais")
    evaluator = AuctionBidEvaluator()
    app = create_app(mock_transcriber, evaluator)
    app.config["TESTING"] = True
    with app.test_client() as c:
        yield c


def test_index_route(client):
    response = client.get("/")
    assert response.status_code == 200
    assert b"Leil\xc3\xa3o Whisper Push-to-Talk" in response.data


def test_transcribe_missing_file(client):
    response = client.post("/transcribe")
    assert response.status_code == 400
    assert "error" in response.get_json()


def test_transcribe_empty_file(client):
    data = {"audio": (io.BytesIO(b""), "empty.wav")}
    response = client.post("/transcribe", data=data, content_type="multipart/form-data")
    assert response.status_code == 400
    assert "error" in response.get_json()


def test_transcribe_successful_bid(client):
    wav_bytes = create_dummy_wav_bytes()
    data = {"audio": (io.BytesIO(wav_bytes), "test.wav")}
    response = client.post("/transcribe", data=data, content_type="multipart/form-data")
    assert response.status_code == 200
    json_data = response.get_json()
    assert json_data["raw_transcript"] == "cinquenta reais"
    assert json_data["value"] == 50.0
    assert json_data["currency"] == "R$ 50,00"
    assert json_data["is_valid"] is True
