namespace AuctionEngine.Core.Phonetics
{
    /// <summary>
    /// Contract for Grapheme-to-Phoneme (G2P) phonetic encoders.
    /// Converts orthographic text or noisy speech transcripts into a phonetic representation.
    /// </summary>
    public interface IPhoneticEncoder
    {
        /// <summary>
        /// Encodes a word or phrase into its canonical phonetic alphabet string.
        /// </summary>
        string Encode(string text);
    }
}
