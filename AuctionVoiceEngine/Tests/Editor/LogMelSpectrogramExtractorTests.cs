using NUnit.Framework;
using AuctionEngine.Inference;

namespace AuctionEngine.Tests
{
    [TestFixture]
    public sealed class LogMelSpectrogramExtractorTests
    {
        private LogMelSpectrogramExtractor _extractor;

        [SetUp]
        public void SetUp()
        {
            _extractor = new LogMelSpectrogramExtractor();
        }

        [Test]
        public void TestExtractorDefaultProperties()
        {
            Assert.AreEqual(80, _extractor.MelBins);
            Assert.AreEqual(16000, _extractor.SampleRate);
            Assert.AreEqual(400, _extractor.FftSize);
            Assert.AreEqual(160, _extractor.HopSize);
        }

        [Test]
        public void TestExtractMelSpectrogram_TooShortAudioReturnsEmptyFrames()
        {
            float[] shortAudio = new float[200]; // Less than fftSize 400
            float[,] mel = _extractor.ExtractMelSpectrogram(shortAudio);

            Assert.AreEqual(80, mel.GetLength(0));
            Assert.AreEqual(0, mel.GetLength(1));
        }

        [Test]
        public void TestExtractMelSpectrogram_SineWaveGeneratesValidFrames()
        {
            // 0.1 second of 440Hz sine wave at 16000Hz = 1600 samples
            int numSamples = 1600;
            float[] audio = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                audio[i] = (float)System.Math.Sin(2.0 * System.Math.PI * 440.0 * i / 16000.0);
            }

            float[,] mel = _extractor.ExtractMelSpectrogram(audio);

            // Number of frames = (1600 - 400) / 160 + 1 = 8 + 1 = 9 frames
            Assert.AreEqual(80, mel.GetLength(0));
            Assert.AreEqual(9, mel.GetLength(1));

            // Verify no NaN or Infinities
            for (int m = 0; m < 80; m++)
            {
                for (int t = 0; t < 9; t++)
                {
                    float val = mel[m, t];
                    Assert.IsFalse(float.IsNaN(val), $"Mel value at [{m},{t}] is NaN");
                    Assert.IsFalse(float.IsInfinity(val), $"Mel value at [{m},{t}] is Infinity");
                }
            }
        }

        [Test]
        public void TestExtractMelFlattened_HasExactTargetFrames()
        {
            float[] audio = new float[1600];
            int targetFrames = 100;

            float[] flat = _extractor.ExtractMelFlattened(audio, targetFrames);

            Assert.AreEqual(80 * targetFrames, flat.Length);

            for (int i = 0; i < flat.Length; i++)
            {
                Assert.IsFalse(float.IsNaN(flat[i]));
                Assert.IsFalse(float.IsInfinity(flat[i]));
            }
        }
    }
}
