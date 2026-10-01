using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using AuctionEngine.Audio;
using AuctionEngine.Core.Models;
using AuctionEngine.Core.Services;
using AuctionEngine.Whisper;

#if WHISPER_UNITY || UNITY_WHISPER
using Whisper;
using Whisper.Utils;
#endif

namespace AuctionEngine.Controllers
{
    /// <summary>
    /// Master controller orchestrating microphone capture, Whisper ASR inference,
    /// phonological normalization, and domain auction bid parsing in Unity.
    /// Follows SOLID principles with swappable components and inspectable UnityEvents.
    /// </summary>
    public sealed class AuctionVoiceController : MonoBehaviour
    {
        [Header("Whisper.unity References")]
#if WHISPER_UNITY || UNITY_WHISPER
        [SerializeField] private WhisperManager _whisperManager;
        [SerializeField] private MicrophoneRecord _microphoneRecord;
#else
        [Tooltip("Attach WhisperManager when whisper.unity is in your project.")]
        [SerializeField] private MonoBehaviour _whisperManager;
        [Tooltip("Attach MicrophoneRecord when whisper.unity is in your project.")]
        [SerializeField] private MonoBehaviour _microphoneRecord;
#endif

        [Header("Language & Preprocessing")]
        [Tooltip("Whisper target language code (e.g. 'pt' for Brazilian Portuguese).")]
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

#if WHISPER_UNITY || UNITY_WHISPER
            if (_whisperManager == null)
            {
                _whisperManager = FindObjectOfType<WhisperManager>();
            }

            if (_microphoneRecord == null)
            {
                _microphoneRecord = FindObjectOfType<MicrophoneRecord>();
            }

            if (_transcriptionService == null && _whisperManager != null)
            {
                _transcriptionService = new WhisperUnityTranscriptionService(
                    _whisperManager,
                    _language,
                    WhisperPromptBias.DefaultPortuguesePrompt,
                    _minRmsEnergy,
                    _minDurationSec
                );
            }

            if (_audioInputProvider == null)
            {
                var adapter = GetComponent<WhisperAudioRecorderAdapter>();
                if (adapter == null && _microphoneRecord != null)
                {
                    adapter = gameObject.AddComponent<WhisperAudioRecorderAdapter>();
                }

                if (adapter != null)
                {
                    _audioInputProvider = adapter;
                    _audioInputProvider.OnAudioReady += HandleAudioReady;
                    _audioInputProvider.OnRecordingStarted += HandleRecordingStarted;
                    _audioInputProvider.OnRecordingStopped += HandleRecordingStopped;
                }
            }
#endif
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
                _events.OnError?.Invoke("Transcription service is not initialized.");
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
