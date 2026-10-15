using System.Collections.Generic;
using NUnit.Framework;
using AuctionEngine.Inference;

namespace AuctionEngine.Tests
{
    [TestFixture]
    public sealed class WhisperTokenizerTests
    {
        private WhisperTokenizer _tokenizer;

        [SetUp]
        public void SetUp()
        {
            _tokenizer = new WhisperTokenizer();
        }

        [Test]
        public void TestSpecialTokenConstants()
        {
            Assert.AreEqual(50257, _tokenizer.EndOfTextToken);
            Assert.AreEqual(50258, _tokenizer.StartOfTranscriptToken);
            Assert.AreEqual(50359, _tokenizer.TranscribeToken);
            Assert.AreEqual(50358, _tokenizer.TranslateToken);
            Assert.AreEqual(50363, _tokenizer.NoTimestampsToken);
        }

        [Test]
        public void TestGetLanguageToken()
        {
            Assert.AreEqual(50275, _tokenizer.GetLanguageToken("pt"));
            Assert.AreEqual(50259, _tokenizer.GetLanguageToken("en"));
            Assert.AreEqual(50262, _tokenizer.GetLanguageToken("es"));
            // Unknown language falls back to Portuguese
            Assert.AreEqual(50275, _tokenizer.GetLanguageToken("unknown_xyz"));
        }

        [Test]
        public void TestGetInitialTokens_PortugueseNoTimestamps()
        {
            int[] tokens = _tokenizer.GetInitialTokens("pt", withTimestamps: false);

            Assert.AreEqual(4, tokens.Length);
            Assert.AreEqual(50258, tokens[0]); // SOT
            Assert.AreEqual(50275, tokens[1]); // PT
            Assert.AreEqual(50359, tokens[2]); // Transcribe
            Assert.AreEqual(50363, tokens[3]); // NoTimestamps
        }

        [Test]
        public void TestGetInitialTokens_WithTimestamps()
        {
            int[] tokens = _tokenizer.GetInitialTokens("pt", withTimestamps: true);

            Assert.AreEqual(3, tokens.Length);
            Assert.AreEqual(50258, tokens[0]);
            Assert.AreEqual(50275, tokens[1]);
            Assert.AreEqual(50359, tokens[2]);
        }

        [Test]
        public void TestDecode_AsciiCharacters()
        {
            // ASCII codes for 'c', 'e', 'm' are 99, 101, 109
            var tokens = new List<int> { 99, 101, 109 };
            string text = _tokenizer.Decode(tokens);
            Assert.AreEqual("cem", text);
        }

        [Test]
        public void TestDecode_StripsSpecialTokens()
        {
            // Tokens with SOT and EOT wrapping 'd', 'e', 'z' (100, 101, 122)
            var tokens = new List<int>
            {
                _tokenizer.StartOfTranscriptToken,
                _tokenizer.GetLanguageToken("pt"),
                100, 101, 122,
                _tokenizer.EndOfTextToken
            };

            string text = _tokenizer.Decode(tokens);
            Assert.AreEqual("dez", text);
        }

        [Test]
        public void TestLoadCustomVocabulary_Json()
        {
            string json = "{\"lance\": 2000, \"leilao\": 2001}";
            _tokenizer.LoadVocabulary(json);

            var tokens = new List<int> { 2000, 32, 2001 }; // 32 is space
            string decoded = _tokenizer.Decode(tokens);
            Assert.AreEqual("lance leilao", decoded);
        }
    }
}
