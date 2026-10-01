using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AuctionEngine.Core.Lexicon;

namespace AuctionEngine.Core.Phonetics
{
    /// <summary>
    /// Indexes domain terms by phonetic alphabet code and performs fast exact/fuzzy matching
    /// to repair misrecognized ASR tokens and speech variations.
    /// </summary>
    public sealed class PhoneticMatcher : IPhoneticMatcher
    {
        private readonly IPhoneticEncoder _encoder;
        private readonly Dictionary<string, string> _exactIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<(string code, string canonical)> _domainTerms = new List<(string, string)>();
        private readonly HashSet<string> _validSingleWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly Regex HasLetter = new Regex(@"[a-zA-Z]", RegexOptions.Compiled);

        public PhoneticMatcher(IPhoneticEncoder encoder = null)
        {
            _encoder = encoder ?? new BrazilianPortuguesePhoneticEncoder();
            InitializeLexiconIndex();
        }

        private void InitializeLexiconIndex()
        {
            // Gather all single-word vocabulary
            var allVocab = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var k in AuctionLexicon.Bases.Keys) allVocab.Add(k);
            foreach (var k in AuctionLexicon.Multipliers.Keys) allVocab.Add(k);
            foreach (var k in AuctionLexicon.Denominations.Keys) allVocab.Add(k);
            foreach (var k in AuctionLexicon.CurrencyUnits) allVocab.Add(k);

            foreach (var term in allVocab)
            {
                if (!term.Contains(" "))
                {
                    _validSingleWords.Add(term);
                }
            }

            var sortedSingleWords = new List<string>(_validSingleWords);
            sortedSingleWords.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (var term in sortedSingleWords)
            {
                string code = _encoder.Encode(term);
                if (!string.IsNullOrEmpty(code))
                {
                    if (!_exactIndex.TryGetValue(code, out var existing) || term.Length < existing.Length)
                    {
                        _exactIndex[code] = term;
                    }
                    _domainTerms.Add((code, term));
                }
            }

            // Explicit homophone resolution in auction context:
            // Phonetic code 'SEN' maps strictly to 'cem' (100)
            _exactIndex["SEN"] = "cem";
        }

        public bool TryMatchToken(string token, out string canonicalMatch, out double similarity)
        {
            canonicalMatch = null;
            similarity = 0.0;

            if (string.IsNullOrWhiteSpace(token) || token.Length < 2)
                return false;

            string cleanToken = token.ToLowerInvariant();
            if (AuctionLexicon.ProtectedWords.Contains(cleanToken))
                return false;

            string pToken = _encoder.Encode(token);
            if (string.IsNullOrEmpty(pToken))
                return false;

            // 1. Exact phonetic match (0 edit distance in phonetic space)
            if (_exactIndex.TryGetValue(pToken, out var exactCanonical))
            {
                canonicalMatch = exactCanonical;
                similarity = 1.0;
                return true;
            }

            // 2. Fuzzy phonetic match with strict bounds
            int tokenLen = pToken.Length;
            if (tokenLen <= 2)
            {
                // Short tokens (<= 2 chars like 'sim', 'um', 'e') require exact phonetic match
                return false;
            }

            int maxAllowedDist = tokenLen <= 5 ? 1 : 2;
            const double minAllowedSim = 0.75;

            string bestMatch = null;
            double bestSim = 0.0;

            for (int i = 0; i < _domainTerms.Count; i++)
            {
                var (pCand, canonical) = _domainTerms[i];
                if (Math.Abs(pCand.Length - tokenLen) > maxAllowedDist)
                    continue;

                int dist = LevenshteinDistance.Calculate(pToken, pCand);
                if (dist <= maxAllowedDist)
                {
                    int maxCompLen = Math.Max(tokenLen, pCand.Length);
                    double sim = 1.0 - ((double)dist / maxCompLen);
                    if (sim >= minAllowedSim && sim > bestSim)
                    {
                        bestSim = sim;
                        bestMatch = canonical;
                    }
                }
            }

            if (bestMatch != null)
            {
                canonicalMatch = bestMatch;
                similarity = bestSim;
                return true;
            }

            return false;
        }

        public string RepairTokens(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            string[] rawTokens = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (rawTokens.Length == 0)
                return text;

            var resultTokens = new List<string>(rawTokens.Length);
            int i = 0;
            int n = rawTokens.Length;

            while (i < n)
            {
                string currToken = rawTokens[i].ToLowerInvariant();

                // Pass-through protected words
                if (AuctionLexicon.ProtectedWords.Contains(currToken))
                {
                    resultTokens.Add(rawTokens[i]);
                    i++;
                    continue;
                }

                // Check 2-token sliding window only if at least one token is unrecognized
                if (i + 1 < n)
                {
                    string nextToken = rawTokens[i + 1].ToLowerInvariant();
                    bool isCurrValid = _validSingleWords.Contains(currToken) || IsDigits(currToken);
                    bool isNextValid = _validSingleWords.Contains(nextToken) || IsDigits(nextToken);

                    if (!(isCurrValid && isNextValid))
                    {
                        string bigram = $"{rawTokens[i]} {rawTokens[i + 1]}";
                        if (HasLetter.IsMatch(bigram))
                        {
                            if (TryMatchToken(bigram, out var match, out _))
                            {
                                resultTokens.Add(match);
                                i += 2;
                                continue;
                            }
                        }
                    }
                }

                // Check single token
                if (HasLetter.IsMatch(currToken))
                {
                    if (_validSingleWords.Contains(currToken))
                    {
                        resultTokens.Add(currToken);
                        i++;
                        continue;
                    }

                    if (TryMatchToken(currToken, out var singleMatch, out _))
                    {
                        resultTokens.Add(singleMatch);
                        i++;
                        continue;
                    }
                }

                resultTokens.Add(rawTokens[i]);
                i++;
            }

            return string.Join(" ", resultTokens);
        }

        private static bool IsDigits(string str)
        {
            if (string.IsNullOrEmpty(str)) return false;
            for (int i = 0; i < str.Length; i++)
            {
                if (!char.IsDigit(str[i]) && str[i] != '.' && str[i] != ',')
                    return false;
            }
            return true;
        }
    }
}
