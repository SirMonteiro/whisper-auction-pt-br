using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using AuctionEngine.Audio;

#if UNITY_INFERENCE_ENGINE || UNITY_SENTIS
using Unity.InferenceEngine;
#endif

namespace AuctionEngine.Inference
{
    /// <summary>
    /// Production implementation of ITranscriptionService powered by Unity AI Inference Engine (formerly Unity Sentis).
    /// Executes speech recognition ONNX neural models directly on GPU or CPU workers,
    /// integrating log-mel spectrogram extraction, autoregressive decoding, and audio silence gating.
    /// </summary>
    public sealed class InferenceEngineTranscriptionService : ITranscriptionService, IDisposable
    {
#if UNITY_INFERENCE_ENGINE || UNITY_SENTIS
        private readonly ModelAsset _modelAsset;
        private readonly ModelAsset _decoderModelAsset;
        private readonly BackendType _backendType;

        private Worker _worker;
        private Worker _decoderWorker;
#endif

        private readonly string _defaultLanguage;
        private readonly string _initialPrompt;
        private readonly float _minRmsEnergy;
        private readonly float _minDurationSec;
        private readonly ILogMelSpectrogramExtractor _spectrogramExtractor;
        private readonly ITokenizer _tokenizer;
        private bool _isDisposed;

#if UNITY_INFERENCE_ENGINE || UNITY_SENTIS
        public InferenceEngineTranscriptionService(
            ModelAsset modelAsset,
            ModelAsset decoderModelAsset = null,
            BackendType backendType = BackendType.GPUCompute,
            string defaultLanguage = "pt",
            string initialPrompt = InferenceEnginePromptBias.DefaultPortuguesePrompt,
            float minRmsEnergy = AudioPreprocessor.DefaultMinRmsEnergy,
            float minDurationSec = AudioPreprocessor.DefaultMinDurationSec,
            ILogMelSpectrogramExtractor spectrogramExtractor = null,
            ITokenizer tokenizer = null)
        {
            _modelAsset = modelAsset;
            _decoderModelAsset = decoderModelAsset;
            _backendType = backendType;
            _defaultLanguage = defaultLanguage ?? "pt";
            _initialPrompt = initialPrompt ?? InferenceEnginePromptBias.DefaultPortuguesePrompt;
            _minRmsEnergy = minRmsEnergy;
            _minDurationSec = minDurationSec;
            _spectrogramExtractor = spectrogramExtractor ?? new LogMelSpectrogramExtractor();
            _tokenizer = tokenizer ?? new WhisperTokenizer();

            InitializeWorkers();
        }

        private void InitializeWorkers()
        {
            if (_modelAsset != null)
            {
                var runtimeModel = ModelLoader.Load(_modelAsset);
                _worker = new Worker(runtimeModel, _backendType);
            }

            if (_decoderModelAsset != null)
            {
                var decoderRuntimeModel = ModelLoader.Load(_decoderModelAsset);
                _decoderWorker = new Worker(decoderRuntimeModel, _backendType);
            }
        }
#else
        public InferenceEngineTranscriptionService(
            object modelAsset = null,
            object decoderModelAsset = null,
            int backendType = 0,
            string defaultLanguage = "pt",
            string initialPrompt = InferenceEnginePromptBias.DefaultPortuguesePrompt,
            float minRmsEnergy = AudioPreprocessor.DefaultMinRmsEnergy,
            float minDurationSec = AudioPreprocessor.DefaultMinDurationSec,
            ILogMelSpectrogramExtractor spectrogramExtractor = null,
            ITokenizer tokenizer = null)
        {
            _defaultLanguage = defaultLanguage ?? "pt";
            _initialPrompt = initialPrompt ?? InferenceEnginePromptBias.DefaultPortuguesePrompt;
            _minRmsEnergy = minRmsEnergy;
            _minDurationSec = minDurationSec;
            _spectrogramExtractor = spectrogramExtractor ?? new LogMelSpectrogramExtractor();
            _tokenizer = tokenizer ?? new WhisperTokenizer();
        }
#endif

        public async Task<string> TranscribeAsync(
            float[] samples,
            int frequency,
            int channels,
            string language = null,
            CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(InferenceEngineTranscriptionService));

            if (samples == null || samples.Length == 0)
                return string.Empty;

            // 1. Audio silence & duration gating
            if (!AudioPreprocessor.IsValidSpeechAudio(samples, frequency, channels, _minDurationSec, _minRmsEnergy))
            {
                return string.Empty;
            }

            // 2. Downmix multi-channel to mono
            float[] monoSamples = channels > 1
                ? AudioPreprocessor.DownmixToMono(samples, channels)
                : samples;

            // 3. Resample to standard 16,000 Hz if necessary
            if (frequency != AudioPreprocessor.TargetSampleRate && frequency > 0)
            {
                monoSamples = AudioPreprocessor.Resample(monoSamples, frequency, AudioPreprocessor.TargetSampleRate);
            }

            string targetLang = !string.IsNullOrEmpty(language) ? language : _defaultLanguage;

#if UNITY_INFERENCE_ENGINE || UNITY_SENTIS
            if (_worker == null)
            {
                Debug.LogWarning("[InferenceEngineTranscriptionService] Worker is not initialized. Ensure ModelAsset is assigned.");
                return string.Empty;
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Compute log-mel spectrogram features: shape [1, 80, 3000]
                const int targetFrames = 3000;
                float[] melFlat = _spectrogramExtractor.ExtractMelFlattened(monoSamples, targetFrames);

                using Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 80, targetFrames), melFlat);

                // Check if running 2-stage encoder-decoder or single end-to-end model
                if (_decoderWorker != null)
                {
                    return await RunEncoderDecoderInferenceAsync(inputTensor, targetLang, cancellationToken);
                }
                else
                {
                    return RunSingleModelInference(inputTensor);
                }
            }
            catch (OperationCanceledException)
            {
                return string.Empty;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InferenceEngineTranscriptionService] Neural inference error: {ex.Message}");
                return string.Empty;
            }
#else
            Debug.LogWarning("[InferenceEngineTranscriptionService] Unity Inference Engine package (Unity.InferenceEngine) is not installed.");
            await Task.Yield();
            return string.Empty;
#endif
        }

#if UNITY_INFERENCE_ENGINE || UNITY_SENTIS
        private async Task<string> RunEncoderDecoderInferenceAsync(
            Tensor<float> inputTensor,
            string targetLanguage,
            CancellationToken ct)
        {
            // 1. Run Audio Encoder
            _worker.Schedule(inputTensor);
            using Tensor<float> audioFeatures = _worker.PeekOutput() as Tensor<float>;
            if (audioFeatures == null)
                return string.Empty;

            // 2. Autoregressive Greedy Decoder Loop
            int[] initialTokens = _tokenizer.GetInitialTokens(targetLanguage, withTimestamps: false);
            var tokenSequence = new List<int>(initialTokens);

            const int maxDecodeTokens = 64;

            for (int step = 0; step < maxDecodeTokens; step++)
            {
                ct.ThrowIfCancellationRequested();

                using Tensor<int> tokensTensor = new Tensor<int>(new TensorShape(1, tokenSequence.Count), tokenSequence.ToArray());

                _decoderWorker.SetInput("audio_features", audioFeatures);
                _decoderWorker.SetInput("tokens", tokensTensor);
                _decoderWorker.Schedule();

                using Tensor<float> logitsTensor = _decoderWorker.PeekOutput() as Tensor<float>;
                if (logitsTensor == null)
                    break;

                int nextToken = ArgMaxLastToken(logitsTensor);

                if (nextToken == _tokenizer.EndOfTextToken)
                    break;

                tokenSequence.Add(nextToken);

                // Periodically yield control if needed for long decoding
                if (step % 16 == 0)
                {
                    await Task.Yield();
                }
            }

            return _tokenizer.Decode(tokenSequence);
        }

        private string RunSingleModelInference(Tensor<float> inputTensor)
        {
            _worker.Schedule(inputTensor);
            using Tensor<float> outputTensor = _worker.PeekOutput() as Tensor<float>;
            if (outputTensor == null)
                return string.Empty;

            float[] results = outputTensor.DownloadToArray();
            if (results == null || results.Length == 0)
                return string.Empty;

            // If output is a token probability distribution or argmax sequence:
            var tokens = new List<int>();
            int vocabSize = outputTensor.shape.rank >= 2 ? outputTensor.shape[outputTensor.shape.rank - 1] : results.Length;
            int numSteps = results.Length / vocabSize;

            for (int t = 0; t < numSteps; t++)
            {
                int offset = t * vocabSize;
                int bestToken = 0;
                float maxVal = float.MinValue;

                for (int v = 0; v < vocabSize; v++)
                {
                    float val = results[offset + v];
                    if (val > maxVal)
                    {
                        maxVal = val;
                        bestToken = v;
                    }
                }

                if (bestToken == _tokenizer.EndOfTextToken)
                    break;

                tokens.Add(bestToken);
            }

            return _tokenizer.Decode(tokens);
        }

        private static int ArgMaxLastToken(Tensor<float> logitsTensor)
        {
            float[] logits = logitsTensor.DownloadToArray();
            int vocabSize = logitsTensor.shape[logitsTensor.shape.rank - 1];
            if (vocabSize <= 0 || logits.Length == 0)
                return 0;

            int lastTokenOffset = logits.Length - vocabSize;
            int bestToken = 0;
            float maxLogit = float.MinValue;

            for (int i = 0; i < vocabSize; i++)
            {
                float val = logits[lastTokenOffset + i];
                if (val > maxLogit)
                {
                    maxLogit = val;
                    bestToken = i;
                }
            }

            return bestToken;
        }
#endif

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

#if UNITY_INFERENCE_ENGINE || UNITY_SENTIS
            _worker?.Dispose();
            _worker = null;

            _decoderWorker?.Dispose();
            _decoderWorker = null;
#endif
        }
    }
}
