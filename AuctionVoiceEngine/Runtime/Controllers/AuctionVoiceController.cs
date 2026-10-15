using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using AuctionEngine.Audio;
using AuctionEngine.Core.Models;
using AuctionEngine.Core.Services;
using AuctionEngine.Inference;

#if UNITY_INFERENCE_ENGINE || UNITY_SENTIS
using Unity.InferenceEngine;
#endif

namespace AuctionEngine.Controllers
{
    /// <summary>
    /// Master controller orchestrating microphone capture, Unity AI Inference Engine ASR,
    /// phonological normalization, and domain auction bid parsing in Unity.
    /// Follows SOLID principles with swappable components and inspectable UnityEvents.
    /// </summary>
    public sealed class AuctionVoiceController : MonoBehaviour
    {
        [Header("Unity AI Inference Engine References")]
#if UNITY_INFERENCE_ENGINE || UNITY_SENTIS
        [Tooltip("ONNX model asset for Speech-to-Text inference (Encoder or single end-to-end model).")]
        [SerializeField] private ModelAsset _modelAsset;

        [Tooltip("Optional Decoder ONNX model asset for 2-stage autoregressive models (e.g. Whisper).")]
        [SerializeField] private ModelAsset _decoderModelAsset;

        [Tooltip("Hardware execution backend (GPUCompute recommended for desktop/mobile/console).")]
        [SerializeField] private BackendType _backendType = BackendType.GPUCompute;
#else
        [Tooltip("Attach ModelAsset (requires com.unity.inference-engine or com.unity.sentis).")]
        [SerializeField] private UnityEngine.Object _modelAsset;

        [Tooltip("Optional Decoder ModelAsset.")]
        [SerializeField] private UnityEngine.Object _decoderModelAsset;
#endif

        [Header("Audio Capture Provider")]
        [Tooltip("Component implementing IAudioInputProvider (e.g. UnityAudioRecorder). Auto-created if null.")]
        [SerializeField] private MonoBehaviour _audioInputProviderComponent;

        [Header("Language & Preprocessing")]
        [Tooltip("Target language code (e.g. 'pt' for Brazilian Portuguese).")]
        [SerializeField] private string _language = "pt";

        [Tooltip("Minimum audio duration in seconds to trigger inference.")]
        [SerializeField] private float _minDurationSec = 0.25f;

        [Tooltip("Minimum RMS energy required to avoid transcribing silent room noise.")]
        [SerializeField] private float _minRmsEnergy = 0.003f;

        [Header("Events")]
        [SerializeField] private AuctionVoiceEvents _events = new AuctionVoiceEvents();

        // Core abstractions
        private IAudioInputProvider _audioInputProvider;
        private ITranscriptionService _transcriptionService;
        private IAuctionBidEvaluator _bidEvaluator;
        private CancellationTokenSource _cts;

        public event Action<BidResult> OnBidEvaluated;
        public AuctionVoiceEvents Events => _events;
        public bool IsRecording => _audioInputProvider != null && _audioInputProvider.IsRecording;

        public string Language
        {
            get => _language;
            set => _language = value;
        }

        private void Awake()
        {
            _cts = new CancellationTokenSource();
            InitializeServices();
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (_audioInputProvider != null)
            {
                _audioInputProvider.OnAudioReady -= HandleAudioReady;
                _audioInputProvider.OnRecordingStarted -= HandleRecordingStarted;
                _audioInputProvider.OnRecordingStopped -= HandleRecordingStopped;
            }

            if (_transcriptionService is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        /// <summary>
        /// Configures dependencies. Allows external dependency injection in unit tests or custom pipelines.
        /// </summary>
        public void Configure(
            IAudioInputProvider audioInputProvider,
            ITranscriptionService transcriptionService,
            IAuctionBidEvaluator bidEvaluator)
        {
            if (_audioInputProvider != null)
            {
                _audioInputProvider.OnAudioReady -= HandleAudioReady;
                _audioInputProvider.OnRecordingStarted -= HandleRecordingStarted;
                _audioInputProvider.OnRecordingStopped -= HandleRecordingStopped;
            }

            if (_transcriptionService is IDisposable oldDisposable && oldDisposable != transcriptionService)
            {
                oldDisposable.Dispose();
            }

            _audioInputProvider = audioInputProvider;
            _transcriptionService = transcriptionService;
            _bidEvaluator = bidEvaluator;

            if (_audioInputProvider != null)
            {
                _audioInputProvider.OnAudioReady += HandleAudioReady;
                _audioInputProvider.OnRecordingStarted += HandleRecordingStarted;
                _audioInputProvider.OnRecordingStopped += HandleRecordingStopped;
            }
        }

        private void InitializeServices()
        {
            if (_bidEvaluator == null)
            {
                _bidEvaluator = new AuctionBidEvaluator();
            }

            // 1. Audio input provider setup
            if (_audioInputProvider == null)
            {
                if (_audioInputProviderComponent is IAudioInputProvider provider)
                {
                    _audioInputProvider = provider;
                }
                else
                {
                    _audioInputProvider = GetComponent<IAudioInputProvider>();
                    if (_audioInputProvider == null)
                    {
                        var recorder = GetComponent<UnityAudioRecorder>();
                        if (recorder == null)
                        {
                            recorder = gameObject.AddComponent<UnityAudioRecorder>();
                        }
                        _audioInputProvider = recorder;
                    }
                }

                if (_audioInputProvider != null)
                {
                    _audioInputProvider.OnAudioReady += HandleAudioReady;
                    _audioInputProvider.OnRecordingStarted += HandleRecordingStarted;
                    _audioInputProvider.OnRecordingStopped += HandleRecordingStopped;
                }
            }

            // 2. Transcription service setup
            if (_transcriptionService == null)
            {
#if UNITY_INFERENCE_ENGINE || UNITY_SENTIS
                if (_modelAsset != null)
                {
                    _transcriptionService = new InferenceEngineTranscriptionService(
                        _modelAsset,
                        _decoderModelAsset,
                        _backendType,
                        _language,
                        InferenceEnginePromptBias.DefaultPortuguesePrompt,
                        _minRmsEnergy,
                        _minDurationSec
                    );
                }
#endif
            }
        }

        #region Public Controls

        /// <summary>
        /// Begins audio capture (e.g. on Push-To-Talk button down).
        /// </summary>
        public void StartListening()
        {
            if (_audioInputProvider != null)
            {
                _audioInputProvider.StartRecording();
            }
            else
            {
                Debug.LogWarning("[AuctionVoiceController] No AudioInputProvider configured.");
            }
        }

        /// <summary>
        /// Stops audio capture and initiates transcription and bid evaluation (e.g. on Push-To-Talk button release).
        /// </summary>
        public void StopListening()
        {
            if (_audioInputProvider != null)
            {
                _audioInputProvider.StopRecording();
            }
        }

        /// <summary>
        /// Toggles audio recording state.
        /// </summary>
        public void ToggleListening()
        {
            if (IsRecording) StopListening();
            else StartListening();
        }

        /// <summary>
        /// Directly processes an orthographic text transcript (ideal for debug console, unit tests, or chat input).
        /// </summary>
        public BidResult ProcessUtterance(string transcript)
        {
            if (_bidEvaluator == null)
            {
                _bidEvaluator = new AuctionBidEvaluator();
            }

            var result = _bidEvaluator.Evaluate(transcript);
            DispatchResult(result);
            return result;
        }

        /// <summary>
        /// Transcribes and evaluates an array of raw audio samples asynchronously.
        /// </summary>
        public async Task<BidResult> ProcessAudioAsync(float[] samples, int frequency, int channels)
        {
            if (_transcriptionService == null)
            {
                _events.OnError?.Invoke("Transcription service is not initialized. Ensure a ModelAsset is assigned or service configured.");
                return null;
            }

            try
            {
                _events.OnTranscriptionStarted?.Invoke();

                string transcript = await _transcriptionService.TranscribeAsync(
                    samples,
                    frequency,
                    channels,
                    _language,
                    _cts != null ? _cts.Token : CancellationToken.None
                );

                _events.OnTranscriptionCompleted?.Invoke(transcript);

                if (string.IsNullOrWhiteSpace(transcript))
                {
                    return null;
                }

                return ProcessUtterance(transcript);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AuctionVoiceController] Audio processing error: {ex.Message}");
                _events.OnError?.Invoke(ex.Message);
                return null;
            }
        }

        #endregion

        #region Event Callbacks

        private void HandleRecordingStarted()
        {
            _events.OnRecordingStateChanged?.Invoke(true);
        }

        private void HandleRecordingStopped()
        {
            _events.OnRecordingStateChanged?.Invoke(false);
        }

        private async void HandleAudioReady(float[] samples, int frequency, int channels)
        {
            await ProcessAudioAsync(samples, frequency, channels);
        }

        private void DispatchResult(BidResult result)
        {
            OnBidEvaluated?.Invoke(result);
            _events.OnBidEvaluated?.Invoke(result);
        }

        #endregion
    }
}
