using System;

namespace AuctionEngine.Audio
{
    /// <summary>
    /// Utility class for conditioning raw audio samples prior to neural network inference.
    /// Handles channel downmix, linear resampling, RMS silence gating, peak normalization,
    /// and minimum speech duration validation.
    /// </summary>
    public static class AudioPreprocessor
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
        /// to avoid neural model hallucinations on ambient room silence.
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

        /// <summary>
        /// Resamples audio from a source sampling rate to a target sampling rate using linear interpolation.
        /// </summary>
        public static float[] Resample(float[] samples, int sourceRate, int targetRate)
        {
            if (samples == null || samples.Length == 0)
                return Array.Empty<float>();

            if (sourceRate == targetRate || sourceRate <= 0 || targetRate <= 0)
                return samples;

            double ratio = (double)sourceRate / targetRate;
            int newLength = (int)Math.Round(samples.Length / ratio);
            if (newLength <= 0)
                return Array.Empty<float>();

            float[] resampled = new float[newLength];

            for (int i = 0; i < newLength; i++)
            {
                double srcPos = i * ratio;
                int idx = (int)srcPos;
                double frac = srcPos - idx;

                if (idx + 1 < samples.Length)
                {
                    resampled[i] = (float)((1.0 - frac) * samples[idx] + frac * samples[idx + 1]);
                }
                else if (idx < samples.Length)
                {
                    resampled[i] = samples[idx];
                }
            }

            return resampled;
        }

        /// <summary>
        /// Normalizes audio peak amplitude to the specified target peak (default 0.95)
        /// to prevent clipping while maintaining dynamic range.
        /// </summary>
        public static float[] Normalize(float[] samples, float targetPeak = 0.95f)
        {
            if (samples == null || samples.Length == 0)
                return Array.Empty<float>();

            float maxAmp = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float abs = Math.Abs(samples[i]);
                if (abs > maxAmp)
                    maxAmp = abs;
            }

            if (maxAmp <= 0f || Math.Abs(maxAmp - targetPeak) < 1e-4f)
                return samples;

            float factor = targetPeak / maxAmp;
            float[] normalized = new float[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                normalized[i] = samples[i] * factor;
            }

            return normalized;
        }

        /// <summary>
        /// Pads audio with zeros or trims it to exactly targetLength samples.
        /// Useful for feeding fixed-size neural network input shapes (e.g. 30s * 16000 = 480,000 samples).
        /// </summary>
        public static float[] PadOrTrim(float[] samples, int targetLength)
        {
            if (targetLength <= 0)
                return Array.Empty<float>();

            float[] result = new float[targetLength];
            if (samples != null && samples.Length > 0)
            {
                int copyLength = Math.Min(samples.Length, targetLength);
                Array.Copy(samples, 0, result, 0, copyLength);
            }

            return result;
        }
    }
}
