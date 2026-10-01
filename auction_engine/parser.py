"""Grammar parsing, number synthesis, and auction bid evaluation logic."""

import re
import math
from typing import List, Tuple
from .lexicon import (
    BASES,
    MULTIPLIERS,
    DENOMINATIONS,
    SINGLE_DIGITS,
    CURRENCY_UNITS,
    NOTE_DENOMINATORS,
    PASS_PHRASES,
)
from .phonetics import phonological_repair
from .currency import format_brazilian_currency
from .models import BidResult


def is_number_word(token: str) -> bool:
    """Checks if a token corresponds to any known numeral, denomination, or currency unit."""
    if re.match(r"^\d+(?:\.\d+)?$", token):
        return True
    if token in BASES or token in MULTIPLIERS or token in DENOMINATIONS:
        return True
    if token in ("meia", "meio") or token in CURRENCY_UNITS:
        return True
    if token in NOTE_DENOMINATORS:
        return True
    return False


def get_token_scale(token: str) -> int:
    """Returns the scale level of a numeral token for syntactic boundary analysis.
    
    Scale hierarchy:
    1: Units (0-9, diminutives/augmentatives of units, meia/meio)
    2: Teens (10-19: dez, onze, doze, treze, ...)
    3: Tens (20-90: vinte, trinta, quarenta, ...)
    4: Hundreds (100-900: cem, cento, duzentos, ...)
    5: Multipliers (1000+: mil, barao, placa, milhao, ...)
    6: Denominations (Real banknotes/fauna: tartaruga, garca, onca, peixe, ...)
    0: Auxiliary tokens (currency units, notes, etc.)
    """
    if re.match(r"^\d+(?:\.\d+)?$", token):
        try:
            val = float(token)
            if val < 10:
                return 1
            elif val < 20:
                return 2
            elif val < 100:
                return 3
            elif val < 1000:
                return 4
            else:
                return 5
        except ValueError:
            return 1
    if (
        token in SINGLE_DIGITS
        or token in ("meia", "meio", "zero", "um", "uma", "dois", "duas", "tres",
                     "quatro", "cinco", "seis", "sete", "oito", "nove", "unzinho",
                     "umzinho", "doizinho", "doisao", "cincao", "cinquinho")
    ):
        return 1
    if token in (
        "dez", "onze", "doze", "treze", "quatorze", "catorze", "quinze",
        "dezesseis", "dezessete", "dezoito", "dezenove", "quinzinho",
        "dezao", "dezinho", "deizinho"
    ):
        return 2
    if (
        token in ("vinte", "trinta", "quarenta", "cinquenta", "sessenta",
                  "setenta", "oitenta", "noventa")
        or token.endswith(("tao", "tola", "tinho", "tinha"))
    ):
        return 3
    if token.startswith((
        "cem", "cento", "senzao", "duzent", "trezent", "quatrocent",
        "quinhent", "seiscent", "setecent", "oitocent", "novecent", "quintinho"
    )):
        return 4
    if token in MULTIPLIERS or token in ("pau", "paus"):
        return 5
    if token in DENOMINATIONS:
        return 6
    return 0


def parse_subexpression(chunk_text: str) -> float:
    """Parses a contiguous chunk of numeral tokens into a single numeric value."""
    raw_tokens = [t for t in chunk_text.split() if t]
    if not raw_tokens:
        return 0.0

    has_e = "e" in raw_tokens
    has_complex = any(t in BASES and BASES[t] >= 10 for t in raw_tokens) or any(t in MULTIPLIERS for t in raw_tokens)

    # Fast path: sequence of digits or digit names without connectors ("sete seis" -> 76, "quatro quatro" -> 44)
    digit_seq = []
    for t in raw_tokens:
        if t in ("meia", "meio"):
            digit_seq.append("6")
        elif t in SINGLE_DIGITS:
            digit_seq.append(str(SINGLE_DIGITS[t]))
        elif re.match(r"^\d$", t):
            digit_seq.append(t)

    if len(digit_seq) >= 2 and not has_e and not has_complex:
        try:
            return float("".join(digit_seq))
        except ValueError:
            pass

    total = 0.0
    current = 0.0
    centavos = 0.0
    note_multiplier = 1.0
    last_multiplier = 0.0
    in_centavos = False

    i = 0
    while i < len(raw_tokens):
        token = raw_tokens[i]
        two_tokens = f"{token} {raw_tokens[i+1]}" if i + 1 < len(raw_tokens) else ""

        # Raw numeric digits (e.g. 50, 100, 100.0)
        if re.match(r"^\d+(?:\.\d+)?$", token):
            current += float(token)
            i += 1
            continue

        # Two-word denomination (e.g. "mico leao", "lobo guara")
        if two_tokens in DENOMINATIONS:
            denom_val = DENOMINATIONS[two_tokens]
            count = current if current > 0 else 1.0
            total += count * denom_val
            last_multiplier = denom_val
            current = 0.0
            i += 2
            continue
        # Single-word denomination (e.g. "onca", "tartaruga", "garoupa")
        elif token in DENOMINATIONS:
            denom_val = DENOMINATIONS[token]
            count = current if current > 0 else 1.0
            total += count * denom_val
            last_multiplier = denom_val
            current = 0.0
            i += 1
            continue

        # Note/Coin quantifiers: "duas notas de cem"
        if token in NOTE_DENOMINATORS:
            note_multiplier = current if current > 0 else 1.0
            current = 0.0
            if i + 1 < len(raw_tokens) and raw_tokens[i + 1] == "de":
                i += 1
            i += 1
            continue

        # Centavos
        if token in ("centavo", "centavos"):
            if in_centavos:
                total += centavos / 100.0
                centavos = 0.0
                in_centavos = False
            elif current > 0:
                total += current / 100.0
                current = 0.0
            i += 1
            continue

        # Direct currency units: "reais", "conto", "pila", "prata"
        if token in ("real", "reais", "conto", "contos", "continho", "pila", "pilas", "prata", "pratas"):
            total += current * note_multiplier
            note_multiplier = 1.0
            last_multiplier = 1.0
            current = 0.0
            if i + 1 < len(raw_tokens) and raw_tokens[i + 1] in BASES:
                in_centavos = True
            i += 1
            continue

        # "pau" / "paus" (informal magnitude rule)
        if token in ("pau", "paus"):
            if 0 < current < 100:
                total += current * 1000.0
                last_multiplier = 1000.0
            elif current == 0:
                # Standalone "um pau" or just "pau"
                total += 1000.0
                last_multiplier = 1000.0
            else:
                total += current
                last_multiplier = 1.0
            current = 0.0
            i += 1
            continue

        # "meio" / "meia"
        if token in ("meio", "meia"):
            prev_token = raw_tokens[i - 1] if i > 0 else ""
            if prev_token == "e":
                if last_multiplier > 0:
                    total += 0.5 * last_multiplier
                elif current > 0:
                    current += 0.5
                elif total > 0:
                    total += 0.5
                i += 1
                continue
            else:
                # Standalone "meia" in bingo / auction typically signifies 6
                current += 6.0
                i += 1
                continue

        # Multipliers (mil, barao, placa, milhao, etc.)
        if token in MULTIPLIERS:
            mult = MULTIPLIERS[token]
            current = 1.0 if current == 0.0 else current
            total += current * mult
            last_multiplier = mult
            current = 0.0
            i += 1
            continue

        # Base numbers
        if token in BASES:
            val = float(BASES[token])
            if in_centavos:
                centavos += val
            else:
                current += val
            i += 1
            continue

        i += 1

    total += current * note_multiplier
    if centavos > 0:
        total += centavos / 100.0

    return float(total)


def extract_numbers_from_segment(segment: str) -> List[float]:
    """Extracts logically coherent numbers from a string segment respecting Portuguese syntax."""
    tokens = segment.split()
    chunks = []
    current_chunk = []
    last_scale = None

    for i, t in enumerate(tokens):
        if is_number_word(t):
            scale = get_token_scale(t)
            is_boundary = False

            if current_chunk and last_scale is not None:
                # Two single digits without "e" (e.g. "sete seis" -> 76)
                if last_scale == 1 and scale == 1:
                    is_boundary = False
                # Count followed by multiplier (5) or denomination (6), e.g. "duas" + "oncas"
                elif last_scale in (1, 2, 3, 4) and scale in (5, 6):
                    is_boundary = False
                # Multiplier followed by lower scales: "mil" (5) + "duzentos" (4)
                elif last_scale == 5 and scale in (1, 2, 3, 4):
                    is_boundary = False
                # Hundreds (4) followed by Tens (3), Teens (2), or Units (1)
                elif last_scale == 4 and scale in (1, 2, 3):
                    is_boundary = False
                # Tens (3) followed ONLY by Units (1): "vinte e um", "trinta e cinco"
                elif last_scale == 3 and scale == 1:
                    is_boundary = False
                # Auxiliary currency tokens
                elif scale == 0:
                    is_boundary = False
                else:
                    # Syntactic mismatch: e.g. Tens (3) + Teens (2) like "trinta e dez" -> BOUNDARY!
                    # Tens (3) + Tens (3) like "trinta e vinte" -> BOUNDARY!
                    # Teens (2) + Units (1) like "doze e tres" -> BOUNDARY!
                    is_boundary = True

            if is_boundary:
                if current_chunk:
                    val = parse_subexpression(" ".join(current_chunk))
                    if val > 0:
                        chunks.append(val)
                current_chunk = [t]
            else:
                current_chunk.append(t)
            last_scale = scale

        elif t in ("e", "de") and current_chunk:
            # Check if followed by another number word
            if i + 1 < len(tokens) and is_number_word(tokens[i + 1]):
                next_scale = get_token_scale(tokens[i + 1])
                # Grammatical validation for connector "e":
                # Tens (3) + "e" + Teens (2) or Tens (3) is forbidden in Portuguese!
                if last_scale == 3 and next_scale in (2, 3):
                    # Boundary triggered at "e"
                    val = parse_subexpression(" ".join(current_chunk))
                    if val > 0:
                        chunks.append(val)
                    current_chunk = []
                    last_scale = None
                else:
                    current_chunk.append(t)
            else:
                if current_chunk:
                    val = parse_subexpression(" ".join(current_chunk))
                    if val > 0:
                        chunks.append(val)
                current_chunk = []
                last_scale = None
        else:
            if current_chunk:
                val = parse_subexpression(" ".join(current_chunk))
                if val > 0:
                    chunks.append(val)
            current_chunk = []
            last_scale = None

    if current_chunk:
        val = parse_subexpression(" ".join(current_chunk))
        if val > 0:
            chunks.append(val)

    return chunks


def evaluate_auction_bid(text: str) -> Tuple[float, bool, bool]:
    """Legacy compatibility function returning (value, has_addition, has_multiplication)."""
    result = AuctionBidEvaluator().evaluate(text)
    return result.value, result.has_addition, result.has_multiplication


class AuctionBidEvaluator:
    """Evaluates auction voice utterances and extracts structured bid outcomes."""

    def evaluate(self, text: str) -> BidResult:
        if not text or not text.strip():
            return BidResult(
                value=0.0,
                currency=format_brazilian_currency(0.0),
                has_addition=False,
                has_multiplication=False,
                raw_text=text,
                cleaned_text="",
                is_valid=False,
                action_type="NONE",
            )

        clean_text = phonological_repair(text)

        # Check for pass phrases ("passo", "tô fora", etc.)
        for phrase in PASS_PHRASES:
            if re.search(rf"\b{phrase}\b", clean_text):
                return BidResult(
                    value=0.0,
                    currency=format_brazilian_currency(0.0),
                    has_addition=False,
                    has_multiplication=False,
                    raw_text=text,
                    cleaned_text=clean_text,
                    is_valid=True,
                    action_type="PASS",
                )

        # Multiplier verbs
        multiplier_verb_val = None
        if re.search(r"\b(?:dobr\w*|duplic\w*)\b", clean_text):
            multiplier_verb_val = 2.0
        elif re.search(r"\b(?:triplic\w*|tripl\w*)\b", clean_text):
            multiplier_verb_val = 3.0
        elif re.search(r"\b(?:quadr[ue]plic\w*|quadr[ue]pl\w*)\b", clean_text):
            multiplier_verb_val = 4.0

        # Segment by addition operator '+'
        plus_segments = clean_text.split("+")
        all_numbers: List[float] = []

        for seg in plus_segments:
            nums = extract_numbers_from_segment(seg)
            all_numbers.extend(nums)

        total_numbers = len(all_numbers)
        plus_count = clean_text.count("+")

        # Standalone multiplier (e.g. "Dobro!", "Triplica!", "Quadreplico!")
        if multiplier_verb_val is not None and total_numbers == 0:
            return BidResult(
                value=multiplier_verb_val,
                currency=format_brazilian_currency(multiplier_verb_val),
                has_addition=False,
                has_multiplication=True,
                raw_text=text,
                cleaned_text=clean_text,
                is_valid=True,
                action_type="MULTIPLY_ONLY",
            )

        # No numbers found
        if total_numbers == 0:
            return BidResult(
                value=0.0,
                currency=format_brazilian_currency(0.0),
                has_addition=False,
                has_multiplication=False,
                raw_text=text,
                cleaned_text=clean_text,
                is_valid=False,
                action_type="NONE",
            )

        # Auction connector and repetition evaluation:
        # 1. When "mais" and a single number (e.g. "mais dois"):
        #    Add = True, value = 2.0 (incremental bid).
        # 2. When two or more numbers are detected all connected by "mais" (e.g. "dois mais dois"):
        #    Calculate sum (4.0), and leave Add = False (absolute calculated bid).
        # 3. When multiple numbers are detected without "mais" connectors between all of them:
        #    Pick the last detected number! (e.g. "50, 40, 20" -> 20.0, "Vinte-sete, vinte-se, vint-sate" -> 27.0).
        if total_numbers == 1:
            result = all_numbers[0]
            has_addition = (plus_count >= 1)
        elif total_numbers >= 2:
            if plus_count == (total_numbers - 1):
                # Two or more numbers all connected by 'mais': sum and set Add = False
                result = sum(all_numbers)
                has_addition = False
            else:
                # Multiple numbers not all connected by '+': pick last detected one
                result = all_numbers[-1]
                has_addition = False
        else:
            result = 0.0
            has_addition = False

        has_multiplication = (multiplier_verb_val is not None)
        if multiplier_verb_val is not None and result > 0:
            result *= multiplier_verb_val

        return BidResult(
            value=float(result),
            currency=format_brazilian_currency(result),
            has_addition=has_addition,
            has_multiplication=has_multiplication,
            raw_text=text,
            cleaned_text=clean_text,
            is_valid=True,
            action_type="BID",
            numbers_found=all_numbers,
        )
