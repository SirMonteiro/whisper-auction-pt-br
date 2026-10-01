using AuctionEngine.Core.Models;

namespace AuctionEngine.Core.Services
{
    /// <summary>
    /// Contract for evaluating spoken auction transcripts and producing structured bid results.
    /// </summary>
    public interface IAuctionBidEvaluator
    {
        /// <summary>
        /// Evaluates an utterance text and extracts numeric value, currency, actions, and flags.
        /// </summary>
        BidResult Evaluate(string text);
    }
}
