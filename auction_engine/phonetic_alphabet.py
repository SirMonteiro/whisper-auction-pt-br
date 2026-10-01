"""Brazilian Portuguese Phonetic Alphabet Encoder (Grapheme-to-Phoneme).

Converts Portuguese orthographic text and Whisper acoustic outputs into a standardized
phonetic alphabet representation based on Brazilian Portuguese (PB) phonology.
"""

import re
import unicodedata
from typing import Dict

# Pre-compiled transformation rules for Brazilian Portuguese phonetic alphabet
_NASAL_DIPHTHONGS = [
    (re.compile(r"(?:ão|ao|om)\b", re.IGNORECASE), "ANW"),  # Coda -om in PB produces back nasal diphthong ~ ão
    (re.compile(r"(?:õe|oe)\b", re.IGNORECASE), "ONJ"),
    (re.compile(r"(?:ãe|ae)\b", re.IGNORECASE), "ANJ"),
    (re.compile(r"ã", re.IGNORECASE), "AN"),
    (re.compile(r"õ", re.IGNORECASE), "ON"),
]

_CONSONANT_HOMOPHONES = [
    (re.compile(r"ç|c(?=[ei])", re.IGNORECASE), "s"),
    (re.compile(r"(?:gar|on)ca\b", re.IGNORECASE), "sa"),
    (re.compile(r"ss", re.IGNORECASE), "s"),
    (re.compile(r"sc|sç|xc", re.IGNORECASE), "s"),
    (re.compile(r"qu(?=[ei])", re.IGNORECASE), "k"),
    (re.compile(r"qu(?=[ao])", re.IGNORECASE), "kw"),
    (re.compile(r"c(?=[aou])", re.IGNORECASE), "k"),
    (re.compile(r"q", re.IGNORECASE), "k"),
    (re.compile(r"g(?=[ei])", re.IGNORECASE), "j"),
]

# Coda L vocalization in Brazilian Portuguese (e.g. mil -> miw, alto -> awto)
_CODA_L = re.compile(r"l(?=[bcdfghjklmnpqrstvwxyz\s]|$)", re.IGNORECASE)

# Whisper English-Portuguese acoustic bleed (e.g. "mew" for "miw")
_WHISPER_EW = re.compile(r"ew", re.IGNORECASE)

# Vowels followed by nasal consonant in syllable coda
_NASAL_VOWELS = [
    (re.compile(r"a[mn](?=[bcdfghjklmnpqrstvwxyz\s]|$)", re.IGNORECASE), "AN"),
    (re.compile(r"e[mn](?=[bcdfghjklmnpqrstvwxyz\s]|$)", re.IGNORECASE), "EN"),
    (re.compile(r"i[mn](?=[bcdfghjklmnpqrstvwxyz\s]|$)", re.IGNORECASE), "IN"),
    (re.compile(r"o[mn](?=[bcdfghjklmnpqrstvwxyz\s]|$)", re.IGNORECASE), "ON"),
    (re.compile(r"u[mn](?=[bcdfghjklmnpqrstvwxyz\s]|$)", re.IGNORECASE), "UN"),
]

_DIGRAPHS = [
    (re.compile(r"ch", re.IGNORECASE), "x"),
    (re.compile(r"nh", re.IGNORECASE), "ny"),
    (re.compile(r"lh", re.IGNORECASE), "ly"),
    (re.compile(r"rr", re.IGNORECASE), "r"),
    (re.compile(r"r\b", re.IGNORECASE), ""),  # Final r elision in colloquial PB
]

# Dental stops affricate/lenite before front vowels [i, e] in PB (ti/te -> T, di/de -> T)
_DENTAL_AFFRICATES = [
    (re.compile(r"t(?=[ie])", re.IGNORECASE), "T"),
    (re.compile(r"d(?=[ie])", re.IGNORECASE), "T"),
]

# Unstressed final vowels and colloquial glide reductions (e.g. video [vidi], vinte [vinti])
_UNSTRESSED_FINALS = [
    (re.compile(r"(?:eo|io|e)\b", re.IGNORECASE), "i"),
    (re.compile(r"o\b", re.IGNORECASE), "u"),
    (re.compile(r"[sz]\b", re.IGNORECASE), ""),  # Plural/coda s/z elision in PB
]

_COLLAPSE_DUPLICATES = re.compile(r"([A-Za-z])\1+")


def encode_to_phonetic_alphabet(text: str) -> str:
    """Encodes a word or phrase into the canonical Brazilian Portuguese Phonetic Alphabet."""
    if not text:
        return ""

    t = text.lower().strip()
    t = t.replace("-", " ")

    # 1. Nasal diphthongs and specific endings
    for pattern, repl in _NASAL_DIPHTHONGS:
        t = pattern.sub(repl, t)

    # 2. Consonant homophones
    for pattern, repl in _CONSONANT_HOMOPHONES:
        t = pattern.sub(repl, t)

    # 3. Coda L vocalization
    t = _CODA_L.sub("w", t)

    # 4. Whisper acoustic shifts
    t = _WHISPER_EW.sub("iw", t)

    # 5. Syllable coda nasals
    for pattern, repl in _NASAL_VOWELS:
        t = pattern.sub(repl, t)

    # 6. Strip non-nasal diacritics
    nfkd = unicodedata.normalize("NFD", t)
    t = "".join(c for c in nfkd if unicodedata.category(c) != "Mn")

    # 7. Digraphs
    for pattern, repl in _DIGRAPHS:
        t = pattern.sub(repl, t)

    # 8. Dental coronal stops before front vowels
    for pattern, repl in _DENTAL_AFFRICATES:
        t = pattern.sub(repl, t)

    # 9. Unstressed final vowels and coda s/z
    for pattern, repl in _UNSTRESSED_FINALS:
        t = pattern.sub(repl, t)

    # 10. Clean up and collapse duplicate consecutive consonants
    t = _COLLAPSE_DUPLICATES.sub(r"\1", t)
    t = re.sub(r"[^A-Za-z0-9]", "", t)

    return t.upper()


def phonetic_distance(s1: str, s2: str) -> int:
    """Computes Levenshtein edit distance between two phonetic alphabet representations."""
    if len(s1) < len(s2):
        return phonetic_distance(s2, s1)
    if len(s2) == 0:
        return len(s1)

    prev = list(range(len(s2) + 1))
    for i, c1 in enumerate(s1):
        curr = [i + 1]
        for j, c2 in enumerate(s2):
            cost = 0 if c1 == c2 else 1
            curr.append(min(prev[j + 1] + 1, curr[j] + 1, prev[j] + cost))
        prev = curr
    return prev[-1]


def phonetic_similarity(s1: str, s2: str) -> float:
    """Computes a normalized similarity ratio [0.0, 1.0] between two phonetic codes."""
    max_len = max(len(s1), len(s2))
    if max_len == 0:
        return 1.0
    dist = phonetic_distance(s1, s2)
    return max(0.0, 1.0 - (dist / max_len))
