using System;
using UnityEngine;
#if WHISPER_UNITY || UNITY_WHISPER
using Whisper.Utils;
#endif

namespace AuctionEngine.Audio
{
    /// <summary>
    /// Adapts whisper.unity's MicrophoneRecord component into the IAudioInputProvider interface.
    /// Can be attached to any GameObject alongside MicrophoneRecord.
    /// </summary>
    public sealed class WhisperAudioRecorderAdapter : MonoBehaviour, IAudioInputProvider
    {
        public event Action<float[], int, int> OnAudioReady;
        public event Action OnRecordingStarted;
        public event Action OnRecordingStopped;

#if WHISPER_UNITY || UNITY_WHISPER
        [SerializeField] private MicrophoneRecord _microphoneRecord;
#endif

        public bool IsRecording
        {
            get
            {
#if WHISPER_UNITY || UNITY_WHISPER
                return _microphoneRecord != null && _microphoneRecord.IsRecording;
#else
                return false;
#endif
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
            }
#endif
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
            if (_microphoneRecord != null && !_microphoneRecord.IsRecording)
            {
                _microphoneRecord.StartRecord();
                OnRecordingStarted?.Invoke();
            }
#else
            Debug.LogWarning("[WhisperAudioRecorderAdapter] whisper.unity is not installed or WHISPER_UNITY define is missing.");
#endif
        }

        public void StopRecording()
        {
#if WHISPER_UNITY || UNITY_WHISPER
            if (_microphoneRecord != null && _microphoneRecord.IsRecording)
            {
                _microphoneRecord.StopRecord();
            }
#endif
        }

        /// <summary>
        /// Manually injects audio samples (useful for automated testing or feeding prerecorded clips).
        /// </summary>
        public void FeedManualAudio(float[] samples, int frequency, int channels)
        {
            OnAudioReady?.Invoke(samples, frequency, channels);
        }
    }
}
