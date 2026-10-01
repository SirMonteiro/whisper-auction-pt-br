using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AuctionEngine.Core.Phonetics
{
    /// <summary>
    /// Converts Portuguese orthographic text and Whisper acoustic outputs into a standardized
    /// phonetic alphabet representation based on Brazilian Portuguese (PB) phonology.
    /// Thread-safe and allocation-conscious with pre-compiled regexes.
    /// </summary>
    public sealed class BrazilianPortuguesePhoneticEncoder : IPhoneticEncoder
    {
        private const RegexOptions RegOpts = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        // 1. Nasal diphthongs and specific endings
        private static readonly (Regex pattern, string repl)[] NasalDiphthongs = new[]
        {
            (new Regex(@"(?:ão|ao|om)\b", RegOpts), "ANW"),
            (new Regex(@"(?:õe|oe)\b", RegOpts), "ONJ"),
            (new Regex(@"(?:ãe|ae)\b", RegOpts), "ANJ"),
            (new Regex(@"ã", RegOpts), "AN"),
            (new Regex(@"õ", RegOpts), "ON")
        };

        // 2. Consonant homophones
        private static readonly (Regex pattern, string repl)[] ConsonantHomophones = new[]
        {
            (new Regex(@"ç|c(?=[ei])", RegOpts), "s"),
            (new Regex(@"(?:gar|on)ca\b", RegOpts), "sa"),
            (new Regex(@"ss", RegOpts), "s"),
            (new Regex(@"sc|sç|xc", RegOpts), "s"),
            (new Regex(@"qu(?=[ei])", RegOpts), "k"),
            (new Regex(@"qu(?=[ao])", RegOpts), "kw"),
            (new Regex(@"c(?=[aou])", RegOpts), "k"),
            (new Regex(@"q", RegOpts), "k"),
            (new Regex(@"g(?=[ei])", RegOpts), "j")
        };

        // 3. Coda L vocalization in Brazilian Portuguese (e.g. mil -> miw, alto -> awto)
        private static readonly Regex CodaL = new Regex(@"l(?=[bcdfghjklmnpqrstvwxyz\s]|$)", RegOpts);

        // 4. Whisper English-Portuguese acoustic bleed (e.g. "mew" for "miw")
        private static readonly Regex WhisperEw = new Regex(@"ew", RegOpts);

        // 5. Vowels followed by nasal consonant in syllable coda
        private static readonly (Regex pattern, string repl)[] NasalVowels = new[]
        {
            (new Regex(@"a[mn](?=[bcdfghjklmnpqrstvwxyz\s]|$)", RegOpts), "AN"),
            (new Regex(@"e[mn](?=[bcdfghjklmnpqrstvwxyz\s]|$)", RegOpts), "EN"),
            (new Regex(@"i[mn](?=[bcdfghjklmnpqrstvwxyz\s]|$)", RegOpts), "IN"),
            (new Regex(@"o[mn](?=[bcdfghjklmnpqrstvwxyz\s]|$)", RegOpts), "ON"),
            (new Regex(@"u[mn](?=[bcdfghjklmnpqrstvwxyz\s]|$)", RegOpts), "UN")
        };

        // 7. Digraphs
        private static readonly (Regex pattern, string repl)[] Digraphs = new[]
        {
            (new Regex(@"ch", RegOpts), "x"),
            (new Regex(@"nh", RegOpts), "ny"),
            (new Regex(@"lh", RegOpts), "ly"),
            (new Regex(@"rr", RegOpts), "r"),
            (new Regex(@"r\b", RegOpts), "")
        };

        // 8. Dental stops affricate/lenite before front vowels [i, e] in PB (ti/te -> T, di/de -> T)
        private static readonly (Regex pattern, string repl)[] DentalAffricates = new[]
        {
            (new Regex(@"t(?=[ie])", RegOpts), "T"),
            (new Regex(@"d(?=[ie])", RegOpts), "T")
        };

        // 9. Unstressed final vowels and colloquial glide reductions
        private static readonly (Regex pattern, string repl)[] UnstressedFinals = new[]
        {
            (new Regex(@"(?:eo|io|e)\b", RegOpts), "i"),
            (new Regex(@"o\b", RegOpts), "u"),
            (new Regex(@"[sz]\b", RegOpts), "")
        };

        private static readonly Regex CollapseDuplicates = new Regex(@"([A-Za-z])\1+", RegOpts);
        private static readonly Regex NonAlphanumeric = new Regex(@"[^A-Za-z0-9]", RegOpts);

        /// <summary>
        /// Encodes a word or phrase into the canonical Brazilian Portuguese Phonetic Alphabet.
        /// </summary>
        public string Encode(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            string t = text.ToLowerInvariant().Trim().Replace("-", " ");

            // 1. Nasal diphthongs and specific endings
            for (int i = 0; i < NasalDiphthongs.Length; i++)
            {
                t = NasalDiphthongs[i].pattern.Replace(t, NasalDiphthongs[i].repl);
            }

            // 2. Consonant homophones
            for (int i = 0; i < ConsonantHomophones.Length; i++)
            {
                t = ConsonantHomophones[i].pattern.Replace(t, ConsonantHomophones[i].repl);
            }

            // 3. Coda L vocalization
            t = CodaL.Replace(t, "w");

            // 4. Whisper acoustic shifts
            t = WhisperEw.Replace(t, "iw");

            // 5. Syllable coda nasals
            for (int i = 0; i < NasalVowels.Length; i++)
            {
                t = NasalVowels[i].pattern.Replace(t, NasalVowels[i].repl);
            }

            // 6. Strip non-nasal diacritics
            string nfd = t.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(nfd.Length);
            for (int i = 0; i < nfd.Length; i++)
            {
                char c = nfd[i];
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }
            t = sb.ToString();

            // 7. Digraphs
            for (int i = 0; i < Digraphs.Length; i++)
            {
                t = Digraphs[i].pattern.Replace(t, Digraphs[i].repl);
            }

            // 8. Dental coronal stops before front vowels
            for (int i = 0; i < DentalAffricates.Length; i++)
            {
                t = DentalAffricates[i].pattern.Replace(t, DentalAffricates[i].repl);
            }

            // 9. Unstressed final vowels and coda s/z
            for (int i = 0; i < UnstressedFinals.Length; i++)
            {
                t = UnstressedFinals[i].pattern.Replace(t, UnstressedFinals[i].repl);
            }

            // 10. Clean up and collapse duplicate consecutive consonants
            t = CollapseDuplicates.Replace(t, "$1");
            t = NonAlphanumeric.Replace(t, string.Empty);

            return t.ToUpperInvariant();
        }
    }
}
