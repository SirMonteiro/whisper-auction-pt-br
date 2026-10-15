using System;

namespace AuctionEngine.Whisper
{
    /// <summary>
    /// Legacy prompt biasing text.
    /// Redirects to <see cref="AuctionEngine.Inference.InferenceEnginePromptBias"/>.
    /// </summary>
    [Obsolete("Use AuctionEngine.Inference.InferenceEnginePromptBias instead.")]
    public static class WhisperPromptBias
    {
        public const string DefaultPortuguesePrompt = AuctionEngine.Inference.InferenceEnginePromptBias.DefaultPortuguesePrompt;
    }
}
