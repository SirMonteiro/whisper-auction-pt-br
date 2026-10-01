using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using AuctionEngine.Audio;
#if WHISPER_UNITY || UNITY_WHISPER
using Whisper;
#endif

namespace AuctionEngine.Whisper
{
    /// <summary>
    /// Production implementation of ITranscriptionService wrapping Macoron's whisper.unity WhisperManager.
    /// Handles audio pre-processing (silence gating, mono conversion) and prompt biasing.
    /// </summary>
    public sealed class WhisperUnityTranscriptionService : ITranscriptionService
    {
#if WHISPER_UNITY || UNITY_WHISPER
        private readonly WhisperManager _whisperManager;
#endif
        private readonly string _defaultLanguage;
        private readonly string _initialPrompt;
        private readonly float _minRmsEnergy;
        private readonly float _minDurationSec;

#if WHISPER_UNITY || UNITY_WHISPER
        public WhisperUnityTranscriptionService(
            WhisperManager whisperManager,
            string defaultLanguage = "pt",
            string initialPrompt = WhisperPromptBias.DefaultPortuguesePrompt,
            float minRmsEnergy = WhisperAudioPreprocessor.DefaultMinRmsEnergy,
            float minDurationSec = WhisperAudioPreprocessor.DefaultMinDurationSec)
        {
            _whisperManager = whisperManager ?? throw new ArgumentNullException(nameof(whisperManager));
            _defaultLanguage = defaultLanguage ?? "pt";
            _initialPrompt = initialPrompt ?? WhisperPromptBias.DefaultPortuguesePrompt;
            _minRmsEnergy = minRmsEnergy;
            _minDurationSec = minDurationSec;

            ConfigureWhisperManager();
        }

        private void ConfigureWhisperManager()
        {
            if (_whisperManager != null)
            {
                _whisperManager.language = _defaultLanguage;
                _whisperManager.prompt = _initialPrompt;
            }
        }
#else
        public WhisperUnityTranscriptionService(
            object whisperManager = null,
            string defaultLanguage = "pt",
            string initialPrompt = WhisperPromptBias.DefaultPortuguesePrompt,
            float minRmsEnergy = WhisperAudioPreprocessor.DefaultMinRmsEnergy,
            float minDurationSec = WhisperAudioPreprocessor.DefaultMinDurationSec)
        {
            _defaultLanguage = defaultLanguage ?? "pt";
            _initialPrompt = initialPrompt ?? WhisperPromptBias.DefaultPortuguesePrompt;
            _minRmsEnergy = minRmsEnergy;
            _minDurationSec = minDurationSec;
        }
#endif

        public async Task<string> TranscribeAsync(
            float[] samples,
            int frequency,
            int channels,
            string language = null,
            CancellationToken cancellationToken = default)
        {
            if (samples == null || samples.Length == 0)
                return string.Empty;

            // 1. Audio silence & duration gating
            if (!WhisperAudioPreprocessor.IsValidSpeechAudio(samples, frequency, channels, _minDurationSec, _minRmsEnergy))
            {
                return string.Empty;
            }

            // 2. Downmix multi-channel to mono
            float[] monoSamples = channels > 1
                ? WhisperAudioPreprocessor.DownmixToMono(samples, channels)
                : samples;

            string targetLang = !string.IsNullOrEmpty(language) ? language : _defaultLanguage;

#if WHISPER_UNITY || UNITY_WHISPER
            if (_whisperManager == null)
            {
                Debug.LogError("[WhisperUnityTranscriptionService] WhisperManager instance is null!");
                return string.Empty;
            }

            // Set language and prompt bias
            _whisperManager.language = targetLang;
            _whisperManager.prompt = _initialPrompt;

            try
            {
                var result = await _whisperManager.GetTextAsync(monoSamples, frequency, 1);
                if (result != null && !string.IsNullOrWhiteSpace(result.Result))
                {
                    return result.Result.Trim();
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WhisperUnityTranscriptionService] Inference exception: {ex.Message}");
                return string.Empty;
            }
#else
            Debug.LogWarning("[WhisperUnityTranscriptionService] whisper.unity is not installed or WHISPER_UNITY define is missing.");
            await Task.Yield();
            return string.Empty;
#endif
        }
    }
}
