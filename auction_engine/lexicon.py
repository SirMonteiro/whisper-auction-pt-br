"""Lexicon and domain vocabulary tables for Brazilian Portuguese auction bidding."""

from typing import Dict

IDIOM_MAP: Dict[str, str] = {
    # Fauna das Cédulas do Real (Cédula -> Valor equivalente)
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
    "lobinho": "200 reais",
    # Bingo & Jogo do Bicho
    "dois patinhos na lagoa": "22",
    "dois patinhos": "22",
    "casal de patos": "22",
    "dois patos": "22",
    "idade de cristo": "33",
    "a idade de cristo": "33",
    "justica de goias": "38",
    "tres oitao": "38",
    "treis oitao": "38",
    "oitao": "38",
    "qua qua qua": "44",
    "bico largo": "44",
    "quatro patos": "44",
    "fim do primeiro tempo": "45",
    "uma boa ideia": "51",
    "boa ideia": "51",
    "a cana": "51",
    "dois portugueses numa perna so": "55",
    "dois cachorros do padre": "55",
    "cachorro do padre": "55",
    "um tapa atras da orelha": "66",
    "as barrigudas": "66",
    "duas velhas": "66",
    "um pra cima e outro pra baixo": "69",
    "um que vai outro que vem": "69",
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
    "entre na fila": "11",
    "pau e bola": "10",
    "camisa dez": "10",
    "craque de bola": "10",
    "uma duzia e meia": "18",
    "meia duzia": "6",
    "meia duzias": "6",
    "meia duzi": "6",
    "uma duzia": "12",
    "uma duzias": "12",
    "uma duzi": "12",
    "dois patinhos da lago": "22",
    "dois patinhos da lagoas": "22",
    "dois patinhos de lagoa": "22",
    "tres patinhos": "3",
    "o azarado": "13",
    "deu azar": "13",
    "numero do zagallo": "13",
    "a debutante": "15",
    "debutante": "15",
    "a menina bonita": "15",
    "menina bonita": "15",
    "menina moca": "15",
    "maioridade": "18",
    "rapazinho alegre": "24",
    "numero feio": "24",
    "dia de natal": "25",
    "natal": "25",
    "meteu num 31": "31",
    "o louco": "35",
    "meia telha": "50",
    "meio caminho": "50",
}

DENOMINATIONS: Dict[str, float] = {
    "tartaruga": 2.0, "tartarugas": 2.0,
    "garca": 5.0, "garcas": 5.0, "garcinha": 5.0, "garcinhas": 5.0,
    "arara": 10.0, "araras": 10.0, "ararinha": 10.0,
    "mico": 20.0, "micos": 20.0, "mico leao": 20.0, "micos leoes": 20.0,
    "onca": 50.0, "oncas": 50.0, "oncinha": 50.0, "oncinhas": 50.0, "onca pintada": 50.0,
    "garoupa": 100.0, "garoupas": 100.0, "peixe": 100.0, "peixes": 100.0,
    "lobo": 200.0, "lobos": 200.0, "lobo guara": 200.0, "lobos guara": 200.0,
    "lobinho": 200.0, "lobinho guara": 200.0,
}

SINGLE_DIGITS: Dict[str, int] = {
    "zero": 0, "um": 1, "uma": 1, "dois": 2, "duas": 2, "tres": 3,
    "quatro": 4, "cinco": 5, "seis": 6, "sete": 7, "oito": 8, "nove": 9,
}

BASES: Dict[str, int] = {
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

MULTIPLIERS: Dict[str, float] = {
    "mil": 1000.0, "milzao": 1000.0, "milzinho": 1000.0,
    "barao": 1000.0, "baroes": 1000.0,
    "placa": 1000.0, "placas": 1000.0,
    "milhao": 1e6, "milhoes": 1e6,
    "bilhao": 1e9, "bilhoes": 1e9,
    "trilhao": 1e12, "trilhoes": 1e12,
}

CURRENCY_UNITS = {
    "real", "reais", "conto", "contos", "continho",
    "pila", "pilas", "prata", "pratas", "centavo", "centavos",
    "pau", "paus"
}

NOTE_DENOMINATORS = {
    "nota", "notas", "cedula", "cedulas", "moeda", "moedas"
}

PASS_PHRASES = {
    "passo", "to fora", "nao quero", "desisto", "sem lance",
    "deixa passar", "nao dou", "to fora dessa"
}
