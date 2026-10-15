using System.Collections.Generic;

namespace AuctionEngine.Inference
{
    /// <summary>
    /// Contract for tokenizing and decoding neural speech recognition tokens.
    /// Manages special tokens, language tokens, and vocabulary text decoding.
    /// </summary>
    public interface ITokenizer
    {
        int EndOfTextToken { get; }
        int StartOfTranscriptToken { get; }
        int TranscribeToken { get; }
        int TranslateToken { get; }
        int NoTimestampsToken { get; }

        /// <summary>
        /// Returns the token ID corresponding to an ISO language code (e.g. "pt" -> 50275).
        /// </summary>
        int GetLanguageToken(string languageCode);

        /// <summary>
        /// Generates the initial sequence of prompt tokens for Whisper autoregressive decoding.
        /// </summary>
        int[] GetInitialTokens(string languageCode = "pt", bool withTimestamps = false);

        /// <summary>
        /// Decodes a sequence of token IDs into plain text.
        /// </summary>
        string Decode(IReadOnlyList<int> tokens);
    }
}
