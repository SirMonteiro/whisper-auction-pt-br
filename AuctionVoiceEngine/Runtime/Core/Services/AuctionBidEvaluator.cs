using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using AuctionEngine.Core.Lexicon;
using AuctionEngine.Core.Models;

namespace AuctionEngine.Core.Services
{
    /// <summary>
    /// Evaluates auction voice utterances and extracts structured bid outcomes,
    /// arithmetic totals, slang multiplier interpretations, and chant repetitions.
    /// </summary>
    public sealed class AuctionBidEvaluator : IAuctionBidEvaluator
    {
        private const RegexOptions RegOpts = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        private readonly IPhonologyService _phonologyService;

        private static readonly Regex DigitsOnlyRegex = new Regex(@"^\d+(?:\.\d+)?$", RegOpts);
        private static readonly Regex SingleDigitCharRegex = new Regex(@"^\d$", RegOpts);

        // Multiplier verbs
        private static readonly Regex DobroRegex = new Regex(@"\b(?:dobr\w*|duplic\w*)\b", RegOpts);
        private static readonly Regex TriploRegex = new Regex(@"\b(?:triplic\w*|tripl\w*)\b", RegOpts);
        private static readonly Regex QuadruploRegex = new Regex(@"\b(?:quadr[ue]plic\w*|quadr[ue]pl\w*)\b", RegOpts);

        public AuctionBidEvaluator(IPhonologyService phonologyService = null)
        {
            _phonologyService = phonologyService ?? new PhonologicalRepairer();
        }

        public BidResult Evaluate(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new BidResult(
                    0.0,
                    CurrencyFormatter.FormatBrazilianCurrency(0.0),
                    false,
                    false,
                    text,
                    string.Empty,
                    false,
                    AuctionActionType.None
                );
            }

            string cleanText = _phonologyService.Repair(text);

            // 1. Check for pass phrases ("passo", "tô fora", etc.)
            foreach (var phrase in AuctionLexicon.PassPhrases)
            {
                var phraseRegex = new Regex(@"\b" + Regex.Escape(phrase) + @"\b", RegOpts);
                if (phraseRegex.IsMatch(cleanText))
                {
                    return new BidResult(
                        0.0,
                        CurrencyFormatter.FormatBrazilianCurrency(0.0),
                        false,
                        false,
                        text,
                        cleanText,
                        true,
                        AuctionActionType.Pass
                    );
                }
            }

            // 2. Multiplier verbs
            double? multiplierVerbVal = null;
            if (DobroRegex.IsMatch(cleanText)) multiplierVerbVal = 2.0;
            else if (TriploRegex.IsMatch(cleanText)) multiplierVerbVal = 3.0;
            else if (QuadruploRegex.IsMatch(cleanText)) multiplierVerbVal = 4.0;

            // 3. Segment by addition operator '+'
            string[] plusSegments = cleanText.Split('+');
            var allNumbers = new List<double>();

            for (int i = 0; i < plusSegments.Length; i++)
            {
                var nums = ExtractNumbersFromSegment(plusSegments[i]);
                allNumbers.AddRange(nums);
            }

            int totalNumbers = allNumbers.Count;
            int plusCount = CountOccurrences(cleanText, '+');

            // Standalone multiplier (e.g. "Dobro!", "Triplica!", "Quadreplico!")
            if (multiplierVerbVal.HasValue && totalNumbers == 0)
            {
                double mVal = multiplierVerbVal.Value;
                return new BidResult(
                    mVal,
                    CurrencyFormatter.FormatBrazilianCurrency(mVal),
                    false,
                    true,
                    text,
                    cleanText,
                    true,
                    AuctionActionType.MultiplyOnly
                );
            }

            // No numbers found
            if (totalNumbers == 0)
            {
                return new BidResult(
                    0.0,
                    CurrencyFormatter.FormatBrazilianCurrency(0.0),
                    false,
                    false,
                    text,
                    cleanText,
                    false,
                    AuctionActionType.None
                );
            }

            // Auction connector and repetition evaluation:
            // 1. When "mais" and a single number (e.g. "mais dois"):
            //    Add = True, value = 2.0 (incremental bid).
            // 2. When two or more numbers are detected all connected by "mais" (e.g. "dois mais dois"):
            //    Calculate sum (4.0), and leave Add = False (absolute calculated bid).
            // 3. When multiple numbers are detected without "mais" connectors between all of them:
            //    Pick the last detected number! (e.g. "50, 40, 20" -> 20.0, "Vinte-sete, vinte-se, vint-sate" -> 27.0).
            double result;
            bool hasAddition;

            if (totalNumbers == 1)
            {
                result = allNumbers[0];
                hasAddition = (plusCount >= 1);
            }
            else if (totalNumbers >= 2)
            {
                if (plusCount == (totalNumbers - 1))
                {
                    // Two or more numbers all connected by 'mais': sum and set Add = False
                    double sum = 0.0;
                    for (int i = 0; i < allNumbers.Count; i++) sum += allNumbers[i];
                    result = sum;
                    hasAddition = false;
                }
                else
                {
                    // Multiple numbers not all connected by '+': pick last detected one
                    result = allNumbers[allNumbers.Count - 1];
                    hasAddition = false;
                }
            }
            else
            {
                result = 0.0;
                hasAddition = false;
            }

            bool hasMultiplication = multiplierVerbVal.HasValue;
            if (multiplierVerbVal.HasValue && result > 0)
            {
                result *= multiplierVerbVal.Value;
            }

            return new BidResult(
                result,
                CurrencyFormatter.FormatBrazilianCurrency(result),
                hasAddition,
                hasMultiplication,
                text,
                cleanText,
                true,
                AuctionActionType.Bid,
                allNumbers
            );
        }

        public static List<float> ExtractNumbersFromSegmentFloat(string segment)
        {
            var doubles = ExtractNumbersFromSegment(segment);
            var floats = new List<float>(doubles.Count);
            for (int i = 0; i < doubles.Count; i++) floats.Add((float)doubles[i]);
            return floats;
        }

        public static List<double> ExtractNumbersFromSegment(string segment)
        {
            string[] tokens = segment.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var chunks = new List<double>();
            var currentChunk = new List<string>();
            int? lastScale = null;

            for (int i = 0; i < tokens.Length; i++)
            {
                string t = tokens[i];
                if (IsNumberWord(t))
                {
                    int scale = GetTokenScale(t);
                    bool isBoundary = false;

                    if (currentChunk.Count > 0 && lastScale.HasValue)
                    {
                        int ls = lastScale.Value;
                        if (ls == 1 && scale == 1)
                        {
                            isBoundary = false;
                        }
                        else if ((ls >= 1 && ls <= 4) && (scale == 5 || scale == 6))
                        {
                            isBoundary = false;
                        }
                        else if (ls == 5 && (scale >= 1 && scale <= 4))
                        {
                            isBoundary = false;
                        }
                        else if (ls == 4 && (scale >= 1 && scale <= 3))
                        {
                            isBoundary = false;
                        }
                        else if (ls == 3 && scale == 1)
                        {
                            isBoundary = false;
                        }
                        else if (scale == 0)
                        {
                            isBoundary = false;
                        }
                        else
                        {
                            isBoundary = true;
                        }
                    }

                    if (isBoundary)
                    {
                        if (currentChunk.Count > 0)
                        {
                            double val = ParseSubexpression(string.Join(" ", currentChunk));
                            if (val > 0) chunks.Add(val);
                        }
                        currentChunk.Clear();
                        currentChunk.Add(t);
                    }
                    else
                    {
                        currentChunk.Add(t);
                    }
                    lastScale = scale;
                }
                else if ((t == "e" || t == "de") && currentChunk.Count > 0)
                {
                    if (i + 1 < tokens.Length && IsNumberWord(tokens[i + 1]))
                    {
                        int nextScale = GetTokenScale(tokens[i + 1]);
                        if (lastScale == 3 && (nextScale == 2 || nextScale == 3))
                        {
                            double val = ParseSubexpression(string.Join(" ", currentChunk));
                            if (val > 0) chunks.Add(val);
                            currentChunk.Clear();
                            lastScale = null;
                        }
                        else
                        {
                            currentChunk.Add(t);
                        }
                    }
                    else
                    {
                        if (currentChunk.Count > 0)
                        {
                            double val = ParseSubexpression(string.Join(" ", currentChunk));
                            if (val > 0) chunks.Add(val);
                        }
                        currentChunk.Clear();
                        lastScale = null;
                    }
                }
                else
                {
                    if (currentChunk.Count > 0)
                    {
                        double val = ParseSubexpression(string.Join(" ", currentChunk));
                        if (val > 0) chunks.Add(val);
                    }
                    currentChunk.Clear();
                    lastScale = null;
                }
            }

            if (currentChunk.Count > 0)
            {
                double val = ParseSubexpression(string.Join(" ", currentChunk));
                if (val > 0) chunks.Add(val);
            }

            return chunks;
        }

        public static double ParseSubexpression(string chunkText)
        {
            string[] rawTokens = chunkText.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (rawTokens.Length == 0)
                return 0.0;

            bool hasE = false;
            bool hasComplex = false;
            for (int k = 0; k < rawTokens.Length; k++)
            {
                string tk = rawTokens[k];
                if (tk == "e") hasE = true;
                if (AuctionLexicon.Bases.TryGetValue(tk, out var baseVal) && baseVal >= 10) hasComplex = true;
                if (AuctionLexicon.Multipliers.ContainsKey(tk)) hasComplex = true;
            }

            // Fast path: sequence of digits or single-digit words without "e" ("sete seis" -> 76)
            var digitSeq = new List<string>();
            for (int k = 0; k < rawTokens.Length; k++)
            {
                string tk = rawTokens[k];
                if (tk == "meia" || tk == "meio")
                {
                    digitSeq.Add("6");
                }
                else if (AuctionLexicon.SingleDigits.TryGetValue(tk, out var dVal))
                {
                    digitSeq.Add(dVal.ToString());
                }
                else if (SingleDigitCharRegex.IsMatch(tk))
                {
                    digitSeq.Add(tk);
                }
            }

            if (digitSeq.Count >= 2 && !hasE && !hasComplex)
            {
                if (double.TryParse(string.Join("", digitSeq), NumberStyles.Float, CultureInfo.InvariantCulture, out double fastVal))
                {
                    return fastVal;
                }
            }

            double total = 0.0;
            double current = 0.0;
            double centavos = 0.0;
            double noteMultiplier = 1.0;
            double lastMultiplier = 0.0;
            bool inCentavos = false;

            int i = 0;
            while (i < rawTokens.Length)
            {
                string token = rawTokens[i];
                string twoTokens = (i + 1 < rawTokens.Length) ? $"{token} {rawTokens[i + 1]}" : string.Empty;

                // Raw numeric digits (e.g. 50, 100, 100.0)
                if (DigitsOnlyRegex.IsMatch(token))
                {
                    if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedNum))
                    {
                        current += parsedNum;
                        i++;
                        continue;
                    }
                }

                // Two-word denomination (e.g. "mico leao", "lobo guara")
                if (!string.IsNullOrEmpty(twoTokens) && AuctionLexicon.Denominations.TryGetValue(twoTokens, out double denomVal2))
                {
                    double count = current > 0 ? current : 1.0;
                    total += count * denomVal2;
                    lastMultiplier = denomVal2;
                    current = 0.0;
                    i += 2;
                    continue;
                }

                // Single-word denomination (e.g. "onca", "tartaruga")
                if (AuctionLexicon.Denominations.TryGetValue(token, out double denomVal1))
                {
                    double count = current > 0 ? current : 1.0;
                    total += count * denomVal1;
                    lastMultiplier = denomVal1;
                    current = 0.0;
                    i++;
                    continue;
                }

                // Note/Coin quantifiers: "duas notas de cem"
                if (AuctionLexicon.NoteDenominators.Contains(token))
                {
                    noteMultiplier = current > 0 ? current : 1.0;
                    current = 0.0;
                    if (i + 1 < rawTokens.Length && rawTokens[i + 1] == "de")
                    {
                        i++;
                    }
                    i++;
                    continue;
                }

                // Centavos
                if (token == "centavo" || token == "centavos")
                {
                    if (inCentavos)
                    {
                        total += centavos / 100.0;
                        centavos = 0.0;
                        inCentavos = false;
                    }
                    else if (current > 0)
                    {
                        total += current / 100.0;
                        current = 0.0;
                    }
                    i++;
                    continue;
                }

                // Direct currency units: "reais", "conto", "pila", "prata"
                if (token == "real" || token == "reais" || token == "conto" || token == "contos" || token == "continho" ||
                    token == "pila" || token == "pilas" || token == "prata" || token == "pratas")
                {
                    total += current * noteMultiplier;
                    noteMultiplier = 1.0;
                    lastMultiplier = 1.0;
                    current = 0.0;
                    if (i + 1 < rawTokens.Length && AuctionLexicon.Bases.ContainsKey(rawTokens[i + 1]))
                    {
                        inCentavos = true;
                    }
                    i++;
                    continue;
                }

                // "pau" / "paus"
                if (token == "pau" || token == "paus")
                {
                    if (current > 0 && current < 100)
                    {
                        total += current * 1000.0;
                        lastMultiplier = 1000.0;
                    }
                    else if (current == 0)
                    {
                        total += 1000.0;
                        lastMultiplier = 1000.0;
                    }
                    else
                    {
                        total += current;
                        lastMultiplier = 1.0;
                    }
                    current = 0.0;
                    i++;
                    continue;
                }

                // "meio" / "meia"
                if (token == "meio" || token == "meia")
                {
                    string prevToken = (i > 0) ? rawTokens[i - 1] : string.Empty;
                    if (prevToken == "e")
                    {
                        if (lastMultiplier > 0)
                        {
                            total += 0.5 * lastMultiplier;
                        }
                        else if (current > 0)
                        {
                            current += 0.5;
                        }
                        else if (total > 0)
                        {
                            total += 0.5;
                        }
                        i++;
                        continue;
                    }
                    else
                    {
                        // Standalone "meia" in bingo / auction = 6
                        current += 6.0;
                        i++;
                        continue;
                    }
                }

                // Multipliers (mil, barao, placa, milhao, etc.)
                if (AuctionLexicon.Multipliers.TryGetValue(token, out double multVal))
                {
                    current = (current == 0.0) ? 1.0 : current;
                    total += current * multVal;
                    lastMultiplier = multVal;
                    current = 0.0;
                    i++;
                    continue;
                }

                // Base numbers
                if (AuctionLexicon.Bases.TryGetValue(token, out int bVal))
                {
                    if (inCentavos)
                    {
                        centavos += bVal;
                    }
                    else
                    {
                        current += bVal;
                    }
                    i++;
                    continue;
                }

                i++;
            }

            total += current * noteMultiplier;
            if (centavos > 0)
            {
                total += centavos / 100.0;
            }

            return total;
        }

        public static bool IsNumberWord(string token)
        {
            if (DigitsOnlyRegex.IsMatch(token)) return true;
            if (AuctionLexicon.Bases.ContainsKey(token)) return true;
            if (AuctionLexicon.Multipliers.ContainsKey(token)) return true;
            if (AuctionLexicon.Denominations.ContainsKey(token)) return true;
            if (token == "meia" || token == "meio") return true;
            if (AuctionLexicon.CurrencyUnits.Contains(token)) return true;
            if (AuctionLexicon.NoteDenominators.Contains(token)) return true;
            return false;
        }

        public static int GetTokenScale(string token)
        {
            if (DigitsOnlyRegex.IsMatch(token))
            {
                if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                {
                    if (val < 10) return 1;
                    if (val < 20) return 2;
                    if (val < 100) return 3;
                    if (val < 1000) return 4;
                    return 5;
                }
                return 1;
            }

            if (AuctionLexicon.SingleDigits.ContainsKey(token) ||
                token == "meia" || token == "meio" || token == "unzinho" ||
                token == "umzinho" || token == "doizinho" || token == "doisao" ||
                token == "cincao" || token == "cinquinho")
            {
                return 1;
            }

            if (token == "dez" || token == "onze" || token == "doze" || token == "treze" ||
                token == "quatorze" || token == "catorze" || token == "quinze" ||
                token == "dezesseis" || token == "dezessete" || token == "dezoito" ||
                token == "dezenove" || token == "quinzinho" || token == "dezao" ||
                token == "dezinho" || token == "deizinho")
            {
                return 2;
            }

            if (token == "vinte" || token == "trinta" || token == "quarenta" ||
                token == "cinquenta" || token == "sessenta" || token == "setenta" ||
                token == "oitenta" || token == "noventa" ||
                token.EndsWith("tao") || token.EndsWith("tola") ||
                token.EndsWith("tinho") || token.EndsWith("tinha"))
            {
                return 3;
            }

            if (token.StartsWith("cem") || token.StartsWith("cento") || token.StartsWith("senzao") ||
                token.StartsWith("duzent") || token.StartsWith("trezent") || token.StartsWith("quatrocent") ||
                token.StartsWith("quinhent") || token.StartsWith("seiscent") || token.StartsWith("setecent") ||
                token.StartsWith("oitocent") || token.StartsWith("novecent") || token == "quintinho")
            {
                return 4;
            }

            if (AuctionLexicon.Multipliers.ContainsKey(token) || token == "pau" || token == "paus")
            {
                return 5;
            }

            if (AuctionLexicon.Denominations.ContainsKey(token))
            {
                return 6;
            }

            return 0;
        }

        private static int CountOccurrences(string text, char c)
        {
            int count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == c) count++;
            }
            return count;
        }
    }
}
