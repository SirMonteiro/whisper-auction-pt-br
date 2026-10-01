using System;
using System.Collections.Generic;

namespace AuctionEngine.Core.Models
{
    /// <summary>
    /// Immutable-friendly data transfer object representing the parsed outcome of an auction voice utterance.
    /// Fully serializable for Unity Inspector and network/events.
    /// </summary>
    [Serializable]
    public sealed class BidResult
    {
        public double Value { get; set; }
        public string Currency { get; set; }
        public bool HasAddition { get; set; }
        public bool HasMultiplication { get; set; }
        public string RawText { get; set; }
        public string CleanedText { get; set; }
        public bool IsValid { get; set; }
        public AuctionActionType ActionType { get; set; }
        public List<double> NumbersFound { get; set; }

        public BidResult()
        {
            Value = 0.0;
            Currency = "R$ 0,00";
            HasAddition = false;
            HasMultiplication = false;
            RawText = string.Empty;
            CleanedText = string.Empty;
            IsValid = false;
            ActionType = AuctionActionType.None;
            NumbersFound = new List<double>();
        }

        public BidResult(
            double value,
            string currency,
            bool hasAddition,
            bool hasMultiplication,
            string rawText,
            string cleanedText,
            bool isValid = true,
            AuctionActionType actionType = AuctionActionType.Bid,
            IEnumerable<double> numbersFound = null)
        {
            Value = value;
            Currency = currency ?? "R$ 0,00";
            HasAddition = hasAddition;
            HasMultiplication = hasMultiplication;
            RawText = rawText ?? string.Empty;
            CleanedText = cleanedText ?? string.Empty;
            IsValid = isValid;
            ActionType = actionType;
            NumbersFound = numbersFound != null ? new List<double>(numbersFound) : new List<double>();
        }

        public override string ToString()
        {
            return $"[BidResult] Action={ActionType}, Value={Value:F2} ({Currency}), Add={HasAddition}, Mult={HasMultiplication}, Valid={IsValid}, Raw=\"{RawText}\"]";
        }
    }
}
