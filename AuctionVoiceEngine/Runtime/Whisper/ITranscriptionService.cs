using System;
using System.Threading;
using System.Threading.Tasks;

namespace AuctionEngine.Whisper
{
    /// <summary>
    /// Legacy interface contract for speech-to-text inference engines.
    /// Redirects to <see cref="AuctionEngine.Inference.ITranscriptionService"/>.
    /// </summary>
    [Obsolete("Use AuctionEngine.Inference.ITranscriptionService instead.")]
    public interface ITranscriptionService : AuctionEngine.Inference.ITranscriptionService
    {
    }
}
