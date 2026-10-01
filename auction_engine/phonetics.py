"""Phonological normalization, pre-compiled regex pipeline, and phonetic alphabet alignment."""

import re
import unicodedata
from typing import Dict, List, Tuple
from .lexicon import IDIOM_MAP
from .phonetic_matcher import PhoneticMatcher


def strip_accents(text: str) -> str:
    """Normalize unicode and strip diacritical accents, returning lowercase text."""
    nfkd = unicodedata.normalize("NFD", text)
    return "".join(c for c in nfkd if unicodedata.category(c) != "Mn").lower()


# ---------------------------------------------------------------------------
# Pre-compiled Regex Pipeline for High Performance and Efficiency
# ---------------------------------------------------------------------------

_RE_HYPHENS = re.compile(r"-")
_RE_MULT_OPERATORS = re.compile(r"\b(?:vezes|multiplicado\s+por|x)\b", re.IGNORECASE)
_RE_ADD_OPERATORS = re.compile(r"\b(?:mais|somado\s+a|com\s+mais|e\s+mais)\b", re.IGNORECASE)
_RE_DECIMAL_COMMAS = re.compile(r"(\d+)[,\.](\d+)")
_RE_STRIP_SYMBOLS = re.compile(r"[^\w\s\.\+\*]")
_RE_ISOLATED_DOTS = re.compile(r"(?<!\d)\.|\.(?!\d)")

# English Number Word Fallbacks (compiled alternation)
_EN_NUM_MAP: Dict[str, str] = {
    "zero": "zero", "one": "um", "two": "dois", "three": "tres",
    "four": "quatro", "five": "cinco", "six": "seis", "seven": "sete",
    "eight": "oito", "nine": "nove", "ten": "dez", "twenty": "vinte",
    "fifty": "cinquenta", "hundred": "cem", "thousand": "mil"
}
_RE_EN_WORDS = re.compile(r"\b(" + "|".join(_EN_NUM_MAP.keys()) + r")\b", re.IGNORECASE)

# Contracted & Sandhi Numeral Rules (pre-compiled)
_TENS_HUNDREDS = (
    r"(?:vinte|trinta|quarenta|cinquenta|sessenta|setenta|oitenta|noventa|"
    r"cento|duzentos?|duzentas?|trezentos?|trezentas?|quatrocentos?|quatrocentas?|"
    r"quinhentos?|quinhentas?|seiscentos?|seiscentas?|setecentos?|setecentas?|"
    r"oitocentos?|oitocentas?|novecentos?|novecentas?)"
)
_UNITS_TEENS = (
    r"(?:um|uma|dois|duas|tres|quatro|cinco|seis|sete|oito|nove|"
    r"dez|onze|doze|treze|quatorze|catorze|quinze|dezesseis|dezessete|dezoito|dezenove|"
    r"vinte|trinta|quarenta|cinquenta|sessenta|setenta|oitenta|noventa)"
)
_RE_SANDHI_NUMERALS = re.compile(rf"\b({_TENS_HUNDREDS})e({_UNITS_TEENS})\b", re.IGNORECASE)
_RE_HUNDREDS_AND = re.compile(
    rf"\b(duzent|trezent|quatrocent|quinhent|seiscent|setecent|oitocent|novecent)[ei]({_UNITS_TEENS})\b",
    re.IGNORECASE,
)
_RE_SETE = re.compile(r"\bsei?t[ei](s)?\b", re.IGNORECASE)
_RE_FILLER_UMA_VEZ = re.compile(r"\b(?:mais\s+)?de\s+uma?\s+vez\b", re.IGNORECASE)

# Fauna article remover
_RE_FAUNA_ARTICLE = re.compile(
    r"\buma?\s+(beija\s+flor|tartaruga|garca|arara|mico\s+leao|mico|onca\s+pintada|onca|oncinha|garoupa|peixe|lobo\s+guara|lobo|lobinho)\b",
    re.IGNORECASE,
)

# Acoustic misrecognitions and dialectal variants
_RE_SIXENTA = re.compile(r"\b(?:sixenta|sisenta|seicenta)\b", re.IGNORECASE)
_RE_NOVEM_MIL = re.compile(r"\bnovem(?=\s+mil\b)", re.IGNORECASE)
_RE_NOVENTENOVE = re.compile(r"\bnovent[ea]n(?:ove|oi)\b", re.IGNORECASE)
_RE_NOVECENTENTA = re.compile(r"\bnovecententa\b", re.IGNORECASE)
_RE_MAS_AS_MAIS = re.compile(
    r"\bmas(?=\s+(?:mil|\d+|um|dois|duas|tres|quatro|cinco|seis|sete|oito|nove|dez|onze|doze|vinte|trinta|quarenta|cinquenta|sessenta|setenta|oitenta|noventa|cem|cento|duzentos|quinhentos|reais|conto|pila|bar[ao]es?|placas?|pau))\b",
    re.IGNORECASE,
)
_RE_NOVENTA_EM_NOVE = re.compile(
    r"\b(vinte|trinta|quarenta|cinquenta|sessenta|setenta|oitenta|noventa)\s+em\s+(um|dois|duas|tres|quatro|cinco|seis|sete|oito|nove)\b",
    re.IGNORECASE,
)
_RE_ENGLISH_AND = re.compile(
    r"\band\b(?=\s*(?:nove|oito|sete|seis|cinco|quatro|tres|dois|um|noventa|oitenta|setenta|sessenta|cinquenta|quarenta|trinta|vinte|dez|\d+))",
    re.IGNORECASE,
)

# Master Compiled Idiom & Bingo Regex (Single Pass DFA)
_SORTED_IDIOMS = sorted(IDIOM_MAP.keys(), key=len, reverse=True)
_RE_MASTER_IDIOM = re.compile(
    r"\b(" + "|".join(re.escape(k) for k in _SORTED_IDIOMS) + r")\b",
    re.IGNORECASE,
)

# Morphological & Sandhi Augmentatives
_SANDHI_AUGMENTATIVES: List[Tuple[re.Pattern, str]] = [
    (re.compile(r"\bduz[aei]\s+entao\b", re.IGNORECASE), "duzentao"),
    (re.compile(r"\bquinz[aei]\s+entao\b", re.IGNORECASE), "quinhentao"),
    (re.compile(r"\bquatr[oe]\s+entao\b", re.IGNORECASE), "quatrocentao"),
    (re.compile(r"\btrez[aei]\s+entao\b", re.IGNORECASE), "trezentao"),
    (re.compile(r"\boit[aeo]\s+entao\b", re.IGNORECASE), "oitocentao"),
    (re.compile(r"\btu[sz]ent(ao|ola|inho|os)\b", re.IGNORECASE), r"duzent\1"),
    (re.compile(r"\bsen(s|z)(ao|inho)\b", re.IGNORECASE), r"cemzao"),
    (re.compile(r"\bcen(s|z)(ao|inho)\b", re.IGNORECASE), r"cemzao"),
    (re.compile(r"\btresent(ao|os|inho|inhos|ola)\b", re.IGNORECASE), r"trezent\1"),
    (re.compile(r"\bseissent(os|as|ao)\b", re.IGNORECASE), r"seiscent\1"),
    (re.compile(r"\btrent(a|ao|inho|ola)\b", re.IGNORECASE), r"trint\1"),
    (re.compile(r"\bcincoent", re.IGNORECASE), "cinquent"),
    (re.compile(r"\bquintinho(s)?\b", re.IGNORECASE), "quinhentos"),
    (re.compile(r"\bvint(im|em)\b", re.IGNORECASE), "vinte"),
    (re.compile(r"\b(?:meio|meu|mepau)\s+pau\b", re.IGNORECASE), "quinhentos reais"),
    (re.compile(r"\b(?:meio|meu)\s+barao\b", re.IGNORECASE), "quinhentos reais"),
]

# Colloquial PB Plural Erasure (Apagamento de plural)
_PLURAL_ERASURE: List[Tuple[re.Pattern, str]] = [
    (re.compile(r"\b(\d+|dois|tres|quatro|cinco|seis|sete|oito|nove|dez)\s+barao\b", re.IGNORECASE), r"\1 baroes"),
    (re.compile(r"\b(\d+|duas|tres|quatro|cinco|seis|sete|oito|nove|dez)\s+placa\b", re.IGNORECASE), r"\1 placas"),
    (re.compile(r"\b(\d+|duas|tres|quatro|cinco|seis|sete|oito|nove|dez)\s+onca\b", re.IGNORECASE), r"\1 oncas"),
    (re.compile(r"\b(\d+|duas|tres|quatro|cinco|seis|sete|oito|nove|dez)\s+garca\b", re.IGNORECASE), r"\1 garcas"),
    (re.compile(r"\b(\d+|dois|tres|quatro|cinco|seis|sete|oito|nove|dez)\s+lobo\b", re.IGNORECASE), r"\1 lobos"),
    (re.compile(r"\b(\d+|dois|tres|quatro|cinco|seis|sete|oito|nove|dez)\s+peixe\b", re.IGNORECASE), r"\1 peixes"),
]

# Singleton Phonetic Matcher instance
_PHONETIC_MATCHER = PhoneticMatcher()


def phonological_repair(text: str) -> str:
    """High-performance normalization pipeline combining pre-compiled regexes and phonetic alphabet matching."""
    if not text:
        return ""

    t = strip_accents(text)
    t = _RE_HYPHENS.sub(" ", t)

    # 1. Direct acoustic dialectal misrecognitions (fast compiled regexes)
    t = _RE_SIXENTA.sub("sessenta", t)
    t = _RE_NOVEM_MIL.sub("nove", t)
    t = _RE_NOVENTENOVE.sub("noventa e nove", t)
    t = _RE_NOVECENTENTA.sub("noventa", t)
    t = _RE_MAS_AS_MAIS.sub("mais", t)
    t = _RE_NOVENTA_EM_NOVE.sub(r"\1 e \2", t)
    t = _RE_ENGLISH_AND.sub("e", t)

    # 2. English number word fallbacks (single pass)
    t = _RE_EN_WORDS.sub(lambda m: _EN_NUM_MAP[m.group(0).lower()], t)

    # 3. Strip singular articles before fauna terms (e.g. "uma tartaruga" -> "tartaruga")
    t = _RE_FAUNA_ARTICLE.sub(r"\1", t)

    # 4. Master Idiom replacement (Single Pass DFA matching all idioms in O(N))
    t = _RE_MASTER_IDIOM.sub(lambda m: IDIOM_MAP[m.group(0).lower()], t)

    # 5. Phonetic Alphabet Resolution (handles homophones like SEM -> CEM, Mew-Zom, Muzinho, etc.)
    t = _PHONETIC_MATCHER.repair_tokens(t)

    # 6. Contracted numerals & sandhi
    t = _RE_SANDHI_NUMERALS.sub(r"\1 e \2", t)
    t = _RE_HUNDREDS_AND.sub(r"\1os e \2", t)
    t = _RE_SETE.sub("sete", t)

    # 7. Arithmetic operators and numbers
    t = _RE_MULT_OPERATORS.sub(" * ", t)
    t = _RE_ADD_OPERATORS.sub(" + ", t)
    t = _RE_DECIMAL_COMMAS.sub(r"\1.\2", t)

    # 8. Clean punctuation
    t = _RE_STRIP_SYMBOLS.sub(" ", t)
    t = _RE_ISOLATED_DOTS.sub(" ", t)
    t = _RE_FILLER_UMA_VEZ.sub(" ", t)

    # 8. Sândi nasal and colloquial PB augmentatives
    for pattern, repl in _SANDHI_AUGMENTATIVES:
        t = pattern.sub(repl, t)

    # 9. Colloquial PB plural erasure
    for pattern, repl in _PLURAL_ERASURE:
        t = pattern.sub(repl, t)

    return t
