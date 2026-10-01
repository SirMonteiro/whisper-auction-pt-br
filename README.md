### 📂 Estrutura de Pastas e Arquitetura Criada

    AuctionVoiceEngine/
    ├── package.json                              # Manifesto UPM para importação no Unity Package Manager
    ├── README.md                                 # Guia completo de configuração, cena e eventos
    ├── Runtime/
    │   ├── AuctionVoiceEngine.asmdef             # Assembly Definition desacoplado
    │   ├── Core/
    │   │   ├── Models/
    │   │   │   ├── AuctionActionType.cs # Enum semântico (Bid, Pass, MultiplyOnly, None, Invalid)
    │   │   │   └── BidResult.cs         # DTO serializável (Valor, Moeda, Flags, Lances)
    │   │   ├── Lexicon/
    │   │   │   ├── AuctionLexicon.cs   # Fauna do Real, bingo e gírias de pregão
    │   │   │   └── CurrencyFormatter.cs # Formatador financeiro padrão Real (R$ 1.000,00)
    │   │   ├── Phonetics/
    │   │   │   ├── IPhoneticEncoder.cs
    │   │   │   ├── BrazilianPortuguesePhoneticEncoder.cs # G2P para Português Brasileiro
    │   │   │   ├── LevenshteinDistance.cs # Distância e similaridade sem alocação
    │   │   │   ├── IPhoneticMatcher.cs
    │   │   │   └── PhoneticMatcher.cs   # Casamento fonético com janela deslizante de bigramas
    │   │   └── Services/
    │   │       ├── IPhonologyService.cs
    │   │       ├── PhonologicalRepairer.cs # DFA master regex de expressões idiomáticas
    │   │       ├── IAuctionBidEvaluator.cs
    │   │       └── AuctionBidEvaluator.cs   # Analisador sintático de numerais, somas e repetições
    │   ├── Audio/
    │   │   ├── IAudioInputProvider.cs
    │   │   ├── WhisperAudioPreprocessor.cs # RMS silence gating (0.003) e downmix mono
    │   │   └── WhisperAudioRecorderAdapter.cs # Adaptador para MicrophoneRecord
    │   ├── Whisper/
    │   │   ├── ITranscriptionService.cs
    │   │   ├── WhisperPromptBias.cs       # Prompt biasing com vocabulário de leilão
    │   │   └── WhisperUnityTranscriptionService.cs # Integração assíncrona com WhisperManager
    │   ├── Controllers/
    │   │   ├── AuctionVoiceEvents.cs   # UnityEvents serializáveis no Inspector
    │   │   └── AuctionVoiceController.cs # MonoBehaviour orquestrador principal
    │   └── UI/
    │       └── AuctionVoiceDebugUI.cs       # UI/Debug controller pronto para teste em cena
    └── Tests/
        └── Editor/
            ├── AuctionVoiceEngine.Tests.asmdef
            ├── AuctionBidEvaluatorTests.cs # 27 testes unitários cobrindo todos os cenários
            └── PhoneticAlphabetTests.cs    # Testes fonéticos e Levenshtein
  ──────
  ### 🏛️ Aplicação dos Princípios SOLID

  1. S - Single Responsibility Principle (SRP):
      • BrazilianPortuguesePhoneticEncoder.cs: converte grafemas em códigos fonéticos PB (sândi, nasalizações, ditongos, homófonos).
      • WhisperAudioPreprocessor.cs: cuida exclusivamente do condicionamento acústico (cálculo de energia RMS para silêncio e conversão estéreo → mono).
      • AuctionBidEvaluator.cs: interpretação semântica das regras de leilão e cálculo aritmético.
      • AuctionVoiceController.cs: coordena o ciclo de vida e despacha eventos no Unity.
  2. O - Open/Closed Principle (OCP):
      • Todo o pipeline depende de contratos (IAuctionBidEvaluator.cs, IPhonologyService.cs, ITranscriptionService.cs, IAudioInputProvider.cs). Novos dialetos, gírias ou provedores de reconhecimento de voz
      podem ser plugados sem alterar o código do controlador.
  3. L - Liskov Substitution Principle (LSP):
      • ITranscriptionService pode ser substituído por WhisperUnityTranscriptionService.cs ou por um mock em testes sem impactos colaterais.
  4. I - Interface Segregation Principle (ISP):
      • Interfaces segregadas e enxutas: o avaliador não sabe de microfone; o gravador não sabe de gramática.
  5. D - Dependency Inversion Principle (DIP):
      • Injeção de dependências suportada tanto via método Configure(...) quanto automaticamente no Awake() via Unity Inspector.

  ──────
  ### 🎮 Como Utilizar no seu Jogo Unity

  #### 1. Importação

  • Copie a pasta AuctionVoiceEngine para dentro de Assets/ (ou adicione como pacote local via UPM).
  • Certifique-se de que o pacote whisper.unity está importado no projeto (https://github.com/Macoron/whisper.unity.git?path=/Packages/com.whisper.unity).

  #### 2. Configuração na Cena

  1. Crie um GameObject vazio (ex: AuctionManager).
  2. Adicione os componentes:
      • WhisperManager (com os pesos .bin em StreamingAssets/)
      • MicrophoneRecord
      • WhisperAudioRecorderAdapter
      • AuctionVoiceController
  3. No Inspector de AuctionVoiceController, configure:
      • Language: "pt" (padrão)
      • Min Rms Energy: 0.003 (evita alucinações em silêncio ambiente)
      • Conecte suas ações de jogo ao UnityEvent On Bid Evaluated (BidResult).


  #### 3. Exemplo de Script de Gameplay (Push-To-Talk)

    using UnityEngine;
    using AuctionEngine.Controllers;
    using AuctionEngine.Core.Models;

    public class AuctionPlayerInput : MonoBehaviour
    {
        [SerializeField] private AuctionVoiceController _voiceController;

        private void Update()
        {
            // Push-to-Talk via barra de espaço
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _voiceController.StartListening();
            }
            else if (Input.GetKeyUp(KeyCode.Space))
            {
                _voiceController.StopListening();
            }
        }

        // Método chamado pelo UnityEvent OnBidEvaluated
        public void OnBidEvaluated(BidResult result)
        {
            switch (result.ActionType)
            {
                case AuctionActionType.Bid:
                    Debug.Log($"Lance recebido: {result.Currency} (Adição incremental: {result.HasAddition})");
                    break;

                case AuctionActionType.MultiplyOnly:
                    Debug.Log($"Multiplicador isolado falado: {result.Value}x!");
                    break;

                case AuctionActionType.Pass:
                    Debug.Log("Jogador passou a vez!");
                    break;
            }
        }
    }
