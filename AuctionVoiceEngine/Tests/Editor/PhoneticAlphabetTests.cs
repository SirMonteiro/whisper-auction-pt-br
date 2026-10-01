using NUnit.Framework;
using AuctionEngine.Core.Phonetics;

namespace AuctionEngine.Tests
{
    [TestFixture]
    public sealed class PhoneticAlphabetTests
    {
        private BrazilianPortuguesePhoneticEncoder _encoder;
        private PhoneticMatcher _matcher;

        [SetUp]
        public void SetUp()
        {
            _encoder = new BrazilianPortuguesePhoneticEncoder();
            _matcher = new PhoneticMatcher(_encoder);
        }

        [Test]
        public void TestHomophonesEncodeIdentically()
        {
            // CEM and SEM produce identical phonetic representation
            string codeCem = _encoder.Encode("cem");
            string codeSem = _encoder.Encode("sem");
            Assert.AreEqual(codeCem, codeSem);
        }

        [Test]
        public void TestLevenshteinDistanceIdentical()
        {
            int dist = LevenshteinDistance.Calculate("SESSENTA", "SESSENTA");
            Assert.AreEqual(0, dist);
            Assert.AreEqual(1.0, LevenshteinDistance.Similarity("SESSENTA", "SESSENTA"));
        }

        [Test]
        public void TestLevenshteinDistanceDifferent()
        {
            int dist = LevenshteinDistance.Calculate("VINTE", "VINTO");
            Assert.AreEqual(1, dist);
            double sim = LevenshteinDistance.Similarity("VINTE", "VINTO");
            Assert.AreEqual(0.8, sim, 1e-4);
        }

        [Test]
        public void TestMatcherRepairsHomophoneSemToCem()
        {
            string repaired = _matcher.RepairTokens("sem");
            Assert.AreEqual("cem", repaired);
        }

        [Test]
        public void TestMatcherRepairsMewZomToMilzao()
        {
            string repaired = _matcher.RepairTokens("mew zom");
            Assert.AreEqual("milzao", repaired);
        }

        [Test]
        public void TestMatcherRepairsMuzinhoToMilzinho()
        {
            string repaired = _matcher.RepairTokens("muzinho");
            Assert.AreEqual("milzinho", repaired);
        }

        [Test]
        public void TestProtectedWordsAreNotMutated()
        {
            Assert.AreEqual("passo", _matcher.RepairTokens("passo"));
            Assert.AreEqual("to fora", _matcher.RepairTokens("to fora"));
            Assert.AreEqual("sim", _matcher.RepairTokens("sim"));
            Assert.AreEqual("nao", _matcher.RepairTokens("nao"));
            Assert.AreEqual("mais", _matcher.RepairTokens("mais"));
        }
    }
}
