namespace AuctionEngine.Core.Phonetics
{
    /// <summary>
    /// Contract for phonetic vocabulary matching and sliding-window token repair.
    /// </summary>
    public interface IPhoneticMatcher
    {
        /// <summary>
        /// Attempts to match a single token (or bigram) against the canonical domain vocabulary.
        /// </summary>
        bool TryMatchToken(string token, out string canonicalMatch, out double similarity);

        /// <summary>
        /// Scans a text string token by token (with sliding bigram window) and replaces noisy/near-phonetic words.
        /// </summary>
        string RepairTokens(string text);
    }
}
