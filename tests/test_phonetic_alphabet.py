"""Unit tests for the Brazilian Portuguese Phonetic Alphabet and Phonetic Matcher."""

import pytest
from auction_engine.phonetic_alphabet import (
    encode_to_phonetic_alphabet,
    phonetic_distance,
    phonetic_similarity,
)
from auction_engine.phonetic_matcher import PhoneticMatcher
from auction_engine.parser import AuctionBidEvaluator


@pytest.fixture
def matcher():
    return PhoneticMatcher()


class TestPhoneticAlphabetEncoding:
    """Verifies that Portuguese and acoustic ASR words map accurately to phonetic alphabet codes."""

    def test_homophones_cem_sem(self):
        p_cem = encode_to_phonetic_alphabet("cem")
        p_sem = encode_to_phonetic_alphabet("sem")
        assert p_cem == p_sem
        assert p_cem == "SEN"

    def test_sim_not_equal_to_cem(self):
        p_cem = encode_to_phonetic_alphabet("cem")
        p_sim = encode_to_phonetic_alphabet("sim")
        assert p_cem != p_sim
        assert phonetic_distance(p_cem, p_sim) == 1

    def test_whisper_acoustic_milzao_mewzom(self):
        p_milzao = encode_to_phonetic_alphabet("milzão")
        p_mewzom = encode_to_phonetic_alphabet("mew-zom")
        assert phonetic_distance(p_milzao, p_mewzom) == 0

    def test_whisper_acoustic_milzinho_muzinho(self):
        p_milzinho = encode_to_phonetic_alphabet("milzinho")
        p_muzinho = encode_to_phonetic_alphabet("muzinho")
        # Near-phonetic distance: 2 edits over 8 chars (similarity >= 0.75)
        dist = phonetic_distance(p_milzinho, p_muzinho)
        assert dist <= 2
        assert phonetic_similarity(p_milzinho, p_muzinho) >= 0.75

    def test_whisper_acoustic_vinte_video(self):
        p_vinte = encode_to_phonetic_alphabet("vinte")
        p_video = encode_to_phonetic_alphabet("video")
        dist = phonetic_distance(p_vinte, p_video)
        assert dist <= 1

    def test_whisper_acoustic_vinte_vindes(self):
        p_vinte = encode_to_phonetic_alphabet("vinte")
        p_vindes = encode_to_phonetic_alphabet("vindes")
        assert phonetic_distance(p_vinte, p_vindes) <= 1

    def test_whisper_acoustic_vinte_vite(self):
        p_vinte = encode_to_phonetic_alphabet("vinte")
        p_vite = encode_to_phonetic_alphabet("vite")
        assert phonetic_distance(p_vinte, p_vite) <= 1

    def test_whisper_acoustic_cinco_itinhas_cinquentinha(self):
        p_cinq = encode_to_phonetic_alphabet("cinquentinha")
        p_split = encode_to_phonetic_alphabet("cinco itinhas")
        dist = phonetic_distance(p_cinq, p_split)
        assert dist <= 2
        assert phonetic_similarity(p_cinq, p_split) >= 0.80


class TestPhoneticMatcher:
    """Verifies that PhoneticMatcher correctly resolves noisy ASR outputs to canonical domain terms."""

    def test_repair_homophone_sem(self, matcher):
        assert matcher.repair_tokens("sem unidades") == "cem unidades"
        assert matcher.repair_tokens("sem reais") == "cem reais"

    def test_repair_acoustic_mew_zom(self, matcher):
        assert matcher.repair_tokens("mew zom") == "milzao"

    def test_repair_acoustic_muzinho(self, matcher):
        assert matcher.repair_tokens("muzinho") == "milzinho"

    def test_repair_acoustic_video(self, matcher):
        assert matcher.repair_tokens("video") == "vinte"

    def test_repair_acoustic_vindes(self, matcher):
        assert matcher.repair_tokens("vindes") == "vinte"

    def test_repair_acoustic_cinco_itinhas(self, matcher):
        assert matcher.repair_tokens("cinco itinhas") == "cinquentinha"

    def test_preserves_non_domain_speech(self, matcher):
        assert matcher.repair_tokens("sim") == "sim"
        assert matcher.repair_tokens("e ai") == "e ai"
        assert matcher.repair_tokens("passo") == "passo"


class TestLanguageParameterControl:
    """Verifies the language configuration across ASR and test environment."""

    def test_whisper_transcriber_default_language(self):
        from asr.whisper_transcriber import WhisperTranscriber
        # Verify class signature accepts language
        import inspect
        sig = inspect.signature(WhisperTranscriber.__init__)
        assert "language" in sig.parameters
        assert sig.parameters["language"].default == "pt"

    def test_evaluator_with_noisy_phonetic_captures(self):
        evaluator = AuctionBidEvaluator()
        # Test noisy/near-phonetic phrase variations
        assert evaluator.evaluate("cinco itinhas").value == 50.0
        assert evaluator.evaluate("mew zom").value == 1000.0
        assert evaluator.evaluate("muzinho").value == 1000.0
        assert evaluator.evaluate("video").value == 20.0
        assert evaluator.evaluate("vindes").value == 20.0
        assert evaluator.evaluate("sem").value == 100.0
