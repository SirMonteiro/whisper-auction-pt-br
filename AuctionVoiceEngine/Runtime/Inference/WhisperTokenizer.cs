using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace AuctionEngine.Inference
{
    /// <summary>
    /// Tokenizer and text decoder for Whisper neural models running via Unity AI Inference Engine.
    /// Manages special tokens, language prefixes, byte-level BPE decoding, and domain vocabulary.
    /// </summary>
    public sealed class WhisperTokenizer : ITokenizer
    {
        public const int Eot = 50257;            // <|endoftext|>
        public const int Sot = 50258;            // <|startoftranscript|>
        public const int Translate = 50358;      // <|translate|>
        public const int Transcribe = 50359;     // <|transcribe|>
        public const int NoTimestamps = 50363;   // <|notimestamps|>
        public const int FirstTimestamp = 50364; // <|0.00|>

        public int EndOfTextToken => Eot;
        public int StartOfTranscriptToken => Sot;
        public int TranscribeToken => Transcribe;
        public int TranslateToken => Translate;
        public int NoTimestampsToken => NoTimestamps;

        private readonly Dictionary<string, int> _languageTokens = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "en", 50259 },
            { "zh", 50260 },
            { "de", 50261 },
            { "es", 50262 },
            { "ru", 50263 },
            { "ko", 50264 },
            { "fr", 50265 },
            { "ja", 50266 },
            { "pt", 50275 }, // Brazilian Portuguese
            { "it", 50274 }
        };

        private readonly Dictionary<int, string> _vocab = new Dictionary<int, string>();

        public WhisperTokenizer(string vocabJson = null)
        {
            InitializeDefaultVocab();
            if (!string.IsNullOrWhiteSpace(vocabJson))
            {
                LoadVocabulary(vocabJson);
            }
        }

        public int GetLanguageToken(string languageCode)
        {
            if (!string.IsNullOrEmpty(languageCode) && _languageTokens.TryGetValue(languageCode, out int token))
            {
                return token;
            }
            return 50275; // Default Portuguese <|pt|>
        }

        public int[] GetInitialTokens(string languageCode = "pt", bool withTimestamps = false)
        {
            int langToken = GetLanguageToken(languageCode);
            if (withTimestamps)
            {
                return new[] { Sot, langToken, Transcribe };
            }
            return new[] { Sot, langToken, Transcribe, NoTimestamps };
        }

        public string Decode(IReadOnlyList<int> tokens)
        {
            if (tokens == null || tokens.Count == 0)
                return string.Empty;

            var sb = new StringBuilder(tokens.Count * 4);

            for (int i = 0; i < tokens.Count; i++)
            {
                int token = tokens[i];

                // Skip special control tokens (SOT, EOT, language, timestamps)
                if (token >= Eot)
                    continue;

                if (_vocab.TryGetValue(token, out string piece))
                {
                    sb.Append(piece);
                }
                else if (token >= 0 && token < 256)
                {
                    // Fallback single ASCII / byte token
                    sb.Append((char)token);
                }
            }

            string decoded = sb.ToString();

            // Whisper GPT-2 BPE space replacement: 'Ġ' (U+0120) represents leading space
            decoded = decoded.Replace('Ġ', ' ');
            decoded = decoded.Replace('Ċ', '\n');

            return decoded.Trim();
        }

        /// <summary>
        /// Loads custom vocabulary JSON mapping token string pieces to integer IDs, or text file lines.
        /// </summary>
        public void LoadVocabulary(string vocabContent)
        {
            if (string.IsNullOrWhiteSpace(vocabContent))
                return;

            try
            {
                // Simple regex parser for JSON {"word": id} or {"id": "word"} format
                var matchWordToId = Regex.Matches(vocabContent, @"""([^""]+)""\s*:\s*(\d+)");
                if (matchWordToId.Count > 0)
                {
                    foreach (Match m in matchWordToId)
                    {
                        string word = m.Groups[1].Value;
                        if (int.TryParse(m.Groups[2].Value, out int id))
                        {
                            _vocab[id] = word;
                        }
                    }
                    return;
                }

                var matchIdToWord = Regex.Matches(vocabContent, @"(\d+)\s*:\s*""([^""]+)""");
                if (matchIdToWord.Count > 0)
                {
                    foreach (Match m in matchIdToWord)
                    {
                        if (int.TryParse(m.Groups[1].Value, out int id))
                        {
                            _vocab[id] = m.Groups[2].Value;
                        }
                    }
                    return;
                }

                // Fallback: line-by-line format: "token" on each line representing its index
                string[] lines = vocabContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (!string.IsNullOrEmpty(line))
                    {
                        _vocab[i] = line;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WhisperTokenizer] Failed to parse custom vocabulary: {ex.Message}");
            }
        }

        private void InitializeDefaultVocab()
        {
            // Seed common Portuguese characters, punctuation, and auction terminology
            for (char c = ' '; c <= '~'; c++)
            {
                _vocab[(int)c] = c.ToString();
            }

            // Seed common Whisper token subwords for Portuguese numbers and bidding
            var commonWords = new[]
            {
                " ", " e", " mais", " dois", " três", " quatro", " cinco", " seis", " sete", " oito", " nove", " dez",
                " vinte", " trinta", " quarenta", " cinquenta", " sessenta", " setenta", " oitenta", " noventa",
                " cem", " cento", " duzentos", " trezentos", " quatrocentos", " quinhentos", " seiscentos", " setecentos", " oitocentos", " novecentos",
                " mil", " milhão", " milhões", " reais", " real", " conto", " pila", " barão", " barões",
                " dobro", " dobra", " triplica", " triplo", " quadruplica", " passo", " tô fora", " sem lance",
                " tartaruga", " garça", " arara", " mico", " onça", " peixe", " garoupa", " lobo"
            };

            // Seed subwords with surrogate BPE space 'Ġ'
            int baseId = 1000;
            for (int i = 0; i < commonWords.Length; i++)
            {
                string word = commonWords[i];
                string bpeWord = word.StartsWith(" ") ? "Ġ" + word.Substring(1) : word;
                _vocab[baseId + i] = bpeWord;
            }
        }
    }
}
