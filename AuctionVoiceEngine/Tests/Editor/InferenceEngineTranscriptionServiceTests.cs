using System;
using System.Threading.Tasks;
using NUnit.Framework;
using AuctionEngine.Inference;

namespace AuctionEngine.Tests
{
    [TestFixture]
    public sealed class InferenceEngineTranscriptionServiceTests
    {
        private InferenceEngineTranscriptionService _service;

        [SetUp]
        public void SetUp()
        {
            _service = new InferenceEngineTranscriptionService();
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
        }

        [Test]
        public async Task TestTranscribeAsync_NullOrEmptyAudioReturnsEmptyString()
        {
            string resNull = await _service.TranscribeAsync(null, 16000, 1);
            Assert.AreEqual(string.Empty, resNull);

            string resEmpty = await _service.TranscribeAsync(Array.Empty<float>(), 16000, 1);
            Assert.AreEqual(string.Empty, resEmpty);
        }

        [Test]
        public async Task TestTranscribeAsync_SilentAudioReturnsEmptyStringWithoutInference()
        {
            // 1 second of silence at 16000Hz = 16000 zero samples
            float[] silentAudio = new float[16000];
            string res = await _service.TranscribeAsync(silentAudio, 16000, 1);
            Assert.AreEqual(string.Empty, res);
        }

        [Test]
        public async Task TestTranscribeAsync_ShortAudioReturnsEmptyString()
        {
            // 0.1 second of audio at 16000Hz = 1600 samples (< minDuration 0.25s)
            float[] shortAudio = new float[1600];
            for (int i = 0; i < shortAudio.Length; i++) shortAudio[i] = 0.5f;

            string res = await _service.TranscribeAsync(shortAudio, 16000, 1);
            Assert.AreEqual(string.Empty, res);
        }

        [Test]
        public void TestDispose_Idempotent()
        {
            _service.Dispose();
            // Second dispose should not throw
            Assert.DoesNotThrow(() => _service.Dispose());
        }

        [Test]
        public void TestTranscribeAsync_AfterDisposeThrowsObjectDisposedException()
        {
            _service.Dispose();
            Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            {
                await _service.TranscribeAsync(new float[16000], 16000, 1);
            });
        }
    }
}
