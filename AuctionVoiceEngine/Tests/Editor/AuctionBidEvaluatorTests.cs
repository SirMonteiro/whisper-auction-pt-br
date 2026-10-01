using NUnit.Framework;
using AuctionEngine.Core.Models;
using AuctionEngine.Core.Services;

namespace AuctionEngine.Tests
{
    [TestFixture]
    public sealed class AuctionBidEvaluatorTests
    {
        private AuctionBidEvaluator _evaluator;

        [SetUp]
        public void SetUp()
        {
            _evaluator = new AuctionBidEvaluator();
        }

        #region User Reported Issues & Arithmetic Connectors

        [Test]
        public void TestSingleNumberWithMais_HasAdditionTrue()
        {
            var res = _evaluator.Evaluate("Mais dois");
            Assert.AreEqual(2.0, res.Value);
            Assert.IsTrue(res.HasAddition);
            Assert.IsFalse(res.HasMultiplication);
            Assert.AreEqual(AuctionActionType.Bid, res.ActionType);
        }

        [Test]
        public void TestDoisMaisDois_HasAdditionFalse()
        {
            var res = _evaluator.Evaluate("Dois mais dois.");
            Assert.AreEqual(4.0, res.Value);
            Assert.IsFalse(res.HasAddition); // Two or more numbers summed together -> Add is False
            Assert.AreEqual(AuctionActionType.Bid, res.ActionType);
        }

        [Test]
        public void TestExplicitAdditionFiftyPlusFifty_HasAdditionFalse()
        {
            var res = _evaluator.Evaluate("50 mais 50.");
            Assert.AreEqual(100.0, res.Value);
            Assert.IsFalse(res.HasAddition);
        }

        [Test]
        public void TestMultipleMaisChain()
        {
            var res = _evaluator.Evaluate("Vinte mais, vinte mais vinte, mais sete.");
            Assert.AreEqual(67.0, res.Value);
            Assert.IsFalse(res.HasAddition);
        }

        [Test]
        public void TestChantRepetitionWithoutPlus_PicksLastNumber()
        {
            var res = _evaluator.Evaluate("50, 40, 20.");
            Assert.AreEqual(20.0, res.Value);
            Assert.IsFalse(res.HasAddition);
        }

        [Test]
        public void TestUngrammaticalTensPlusTens_PicksLastNumber()
        {
            var res = _evaluator.Evaluate("Trinta e dez.");
            Assert.AreEqual(10.0, res.Value);
            Assert.IsFalse(res.HasAddition);
        }

        [Test]
        public void TestVinteSeteRepetition_PicksLast()
        {
            var res = _evaluator.Evaluate("Vinte-sete, vinte-se, vint-sate");
            Assert.AreEqual(27.0, res.Value);
            Assert.IsFalse(res.HasAddition);
        }

        [Test]
        public void TestCatorzeOitentaEOito_PicksLast()
        {
            var res = _evaluator.Evaluate("Catorze oitenta e oito");
            Assert.AreEqual(88.0, res.Value);
        }

        [Test]
        public void TestNovemMilNovecentosNoventenove()
        {
            var res = _evaluator.Evaluate("Novem mil novecentos e noventenove.");
            Assert.AreEqual(9999.0, res.Value);
        }

        [Test]
        public void TestMasMilNovecentosNoventaNove()
        {
            var res = _evaluator.Evaluate("Mas mil novecentos e noventa e nove.");
            Assert.AreEqual(1999.0, res.Value);
            Assert.IsTrue(res.HasAddition); // Single number with "Mas" (="Mais") -> Add is True
        }

        [Test]
        public void TestNoveMilNovecentosNoventenoi()
        {
            var res = _evaluator.Evaluate("Nove mil novecentos e noventenói.");
            Assert.AreEqual(9999.0, res.Value);
        }

        [Test]
        public void TestNoventaENoveMilNovecentosNoventaEmNove()
        {
            var res = _evaluator.Evaluate("Noventa e nove mil novecentos e noventa em nove.");
            Assert.AreEqual(99999.0, res.Value);
        }

        #endregion

        #region Multipliers & Verbal Modifiers

        [Test]
        public void TestDobroMultiplierOnly()
        {
            var res = _evaluator.Evaluate("Dobro");
            Assert.AreEqual(2.0, res.Value);
            Assert.IsTrue(res.HasMultiplication);
            Assert.AreEqual(AuctionActionType.MultiplyOnly, res.ActionType);
        }

        [Test]
        public void TestTriplicaMultiplierOnly()
        {
            var res = _evaluator.Evaluate("Triplica");
            Assert.AreEqual(3.0, res.Value);
            Assert.IsTrue(res.HasMultiplication);
            Assert.AreEqual(AuctionActionType.MultiplyOnly, res.ActionType);
        }

        [Test]
        public void TestQuadreplicoMultiplierOnly()
        {
            var res = _evaluator.Evaluate("Quadreplico");
            Assert.AreEqual(4.0, res.Value);
            Assert.IsTrue(res.HasMultiplication);
            Assert.AreEqual(AuctionActionType.MultiplyOnly, res.ActionType);
        }

        [Test]
        public void TestDobroWithNumber()
        {
            var res = _evaluator.Evaluate("Dobro de cinquenta");
            Assert.AreEqual(100.0, res.Value);
            Assert.IsTrue(res.HasMultiplication);
            Assert.AreEqual(AuctionActionType.Bid, res.ActionType);
        }

        #endregion

        #region Dialects & Phonetics

        [Test]
        public void TestSixentaAsSessenta()
        {
            var res = _evaluator.Evaluate("Sixenta");
            Assert.AreEqual(60.0, res.Value);
        }

        [Test]
        public void TestSixSevenEnglishNumeral()
        {
            var res = _evaluator.Evaluate("Six-seven");
            Assert.AreEqual(67.0, res.Value);
        }

        [Test]
        public void TestMewZomWhisperError()
        {
            var res = _evaluator.Evaluate("Mew-Zom.");
            Assert.AreEqual(1000.0, res.Value);
        }

        [Test]
        public void TestSemHomophoneAsCem()
        {
            var res = _evaluator.Evaluate("Sem");
            Assert.AreEqual(100.0, res.Value);
        }

        [Test]
        public void TestMuzinhoAsMilzinho()
        {
            var res = _evaluator.Evaluate("Muzinho");
            Assert.AreEqual(1000.0, res.Value);
        }

        #endregion

        #region Fauna & Bingo Idioms

        [Test]
        public void TestFaunaNotes()
        {
            Assert.AreEqual(2.0, _evaluator.Evaluate("Tartaruga").Value);
            Assert.AreEqual(10.0, _evaluator.Evaluate("Duas garças").Value);
            Assert.AreEqual(50.0, _evaluator.Evaluate("Uma onça").Value);
            Assert.AreEqual(100.0, _evaluator.Evaluate("Um peixe").Value);
            Assert.AreEqual(200.0, _evaluator.Evaluate("Lobo guará").Value);
        }

        [Test]
        public void TestPatinhosVariants()
        {
            Assert.AreEqual(22.0, _evaluator.Evaluate("Dois patinhos na lagoa").Value);
            Assert.AreEqual(22.0, _evaluator.Evaluate("Dois patinhos da lago").Value);
            Assert.AreEqual(22.0, _evaluator.Evaluate("Dois patinhos da lagoas").Value);
            Assert.AreEqual(3.0, _evaluator.Evaluate("Três patinhos").Value);
        }

        [Test]
        public void TestDuziaVariants()
        {
            Assert.AreEqual(6.0, _evaluator.Evaluate("Meia dúzi").Value);
            Assert.AreEqual(6.0, _evaluator.Evaluate("Meia dúzias").Value);
            Assert.AreEqual(12.0, _evaluator.Evaluate("Uma dúzia").Value);
        }

        [Test]
        public void TestBingoTerms()
        {
            Assert.AreEqual(33.0, _evaluator.Evaluate("Idade de cristo").Value);
            Assert.AreEqual(38.0, _evaluator.Evaluate("Justiça de Goiás").Value);
            Assert.AreEqual(11.0, _evaluator.Evaluate("Pernas da xuxa").Value);
        }

        #endregion

        #region Slang Multipliers & Currency

        [Test]
        public void TestPauAndBarao()
        {
            Assert.AreEqual(1000.0, _evaluator.Evaluate("Um pau").Value);
            Assert.AreEqual(2000.0, _evaluator.Evaluate("Dois paus").Value);
            Assert.AreEqual(500.0, _evaluator.Evaluate("Meio pau").Value);
            Assert.AreEqual(5000.0, _evaluator.Evaluate("Cinco barões").Value);
        }

        [Test]
        public void TestCurrencyFormatting()
        {
            var res = _evaluator.Evaluate("Cem reais");
            Assert.AreEqual(100.0, res.Value);
            Assert.AreEqual("R$ 100,00", res.Currency);

            var resMil = _evaluator.Evaluate("Mil reais");
            Assert.AreEqual(1000.0, resMil.Value);
            Assert.AreEqual("R$ 1.000,00", resMil.Currency);
        }

        [Test]
        public void TestPassPhrases()
        {
            var resPasso = _evaluator.Evaluate("Passo");
            Assert.AreEqual(AuctionActionType.Pass, resPasso.ActionType);
            Assert.AreEqual(0.0, resPasso.Value);

            var resToFora = _evaluator.Evaluate("Tô fora!");
            Assert.AreEqual(AuctionActionType.Pass, resToFora.ActionType);
        }

        #endregion
    }
}
