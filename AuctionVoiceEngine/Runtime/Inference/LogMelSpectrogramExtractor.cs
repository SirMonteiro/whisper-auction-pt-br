using System;

namespace AuctionEngine.Inference
{
    /// <summary>
    /// Computes log-mel spectrogram features from 16kHz mono audio waveforms.
    /// Implements standard STFT (Short-Time Fourier Transform) with Hann windowing,
    /// 80-channel triangular mel filterbank, and Whisper log-scale dynamic range normalization.
    /// Thread-safe and allocation-conscious with precomputed filterbanks and window tables.
    /// </summary>
    public sealed class LogMelSpectrogramExtractor : ILogMelSpectrogramExtractor
    {
        public const int DefaultSampleRate = 16000;
        public const int DefaultFftSize = 400;
        public const int DefaultHopSize = 160;
        public const int DefaultMelBins = 80;

        private readonly int _sampleRate;
        private readonly int _fftSize;
        private readonly int _hopSize;
        private readonly int _melBins;
        private readonly int _fftBins;

        private readonly float[] _hannWindow;
        private readonly float[][] _melFilterbank; // [melBins][fftBins]

        // Precomputed cos/sin tables for 400-point DFT
        private readonly float[,] _dftCos;
        private readonly float[,] _dftSin;

        public int MelBins => _melBins;
        public int SampleRate => _sampleRate;
        public int FftSize => _fftSize;
        public int HopSize => _hopSize;

        public LogMelSpectrogramExtractor(
            int sampleRate = DefaultSampleRate,
            int fftSize = DefaultFftSize,
            int hopSize = DefaultHopSize,
            int melBins = DefaultMelBins)
        {
            _sampleRate = sampleRate;
            _fftSize = fftSize;
            _hopSize = hopSize;
            _melBins = melBins;
            _fftBins = (fftSize / 2) + 1; // 201 bins for 400 FFT

            _hannWindow = PrecomputeHannWindow(_fftSize);
            _melFilterbank = PrecomputeMelFilterbank(_sampleRate, _fftSize, _fftBins, _melBins);

            _dftCos = new float[_fftBins, _fftSize];
            _dftSin = new float[_fftBins, _fftSize];
            PrecomputeDftTables();
        }

        private static float[] PrecomputeHannWindow(int size)
        {
            float[] window = new float[size];
            double factor = 2.0 * Math.PI / size;
            for (int i = 0; i < size; i++)
            {
                window[i] = (float)(0.5 * (1.0 - Math.Cos(i * factor)));
            }
            return window;
        }

        private void PrecomputeDftTables()
        {
            for (int k = 0; k < _fftBins; k++)
            {
                double angleFactor = 2.0 * Math.PI * k / _fftSize;
                for (int n = 0; n < _fftSize; n++)
                {
                    double angle = angleFactor * n;
                    _dftCos[k, n] = (float)Math.Cos(angle);
                    _dftSin[k, n] = (float)Math.Sin(angle);
                }
            }
        }

        private static float[][] PrecomputeMelFilterbank(int sampleRate, int fftSize, int fftBins, int melBins)
        {
            float[][] filterbank = new float[melBins][];
            for (int i = 0; i < melBins; i++)
            {
                filterbank[i] = new float[fftBins];
            }

            double minMel = HzToMel(0.0);
            double maxMel = HzToMel(sampleRate / 2.0); // 8000 Hz

            // melBins + 2 points define melBins triangular filters
            double[] melPoints = new double[melBins + 2];
            double[] hzPoints = new double[melBins + 2];
            int[] binPoints = new int[melBins + 2];

            for (int i = 0; i < melPoints.Length; i++)
            {
                melPoints[i] = minMel + i * (maxMel - minMel) / (melBins + 1);
                hzPoints[i] = MelToHz(melPoints[i]);
                binPoints[i] = (int)Math.Floor((fftSize + 1) * hzPoints[i] / sampleRate);
                if (binPoints[i] >= fftBins)
                    binPoints[i] = fftBins - 1;
            }

            for (int m = 0; m < melBins; m++)
            {
                int left = binPoints[m];
                int center = binPoints[m + 1];
                int right = binPoints[m + 2];

                double width = (hzPoints[m + 2] - hzPoints[m]) * 0.5;
                float norm = width > 0 ? (float)(2.0 / (hzPoints[m + 2] - hzPoints[m])) : 1f;

                for (int k = left; k < center && k < fftBins; k++)
                {
                    if (center != left)
                    {
                        float weight = (float)(k - left) / (center - left) * norm;
                        filterbank[m][k] = weight;
                    }
                }

                for (int k = center; k <= right && k < fftBins; k++)
                {
                    if (right != center)
                    {
                        float weight = (float)(right - k) / (right - center) * norm;
                        filterbank[m][k] = weight;
                    }
                }
            }

            return filterbank;
        }

        private static double HzToMel(double hz)
        {
            return 2595.0 * Math.Log10(1.0 + hz / 700.0);
        }

        private static double MelToHz(double mel)
        {
            return 700.0 * (Math.Pow(10.0, mel / 2595.0) - 1.0);
        }

        public float[,] ExtractMelSpectrogram(float[] audioSamples)
        {
            if (audioSamples == null || audioSamples.Length < _fftSize)
            {
                return new float[_melBins, 0];
            }

            int numFrames = (audioSamples.Length - _fftSize) / _hopSize + 1;
            if (numFrames <= 0)
            {
                return new float[_melBins, 0];
            }

            float[,] melSpectrogram = new float[_melBins, numFrames];
            float[] powerSpectrum = new float[_fftBins];

            float maxLogVal = float.MinValue;

            for (int t = 0; t < numFrames; t++)
            {
                int sampleOffset = t * _hopSize;

                // 1. Windowed DFT Power Spectrum
                for (int k = 0; k < _fftBins; k++)
                {
                    float real = 0f;
                    float imag = 0f;

                    for (int n = 0; n < _fftSize; n++)
                    {
                        float sample = audioSamples[sampleOffset + n] * _hannWindow[n];
                        real += sample * _dftCos[k, n];
                        imag -= sample * _dftSin[k, n];
                    }

                    powerSpectrum[k] = (real * real) + (imag * imag);
                }

                // 2. Mel Filterbank Dot Product & Log10
                for (int m = 0; m < _melBins; m++)
                {
                    float melEnergy = 0f;
                    float[] filter = _melFilterbank[m];

                    for (int k = 0; k < _fftBins; k++)
                    {
                        melEnergy += powerSpectrum[k] * filter[k];
                    }

                    // Log10 with clamp threshold 1e-5
                    float logVal = (float)Math.Log10(Math.Max(melEnergy, 1e-5f));
                    melSpectrogram[m, t] = logVal;

                    if (logVal > maxLogVal)
                    {
                        maxLogVal = logVal;
                    }
                }
            }

            // 3. Dynamic Range Normalization (Whisper spec: clamp to [max - 8.0, max], scale to (x + 4.0) / 4.0)
            float floorVal = maxLogVal - 8.0f;
            for (int m = 0; m < _melBins; m++)
            {
                for (int t = 0; t < numFrames; t++)
                {
                    float v = Math.Max(melSpectrogram[m, t], floorVal);
                    melSpectrogram[m, t] = (v + 4.0f) / 4.0f;
                }
            }

            return melSpectrogram;
        }

        public float[] ExtractMelFlattened(float[] audioSamples, int targetFrames = 3000)
        {
            if (targetFrames <= 0)
                targetFrames = 3000;

            float[] flat = new float[_melBins * targetFrames];
            float[,] spec = ExtractMelSpectrogram(audioSamples);
            int availableFrames = spec.GetLength(1);

            int copyFrames = Math.Min(availableFrames, targetFrames);

            for (int m = 0; m < _melBins; m++)
            {
                int rowOffset = m * targetFrames;
                for (int t = 0; t < copyFrames; t++)
                {
                    flat[rowOffset + t] = spec[m, t];
                }
                // Whisper silence padding normalized value is ~ -1.0
                for (int t = copyFrames; t < targetFrames; t++)
                {
                    flat[rowOffset + t] = -1.0f;
                }
            }

            return flat;
        }
    }
}
