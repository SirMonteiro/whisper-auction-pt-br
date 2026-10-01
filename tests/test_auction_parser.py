"""Unit tests verifying auction voice bid parsing and phonetic/linguistic repairs."""

import pytest
from auction_engine.parser import AuctionBidEvaluator


@pytest.fixture
def evaluator():
    return AuctionBidEvaluator()


class TestUserReportedErrors:
    """Verifies all test cases reported by the user."""

    def test_filler_sim(self, evaluator):
        res = evaluator.evaluate("Sim.")
        assert res.value == 0.0
        assert not res.is_valid or res.action_type == "NONE"

    def test_sem_homophone_cem(self, evaluator):
        res = evaluator.evaluate("SEM")
        assert res.value == 100.0

    def test_sem_unidades(self, evaluator):
        res = evaluator.evaluate("Sem unidades.")
        assert res.value == 100.0

    def test_percentage(self, evaluator):
        res = evaluator.evaluate("100%.")
        assert res.value == 100.0

    def test_auction_chant_repetition(self, evaluator):
        res = evaluator.evaluate("Cinquenta, cinquenta, cinquenta.")
        assert res.value == 50.0
        assert not res.has_addition

    def test_explicit_addition(self, evaluator):
        res = evaluator.evaluate("50 mais 50.")
        assert res.value == 100.0
        assert not res.has_addition  # Two or more numbers summed together -> Add is False

    def test_single_number_with_mais(self, evaluator):
        res = evaluator.evaluate("Mais dois")
        assert res.value == 2.0
        assert res.has_addition  # Single number with "mais" -> Add is True (incremental bid)

    def test_dois_mais_dois(self, evaluator):
        res = evaluator.evaluate("Dois mais dois.")
        assert res.value == 4.0
        assert not res.has_addition  # Two or more numbers with "mais" -> Add is False

    def test_multiple_mais_chain(self, evaluator):
        res = evaluator.evaluate("Vinte mais, vinte mais vinte, mais sete.")
        assert res.value == 67.0
        assert not res.has_addition

    def test_incoherent_distinct_numbers_picks_last(self, evaluator):
        res = evaluator.evaluate("50, 40, 20.")
        assert res.value == 20.0  # Picks last detected number
        assert not res.has_addition

    def test_ungrammatical_tens_plus_tens_picks_last(self, evaluator):
        res = evaluator.evaluate("Trinta e dez.")
        assert res.value == 10.0  # Picks last detected number
        assert not res.has_addition

    def test_mew_zom_whisper_error(self, evaluator):
        res = evaluator.evaluate("Mew-Zom.")
        assert res.value == 1000.0

    def test_milzao(self, evaluator):
        res = evaluator.evaluate("Milzão.")
        assert res.value == 1000.0

    def test_milzinho(self, evaluator):
        res = evaluator.evaluate("Milzinho.")
        assert res.value == 1000.0

    def test_muzinho_whisper_error(self, evaluator):
        res = evaluator.evaluate("Muzinho.")
        assert res.value == 1000.0

    def test_mil_zinhos(self, evaluator):
        res = evaluator.evaluate("Mil zinhos.")
        assert res.value == 1000.0

    def test_e_ai_filler(self, evaluator):
        res = evaluator.evaluate("E aí,")
        assert res.value == 0.0

    def test_um(self, evaluator):
        res = evaluator.evaluate("Um.")
        assert res.value == 1.0

    def test_dots_silence(self, evaluator):
        res = evaluator.evaluate("...")
        assert res.value == 0.0

    def test_quarenta(self, evaluator):
        res = evaluator.evaluate("Quarenta.")
        assert res.value == 40.0

    def test_digits_76(self, evaluator):
        res = evaluator.evaluate("76.")
        assert res.value == 76.0

    def test_digits_78(self, evaluator):
        res = evaluator.evaluate("78.")
        assert res.value == 78.0

    def test_vindes_ai_whisper_error(self, evaluator):
        res = evaluator.evaluate("Vindes aí.")
        assert res.value == 20.0

    def test_video_whisper_error(self, evaluator):
        res = evaluator.evaluate("Vídeo.")
        assert res.value == 20.0

    def test_e_oito(self, evaluator):
        res = evaluator.evaluate("E oito.")
        assert res.value == 8.0

    def test_vite_na_vizinha(self, evaluator):
        res = evaluator.evaluate("Vite na vizinha.")
        assert res.value == 20.0

    def test_cinquenta_anos(self, evaluator):
        res = evaluator.evaluate("Cinquenta anos.")
        assert res.value == 50.0

    def test_cinquentao(self, evaluator):
        res = evaluator.evaluate("Cinquentão.")
        assert res.value == 50.0

    def test_cinco_itinhas_whisper_error(self, evaluator):
        res = evaluator.evaluate("Cinco itinhas.")
        assert res.value == 50.0

    def test_quarentao(self, evaluator):
        res = evaluator.evaluate("Quarentão.")
        assert res.value == 40.0


class TestLinguisticsAndDomain:
    """Verifies fauna, bingo, grandezas and Brazilian Portuguese phonology."""

    def test_zoology_of_real(self, evaluator):
        assert evaluator.evaluate("beija flor").value == 1.0
        assert evaluator.evaluate("uma tartaruga").value == 2.0
        assert evaluator.evaluate("uma garça").value == 5.0
        assert evaluator.evaluate("duas garças").value == 10.0
        assert evaluator.evaluate("uma arara").value == 10.0
        assert evaluator.evaluate("um mico").value == 20.0
        assert evaluator.evaluate("uma onça").value == 50.0
        assert evaluator.evaluate("duas onças").value == 100.0
        assert evaluator.evaluate("uma garoupa").value == 100.0
        assert evaluator.evaluate("um peixe").value == 100.0
        assert evaluator.evaluate("um lobo guará").value == 200.0
        assert evaluator.evaluate("dois lobos").value == 400.0

    def test_bingo_nicknames(self, evaluator):
        assert evaluator.evaluate("dois patinhos na lagoa").value == 22.0
        assert evaluator.evaluate("a idade de cristo").value == 33.0
        assert evaluator.evaluate("meia dúzia").value == 6.0
        assert evaluator.evaluate("uma dúzia").value == 12.0
        assert evaluator.evaluate("pernas da xuxa").value == 11.0
        assert evaluator.evaluate("justiça de goiás").value == 38.0
        assert evaluator.evaluate("uma boa ideia").value == 51.0

    def test_grandezas_and_slang(self, evaluator):
        assert evaluator.evaluate("um barão").value == 1000.0
        assert evaluator.evaluate("dois barão").value == 2000.0
        assert evaluator.evaluate("três placas").value == 3000.0
        assert evaluator.evaluate("um pau").value == 1000.0
        assert evaluator.evaluate("três paus").value == 3000.0
        assert evaluator.evaluate("meio pau").value == 500.0
        assert evaluator.evaluate("cem conto").value == 100.0
        assert evaluator.evaluate("cinquenta pila").value == 50.0

    def test_sandhi_nasal_repairs(self, evaluator):
        assert evaluator.evaluate("duza então").value == 200.0
        assert evaluator.evaluate("quinza então").value == 500.0
        assert evaluator.evaluate("tresentão").value == 300.0
        assert evaluator.evaluate("seissentos").value == 600.0

    def test_multiplication_verbs(self, evaluator):
        res1 = evaluator.evaluate("dobro cinquenta")
        assert res1.value == 100.0
        assert res1.has_multiplication

        res2 = evaluator.evaluate("dobro")
        assert res2.value == 2.0
        assert res2.action_type == "MULTIPLY_ONLY"

    def test_pass_action(self, evaluator):
        res = evaluator.evaluate("passo")
        assert res.value == 0.0
        assert res.action_type == "PASS"

        res2 = evaluator.evaluate("tô fora")
        assert res2.value == 0.0
        assert res2.action_type == "PASS"


class TestNewUserExamples:
    """Verifies all new test cases reported by the user in the latest session."""

    def test_quadreplico_multiplier(self, evaluator):
        res = evaluator.evaluate("Quadreplico")
        assert res.value == 4.0
        assert res.has_multiplication
        assert res.action_type == "MULTIPLY_ONLY"

    def test_sixenta_as_sessenta(self, evaluator):
        res = evaluator.evaluate("Sixenta")
        assert res.value == 60.0

    def test_meia_duzi_variants(self, evaluator):
        assert evaluator.evaluate("Meia dúzi").value == 6.0
        assert evaluator.evaluate("Meia dúzias").value == 6.0
        assert evaluator.evaluate("Uma dúzia").value == 12.0

    def test_patinhos_variants(self, evaluator):
        assert evaluator.evaluate("Dois patinhos da lago").value == 22.0
        assert evaluator.evaluate("Dois patinhos da lagoas").value == 22.0
        assert evaluator.evaluate("Três patinhos").value == 3.0

    def test_vinte_sete_repetition_picks_last(self, evaluator):
        res = evaluator.evaluate("Vinte-sete, vinte-se, vint-sate")
        assert res.value == 27.0
        assert not res.has_addition

    def test_catorze_oitenta_e_oito_picks_last(self, evaluator):
        res = evaluator.evaluate("Catorze oitenta e oito")
        assert res.value == 88.0

    def test_novem_mil_novecentos_noventenove(self, evaluator):
        res = evaluator.evaluate("Novem mil novecentos e noventenove.")
        assert res.value == 9999.0

    def test_mas_mil_novecentos_noventa_nove(self, evaluator):
        res = evaluator.evaluate("Mas mil novecentos e noventa e nove.")
        assert res.value == 1999.0
        assert res.has_addition  # Single number with "Mas" (="Mais") -> Add is True

    def test_nove_mil_novecentos_noventenoi(self, evaluator):
        res = evaluator.evaluate("Nove mil novecentos e noventenói.")
        assert res.value == 9999.0

    def test_noventa_e_nove_mil_novecentos_noventa_em_nove(self, evaluator):
        res = evaluator.evaluate("Noventa e nove mil novecentos e noventa em nove.")
        assert res.value == 99999.0

