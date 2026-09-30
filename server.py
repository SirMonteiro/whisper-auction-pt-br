import io
import os
import re
import math
import argparse
import warnings
import unicodedata
from typing import Tuple, List

os.environ["PYTHONWARNINGS"] = "ignore"
warnings.filterwarnings("ignore")

import numpy as np
import soundfile as sf
import torch
from flask import Flask, request, jsonify, render_template_string
from transformers import (
    WhisperForConditionalGeneration,
    WhisperProcessor,
    logging as hf_logging,
)

hf_logging.set_verbosity_error()

TARGET_SR = 16000
WHISPER_ID = "openai/whisper-small"

WHISPER_PROMPT = (
    "Lances e pregão de leilão: dobro, dobra, dobre, dobrar, duplico, duplicar, duplicarei, "
    "triplico, triplicar, quadruplicar, mais, vezes, centavos, reais, barão, barões, "
    "pau, paus, conto, pila, onça, garoupa, lobo guará, dois patinhos na lagoa, "
    "idade de cristo, justiça de goiás, três oitão, pernas da xuxa, meia dúzia, duzentão, quinhentão."
)

# ---------------------------------------------------------------------------
# 1. Normalização Léxica & Resolução Fonológica
# ---------------------------------------------------------------------------
def strip_accents(text: str) -> str:
    return "".join(c for c in unicodedata.normalize("NFD", text) if unicodedata.category(c) != "Mn").lower()


IDIOM_MAP = {
    # Fauna das Cédulas do Real
    "beija flor": "1 real",
    "tartaruga": "2 reais",
    "garca": "5 reais",
    "arara": "10 reais",
    "mico leao": "20 reais",
    "mico": "20 reais",
    "onca pintada": "50 reais",
    "onca": "50 reais",
    "oncinha": "50 reais",
    "garoupa": "100 reais",
    "peixe": "100 reais",
    "lobo guara": "200 reais",
    "lobo": "200 reais",
    # Bingo & Jogo do Bicho
    "dois patinhos na lagoa": "22",
    "dois patinhos": "22",
    "casal de patos": "22",
    "idade de cristo": "33",
    "justica de goias": "38",
    "tres oitao": "38",
    "treis oitao": "38",
    "oitao": "38",
    "qua qua qua": "44",
    "bico largo": "44",
    "fim do primeiro tempo": "45",
    "uma boa ideia": "51",
    "boa ideia": "51",
    "dois portugueses numa perna so": "55",
    "dois cachorros do padre": "55",
    "um tapa atras da orelha": "66",
    "as barrigudas": "66",
    "duas velhas": "66",
    "um pra cima e outro pra baixo": "69",
    "par de machados": "77",
    "dois machados": "77",
    "dois bujoes": "88",
    "duas gordas": "88",
    "as pernas da avo": "11",
    "pernas da avo": "11",
    "as pernas da xuxa": "11",
    "duas pernas da xuxa": "11",
    "pernas da xuxa": "11",
    "dois palitos": "11",
    "pau e bola": "10",
    "camisa dez": "10",
    "craque de bola": "10",
    "uma duzia e meia": "18",
    "meia duzia": "6",
    "uma duzia": "12",
    "o azarado": "13",
    "menina bonita": "15",
    "menina moca": "15",
    "debutante": "15",
    "meia telha": "50",
}

DENOMINATIONS = {
    "tartaruga": 2.0, "tartarugas": 2.0,
    "garca": 5.0, "garcas": 5.0, "garcinha": 5.0,
    "arara": 10.0, "araras": 10.0,
    "mico": 20.0, "micos": 20.0, "mico leao": 20.0,
    "onca": 50.0, "oncas": 50.0, "oncinha": 50.0, "onca pintada": 50.0,
    "garoupa": 100.0, "garoupas": 100.0, "peixe": 100.0, "peixes": 100.0,
    "lobo": 200.0, "lobos": 200.0, "lobo guara": 200.0, "lobinho": 200.0, "lobinho guara": 200.0,
}

SINGLE_DIGITS = {
    "zero": 0, "um": 1, "uma": 1, "dois": 2, "duas": 2, "tres": 3,
    "quatro": 4, "cinco": 5, "seis": 6, "sete": 7, "oito": 8, "nove": 9,
}

BASES = {
    "zero": 0, "um": 1, "uma": 1, "unzinho": 1, "umzinho": 1,
    "dois": 2, "duas": 2, "doizinho": 2, "doisao": 2,
    "tres": 3, "quatro": 4,
    "cinco": 5, "cincao": 5, "cinquinho": 5,
    "seis": 6, "sete": 7, "oito": 8, "nove": 9,
    "dez": 10, "dezao": 10, "dezinho": 10, "deizinho": 10,
    "onze": 11, "doze": 12, "treze": 13, "quatorze": 14, "catorze": 14,
    "quinze": 15, "quinzinho": 15,
    "dezesseis": 16, "dezessete": 17, "dezoito": 18, "dezenove": 19,
    "vinte": 20, "vintao": 20, "vintinho": 20, "vintorzinho": 20, "vintim": 20, "vintola": 20,
    "trinta": 30, "trintao": 30, "trintinho": 30, "trintola": 30,
    "quarenta": 40, "quarentao": 40, "quarentola": 40,
    "cinquenta": 50, "cinquentao": 50, "cinquentinha": 50, "cinquentola": 50, "cincoentola": 50,
    "sessenta": 60, "sessentao": 60,
    "setenta": 70, "setentao": 70,
    "oitenta": 80, "oitentao": 80,
    "noventa": 90, "noventao": 90,
    "cem": 100, "cento": 100, "cemzao": 100, "cemzinho": 100, "senzao": 100,
    "duzentos": 200, "duzento": 200, "duzentas": 200, "duzenta": 200, "duzentao": 200, "duzentinho": 200, "duzentola": 200,
    "trezentos": 300, "trezento": 300, "trezentas": 300, "trezenta": 300, "trezentao": 300, "trezentinho": 300,
    "quatrocentos": 400, "quatrocento": 400, "quatrocentas": 400, "quatrocenta": 400, "quatrocentao": 400,
    "quinhentos": 500, "quinhento": 500, "quinhentas": 500, "quinhenta": 500, "quinhentao": 500, "quintinho": 500,
    "seiscentos": 600, "seiscento": 600, "seiscentas": 600, "seiscenta": 600, "seiscentao": 600,
    "setecentos": 700, "setecento": 700, "setecentas": 700, "setecenta": 700, "setecentao": 700,
    "oitocentos": 800, "oitocento": 800, "oitocentas": 800, "oitocenta": 800, "oitocentao": 800,
    "novecentos": 900, "novecento": 900, "novecentas": 900, "novecenta": 900, "novecentao": 900,
}

MULTIPLIERS = {
    "mil": 1000.0, "milzao": 1000.0, "milzinho": 1000.0,
    "barao": 1000.0, "baroes": 1000.0,
    "placa": 1000.0, "placas": 1000.0,
    "milhao": 1e6, "milhoes": 1e6,
    "bilhao": 1e9, "bilhoes": 1e9,
    "trilhao": 1e12, "trilhoes": 1e12,
}


def phonological_repair(text: str) -> str:
    t = strip_accents(text)
    t = t.replace("-", " ")

    en_map = {
        "zero": "zero", "one": "um", "two": "dois", "three": "tres", "four": "quatro",
        "five": "cinco", "six": "seis", "seven": "sete", "eight": "oito", "nine": "nove",
        "ten": "dez", "twenty": "vinte", "fifty": "cinquenta", "hundred": "cem", "thousand": "mil"
    }
    for en_word, pt_word in en_map.items():
        t = re.sub(rf"\b{en_word}\b", pt_word, t)

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
    t = re.sub(r"\b(?:mais|somado\s+a)\b", " + ", t)
    t = re.sub(r"(\d+)[,\.](\d+)", r"\1.\2", t)

    t = re.sub(r"[^\w\s\.\+\*]", " ", t)
    t = re.sub(r"(?<!\d)\.|\.(?!\d)", " ", t)

    t = re.sub(r"\bde\s+uma?\s+vez\b", " ", t)
    t = re.sub(r"\bmais\s+uma?\s+vez\b", " ", t)

    t = re.sub(
        r"\buma?\s+(beija\s+flor|tartaruga|garca|arara|mico\s+leao|mico|onca\s+pintada|onca|oncinha|garoupa|peixe|lobo\s+guara|lobo)\b",
        r"\1",
        t,
    )

    for idiom in sorted(IDIOM_MAP.keys(), key=len, reverse=True):
        t = re.sub(rf"\b{idiom}\b", IDIOM_MAP[idiom], t)

    t = re.sub(r"\bduz[aei]\s+entao\b", "duzentao", t)
    t = re.sub(r"\bquinz[aei]\s+entao\b", "quinhentao", t)
    t = re.sub(r"\bquatr[oe]\s+entao\b", "quatrocentao", t)

    t = re.sub(r"\btu[sz]ent(ao|ola|inho|os)\b", r"duzent\1", t)
    t = re.sub(r"\bsen(s|z)(ao|inho)\b", r"cemzao", t)
    t = re.sub(r"\bcen(s|z)(ao|inho)\b", r"cemzao", t)
    t = re.sub(r"\btresent(ao|os|inho|inhos|ola)\b", r"trezent\1", t)
    t = re.sub(r"\btrent(a|ao|inho|ola)\b", r"trint\1", t)
    t = re.sub(r"\bcincoent", "cinquent", t)

    t = re.sub(r"\bquintinho(s)?\b", "quinhentos", t)
    t = re.sub(r"\bvint(im|em)\b", "vinte", t)
    t = re.sub(r"\bmeio\s+pau\b", "quinhentos reais", t)
    t = re.sub(r"\bmeio\s+barao\b", "quinhentos reais", t)

    return t


def is_number_word(token: str) -> bool:
    if re.match(r"^\d+(?:\.\d+)?$", token):
        return True
    if token in BASES or token in MULTIPLIERS or token in DENOMINATIONS:
        return True
    if token in ("meia", "meio", "real", "reais", "conto", "contos", "continho", "pila", "pilas", "prata", "pratas", "centavo", "centavos"):
        return True
    if token in ("nota", "notas", "cedula", "cedulas", "moeda", "moedas"):
        return True
    return False


def get_token_scale(token: str) -> int:
    if token in SINGLE_DIGITS or token in ("meia", "meio", "zero", "um", "uma", "dois", "duas", "tres", "quatro", "cinco", "seis", "sete", "oito", "nove", "unzinho", "umzinho", "doizinho", "doisao", "cincao", "cinquinho"):
        return 1
    if token in ("dez", "onze", "doze", "treze", "quatorze", "catorze", "quinze", "dezesseis", "dezessete", "dezoito", "dezenove", "quinzinho", "dezao", "dezinho", "deizinho"):
        return 2
    if token in ("vinte", "trinta", "quarenta", "cinquenta", "sessenta", "setenta", "oitenta", "noventa") or token.endswith(("tao", "tola", "tinho", "tinha")):
        return 3
    if token.startswith(("cem", "cento", "senzao", "duzent", "trezent", "quatrocent", "quinhent", "seiscent", "setecent", "oitocent", "novecent", "quintinho")):
        return 4
    if token in MULTIPLIERS:
        return 5
    if token in DENOMINATIONS:
        return 6
    return 0


def parse_subexpression(chunk_text: str) -> float:
    raw_tokens = [t for t in chunk_text.split() if t]
    if not raw_tokens:
        return 0.0

    has_e = "e" in raw_tokens
    has_complex = any(t in BASES and BASES[t] >= 10 for t in raw_tokens) or any(t in MULTIPLIERS for t in raw_tokens)

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

        if re.match(r"^\d+(?:\.\d+)?$", token):
            current += float(token)
            i += 1
            continue

        if two_tokens in DENOMINATIONS:
            denom_val = DENOMINATIONS[two_tokens]
            count = current if current > 0 else 1.0
            total += count * denom_val
            last_multiplier = denom_val
            current = 0.0
            i += 2
            continue
        elif token in DENOMINATIONS:
            denom_val = DENOMINATIONS[token]
            count = current if current > 0 else 1.0
            total += count * denom_val
            last_multiplier = denom_val
            current = 0.0
            i += 1
            continue

        if token in ("nota", "notas", "cedula", "cedulas", "moeda", "moedas"):
            note_multiplier = current if current > 0 else 1.0
            current = 0.0
            if i + 1 < len(raw_tokens) and raw_tokens[i + 1] == "de":
                i += 1
            i += 1
            continue

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

        if token in ("real", "reais", "conto", "contos", "continho", "pila", "pilas", "prata", "pratas"):
            total += current * note_multiplier
            note_multiplier = 1.0
            last_multiplier = 1.0
            current = 0.0
            if i + 1 < len(raw_tokens) and raw_tokens[i + 1] in BASES:
                in_centavos = True
            i += 1
            continue

        if token in ("pau", "paus"):
            if 0 < current < 100:
                total += current * 1000.0
                last_multiplier = 1000.0
            else:
                total += current
                last_multiplier = 1.0
            current = 0.0
            i += 1
            continue

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
                current += 6.0
                i += 1
                continue

        if token in MULTIPLIERS:
            mult = MULTIPLIERS[token]
            current = 1.0 if current == 0.0 else current
            total += current * mult
            last_multiplier = mult
            current = 0.0
            i += 1
            continue

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
    tokens = segment.split()
    chunks = []
    current_chunk = []
    last_scale = None

    for i, t in enumerate(tokens):
        if is_number_word(t):
            scale = get_token_scale(t)
            is_boundary = False
            if current_chunk and last_scale is not None:
                if last_scale == 1 and scale == 1:
                    is_boundary = False
                elif scale in (5, 6):
                    is_boundary = False
                elif last_scale == 5 and scale in (1, 2, 3, 4):
                    is_boundary = False
                elif last_scale > scale and scale in (1, 2, 3, 4):
                    is_boundary = False
                elif scale == 0:
                    is_boundary = False
                else:
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
            if i + 1 < len(tokens) and is_number_word(tokens[i + 1]):
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
    clean_text = phonological_repair(text)

    multiplier_verb_val = None
    if re.search(r"\b(?:dobr\w*|duplic\w*)\b", clean_text):
        multiplier_verb_val = 2.0
    elif re.search(r"\b(?:triplic\w*|tripl\w*)\b", clean_text):
        multiplier_verb_val = 3.0
    elif re.search(r"\b(?:quadruplic\w*|quadrupl\w*)\b", clean_text):
        multiplier_verb_val = 4.0

    plus_segments = clean_text.split("+")
    all_numbers: List[float] = []

    for seg in plus_segments:
        nums = extract_numbers_from_segment(seg)
        all_numbers.extend(nums)

    total_numbers = len(all_numbers)
    plus_count = clean_text.count("+")

    if multiplier_verb_val is not None and total_numbers == 0:
        return multiplier_verb_val, False, True

    if total_numbers == 0:
        return 0.0, False, False

    # Regra estrita: X números (X >= 2) devem ter exatamente X-1 conectivos '+'
    if total_numbers >= 2 and plus_count != (total_numbers - 1):
        return 0.0, False, False

    has_addition = (total_numbers == 1) and (plus_count >= 1)
    has_multiplication = (multiplier_verb_val is not None)

    result = sum(all_numbers)
    if multiplier_verb_val is not None and result > 0:
        result *= multiplier_verb_val

    return float(result), has_addition, has_multiplication


def format_brazilian_currency(val: float) -> str:
    formatted = f"{val:,.2f}"
    return "R$ " + formatted.replace(",", "X").replace(".", ",").replace("X", ".")


# ---------------------------------------------------------------------------
# 2. Inicialização dos Modelos e Flask
# ---------------------------------------------------------------------------
device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
print(f"Carregando {WHISPER_ID} em {device}...")

processor = WhisperProcessor.from_pretrained(WHISPER_ID)
model = WhisperForConditionalGeneration.from_pretrained(
    WHISPER_ID,
    torch_dtype=torch.float16 if device.type == "cuda" else torch.float32
).to(device)
model.eval()

prompt_ids = processor.get_prompt_ids(WHISPER_PROMPT, return_tensors="pt").to(device)
print("✓ Pipeline do Whisper pronto.")

app = Flask(__name__)

HTML_PAGE = """
<!DOCTYPE html>
<html lang="pt-BR">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Leilão Whisper Push-To-Talk</title>
    <style>
        body {
            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
            background: #121214;
            color: #e1e1e6;
            display: flex;
            flex-direction: column;
            align-items: center;
            padding: 30px 20px;
            margin: 0;
        }
        .container {
            width: 100%;
            max-width: 720px;
            display: flex;
            flex-direction: column;
            gap: 16px;
        }
        h1 { margin: 0 0 4px 0; font-size: 1.5rem; color: #fff; }
        p { margin: 0; color: #a8a8b3; font-size: 0.95rem; }
        .ptt-box {
            padding: 24px;
            border: 2px dashed #323238;
            border-radius: 8px;
            text-align: center;
            background: #202024;
            transition: all 0.2s ease;
            user-select: none;
        }
        .ptt-box.recording {
            border-color: #e54242;
            background: #2c1515;
            color: #ff8585;
            box-shadow: 0 0 16px rgba(229, 66, 66, 0.3);
        }
        .controls {
            display: flex;
            justify-content: space-between;
            align-items: center;
        }
        button {
            background: #04d361;
            color: #000;
            font-weight: bold;
            border: none;
            padding: 10px 18px;
            border-radius: 4px;
            cursor: pointer;
            transition: background 0.2s;
        }
        button:hover { background: #00b352; }
        button:active { transform: scale(0.98); }
        textarea {
            width: 100%;
            height: 340px;
            background: #09090a;
            border: 1px solid #323238;
            color: #00ff66;
            font-family: "JetBrains Mono", Consolas, Monaco, monospace;
            font-size: 0.88rem;
            padding: 14px;
            border-radius: 6px;
            box-sizing: border-box;
            resize: vertical;
            line-height: 1.4;
        }
    </style>
</head>
<body>
    <div class="container">
        <div>
            <h1>Leilão Whisper Push-to-Talk</h1>
            <p>Clique nesta página e <strong>SEGURE [ESPAÇO]</strong> para falar. Solte para processar o lance.</p>
        </div>

        <div id="ptt-indicator" class="ptt-box">
            Aguardando microfone... (Pressione e segure ESPAÇO)
        </div>

        <div class="controls">
            <span id="status-label" style="font-size: 0.85rem; color: #737380;">Status: Pronto</span>
            <button onclick="copyLogs()">Copiar Tudo</button>
        </div>

        <textarea id="log-output" readonly placeholder="Os lances decodificados aparecerão aqui..."></textarea>
    </div>

    <script>
        let isSpacePressed = false;
        let audioContext;
        let mediaStream;
        let audioInput;
        let processorNode;
        let pcmData = [];

        const pttBox = document.getElementById("ptt-indicator");
        const statusLabel = document.getElementById("status-label");
        const logArea = document.getElementById("log-output");

        // Solicita acesso ao microfone ao carregar
        navigator.mediaDevices.getUserMedia({ audio: true })
            .then(s => {
                mediaStream = s;
                pttBox.innerText = "PRONTO: Segure ESPAÇO para falar";
            })
            .catch(err => {
                pttBox.innerText = "Erro ao acessar microfone: " + err;
                pttBox.style.borderColor = "red";
            });

        window.addEventListener("keydown", (e) => {
            if (e.code === "Space" && !isSpacePressed && mediaStream) {
                e.preventDefault();
                isSpacePressed = true;
                startRecording();
            }
        });

        window.addEventListener("keyup", (e) => {
            if (e.code === "Space" && isSpacePressed) {
                e.preventDefault();
                isSpacePressed = false;
                stopRecording();
            }
        });

        function startRecording() {
            pcmData = [];
            // Cria AudioContext com a taxa nativa de 16 kHz para evitar qualquer incompatibilidade
            audioContext = new (window.AudioContext || window.webkitAudioContext)({ sampleRate: 16000 });
            audioInput = audioContext.createMediaStreamSource(mediaStream);

            // Buffer de captura de áudio direto
            processorNode = audioContext.createScriptProcessor(4096, 1, 1);
            processorNode.onaudioprocess = (e) => {
                if (!isSpacePressed) return;
                const channelData = e.inputBuffer.getChannelData(0);
                pcmData.push(new Float32Array(channelData));
            };

            audioInput.connect(processorNode);
            processorNode.connect(audioContext.destination);

            pttBox.classList.add("recording");
            pttBox.innerText = "● GRAVANDO LANCE... (Solte ESPAÇO para finalizar)";
            statusLabel.innerText = "Status: Capturando áudio...";
        }

        function stopRecording() {
            if (processorNode && audioInput) {
                audioInput.disconnect();
                processorNode.disconnect();
            }

            pttBox.classList.remove("recording");
            pttBox.innerText = "Processando lance...";
            statusLabel.innerText = "Status: Codificando WAV 16kHz...";

            const totalLength = pcmData.reduce((acc, chunk) => acc + chunk.length, 0);
            if (totalLength === 0) {
                pttBox.innerText = "PRONTO: Segure ESPAÇO para falar";
                statusLabel.innerText = "Status: Nenhum áudio gravado";
                return;
            }

            const flatPcm = new Float32Array(totalLength);
            let offset = 0;
            for (let chunk of pcmData) {
                flatPcm.set(chunk, offset);
                offset += chunk.length;
            }

            const sampleRate = audioContext.sampleRate;
            if (audioContext && audioContext.state !== "closed") {
                audioContext.close();
            }

            const wavBlob = encodeWAV(flatPcm, sampleRate);
            sendAudioToServer(wavBlob);
        }

        // Codificador nativo WAV PCM 16-bit
        function encodeWAV(samples, sampleRate) {
            const buffer = new ArrayBuffer(44 + samples.length * 2);
            const view = new DataView(buffer);

            const writeString = (view, offset, string) => {
                for (let i = 0; i < string.length; i++) {
                    view.setUint8(offset + i, string.charCodeAt(i));
                }
            };

            writeString(view, 0, 'RIFF');
            view.setUint32(4, 36 + samples.length * 2, true);
            writeString(view, 8, 'WAVE');
            writeString(view, 12, 'fmt ');
            view.setUint32(16, 16, true);
            view.setUint16(20, 1, true); // PCM format
            view.setUint16(22, 1, true); // Mono
            view.setUint32(24, sampleRate, true);
            view.setUint32(28, sampleRate * 2, true); // byte rate
            view.setUint16(32, 2, true); // block align
            view.setUint16(34, 16, true); // 16-bit
            writeString(view, 36, 'data');
            view.setUint32(40, samples.length * 2, true);

            // Converte float [-1.0, 1.0] para 16-bit PCM
            let index = 44;
            for (let i = 0; i < samples.length; i++) {
                let s = Math.max(-1, Math.min(1, samples[i]));
                view.setInt16(index, s < 0 ? s * 0x8000 : s * 0x7FFF, true);
                index += 2;
            }

            return new Blob([view], { type: 'audio/wav' });
        }

        function sendAudioToServer(wavBlob) {
            statusLabel.innerText = "Status: Enviando ao Whisper...";
            const formData = new FormData();
            formData.append("audio", wavBlob, "speech.wav");

            fetch("/transcribe", {
                method: "POST",
                body: formData
            })
            .then(res => res.json())
            .then(data => {
                pttBox.innerText = "PRONTO: Segure ESPAÇO para falar";
                statusLabel.innerText = "Status: Concluído";
                
                if (data.error) {
                    appendLog(`[ERRO]: ${data.error}\\n`);
                    return;
                }

                const entry = `[ASR]: "${data.raw_transcript}"\\n` +
                              `[FLOAT]: ${data.value.toFixed(2)}f (${data.currency}) | Add: ${data.has_addition} | Mult: ${data.has_multiplication}\\n` +
                              `--------------------------------------------------\\n`;
                appendLog(entry);
            })
            .catch(err => {
                pttBox.innerText = "PRONTO: Segure ESPAÇO para falar";
                statusLabel.innerText = "Status: Erro na requisição";
                appendLog(`[ERRO DE CONEXÃO]: ${err}\\n`);
            });
        }

        function appendLog(text) {
            logArea.value += text;
            logArea.scrollTop = logArea.scrollHeight;
        }

        function copyLogs() {
            navigator.clipboard.writeText(logArea.value).then(() => {
                const btn = document.querySelector(".controls button");
                const oldText = btn.innerText;
                btn.innerText = "Copiado!";
                setTimeout(() => btn.innerText = oldText, 1500);
            });
        }
    </script>
</body>
</html>
"""

@app.route("/")
def index():
    return render_template_string(HTML_PAGE)

@app.route("/transcribe", methods=["POST"])
def transcribe_endpoint():
    if "audio" not in request.files:
        return jsonify({"error": "Nenhum arquivo de áudio enviado"}), 400

    audio_file = request.files["audio"]
    audio_bytes = audio_file.read()

    try:
        # Lê o WAV padrão enviado diretamente pelo browser
        data, orig_sr = sf.read(io.BytesIO(audio_bytes), dtype="float32")
        
        if data.ndim > 1:
            data = data.mean(axis=1)

        # Fallback de resample caso o browser use outra taxa nativa
        if orig_sr != TARGET_SR:
            import scipy.signal
            gcd = math.gcd(int(orig_sr), int(TARGET_SR))
            up = TARGET_SR // gcd
            down = orig_sr // gcd
            audio_16k = scipy.signal.resample_poly(data, up, down).astype(np.float32)
        else:
            audio_16k = data

        duration = len(audio_16k) / TARGET_SR
        if duration < 0.25:
            return jsonify({"error": "Áudio muito curto"}), 400

        inputs = processor(
            audio_16k,
            sampling_rate=TARGET_SR,
            return_tensors="pt"
        ).input_features.to(device, dtype=model.dtype)

        with torch.no_grad():
            predicted_ids = model.generate(
                inputs,
                language="pt",
                task="transcribe",
                prompt_ids=prompt_ids
            )
            raw_text = processor.batch_decode(
                predicted_ids,
                skip_special_tokens=True,
                clean_up_tokenization_spaces=False
            )[0].strip()

        final_val, has_add, has_mult = evaluate_auction_bid(raw_text)
        currency_str = format_brazilian_currency(final_val)

        print("\n================================")
        print(f"1. ASR Bruto: \"{raw_text}\"")
        print(f"2. Valor C Float: {final_val:.2f}f ({currency_str}) | has_addition: {has_add} | has_multiplication: {has_mult}")
        print("================================")

        return jsonify({
            "raw_transcript": raw_text,
            "value": final_val,
            "currency": currency_str,
            "has_addition": has_add,
            "has_multiplication": has_mult
        })

    except Exception as e:
        print(f"[ERRO]: {e}")
        return jsonify({"error": str(e)}), 500


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Servidor HTTP Leilão Whisper Push-to-Talk")
    parser.add_argument("--port", type=int, default=5000, help="Porta do servidor HTTP (padrão: 5000)")
    parser.add_argument("--host", type=str, default="127.0.0.1", help="Host (ex: 127.0.0.1 ou 0.0.0.0)")
    cli_args = parser.parse_args()

    print(f"\n🚀 Servidor web iniciado! Abra em seu navegador: http://{cli_args.host}:{cli_args.port}\n")
    app.run(host=cli_args.host, port=cli_args.port, debug=False)
