using System.Threading;
using System.Threading.Tasks;

namespace AuctionEngine.Whisper
{
    /// <summary>
    /// Contract for speech-to-text inference engines.
    /// Supports asynchronous transcription with language selection and cancellation.
    /// </summary>
    public interface ITranscriptionService
    {
        /// <summary>
        /// Transcribes an array of audio samples into plain text.
        /// </summary>
        /// <param name="samples">PCM float32 audio samples.</param>
        /// <param name="frequency">Audio sample rate (e.g. 16000).</param>
        /// <param name="channels">Number of audio channels.</param>
        /// <param name="language">Language code (e.g. "pt", "en"). If null, uses default.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Transcribed text string or empty if silent/invalid.</returns>
        Task<string> TranscribeAsync(
            float[] samples,
            int frequency,
            int channels,
            string language = null,
            CancellationToken cancellationToken = default
        );
    }
}
