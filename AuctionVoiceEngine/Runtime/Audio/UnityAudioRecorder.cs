using System;
using UnityEngine;

namespace AuctionEngine.Audio
{
    /// <summary>
    /// Native Unity microphone recorder implementing IAudioInputProvider.
    /// Operates without third-party dependencies, capturing audio directly via UnityEngine.Microphone
    /// at standard speech recognition rates (16,000 Hz mono).
    /// </summary>
    public sealed class UnityAudioRecorder : MonoBehaviour, IAudioInputProvider
    {
        public event Action<float[], int, int> OnAudioReady;
        public event Action OnRecordingStarted;
        public event Action OnRecordingStopped;

        [Header("Microphone Configuration")]
        [Tooltip("Microphone device name. Leave empty to use default system microphone.")]
        [SerializeField] private string _deviceName;

        [Tooltip("Target sample rate in Hz. 16,000 Hz is standard for ASR models (Whisper/Inference Engine).")]
        [SerializeField] private int _frequency = 16000;

        [Tooltip("Maximum recording duration in seconds before automatically stopping.")]
        [SerializeField] private int _maxDurationSec = 30;

        [Tooltip("Downmix multi-channel microphone streams to single-channel mono.")]
        [SerializeField] private bool _forceMono = true;

        private AudioClip _recordedClip;
        private bool _isRecording;

        public bool IsRecording => _isRecording;

        public string DeviceName
        {
            get => _deviceName;
            set => _deviceName = value;
        }

        public int Frequency
        {
            get => _frequency;
            set => _frequency = value;
        }

        public int MaxDurationSec
        {
            get => _maxDurationSec;
            set => _maxDurationSec = value;
        }

        private void OnDisable()
        {
            if (_isRecording)
            {
                StopRecording();
            }
        }

        private void OnDestroy()
        {
            if (_isRecording)
            {
                StopRecording();
            }
        }

        /// <summary>
        /// Begins audio capture from the selected or default microphone device.
        /// </summary>
        public void StartRecording()
        {
            if (_isRecording)
                return;

            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[UnityAudioRecorder] No microphone audio input devices found on system.");
                return;
            }

            string targetDevice = ResolveDeviceName();

            // Clean up any previously allocated AudioClip
            if (_recordedClip != null)
            {
                Destroy(_recordedClip);
                _recordedClip = null;
            }

            _recordedClip = Microphone.Start(targetDevice, false, _maxDurationSec, _frequency);
            _isRecording = true;
            OnRecordingStarted?.Invoke();
        }

        /// <summary>
        /// Stops audio capture, extracts recorded PCM samples, and delivers them via OnAudioReady.
        /// </summary>
        public void StopRecording()
        {
            if (!_isRecording)
                return;

            string targetDevice = ResolveDeviceName();
            int lastSamplePos = Microphone.GetPosition(targetDevice);
            Microphone.End(targetDevice);
            _isRecording = false;
            OnRecordingStopped?.Invoke();

            if (_recordedClip == null || lastSamplePos <= 0)
                return;

            int channels = _recordedClip.channels;
            int totalSamples = lastSamplePos * channels;

            float[] rawSamples = new float[totalSamples];
            _recordedClip.GetData(rawSamples, 0);

            float[] finalSamples = (_forceMono && channels > 1)
                ? AudioPreprocessor.DownmixToMono(rawSamples, channels)
                : rawSamples;

            int finalChannels = (_forceMono && channels > 1) ? 1 : channels;
            int finalFrequency = _recordedClip.frequency;

            OnAudioReady?.Invoke(finalSamples, finalFrequency, finalChannels);
        }

        /// <summary>
        /// Manually delivers an audio buffer (ideal for automated unit testing or feeding pre-recorded clips).
        /// </summary>
        public void FeedManualAudio(float[] samples, int frequency, int channels)
        {
            OnAudioReady?.Invoke(samples, frequency, channels);
        }

        private string ResolveDeviceName()
        {
            if (!string.IsNullOrEmpty(_deviceName))
                return _deviceName;

            if (Microphone.devices != null && Microphone.devices.Length > 0)
                return Microphone.devices[0];

            return null;
        }
    }
}
