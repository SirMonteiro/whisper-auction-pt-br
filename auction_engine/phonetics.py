"""Phonological normalization and acoustic repair rules for Brazilian Portuguese ASR."""

import re
import unicodedata
from typing import Dict
from .lexicon import IDIOM_MAP


def strip_accents(text: str) -> str:
    """Normalize unicode and strip diacritical accents, returning lowercase text."""
    nfkd = unicodedata.normalize("NFD", text)
    return "".join(c for c in nfkd if unicodedata.category(c) != "Mn").lower()


def repair_whisper_acoustics(text: str) -> str:
    """Repairs common Whisper acoustic misrecognitions in Brazilian Portuguese."""
    t = text

    # "Mew-Zom", "mew zom", "miw zom" -> "milzão"
    t = re.sub(r"\b(?:mew|miw|mil)[-\s]+zom\b", "milzao", t, flags=re.IGNORECASE)

    # "Muzinho", "muizinho", "miuzinho" -> "milzinho"
    t = re.sub(r"\b(?:muzinho|muizinho|miuzinho|melzinho)\b", "milzinho", t, flags=re.IGNORECASE)
    t = re.sub(r"\bmil\s+zinhos?\b", "milzinho", t, flags=re.IGNORECASE)

    # "Cinco itinhas" -> "cinquentinha"
    t = re.sub(r"\bcinco\s+itinhas?\b", "cinquentinha", t, flags=re.IGNORECASE)

    # "Vindes aí", "vindes", "vinde aí", "vite" -> "vinte"
    t = re.sub(r"\bvindes(?:\s+ai)?\b", "vinte", t, flags=re.IGNORECASE)
    t = re.sub(r"\bvinde(?:\s+ai)?\b", "vinte", t, flags=re.IGNORECASE)
    t = re.sub(r"\bvite\b", "vinte", t, flags=re.IGNORECASE)

    # "Vídeo" misrecognized for "Vinte"
    # When standalone, e.g. "Vídeo." or "video 20" or "video reais"
    t = re.sub(r"\bvideo\b", "vinte", t, flags=re.IGNORECASE)

    # "SEM" homophone of "CEM" in Brazilian Portuguese (/sẽj̃/)
    # If followed by negative words (lance, oferta, nada, dinheiro, grana), keep as negative
    # Otherwise: standalone "sem", or "sem" followed by units/numbers/currency -> "cem"
    negative_pattern = r"\bsem\s+(?:lance|oferta|proposta|nada|dinheiro|grana|valor)\b"
    if not re.search(negative_pattern, t, flags=re.IGNORECASE):
        # "sem unidades", "sem reais", "sem conto", "sem e cinquenta", "sem mil", etc.
        t = re.sub(
            r"\bsem\b(?=\s*(?:unidades?|reais?|real|conto|contos|pila|pilas|prata|pratas|"
            r"bar[ao]es?|placas?|mil|cento|e\s+\w+|\d+|$))",
            "cem",
            t,
            flags=re.IGNORECASE,
        )
        # Standalone "sem" at start/end or isolated
        t = re.sub(r"^\s*sem\s*$", "cem", t, flags=re.IGNORECASE)

    return t


def phonological_repair(text: str) -> str:
    """Complete phonetic, lexical and morphological normalization pipeline for auction speech."""
    t = strip_accents(text)
    t = t.replace("-", " ")

    # Whisper acoustic corrections before stripping/normalizing
    t = repair_whisper_acoustics(t)

    # English number word fallbacks (in case of Whisper English token bleed)
    en_map: Dict[str, str] = {
        "zero": "zero", "one": "um", "two": "dois", "three": "tres",
        "four": "quatro", "five": "cinco", "six": "seis", "seven": "sete",
        "eight": "oito", "nine": "nove", "ten": "dez", "twenty": "vinte",
        "fifty": "cinquenta", "hundred": "cem", "thousand": "mil"
    }
    for en_word, pt_word in en_map.items():
        t = re.sub(rf"\b{en_word}\b", pt_word, t)

    # Sandhi & contracted numeral repair: "cincoenta" -> "cinquenta", etc.
    tens_and_hundreds = (
        r"(?:vinte|trinta|quarenta|cinquenta|sessenta|setenta|oitenta|noventa|"
        r"cento|duzentos?|duzentas?|trezentos?|trezentas?|quatrocentos?|quatrocentas?|"
        r"quinhentos?|quinhentas?|seiscentos?|seiscentas?|setecentos?|setecentas?|"
        r"oitocentos?|oitocentas?|novecentos?|novecentas?)"
    )
    units_and_teens = (
        r"(?:um|uma|dois|duas|tres|quatro|cinco|seis|sete|oito|nove|"
        r"dez|onze|doze|treze|quatorze|catorze|quinze|dezesseis|dezessete|dezoito|dezenove|"
        r"vinte|trinta|quarenta|cinquenta|sessenta|setenta|oitenta|noventa)"
    )
    t = re.sub(rf"\b({tens_and_hundreds})e({units_and_teens})\b", r"\1 e \2", t)

    t = re.sub(
        rf"\b(duzent|trezent|quatrocent|quinhent|seiscent|setecent|oitocent|novecent)[ei]({units_and_teens})\b",
        r"\1os e \2",
        t,
    )

    t = re.sub(r"\bsei?t[ei](s)?\b", "sete", t)
    t = re.sub(r"\b(?:vezes|multiplicado\s+por|x)\b", " * ", t)
    t = re.sub(r"\b(?:mais|somado\s+a|com\s+mais|e\s+mais)\b", " + ", t)
    t = re.sub(r"(\d+)[,\.](\d+)", r"\1.\2", t)

    # Strip symbols except math operators, decimals and alphanumerics
    t = re.sub(r"[^\w\s\.\+\*]", " ", t)
    t = re.sub(r"(?<!\d)\.|\.(?!\d)", " ", t)

    # Idiom noise reductions
    t = re.sub(r"\bde\s+uma?\s+vez\b", " ", t)
    t = re.sub(r"\bmais\s+uma?\s+vez\b", " ", t)

    # Remove article before fauna terms: "uma onça" -> "onça"
    t = re.sub(
        r"\buma?\s+(beija\s+flor|tartaruga|garca|arara|mico\s+leao|mico|onca\s+pintada|onca|oncinha|garoupa|peixe|lobo\s+guara|lobo|lobinho)\b",
        r"\1",
        t,
    )

    # Replace cultural idioms and bingo expressions (longest first)
    for idiom in sorted(IDIOM_MAP.keys(), key=len, reverse=True):
        t = re.sub(rf"\b{idiom}\b", IDIOM_MAP[idiom], t)

    # Sândi nasal in augmentatives: "duza então" -> "duzentão"
    t = re.sub(r"\bduz[aei]\s+entao\b", "duzentao", t)
    t = re.sub(r"\bquinz[aei]\s+entao\b", "quinhentao", t)
    t = re.sub(r"\bquatr[oe]\s+entao\b", "quatrocentao", t)
    t = re.sub(r"\btrez[aei]\s+entao\b", "trezentao", t)
    t = re.sub(r"\boit[aeo]\s+entao\b", "oitocentao", t)

    # Dessonorização sibilante & spelling variations
    t = re.sub(r"\btu[sz]ent(ao|ola|inho|os)\b", r"duzent\1", t)
    t = re.sub(r"\bsen(s|z)(ao|inho)\b", r"cemzao", t)
    t = re.sub(r"\bcen(s|z)(ao|inho)\b", r"cemzao", t)
    t = re.sub(r"\btresent(ao|os|inho|inhos|ola)\b", r"trezent\1", t)
    t = re.sub(r"\bseissent(os|as|ao)\b", r"seiscent\1", t)
    t = re.sub(r"\btrent(a|ao|inho|ola)\b", r"trint\1", t)
    t = re.sub(r"\bcincoent", "cinquent", t)

    t = re.sub(r"\bquintinho(s)?\b", "quinhentos", t)
    t = re.sub(r"\bvint(im|em)\b", "vinte", t)

    # Colloquial vowel elision
    t = re.sub(r"\b(?:meio|meu|mepau)\s+pau\b", "quinhentos reais", t)
    t = re.sub(r"\b(?:meio|meu)\s+barao\b", "quinhentos reais", t)

    # Plural erasure in colloquial Brazilian Portuguese (apagamento de plural)
    t = re.sub(r"\b(\d+|dois|tres|quatro|cinco|seis|sete|oito|nove|dez)\s+barao\b", r"\1 baroes", t)
    t = re.sub(r"\b(\d+|duas|tres|quatro|cinco|seis|sete|oito|nove|dez)\s+placa\b", r"\1 placas", t)
    t = re.sub(r"\b(\d+|duas|tres|quatro|cinco|seis|sete|oito|nove|dez)\s+onca\b", r"\1 oncas", t)
    t = re.sub(r"\b(\d+|duas|tres|quatro|cinco|seis|sete|oito|nove|dez)\s+garca\b", r"\1 garcas", t)
    t = re.sub(r"\b(\d+|dois|tres|quatro|cinco|seis|sete|oito|nove|dez)\s+lobo\b", r"\1 lobos", t)
    t = re.sub(r"\b(\d+|dois|tres|quatro|cinco|seis|sete|oito|nove|dez)\s+peixe\b", r"\1 peixes", t)

    return t
