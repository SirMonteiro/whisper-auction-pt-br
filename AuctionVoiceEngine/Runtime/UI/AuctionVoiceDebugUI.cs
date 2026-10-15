using UnityEngine;
using UnityEngine.UI;
using AuctionEngine.Controllers;
using AuctionEngine.Core.Models;

namespace AuctionEngine.UI
{
    /// <summary>
    /// Sample gameplay UI component demonstrating how to consume AuctionVoiceController events.
    /// Compatible with standard UGUI and easily adapted to TextMeshPro.
    /// </summary>
    public sealed class AuctionVoiceDebugUI : MonoBehaviour
    {
        [Header("Controller Reference")]
        [SerializeField] private AuctionVoiceController _controller;

        [Header("UI Elements (Optional)")]
        [SerializeField] private Text _statusText;
        [SerializeField] private Text _transcriptText;
        [SerializeField] private Text _bidResultText;
        [SerializeField] private Button _pushToTalkButton;
        [SerializeField] private InputField _testInputField;
        [SerializeField] private Button _testSubmitButton;

        private void Start()
        {
            if (_controller == null)
            {
                _controller = FindObjectOfType<AuctionVoiceController>();
            }

            if (_controller != null)
            {
                _controller.Events.OnRecordingStateChanged.AddListener(HandleRecordingStateChanged);
                _controller.Events.OnTranscriptionStarted.AddListener(HandleTranscriptionStarted);
                _controller.Events.OnTranscriptionCompleted.AddListener(HandleTranscriptionCompleted);
                _controller.Events.OnBidEvaluated.AddListener(HandleBidEvaluated);
                _controller.Events.OnError.AddListener(HandleError);
            }

            if (_pushToTalkButton != null)
            {
                // Can be hooked via EventTrigger for PointerDown / PointerUp, or simple click toggle
                _pushToTalkButton.onClick.AddListener(ToggleRecording);
            }

            if (_testSubmitButton != null && _testInputField != null)
            {
                _testSubmitButton.onClick.AddListener(SubmitTextInput);
            }

            UpdateStatus("Pronto (Idle)");
        }

        private void OnDestroy()
        {
            if (_controller != null)
            {
                _controller.Events.OnRecordingStateChanged.RemoveListener(HandleRecordingStateChanged);
                _controller.Events.OnTranscriptionStarted.RemoveListener(HandleTranscriptionStarted);
                _controller.Events.OnTranscriptionCompleted.RemoveListener(HandleTranscriptionCompleted);
                _controller.Events.OnBidEvaluated.RemoveListener(HandleBidEvaluated);
                _controller.Events.OnError.RemoveListener(HandleError);
            }
        }

        public void StartRecording()
        {
            if (_controller != null) _controller.StartListening();
        }

        public void StopRecording()
        {
            if (_controller != null) _controller.StopListening();
        }

        public void ToggleRecording()
        {
            if (_controller != null) _controller.ToggleListening();
        }

        public void SubmitTextInput()
        {
            if (_testInputField != null && !string.IsNullOrWhiteSpace(_testInputField.text))
            {
                if (_controller != null)
                {
                    _controller.ProcessUtterance(_testInputField.text);
                }
            }
        }

        private void HandleRecordingStateChanged(bool isRecording)
        {
            UpdateStatus(isRecording ? "🔴 Gravando áudio..." : "Parou de gravar");
        }

        private void HandleTranscriptionStarted()
        {
            UpdateStatus("⏳ Transcrevendo com AI Inference Engine...");
        }

        private void HandleTranscriptionCompleted(string transcript)
        {
            if (_transcriptText != null)
            {
                _transcriptText.text = $"Transcrição: \"{transcript}\"";
            }
            UpdateStatus("Processando lance...");
        }

        private void HandleBidEvaluated(BidResult result)
        {
            if (result == null) return;

            string display = $"Ação: {result.ActionType}\n" +
                             $"Valor: {result.Currency}\n" +
                             $"Adição: {result.HasAddition} | Multiplicação: {result.HasMultiplication}\n" +
                             $"Válido: {result.IsValid}";

            if (_bidResultText != null)
            {
                _bidResultText.text = display;
            }

            UpdateStatus("✓ Lance avaliado");
            Debug.Log($"[AuctionVoiceDebugUI] {result}");
        }

        private void HandleError(string error)
        {
            UpdateStatus($"Erro: {error}");
            Debug.LogError($"[AuctionVoiceDebugUI] {error}");
        }

        private void UpdateStatus(string status)
        {
            if (_statusText != null)
            {
                _statusText.text = status;
            }
        }
    }
}
