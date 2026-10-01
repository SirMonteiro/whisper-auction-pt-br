"""Main HTTP Server Entrypoint for Auction Voice Bid Testing.

This script wires together the ASR Whisper transcriber, the linguistic auction
evaluation engine, and the web testing environment.
"""

import argparse
from asr.whisper_transcriber import WhisperTranscriber
from auction_engine.parser import AuctionBidEvaluator, evaluate_auction_bid
from auction_engine.currency import format_brazilian_currency
from auction_engine.phonetics import phonological_repair
from web.app import create_app

__all__ = [
    "evaluate_auction_bid",
    "format_brazilian_currency",
    "phonological_repair",
    "WhisperTranscriber",
    "AuctionBidEvaluator",
    "create_app",
]


def main():
    parser = argparse.ArgumentParser(description="Servidor HTTP Leilão Whisper Push-to-Talk")
    parser.add_argument("--port", type=int, default=5000, help="Porta do servidor HTTP (padrão: 5000)")
    parser.add_argument("--host", type=str, default="127.0.0.1", help="Host (ex: 127.0.0.1 ou 0.0.0.0)")
    parser.add_argument("--model", type=str, default="openai/whisper-small", help="Modelo Whisper HuggingFace")
    parser.add_argument("--language", type=str, default="pt", help="Código de idioma para transcrição Whisper (padrão: pt)")
    cli_args = parser.parse_args()

    transcriber = WhisperTranscriber(model_id=cli_args.model, language=cli_args.language)
    evaluator = AuctionBidEvaluator()
    app = create_app(transcriber, evaluator)

    print(f"\n🚀 Servidor web iniciado! Abra em seu navegador: http://{cli_args.host}:{cli_args.port}\n")
    app.run(host=cli_args.host, port=cli_args.port, debug=False)


if __name__ == "__main__":
    main()
