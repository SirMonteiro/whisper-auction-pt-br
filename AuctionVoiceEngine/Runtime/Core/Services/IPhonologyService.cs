namespace AuctionEngine.Core.Services
{
    /// <summary>
    /// Service responsible for phonological repairs, dialectal acoustic corrections,
    /// and idiom normalization of speech-to-text transcripts.
    /// </summary>
    public interface IPhonologyService
    {
        /// <summary>
        /// Normalizes raw transcribed text through dialect, idiom, and phonetic repair pipelines.
        /// </summary>
        string Repair(string text);

        /// <summary>
        /// Strips diacritical accents and returns lower-cased ASCII/latin text.
        /// </summary>
        string StripAccents(string text);
    }
}
