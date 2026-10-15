using System;
using NUnit.Framework;
using AuctionEngine.Audio;

namespace AuctionEngine.Tests
{
    [TestFixture]
    public sealed class AudioPreprocessorTests
    {
        [Test]
        public void TestDownmixToMono_StereoToMono()
        {
            // Stereo buffer with L=0.2, R=0.4
            float[] stereo = new float[] { 0.2f, 0.4f, 0.6f, 0.8f };
            float[] mono = AudioPreprocessor.DownmixToMono(stereo, 2);

            Assert.AreEqual(2, mono.Length);
            Assert.AreEqual(0.3f, mono[0], 1e-5f);
            Assert.AreEqual(0.7f, mono[1], 1e-5f);
        }

        [Test]
        public void TestDownmixToMono_AlreadyMonoReturnsOriginal()
        {
            float[] mono = new float[] { 0.1f, 0.5f, 0.9f };
            float[] result = AudioPreprocessor.DownmixToMono(mono, 1);

            Assert.AreSame(mono, result);
        }

        [Test]
        public void TestDownmixToMono_NullOrEmptyReturnsEmpty()
        {
            Assert.AreEqual(0, AudioPreprocessor.DownmixToMono(null, 1).Length);
            Assert.AreEqual(0, AudioPreprocessor.DownmixToMono(Array.Empty<float>(), 2).Length);
        }

        [Test]
        public void TestCalculateRmsEnergy_Silence()
        {
            float[] silence = new float[100];
            float rms = AudioPreprocessor.CalculateRmsEnergy(silence);
            Assert.AreEqual(0f, rms);
        }

        [Test]
        public void TestCalculateRmsEnergy_ConstantSignal()
        {
            float[] signal = new float[] { 0.5f, -0.5f, 0.5f, -0.5f };
            float rms = AudioPreprocessor.CalculateRmsEnergy(signal);
            Assert.AreEqual(0.5f, rms, 1e-5f);
        }

        [Test]
        public void TestIsValidSpeechAudio_Gating()
        {
            // 0.1 second of audio at 16000Hz = 1600 samples (< minDuration 0.25s)
            float[] shortAudio = new float[1600];
            for (int i = 0; i < shortAudio.Length; i++) shortAudio[i] = 0.1f;

            Assert.IsFalse(AudioPreprocessor.IsValidSpeechAudio(shortAudio, 16000, 1, minDurationSec: 0.25f));

            // 0.5 second of silence (< minRms 0.003)
            float[] silentAudio = new float[8000];
            Assert.IsFalse(AudioPreprocessor.IsValidSpeechAudio(silentAudio, 16000, 1, minRmsEnergy: 0.003f));

            // 0.5 second of loud audio (> 0.003 RMS, >= 0.25s)
            float[] speechAudio = new float[8000];
            for (int i = 0; i < speechAudio.Length; i++) speechAudio[i] = 0.05f;
            Assert.IsTrue(AudioPreprocessor.IsValidSpeechAudio(speechAudio, 16000, 1));
        }

        [Test]
        public void TestResample_SameRateReturnsSameBuffer()
        {
            float[] samples = new float[] { 0.1f, 0.2f, 0.3f };
            float[] result = AudioPreprocessor.Resample(samples, 16000, 16000);
            Assert.AreSame(samples, result);
        }

        [Test]
        public void TestResample_DownsampleHalfRate()
        {
            // Resampling 32000Hz to 16000Hz (downsampling 2:1)
            float[] samples = new float[] { 0.0f, 0.5f, 1.0f, 0.5f };
            float[] resampled = AudioPreprocessor.Resample(samples, 32000, 16000);

            Assert.AreEqual(2, resampled.Length);
            Assert.AreEqual(0.0f, resampled[0], 1e-4f);
            Assert.AreEqual(1.0f, resampled[1], 1e-4f);
        }

        [Test]
        public void TestNormalize_ScalesToTargetPeak()
        {
            float[] samples = new float[] { 0.1f, -0.5f, 0.2f };
            float[] normalized = AudioPreprocessor.Normalize(samples, 0.95f);

            Assert.AreEqual(0.95f, Math.Abs(normalized[1]), 1e-5f);
            Assert.AreEqual(0.19f, normalized[0], 1e-5f);
        }

        [Test]
        public void TestPadOrTrim()
        {
            float[] shortBuf = new float[] { 1f, 2f, 3f };
            float[] padded = AudioPreprocessor.PadOrTrim(shortBuf, 5);
            Assert.AreEqual(5, padded.Length);
            Assert.AreEqual(1f, padded[0]);
            Assert.AreEqual(3f, padded[2]);
            Assert.AreEqual(0f, padded[3]);
            Assert.AreEqual(0f, padded[4]);

            float[] trimmed = AudioPreprocessor.PadOrTrim(shortBuf, 2);
            Assert.AreEqual(2, trimmed.Length);
            Assert.AreEqual(1f, trimmed[0]);
            Assert.AreEqual(2f, trimmed[1]);
        }
    }
}
