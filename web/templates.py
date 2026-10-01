"""HTML/JS Push-to-Talk web interface template for test environment."""

HTML_PAGE = """<!DOCTYPE html>
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
            // Cria AudioContext com a taxa de 16 kHz
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

        // Codificador WAV PCM 16-bit
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
            view.setUint16(20, 1, true); // PCM
            view.setUint16(22, 1, true); // Mono
            view.setUint32(24, sampleRate, true);
            view.setUint32(28, sampleRate * 2, true); // byte rate
            view.setUint16(32, 2, true); // block align
            view.setUint16(34, 16, true); // 16-bit
            writeString(view, 36, 'data');
            view.setUint32(40, samples.length * 2, true);

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
