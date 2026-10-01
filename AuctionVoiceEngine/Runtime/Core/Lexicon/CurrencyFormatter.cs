using System;
using System.Globalization;

namespace AuctionEngine.Core.Lexicon
{
    /// <summary>
    /// Thread-safe formatter for Brazilian currency (Real - R$).
    /// Conforms to pt-BR financial standards (e.g. 1000.5 -> "R$ 1.000,50").
    /// </summary>
    public static class CurrencyFormatter
    {
        private static readonly NumberFormatInfo BrazilianFormat = new NumberFormatInfo
        {
            NumberGroupSeparator = ".",
            NumberDecimalSeparator = ",",
            CurrencySymbol = "R$ ",
            CurrencyDecimalDigits = 2,
            NumberDecimalDigits = 2
        };

        /// <summary>
        /// Formats a numeric value into Brazilian Real representation.
        /// </summary>
        public static string FormatBrazilianCurrency(double val)
        {
            return $"R$ {val.ToString("N2", BrazilianFormat)}";
        }
    }
}
