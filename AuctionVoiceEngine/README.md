# Auction Voice Engine for Unity (C# / Unity AI Inference Engine)

Arquitetura em **C#** desenvolvida segundo os princípios **SOLID** para execução do modelo Whisper finetunado em formato **.onnx** utilizando o **Unity AI Inference Engine** (anteriormente Unity Sentis) e motor completo de pré-processamento de áudio, extração de log-mel espectrogramas, decodificação neural, normalização fonética, reparo dialetal e sintaxe aritmética de leilões em jogos Unity.

**100% nativo Unity**: Não requer bibliotecas ou plugins de terceiros além deste pacote e do pacote oficial `com.unity.inference-engine`.

---

## 🏛️ Arquitetura SOLID

```
AuctionVoiceEngine/
├── package.json                               # Manifesto UPM (dependência oficial: com.unity.inference-engine)
├── README.md                                  # Documentação técnica e guia de integração
├── Runtime/
│   ├── AuctionVoiceEngine.asmdef              # Assembly Definition modular (referência Unity.InferenceEngine)
│   ├── Core/
│   │   ├── Models/
│   │   │   ├── AuctionActionType.cs           # Enum de intenção semântica (Bid, Pass, MultiplyOnly, etc.)
│   │   │   └── BidResult.cs                   # DTO serializável contendo valor, moeda, flags e lances
│   │   ├── Lexicon/
│   │   │   ├── AuctionLexicon.cs              # Tabelas de fauna do Real, gírias de pregão e bingo
│   │   │   └── CurrencyFormatter.cs           # Formatador financeiro padrão Real (R$ 1.000,00)
│   │   ├── Phonetics/
│   │   │   ├── IPhoneticEncoder.cs            # Interface G2P (Grapheme-to-Phoneme)
│   │   │   ├── BrazilianPortuguesePhoneticEncoder.cs # Fonética do Português Brasileiro (PB)
│   │   │   ├── LevenshteinDistance.cs         # Cálculo de distância de edição e similaridade
│   │   │   ├── IPhoneticMatcher.cs            # Interface de casamento fonético de vocabulário
│   │   │   └── PhoneticMatcher.cs             # Matcher fonético com janela deslizante de bigramas
│   │   └── Services/
│   │       ├── IPhonologyService.cs           # Interface de reparo dialetal e fonológico
│   │       ├── PhonologicalRepairer.cs        # DFA master regex de expressões idiomáticas e gírias
│   │       ├── IAuctionBidEvaluator.cs        # Interface de avaliação de lances
│   │       └── AuctionBidEvaluator.cs         # Analisador sintático de numerais, somas e repetições
│   ├── Audio/
│   │   ├── IAudioInputProvider.cs             # Abstração de fontes de áudio (ISP/DIP)
│   │   ├── AudioPreprocessor.cs               # Condicionamento de áudio (RMS gating, mono downmix, resampling)
│   │   └── UnityAudioRecorder.cs              # Gravador nativo via UnityEngine.Microphone (16kHz mono)
│   ├── Inference/
│   │   ├── ITranscriptionService.cs           # Abstração de motores ASR / Speech-to-Text
│   │   ├── ILogMelSpectrogramExtractor.cs     # Interface de extração de log-mel espectrograma
│   │   ├── LogMelSpectrogramExtractor.cs      # STFT (FFT 400, Hop 160), janela Hann e 80 filtros Mel
│   │   ├── ITokenizer.cs                      # Interface de tokenização e decodificação de texto
│   │   ├── WhisperTokenizer.cs                # Tokenizer com tokens especiais e vocabulário
│   │   ├── InferenceEnginePromptBias.cs       # Prompt biasing de domínio para decodificador
│   │   └── InferenceEngineTranscriptionService.cs # Orquestrador neural sobre Unity.InferenceEngine (Worker/GPU)
│   ├── Controllers/
│   │   ├── AuctionVoiceEvents.cs              # UnityEvents serializáveis no Inspector
│   │   └── AuctionVoiceController.cs          # MonoBehaviour principal coordenador do leilão
│   └── UI/
│       └── AuctionVoiceDebugUI.cs             # Script de UI/Debug pronto para jogo (UGUI / TextMeshPro)
└── Tests/
    └── Editor/
        ├── AuctionVoiceEngine.Tests.asmdef    # Assembly de testes NUnit
        ├── AuctionBidEvaluatorTests.cs        # Suite de testes para todas as regras de lances
        ├── PhoneticAlphabetTests.cs           # Suite de testes para codificação fonética e Levenshtein
        ├── AudioPreprocessorTests.cs          # Suite de testes para mono, RMS, gating e resampling
        ├── LogMelSpectrogramExtractorTests.cs # Suite de testes para STFT e banco de filtros mel
        ├── WhisperTokenizerTests.cs           # Suite de testes para tokens especiais e decodificação
        └── InferenceEngineTranscriptionServiceTests.cs # Suite de testes para ciclo de vida e silêncio
```

---

## 🎯 Aplicação dos Princípios SOLID

1. **S - Single Responsibility Principle (SRP)**:
   - `BrazilianPortuguesePhoneticEncoder`: exclusivo para codificação G2P em Português Brasileiro.
   - `PhonologicalRepairer`: exclusivo para correções dialetais, DFA de expressões e expansão de gírias.
   - `AuctionBidEvaluator`: focado unicamente na semântica de leilão, somas e conectivos aritméticos.
   - `AudioPreprocessor`: isola gating de silêncio (RMS), conversão mono, resampling linear e normalização de pico.
   - `UnityAudioRecorder`: exclusivo para gravação de microfone nativa no Unity sem dependências externas.
   - `LogMelSpectrogramExtractor`: dedicado à computação matemática de STFT e banco de 80 filtros Mel.
   - `WhisperTokenizer`: focado na gestão de tokens especiais, BPE e vocabulário de texto.
   - `InferenceEngineTranscriptionService`: orquestra workers do Unity AI Inference Engine (`Unity.InferenceEngine`).
   - `AuctionVoiceController`: atua unicamente como orquestrador de alto nível e despachante de eventos.

2. **O - Open/Closed Principle (OCP)**:
   - O pipeline opera através de contratos claros (`IAuctionBidEvaluator`, `IPhonologyService`, `IPhoneticEncoder`, `ITranscriptionService`, `IAudioInputProvider`, `ILogMelSpectrogramExtractor`, `ITokenizer`). Novos modelos ONNX, codecs de áudio ou fontes de captura podem ser plugados sem modificar o código existente.

3. **L - Liskov Substitution Principle (LSP)**:
   - Qualquer implementação de `ITranscriptionService` (ex: `InferenceEngineTranscriptionService` em produção ou um mock assíncrono em testes unitários) pode substituir o motor neural sem quebrar o fluxo.
   - Qualquer `IAudioInputProvider` (ex: `UnityAudioRecorder` ou simulação via `FeedManualAudio`) substitui a captura de áudio com paridade.

4. **I - Interface Segregation Principle (ISP)**:
   - Interfaces pequenas e coesas em vez de uma interface monolítica: `IAudioInputProvider` gerencia gravação; `ITranscriptionService` gerencia inferência textual; `IAuctionBidEvaluator` avalia regras de negócio.

5. **D - Dependency Inversion Principle (DIP)**:
   - O `AuctionVoiceController` depende de abstrações injetáveis (`IAudioInputProvider`, `ITranscriptionService`, `IAuctionBidEvaluator`) configuráveis via Inspector ou por injeção externa (`controller.Configure(...)`).

---

## 🚀 Como Integrar no seu Projeto Unity

### 1. Adicionar o Pacote Oficial Unity AI Inference Engine
No Unity 2022.3+, abra **Window > Package Manager**, clique no ícone **`+`** e adicione o pacote oficial:
- **Inference Engine** (`com.unity.inference-engine`).

### 2. Adicionar o seu Modelo ONNX Finetunado
1. Copie o seu arquivo de modelo finetunado `.onnx` (ex: `whisper_pt_finetuned.onnx`) para dentro da pasta `Assets/` do seu projeto Unity.
2. O Unity o importará automaticamente como um `ModelAsset`.

### 3. Configurar a Cena
1. Crie um GameObject na cena (ex: `AuctionSystem`).
2. Adicione o componente `UnityAudioRecorder`:
   - **Frequency**: `16000` (padrão otimizado para reconhecimento de fala).
   - **Force Mono**: `true`.
3. Adicione o componente `AuctionVoiceController`:
   - Arraste o seu modelo ONNX importado para o campo **Model Asset** (e opcionalmente o Decoder para modelos em 2 estágios).
   - Defina **Backend Type**: `GPUCompute` (aceleração na GPU via compute shaders) ou `CPU`.
   - **Language**: `"pt"` (Português Brasileiro padrão).
   - **Min Duration Sec**: `0.25`
   - **Min Rms Energy**: `0.003` (evita alucinações de inferência em silêncio ambiente).
4. Conecte seus métodos de jogo no evento **On Bid Evaluated (BidResult)**.

### 4. Push-To-Talk ou Ativação por Voz
No seu script de UI ou controle de jogador:
```csharp
using UnityEngine;
using AuctionEngine.Controllers;
using AuctionEngine.Core.Models;

public class PlayerAuctionInput : MonoBehaviour
{
    [SerializeField] private AuctionVoiceController _voiceController;

    private void Update()
    {
        // Push-to-Talk com a tecla Espaço
        if (Input.GetKeyDown(KeyCode.Space))
        {
            _voiceController.StartListening();
        }
        else if (Input.GetKeyUp(KeyCode.Space))
        {
            _voiceController.StopListening();
        }
    }

    public void OnBidReceived(BidResult result)
    {
        if (result.ActionType == AuctionActionType.Bid)
        {
            Debug.Log($"Novo lance: {result.Currency} (Adição: {result.HasAddition})");
        }
        else if (result.ActionType == AuctionActionType.MultiplyOnly)
        {
            Debug.Log($"Multiplicador solto: {result.Value}x!");
        }
        else if (result.ActionType == AuctionActionType.Pass)
        {
            Debug.Log("Jogador passou a vez!");
        }
    }
}
```

---

## 🧪 Regras de Negócio e Casos de Uso Suportados

| Entrada Falada | Valor | Ação | HasAddition | Explicação |
|---|---|---|---|---|
| `"Mais dois"` | `2.0` | `Bid` | `true` | Lance incremental sobre a mesa |
| `"Dois mais dois"` | `4.0` | `Bid` | `false` | Dois números somados $\rightarrow$ soma direta |
| `"50 mais 50"` | `100.0` | `Bid` | `false` | Soma aritmética explícita |
| `"50, 40, 20"` | `20.0` | `Bid` | `false` | Pregão/hesitação: escolhe o último valor |
| `"Vinte-sete, vinte-se, vint-sate"` | `27.0` | `Bid` | `false` | Repetições acústicas: pega a última |
| `"Dobro"` | `2.0` | `MultiplyOnly` | `false` | Multiplicador verbal isolado |
| `"Quadreplico"` | `4.0` | `MultiplyOnly` | `false` | Multiplicador 4x isolado |
| `"Dobro de cinquenta"` | `100.0` | `Bid` | `false` | Multiplicador aplicado sobre número |
| `"Sixenta"` | `60.0` | `Bid` | `false` | Fonética corrigida para sessenta |
| `"Sem"` | `100.0` | `Bid` | `false` | Homófono resolvido para "cem" |
| `"Mew-Zom"` | `1000.0` | `Bid` | `false` | Correção fonética para "milzão" |
| `"Tartaruga"` | `2.0` | `Bid` | `false` | Cédula do Real de R$ 2,00 |
| `"Duas garças"` | `10.0` | `Bid` | `false` | 2 × R$ 5,00 = R$ 10,00 |
| `"Dois patinhos na lagoa"` | `22.0` | `Bid` | `false` | Expressão clássica de bingo |
| `"Meia dúzi"` | `6.0` | `Bid` | `false` | Variante coloquial de meia dúzia |
| `"Passo"` / `"Tô fora"` | `0.0` | `Pass` | `false` | Desistência da rodada |

---

## ⚙️ Testes no Unity Test Runner

A suite de testes completa na pasta `Tests/Editor` roda nativamente no **Unity Test Runner** (`Window > General > Test Runner`), cobrindo:
1. `AuctionBidEvaluatorTests`: 100% de cobertura de todas as expressões idiomáticas, fauna, bingo, multiplicadores e conectivos aritméticos.
2. `PhoneticAlphabetTests`: paridade fonética do Português Brasileiro (PB), Levenshtein e resolução de homófonos.
3. `AudioPreprocessorTests`: downmix estéreo $\rightarrow$ mono, cálculo RMS de energia, validação temporal de fala, normalização de amplitude e resampling linear.
4. `LogMelSpectrogramExtractorTests`: integridade dimensional de STFT com janela Hann e 80 canais mel.
5. `WhisperTokenizerTests`: validação de tokens especiais (`SOT`, `EOT`, `PT`), sequências iniciais e decodificação textual.
6. `InferenceEngineTranscriptionServiceTests`: ciclo de vida de workers, validação e descarte de silêncio acústico sem desperdício de GPU.
