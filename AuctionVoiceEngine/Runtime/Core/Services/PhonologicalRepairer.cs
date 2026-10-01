using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AuctionEngine.Core.Lexicon;
using AuctionEngine.Core.Phonetics;

namespace AuctionEngine.Core.Services
{
    /// <summary>
    /// High-performance phonological repair pipeline combining pre-compiled regexes,
    /// master DFA idiom matching, and the Brazilian Portuguese Phonetic Alphabet.
    /// </summary>
    public sealed class PhonologicalRepairer : IPhonologyService
    {
        private const RegexOptions RegOpts = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        private readonly IPhoneticMatcher _phoneticMatcher;

        private static readonly Regex HyphensRegex = new Regex(@"-", RegOpts);
        private static readonly Regex MultOperators = new Regex(@"\b(?:vezes|multiplicado\s+por|x)\b", RegOpts);
        private static readonly Regex AddOperators = new Regex(@"\b(?:mais|somado\s+a|com\s+mais|e\s+mais)\b", RegOpts);
        private static readonly Regex DecimalCommas = new Regex(@"(\d+)[,\.](\d+)", RegOpts);
        private static readonly Regex StripSymbols = new Regex(@"[^\w\s\.\+\*]", RegOpts);
        private static readonly Regex IsolatedDots = new Regex(@"(?<!\d)\.|\.(?!\d)", RegOpts);
        private static readonly Regex FillerUmaVez = new Regex(@"\b(?:mais\s+)?de\s+uma?\s+vez\b", RegOpts);

        // English number word mappings
        private static readonly Dictionary<string, string> EnglishNumMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "zero", "zero" }, { "one", "um" }, { "two", "dois" }, { "three", "tres" },
            { "four", "quatro" }, { "five", "cinco" }, { "six", "seis" }, { "seven", "sete" },
            { "eight", "oito" }, { "nine", "nove" }, { "ten", "dez" }, { "twenty", "vinte" },
            { "fifty", "cinquenta" }, { "hundred", "cem" }, { "thousand", "mil" }
        };
        private static readonly Regex EnWordsRegex = new Regex(@"\b(zero|one|two|three|four|five|six|seven|eight|nine|ten|twenty|fifty|hundred|thousand)\b", RegOpts);

        // Acoustic misrecognitions and dialectal variants
        private static readonly Regex SixentaRegex = new Regex(@"\b(?:sixenta|sisenta|seicenta)\b", RegOpts);
        private static readonly Regex NovemMilRegex = new Regex(@"\bnovem(?=\s+mil\b)", RegOpts);
        private static readonly Regex NoventenoveRegex = new Regex(@"\bnovent[ea]n(?:ove|oi)\b", RegOpts);
        private static readonly Regex NovecententaRegex = new Regex(@"\bnovecententa\b", RegOpts);
        private static readonly Regex MasAsMaisRegex = new Regex(
            @"\bmas(?=\s+(?:mil|\d+|um|dois|duas|tres|quatro|cinco|seis|sete|oito|nove|dez|onze|doze|vinte|trinta|quarenta|cinquenta|sessenta|setenta|oitenta|noventa|cem|cento|duzentos|quinhentos|reais|conto|pila|bar[ao]es?|placas?|pau))\b",
            RegOpts
        );
        private static readonly Regex NoventaEmNoveRegex = new Regex(
            @"\b(vinte|trinta|quarenta|cinquenta|sessenta|setenta|oitenta|noventa)\s+em\s+(um|dois|duas|tres|quatro|cinco|seis|sete|oito|nove)\b",
            RegOpts
        );
        private static readonly Regex EnglishAndRegex = new Regex(
            @"\band\b(?=\s*(?:nove|oito|sete|seis|cinco|quatro|tres|dois|um|noventa|oitenta|setenta|sessenta|cinquenta|quarenta|trinta|vinte|dez|\d+))",
            RegOpts
        );

        // Fauna article remover
        private static readonly Regex FaunaArticleRegex = new Regex(
            @"\buma?\s+(beija\s+flor|tartaruga|garca|arara|mico\s+leao|mico|onca\s+pintada|onca|oncinha|garoupa|peixe|lobo\s+guara|lobo|lobinho)\b",
            RegOpts
        );

        // Sandhi and contracted numerals
        private const string TensHundreds = @"(?:vinte|trinta|quarenta|cinquenta|sessenta|setenta|oitenta|noventa|cento|duzentos?|duzentas?|trezentos?|trezentas?|quatrocentos?|quatrocentas?|quinhentos?|quinhentas?|seiscentos?|seiscentas?|setecentos?|setecentas?|oitocentos?|oitocentas?|novecentos?|novecentas?)";
        private const string UnitsTeens = @"(?:um|uma|dois|duas|tres|quatro|cinco|seis|sete|oito|nove|dez|onze|doze|treze|quatorze|catorze|quinze|dezesseis|dezessete|dezoito|dezenove|vinte|trinta|quarenta|cinquenta|sessenta|setenta|oitenta|noventa)";

        private static readonly Regex SandhiNumeralsRegex = new Regex(@"\b(" + TensHundreds + @")e(" + UnitsTeens + @")\b", RegOpts);
        private static readonly Regex HundredsAndRegex = new Regex(@"\b(duzent|trezent|quatrocent|quinhent|seiscent|setecent|oitocent|novecent)[ei](" + UnitsTeens + @")\b", RegOpts);
        private static readonly Regex SeteRegex = new Regex(@"\bsei?t[ei](s)?\b", RegOpts);

        // Master Compiled Idiom Regex (Single Pass DFA matching longest idioms first)
        private static readonly Regex MasterIdiomRegex;

        // Sandhi augmentatives
        private static readonly (Regex pattern, string repl)[] SandhiAugmentatives = new[]
        {
            (new Regex(@"\bduz[aei]\s+entao\b", RegOpts), "duzentao"),
            (new Regex(@"\bquinz[aei]\s+entao\b", RegOpts), "quinhentao"),
            (new Regex(@"\bquatr[oe]\s+entao\b", RegOpts), "quatrocentao"),
            (new Regex(@"\btrez[aei]\s+entao\b", RegOpts), "trezentao"),
            (new Regex(@"\boit[aeo]\s+entao\b", RegOpts), "oitocentao"),
            (new Regex(@"\btu[sz]ent(ao|ola|inho|os)\b", RegOpts), "duzent$1"),
            (new Regex(@"\bsen(s|z)(ao|inho)\b", RegOpts), "cemzao"),
            (new Regex(@"\bcen(s|z)(ao|inho)\b", RegOpts), "cemzao"),
            (new Regex(@"\btresent(ao|os|inho|inhos|ola)\b", RegOpts), "trezent$1"),
            (new Regex(@"\bseissent(os|as|ao)\b", RegOpts), "seiscent$1"),
            (new Regex(@"\btrent(a|ao|inho|ola)\b", RegOpts), "trint$1"),
            (new Regex(@"\bcincoent", RegOpts), "cinquent"),
            (new Regex(@"\bquintinho(s)?\b", RegOpts), "quinhentos"),
            (new Regex(@"\bvint(im|em)\b", RegOpts), "vinte"),
            (new Regex(@"\b(?:meio|meu|mepau)\s+pau\b", RegOpts), "quinhentos reais"),
            (new Regex(@"\b(?:meio|meu)\s+barao\b", RegOpts), "quinhentos reais")
        };

        // Colloquial PB plural erasure
        private static readonly (Regex pattern, string repl)[] PluralErasure = new[]
        {
            (new Regex(@"\b(\d+|dois|tres|quatro|cinco|seis|sete|oito|nove|dez)\s+barao\b", RegOpts), "$1 baroes"),
            (new Regex(@"\b(\d+|duas|tres|quatro|cinco|seis|sete|oito|nove|dez)\s+placa\b", RegOpts), "$1 placas"),
            (new Regex(@"\b(\d+|duas|tres|quatro|cinco|seis|sete|oito|nove|dez)\s+onca\b", RegOpts), "$1 oncas"),
            (new Regex(@"\b(\d+|duas|tres|quatro|cinco|seis|sete|oito|nove|dez)\s+garca\b", RegOpts), "$1 garcas"),
            (new Regex(@"\b(\d+|dois|tres|quatro|cinco|seis|sete|oito|nove|dez)\s+lobo\b", RegOpts), "$1 lobos"),
            (new Regex(@"\b(\d+|dois|tres|quatro|cinco|seis|sete|oito|nove|dez)\s+peixe\b", RegOpts), "$1 peixes")
        };

        static PhonologicalRepairer()
        {
            var sortedKeys = new List<string>(AuctionLexicon.IdiomMap.Keys);
            sortedKeys.Sort((a, b) => b.Length.CompareTo(a.Length)); // Longest first

            var escapedKeys = new List<string>(sortedKeys.Count);
            for (int i = 0; i < sortedKeys.Count; i++)
            {
                escapedKeys.Add(Regex.Escape(sortedKeys[i]));
            }

            string masterPattern = @"\b(" + string.Join("|", escapedKeys) + @")\b";
            MasterIdiomRegex = new Regex(masterPattern, RegOpts);
        }

        public PhonologicalRepairer(IPhoneticMatcher phoneticMatcher = null)
        {
            _phoneticMatcher = phoneticMatcher ?? new PhoneticMatcher();
        }

        public string StripAccents(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            string nfd = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(nfd.Length);
            for (int i = 0; i < nfd.Length; i++)
            {
                char c = nfd[i];
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }
            return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        }

        public string Repair(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            string t = StripAccents(text);
            t = HyphensRegex.Replace(t, " ");

            // 1. Direct acoustic dialectal misrecognitions
            t = SixentaRegex.Replace(t, "sessenta");
            t = NovemMilRegex.Replace(t, "nove");
            t = NoventenoveRegex.Replace(t, "noventa e nove");
            t = NovecententaRegex.Replace(t, "noventa");
            t = MasAsMaisRegex.Replace(t, "mais");
            t = NoventaEmNoveRegex.Replace(t, "$1 e $2");
            t = EnglishAndRegex.Replace(t, "e");

            // 2. English number word fallbacks
            t = EnWordsRegex.Replace(t, m => EnglishNumMap.TryGetValue(m.Value, out var pt) ? pt : m.Value);

            // 3. Strip singular articles before fauna terms
            t = FaunaArticleRegex.Replace(t, "$1");

            // 4. Master Idiom replacement (Single Pass DFA)
            t = MasterIdiomRegex.Replace(t, m => AuctionLexicon.IdiomMap.TryGetValue(m.Value, out var repl) ? repl : m.Value);

            // 5. Phonetic Alphabet Resolution (handles homophones like SEM -> CEM, Mew-Zom, Muzinho, etc.)
            t = _phoneticMatcher.RepairTokens(t);

            // 6. Contracted numerals & sandhi
            t = SandhiNumeralsRegex.Replace(t, "$1 e $2");
            t = HundredsAndRegex.Replace(t, "$1os e $2");
            t = SeteRegex.Replace(t, "sete");

            // 7. Arithmetic operators and numbers
            t = MultOperators.Replace(t, " * ");
            t = AddOperators.Replace(t, " + ");
            t = DecimalCommas.Replace(t, "$1.$2");

            // 8. Clean punctuation
            t = StripSymbols.Replace(t, " ");
            t = IsolatedDots.Replace(t, " ");
            t = FillerUmaVez.Replace(t, " ");

            // 9. Sândi nasal and colloquial PB augmentatives
            for (int i = 0; i < SandhiAugmentatives.Length; i++)
            {
                t = SandhiAugmentatives[i].pattern.Replace(t, SandhiAugmentatives[i].repl);
            }

            // 10. Colloquial PB plural erasure
            for (int i = 0; i < PluralErasure.Length; i++)
            {
                t = PluralErasure[i].pattern.Replace(t, PluralErasure[i].repl);
            }

            return t;
        }
    }
}
