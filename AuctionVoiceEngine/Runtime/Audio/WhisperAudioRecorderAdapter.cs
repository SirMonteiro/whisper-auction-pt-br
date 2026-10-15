using System;
using UnityEngine;
#if WHISPER_UNITY || UNITY_WHISPER
using Whisper.Utils;
#endif

namespace AuctionEngine.Audio
{
    /// <summary>
    /// Legacy adapter for whisper.unity's MicrophoneRecord component.
    /// In modern setups, prefer using <see cref="UnityAudioRecorder"/> which does not require third-party packages.
    /// </summary>
    [Obsolete("Use UnityAudioRecorder instead for native Unity audio capture.")]
    public sealed class WhisperAudioRecorderAdapter : MonoBehaviour, IAudioInputProvider
    {
        public event Action<float[], int, int> OnAudioReady;
        public event Action OnRecordingStarted;
        public event Action OnRecordingStopped;

#if WHISPER_UNITY || UNITY_WHISPER
        [SerializeField] private MicrophoneRecord _microphoneRecord;
#endif
        private UnityAudioRecorder _fallbackRecorder;

        public bool IsRecording
        {
            get
            {
#if WHISPER_UNITY || UNITY_WHISPER
                if (_microphoneRecord != null)
                    return _microphoneRecord.IsRecording;
#endif
                return _fallbackRecorder != null && _fallbackRecorder.IsRecording;
            }
        }

        private void Awake()
        {
#if WHISPER_UNITY || UNITY_WHISPER
            if (_microphoneRecord == null)
            {
                _microphoneRecord = GetComponent<MicrophoneRecord>();
            }

            if (_microphoneRecord != null)
            {
                _microphoneRecord.OnRecordStop += HandleWhisperRecordStop;
                return;
            }
#endif
            // Fallback to native recorder if whisper.unity is not present
            _fallbackRecorder = GetComponent<UnityAudioRecorder>();
            if (_fallbackRecorder == null)
            {
                _fallbackRecorder = gameObject.AddComponent<UnityAudioRecorder>();
            }

            _fallbackRecorder.OnAudioReady += (s, f, c) => OnAudioReady?.Invoke(s, f, c);
            _fallbackRecorder.OnRecordingStarted += () => OnRecordingStarted?.Invoke();
            _fallbackRecorder.OnRecordingStopped += () => OnRecordingStopped?.Invoke();
        }

        private void OnDestroy()
        {
#if WHISPER_UNITY || UNITY_WHISPER
            if (_microphoneRecord != null)
            {
                _microphoneRecord.OnRecordStop -= HandleWhisperRecordStop;
            }
#endif
        }

#if WHISPER_UNITY || UNITY_WHISPER
        private void HandleWhisperRecordStop(AudioChunk chunk)
        {
            OnRecordingStopped?.Invoke();

            if (chunk.Data != null && chunk.Data.Length > 0)
            {
                OnAudioReady?.Invoke(chunk.Data, chunk.Frequency, chunk.Channels);
            }
        }
#endif

        public void StartRecording()
        {
#if WHISPER_UNITY || UNITY_WHISPER
            if (_microphoneRecord != null)
            {
                if (!_microphoneRecord.IsRecording)
                {
                    _microphoneRecord.StartRecord();
                    OnRecordingStarted?.Invoke();
                }
                return;
            }
#endif
            if (_fallbackRecorder != null)
            {
                _fallbackRecorder.StartRecording();
            }
        }

        public void StopRecording()
        {
#if WHISPER_UNITY || UNITY_WHISPER
            if (_microphoneRecord != null)
            {
                if (_microphoneRecord.IsRecording)
                {
                    _microphoneRecord.StopRecord();
                }
                return;
            }
#endif
            if (_fallbackRecorder != null)
            {
                _fallbackRecorder.StopRecording();
            }
        }

        public void FeedManualAudio(float[] samples, int frequency, int channels)
        {
            OnAudioReady?.Invoke(samples, frequency, channels);
        }
    }
}
