using System;

namespace AuctionEngine.Audio
{
    /// <summary>
    /// Backward-compatibility wrapper for AudioPreprocessor.
    /// Redirects all preprocessing methods to the generalized AudioPreprocessor class.
    /// </summary>
    [Obsolete("Use AuctionEngine.Audio.AudioPreprocessor instead.")]
    public static class WhisperAudioPreprocessor
    {
        public const int TargetSampleRate = AudioPreprocessor.TargetSampleRate;
        public const float DefaultMinRmsEnergy = AudioPreprocessor.DefaultMinRmsEnergy;
        public const float DefaultMinDurationSec = AudioPreprocessor.DefaultMinDurationSec;

        public static float[] DownmixToMono(float[] samples, int channels)
        {
            return AudioPreprocessor.DownmixToMono(samples, channels);
        }

        public static float CalculateRmsEnergy(float[] samples)
        {
            return AudioPreprocessor.CalculateRmsEnergy(samples);
        }

        public static bool IsValidSpeechAudio(
            float[] samples,
            int frequency,
            int channels,
            float minDurationSec = DefaultMinDurationSec,
            float minRmsEnergy = DefaultMinRmsEnergy)
        {
            return AudioPreprocessor.IsValidSpeechAudio(samples, frequency, channels, minDurationSec, minRmsEnergy);
        }
    }
}
