# Auction Voice Engine for Unity (C# / whisper.unity)

Arquitetura em **C#** desenvolvida segundo os princípios **SOLID** para integração do modelo de reconhecimento de voz (**whisper.unity**) e do motor completo de pós-processamento linguístico, fonético e aritmético de leilões em jogos Unity.

---

## 🏛️ Arquitetura SOLID

```
AuctionVoiceEngine/
├── package.json                         # Manifesto UPM (Unity Package Manager)
├── README.md                            # Documentação técnica e guia de integração
├── Runtime/
│   ├── AuctionVoiceEngine.asmdef        # Assembly Definition modular (vincula com "Whisper")
│   ├── Core/
│   │   ├── Models/
│   │   │   ├── AuctionActionType.cs     # Enum de intenção semântica (Bid, Pass, MultiplyOnly, etc.)
│   │   │   └── BidResult.cs             # DTO serializável contendo valor, moeda, flags e lances
│   │   ├── Lexicon/
│   │   │   ├── AuctionLexicon.cs        # Tabelas de fauna do Real, gírias de pregão e bingo
│   │   │   └── CurrencyFormatter.cs     # Formatador financeiro padrão Real (R$ 1.000,00)
│   │   ├── Phonetics/
│   │   │   ├── IPhoneticEncoder.cs      # Interface G2P (Grapheme-to-Phoneme)
│   │   │   ├── BrazilianPortuguesePhoneticEncoder.cs # Fonética do Português Brasileiro (PB)
│   │   │   ├── LevenshteinDistance.cs   # Cálculo de distância de edição e similaridade
│   │   │   ├── IPhoneticMatcher.cs      # Interface de casamento fonético de vocabulário
│   │   │   └── PhoneticMatcher.cs       # Matcher fonético com janela deslizante de bigramas
│   │   └── Services/
│   │       ├── IPhonologyService.cs     # Interface de reparo dialetal e fonológico
│   │       ├── PhonologicalRepairer.cs  # DFA master regex de expressões idiomáticas e gírias
│   │       ├── IAuctionBidEvaluator.cs  # Interface de avaliação de lances
│   │       └── AuctionBidEvaluator.cs   # Analisador sintático de numerais, somas e repetições
│   ├── Audio/
│   │   ├── IAudioInputProvider.cs       # Abstração de fontes de áudio
│   │   ├── WhisperAudioPreprocessor.cs  # Condicionamento de áudio (RMS silence gating, downmix mono)
│   │   └── WhisperAudioRecorderAdapter.cs # Adaptador para o MicrophoneRecord do whisper.unity
│   ├── Whisper/
│   │   ├── ITranscriptionService.cs     # Abstração de motores ASR
│   │   ├── WhisperPromptBias.cs         # Prompt biasing de domínio para o decodificador Whisper
│   │   └── WhisperUnityTranscriptionService.cs # Wrapper concreto do WhisperManager
│   ├── Controllers/
│   │   ├── AuctionVoiceEvents.cs        # UnityEvents serializáveis no Inspector
│   │   └── AuctionVoiceController.cs    # MonoBehaviour principal coordenador do leilão
│   └── UI/
│       └── AuctionVoiceDebugUI.cs       # Script de UI/Debug pronto para jogo (UGUI / TextMeshPro)
└── Tests/
    └── Editor/
        ├── AuctionVoiceEngine.Tests.asmdef # Assembly de testes NUnit
        ├── AuctionBidEvaluatorTests.cs  # Suite de testes para todas as regras de lances
        └── PhoneticAlphabetTests.cs     # Suite de testes para codificação fonética e Levenshtein
```

---

## 🎯 Aplicação dos Princípios SOLID

1. **S - Single Responsibility Principle (SRP)**:
   - `BrazilianPortuguesePhoneticEncoder`: exclusivo para codificação G2P.
   - `PhonologicalRepairer`: exclusivo para correções dialetais e expansão de gírias.
   - `AuctionBidEvaluator`: focado unicamente na semântica de leilão, somas e conectivos.
   - `WhisperAudioPreprocessor`: isola o gating de silêncio (RMS) e conversão para mono.
   - `AuctionVoiceController`: atua apenas como orquestrador e despachante de eventos.

2. **O - Open/Closed Principle (OCP)**:
   - O pipeline opera através de interfaces (`IAuctionBidEvaluator`, `IPhonologyService`, `IPhoneticEncoder`, `ITranscriptionService`, `IAudioInputProvider`). Novos dialetos, modelos ou fornecedores de áudio podem ser introduzidos sem alterar o código existente.

3. **L - Liskov Substitution Principle (LSP)**:
   - Qualquer implementação de `ITranscriptionService` (ex: `WhisperUnityTranscriptionService` ou um `MockTranscriptionService` em testes) pode substituir o Whisper real sem quebrar o fluxo.

4. **I - Interface Segregation Principle (ISP)**:
   - Interfaces pequenas e coesas em vez de uma interface monolítica (`IAudioInputProvider` não sabe de gramática; `IAuctionBidEvaluator` não sabe de microfone).

5. **D - Dependency Inversion Principle (DIP)**:
   - O controlador de alto nível depende de abstrações (`IAuctionBidEvaluator`, `ITranscriptionService`, `IAudioInputProvider`) configuráveis via código ou inicializadas via Unity Inspector.

---

## 🚀 Como Integrar no seu Projeto Unity

### 1. Adicionar o pacote whisper.unity
No Unity, abra **Window > Package Manager**, clique no ícone **`+`** e selecione **Add package from git URL...**:
```
https://github.com/Macoron/whisper.unity.git?path=/Packages/com.whisper.unity
```

Baixe os pesos do modelo (ex: `ggml-tiny.bin` ou `ggml-small.bin`) e coloque na pasta `Assets/StreamingAssets/`.

### 2. Adicionar o AuctionVoiceEngine
Você pode:
- Copiar a pasta `AuctionVoiceEngine` diretamente para dentro da pasta `Assets/` do seu projeto Unity; OU
- No Package Manager, selecionar **Add package from disk...** e apontar para o `package.json` de `AuctionVoiceEngine`.

### 3. Configurar a Cena
1. Em um GameObject (ex: `AuctionSystem`):
   - Adicione o componente `WhisperManager` (do whisper.unity) e aponte para o modelo em StreamingAssets.
   - Adicione o componente `MicrophoneRecord` (do whisper.unity).
   - Adicione o componente `WhisperAudioRecorderAdapter`.
   - Adicione o componente `AuctionVoiceController`.
2. No Inspector do `AuctionVoiceController`:
   - **Language**: `"pt"` (padrão).
   - **Min Duration Sec**: `0.25`
   - **Min Rms Energy**: `0.003` (filtra silêncio ambiente para evitar alucinações do Whisper).
   - Conecte seus métodos de jogo no evento **On Bid Evaluated (BidResult)**.

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
Os testes unitários incluídos na pasta `Tests/Editor` rodam nativamente no **Unity Test Runner** (`Window > General > Test Runner`), garantindo 100% de paridade lógica com o motor Python original.
