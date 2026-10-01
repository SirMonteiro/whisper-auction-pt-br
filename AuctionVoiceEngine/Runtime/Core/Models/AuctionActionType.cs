namespace AuctionEngine.Core.Models
{
    /// <summary>
    /// Represents the high-level semantic intent identified from an auction voice utterance.
    /// </summary>
    public enum AuctionActionType
    {
        /// <summary>Valid monetary bid.</summary>
        Bid,

        /// <summary>Participant explicitly passes or withdraws from the auction (e.g. "passo", "tô fora").</summary>
        Pass,

        /// <summary>Standalone verbal multiplier intent without numbers (e.g. "dobro!", "triplica!").</summary>
        MultiplyOnly,

        /// <summary>No numbers or actionable auction intent detected.</summary>
        None,

        /// <summary>Unrecognized or ungrammatical input.</summary>
        Invalid
    }
}
