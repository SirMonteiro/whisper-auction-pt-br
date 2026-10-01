using System;
using UnityEngine.Events;
using AuctionEngine.Core.Models;

namespace AuctionEngine.Controllers
{
    [Serializable]
    public sealed class BidEvaluatedEvent : UnityEvent<BidResult> { }

    [Serializable]
    public sealed class TranscriptionStartedEvent : UnityEvent { }

    [Serializable]
    public sealed class TranscriptionCompletedEvent : UnityEvent<string> { }

    [Serializable]
    public sealed class AuctionErrorEvent : UnityEvent<string> { }

    [Serializable]
    public sealed class RecordingStateEvent : UnityEvent<bool> { }

    /// <summary>
    /// Bundles UnityEvents for easy hookup in the Unity Inspector.
    /// </summary>
    [Serializable]
    public sealed class AuctionVoiceEvents
    {
        public BidEvaluatedEvent OnBidEvaluated = new BidEvaluatedEvent();
        public TranscriptionStartedEvent OnTranscriptionStarted = new TranscriptionStartedEvent();
        public TranscriptionCompletedEvent OnTranscriptionCompleted = new TranscriptionCompletedEvent();
        public AuctionErrorEvent OnError = new AuctionErrorEvent();
        public RecordingStateEvent OnRecordingStateChanged = new RecordingStateEvent();
    }
}
