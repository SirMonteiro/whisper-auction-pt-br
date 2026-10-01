using System;

namespace AuctionEngine.Audio
{
    /// <summary>
    /// Contract for audio input providers (Microphone, AudioClips, streaming sources).
    /// Decouples audio recording hardware from ASR and bidding logic.
    /// </summary>
    public interface IAudioInputProvider
    {
        /// <summary>Fired when a chunk or recorded buffer of audio samples is ready for transcription.</summary>
        event Action<float[], int, int> OnAudioReady;

        /// <summary>Fired when recording starts.</summary>
        event Action OnRecordingStarted;

        /// <summary>Fired when recording stops.</summary>
        event Action OnRecordingStopped;

        /// <summary>Whether the provider is currently capturing audio.</summary>
        bool IsRecording { get; }

        /// <summary>Begins capturing audio.</summary>
        void StartRecording();

        /// <summary>Stops capturing audio and delivers the recorded buffer via OnAudioReady.</summary>
        void StopRecording();
    }
}
