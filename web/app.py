"""Flask web application factory for auction speech test environment."""

from flask import Flask, request, jsonify, render_template_string
from asr.base import ITranscriber
from auction_engine.parser import AuctionBidEvaluator
from .templates import HTML_PAGE


def create_app(
    transcriber: ITranscriber,
    evaluator: AuctionBidEvaluator,
) -> Flask:
    """Creates and configures the Flask application injecting dependencies."""
    app = Flask(__name__)

    @app.route("/")
    def index():
        return render_template_string(HTML_PAGE)

    @app.route("/transcribe", methods=["POST"])
    def transcribe_endpoint():
        if "audio" not in request.files:
            return jsonify({"error": "Nenhum arquivo de áudio enviado"}), 400

        audio_file = request.files["audio"]
        audio_bytes = audio_file.read()

        if not audio_bytes:
            return jsonify({"error": "Arquivo de áudio vazio"}), 400

        try:
            req_language = request.form.get("language")
            raw_text = transcriber.transcribe_bytes(audio_bytes, language=req_language)
            bid_result = evaluator.evaluate(raw_text)

            print("\n================================")
            print(f"1. ASR Bruto: \"{raw_text}\"")
            print(
                f"2. Valor C Float: {bid_result.value:.2f}f ({bid_result.currency}) | "
                f"has_addition: {bid_result.has_addition} | has_multiplication: {bid_result.has_multiplication}"
            )
            print("================================")

            return jsonify(bid_result.to_dict())

        except Exception as e:
            print(f"[ERRO]: {e}")
            return jsonify({"error": str(e)}), 500

    return app
