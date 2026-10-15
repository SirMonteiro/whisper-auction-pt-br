namespace AuctionEngine.Inference
{
    /// <summary>
    /// Contract for extracting log-mel spectrograms from audio waveforms.
    /// Converts 16,000 Hz audio into the standard 80-channel log-mel representation required by Whisper and ASR models.
    /// </summary>
    public interface ILogMelSpectrogramExtractor
    {
        int MelBins { get; }
        int SampleRate { get; }
        int FftSize { get; }
        int HopSize { get; }

        /// <summary>
        /// Computes 2D log-mel spectrogram [melBins, frames] from 16kHz mono audio.
        /// </summary>
        float[,] ExtractMelSpectrogram(float[] audioSamples);

        /// <summary>
        /// Flattens the spectrogram into a 1D float array of shape [melBins * targetFrames]
        /// formatted in row-major order for neural model tensors.
        /// </summary>
        float[] ExtractMelFlattened(float[] audioSamples, int targetFrames = 3000);
    }
}
