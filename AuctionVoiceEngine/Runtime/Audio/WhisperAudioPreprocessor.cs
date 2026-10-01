using System;

namespace AuctionEngine.Audio
{
    /// <summary>
    /// Utility class for conditioning raw audio samples prior to Whisper inference.
    /// Handles channel downmix, RMS silence gating, and minimum duration validation.
    /// </summary>
    public static class WhisperAudioPreprocessor
    {
        public const int TargetSampleRate = 16000;
        public const float DefaultMinRmsEnergy = 0.003f;
        public const float DefaultMinDurationSec = 0.25f;

        /// <summary>
        /// Downmixes multi-channel audio to mono in-place or into a new buffer.
        /// </summary>
        public static float[] DownmixToMono(float[] samples, int channels)
        {
            if (samples == null || samples.Length == 0)
                return Array.Empty<float>();

            if (channels <= 1)
                return samples;

            int monoLength = samples.Length / channels;
            float[] mono = new float[monoLength];

            for (int i = 0; i < monoLength; i++)
            {
                float sum = 0f;
                int offset = i * channels;
                for (int c = 0; c < channels; c++)
                {
                    sum += samples[offset + c];
                }
                mono[i] = sum / channels;
            }

            return mono;
        }

        /// <summary>
        /// Computes Root-Mean-Square (RMS) energy across audio samples.
        /// </summary>
        public static float CalculateRmsEnergy(float[] samples)
        {
            if (samples == null || samples.Length == 0)
                return 0f;

            double sumSquares = 0.0;
            for (int i = 0; i < samples.Length; i++)
            {
                float s = samples[i];
                sumSquares += s * s;
            }

            return (float)Math.Sqrt(sumSquares / samples.Length);
        }

        /// <summary>
        /// Validates whether the audio chunk meets the minimum energy and duration criteria
        /// to avoid Whisper model hallucinations on ambient room silence.
        /// </summary>
        public static bool IsValidSpeechAudio(
            float[] samples,
            int frequency,
            int channels,
            float minDurationSec = DefaultMinDurationSec,
            float minRmsEnergy = DefaultMinRmsEnergy)
        {
            if (samples == null || samples.Length == 0)
                return false;

            int actualChannels = Math.Max(1, channels);
            float duration = (float)samples.Length / (frequency * actualChannels);
            if (duration < minDurationSec)
                return false;

            float rms = CalculateRmsEnergy(samples);
            if (rms < minRmsEnergy)
                return false;

            return true;
        }
    }
}
