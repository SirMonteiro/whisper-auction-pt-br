"""Phonetic Matcher for Brazilian Portuguese Auction Speech.

Resolves noisy, clipped, and near-phonetic ASR tokens against the canonical
domain vocabulary using the Brazilian Portuguese Phonetic Alphabet.
"""

import re
from typing import Dict, List, Optional, Tuple, Set
from .phonetic_alphabet import (
    encode_to_phonetic_alphabet,
    phonetic_distance,
)
from .lexicon import (
    BASES,
    MULTIPLIERS,
    DENOMINATIONS,
    CURRENCY_UNITS,
    PASS_PHRASES,
)

PROTECTED_WORDS: Set[str] = {
    "passo", "to fora", "nao quero", "desisto", "sem lance",
    "sim", "nao", "e", "de", "com", "mais", "vezes", "x"
} | PASS_PHRASES


class PhoneticMatcher:
    """Indexes domain terms by phonetic alphabet code and performs fast exact/fuzzy matching."""

    def __init__(self) -> None:
        self.exact_index: Dict[str, str] = {}
        self.domain_terms: List[Tuple[str, str]] = []  # (phonetic_code, canonical_word)
        self.valid_single_words: Set[str] = set()

        # 1. Register canonical single-word vocabulary
        all_vocab = set(BASES.keys()) | set(MULTIPLIERS.keys()) | set(DENOMINATIONS.keys()) | CURRENCY_UNITS
        single_words = {t for t in all_vocab if " " not in t}
        self.valid_single_words = single_words

        # Pre-compute phonetic codes
        for term in sorted(single_words):
            code = encode_to_phonetic_alphabet(term)
            if code:
                # Prefer exact base numbers and shorter canonical keys
                if code not in self.exact_index or len(term) < len(self.exact_index[code]):
                    self.exact_index[code] = term
                self.domain_terms.append((code, term))

        # Explicit homophone resolution in auction context:
        # Code 'SEN' maps strictly to 'cem' (100)
        self.exact_index["SEN"] = "cem"

    def match_token(self, token: str) -> Optional[Tuple[str, float]]:
        """Matches a single token to the nearest canonical domain term via phonetic alphabet."""
        if not token or len(token) < 2:
            return None

        clean_token = token.lower()
        if clean_token in PROTECTED_WORDS:
            return None

        p_token = encode_to_phonetic_alphabet(token)
        if not p_token:
            return None

        # 1. Exact phonetic match (0 edit distance in phonetic space)
        if p_token in self.exact_index:
            return self.exact_index[p_token], 1.0

        # 2. Fuzzy phonetic match with strict bounds to prevent false positives
        token_len = len(p_token)
        if token_len <= 2:
            # Short tokens (<= 2 chars like 'sim', 'um', 'e') require exact phonetic match
            return None

        max_allowed_dist = 1 if token_len <= 5 else 2
        min_allowed_sim = 0.75

        best_match: Optional[str] = None
        best_sim = 0.0

        for p_cand, canonical in self.domain_terms:
            if abs(len(p_cand) - token_len) > max_allowed_dist:
                continue

            dist = phonetic_distance(p_token, p_cand)
            if dist <= max_allowed_dist:
                sim = 1.0 - (dist / max(token_len, len(p_cand)))
                if sim >= min_allowed_sim and sim > best_sim:
                    best_sim = sim
                    best_match = canonical

        if best_match is not None:
            return best_match, best_sim

        return None

    def repair_tokens(self, text: str) -> str:
        """Applies phonetic window matching over words to repair near-phonetic and split tokens."""
        raw_tokens = text.split()
        if not raw_tokens:
            return text

        result_tokens: List[str] = []
        i = 0
        n = len(raw_tokens)

        while i < n:
            curr_token = raw_tokens[i].lower()

            # Pass-through protected words
            if curr_token in PROTECTED_WORDS:
                result_tokens.append(raw_tokens[i])
                i += 1
                continue

            # Check 2-token sliding window only if at least one token is unrecognized
            if i + 1 < n:
                next_token = raw_tokens[i + 1].lower()
                is_curr_valid = curr_token in self.valid_single_words or curr_token.isdigit()
                is_next_valid = next_token in self.valid_single_words or next_token.isdigit()

                # Only attempt bigram join if one token is not a recognized standalone word
                if not (is_curr_valid and is_next_valid):
                    bigram = f"{raw_tokens[i]} {raw_tokens[i+1]}"
                    if re.search(r"[a-zA-Z]", bigram):
                        match = self.match_token(bigram)
                        if match:
                            canonical, _ = match
                            result_tokens.append(canonical)
                            i += 2
                            continue

            # Check single token
            if re.search(r"[a-zA-Z]", curr_token):
                if curr_token in self.valid_single_words:
                    result_tokens.append(curr_token)
                    i += 1
                    continue

                match = self.match_token(curr_token)
                if match:
                    canonical, _ = match
                    result_tokens.append(canonical)
                    i += 1
                    continue

            result_tokens.append(raw_tokens[i])
            i += 1

        return " ".join(result_tokens)
