// Core/CompanionManager.cs
// Central state machine. Owns the full voice pipeline:
//   Idle → Listening (PTT pressed)
//   Listening → Processing (PTT released, transcript ready)
//   Processing → Responding (first Claude token arrives)
//   Responding → Idle (TTS finishes)
//
// Port of CompanionManager.swift.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClickyWindows;

internal class CompanionManager : IDisposable
{
    // ── Components ───────────────────────────────────────────────────────────
    private readonly GlobalHotkeyMonitor _hotkeyMonitor = new();
    private readonly PushToTalkManager _pushToTalkManager = new();
    private readonly TtsAudioPlayer _ttsPlayer = new();
    private readonly ITranscriptionProvider _transcriptionProvider
        = new AssemblyAIStreamingProvider();

    // ── State ────────────────────────────────────────────────────────────────
    private CompanionVoiceState _voiceState = CompanionVoiceState.Idle;
    private ITranscriptionSession? _activeTranscriptionSession;
    private CancellationTokenSource _claudeCts = new();

    // Conversation history — last 10 exchanges (20 messages)
    private readonly List<ClaudeConversationMessage> _conversationHistory = new();
    private const int MaxConversationHistoryMessages = 20;

    // Model selection — persisted across launches
    private string _selectedModel = AppConfiguration.DefaultClaudeModel;

    // ── Public State Change Events (consumed by overlay/panel UI) ───────────
    public event Action<CompanionVoiceState>? OnVoiceStateChanged;
    public event Action<string>? OnTranscriptUpdated;       // live transcript
    public event Action<string>? OnResponseTextChunk;       // streaming response
    public event Action<PointTag?>? OnPointTagDetected;     // cursor pointing
    public event Action? OnResponseCompleted;               // TTS done

    // ── Startup / Shutdown ───────────────────────────────────────────────────

    public void Start()
    {
        _hotkeyMonitor.OnShortcutTransition += HandleShortcutTransition;
        _hotkeyMonitor.Start();

        _ttsPlayer.OnPlaybackCompleted += HandleTtsPlaybackCompleted;

        System.Diagnostics.Debug.WriteLine("Clicky: CompanionManager started");
    }

    public void Stop()
    {
        _hotkeyMonitor.Dispose();
        _pushToTalkManager.Dispose();
        _ttsPlayer.Dispose();
        _claudeCts.Dispose();
    }

    // ── Hotkey Events ────────────────────────────────────────────────────────

    private void HandleShortcutTransition(PushToTalkTransition transition)
    {
        switch (transition)
        {
            case PushToTalkTransition.Pressed:
                HandlePushToTalkPressed();
                break;
            case PushToTalkTransition.Released:
                HandlePushToTalkReleased();
                break;
        }
    }

    private void HandlePushToTalkPressed()
    {
        // Cancel any in-progress Claude response
        _claudeCts.Cancel();
        _claudeCts = new CancellationTokenSource();
        _ttsPlayer.StopPlayback();

        SetVoiceState(CompanionVoiceState.Listening);

        _ = StartListeningSessionAsync();
    }

    private async Task StartListeningSessionAsync()
    {
        try
        {
            _activeTranscriptionSession = await _transcriptionProvider
                .StartSessionAsync(
                    keyterms: BuildKeyterms(),
                    onTranscriptUpdate: text =>
                    {
                        OnTranscriptUpdated?.Invoke(text);
                    },
                    onFinalTranscriptReady: OnFinalTranscriptReady,
                    onError: OnTranscriptionError);

            _pushToTalkManager.StartCapture(
                chunk => _activeTranscriptionSession?.AppendPCM16AudioChunk(chunk));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Clicky: failed to start listening: {ex.Message}");
            SetVoiceState(CompanionVoiceState.Idle);
        }
    }

    private void HandlePushToTalkReleased()
    {
        if (_voiceState != CompanionVoiceState.Listening) return;

        _pushToTalkManager.StopCapture();
        _activeTranscriptionSession?.RequestFinalTranscript();

        SetVoiceState(CompanionVoiceState.Processing);
    }

    // ── Transcript → Claude Pipeline ─────────────────────────────────────────

    private void OnFinalTranscriptReady(string transcript)
    {
        transcript = transcript.Trim();
        if (string.IsNullOrEmpty(transcript))
        {
            SetVoiceState(CompanionVoiceState.Idle);
            return;
        }

        _ = SendTranscriptToClaudeAsync(transcript, _claudeCts.Token);
    }

    private void OnTranscriptionError(Exception error)
    {
        System.Diagnostics.Debug.WriteLine(
            $"Clicky: transcription error: {error.Message}");
        SetVoiceState(CompanionVoiceState.Idle);
    }

    private async Task SendTranscriptToClaudeAsync(
        string transcript, CancellationToken cancellationToken)
    {
        // Capture all screens before calling Claude
        IReadOnlyList<ScreenCaptureResult> screens = Array.Empty<ScreenCaptureResult>();
        try
        {
            screens = await ScreenCaptureUtility.CaptureAllScreensAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Clicky: screen capture failed: {ex.Message}");
        }

        var screenshots = screens
            .Select(s => new ClaudeImageAttachment(
                Base64Data: Convert.ToBase64String(s.JpegBytes),
                MediaType: "image/jpeg"))
            .ToList();

        var responseBuilder = new StringBuilder();
        bool firstChunkReceived = false;

        try
        {
            await foreach (var textChunk in ClaudeApiClient.StreamResponseAsync(
                transcript, _conversationHistory, screenshots,
                _selectedModel, cancellationToken))
            {
                if (!firstChunkReceived)
                {
                    firstChunkReceived = true;
                    SetVoiceState(CompanionVoiceState.Responding);
                }

                responseBuilder.Append(textChunk);
                OnResponseTextChunk?.Invoke(textChunk);
            }
        }
        catch (OperationCanceledException)
        {
            return; // Cancelled by new PTT press
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Clicky: Claude error: {ex.Message}");
            SetVoiceState(CompanionVoiceState.Idle);
            return;
        }

        string fullResponse = responseBuilder.ToString();

        // Store exchange in conversation history
        AddToConversationHistory(
            new ClaudeConversationMessage(ClaudeMessageRole.User, transcript));
        AddToConversationHistory(
            new ClaudeConversationMessage(ClaudeMessageRole.Assistant, fullResponse));

        // Parse pointing tag
        var pointTag = PointTagParser.ExtractFirstTag(fullResponse);
        var displayText = PointTagParser.StripAllTags(fullResponse);

        OnPointTagDetected?.Invoke(pointTag);

        // Speak the response
        _ = Task.Run(async () =>
        {
            try { await _ttsPlayer.SpeakAsync(displayText, cancellationToken); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TTS error: {ex.Message}");
            }
        }, cancellationToken);
    }

    // ── TTS Completion → Idle ────────────────────────────────────────────────

    private void HandleTtsPlaybackCompleted()
    {
        // Only return to idle if we're still in responding state.
        // A new PTT press may have already moved us to Listening.
        if (_voiceState == CompanionVoiceState.Responding)
        {
            SetVoiceState(CompanionVoiceState.Idle);
            OnResponseCompleted?.Invoke();
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void SetVoiceState(CompanionVoiceState newState)
    {
        if (_voiceState == newState) return;
        _voiceState = newState;
        OnVoiceStateChanged?.Invoke(newState);
        System.Diagnostics.Debug.WriteLine($"Clicky: voice state → {newState}");
    }

    private void AddToConversationHistory(ClaudeConversationMessage message)
    {
        _conversationHistory.Add(message);
        // Trim to max window (10 exchanges = 20 messages)
        while (_conversationHistory.Count > MaxConversationHistoryMessages)
            _conversationHistory.RemoveAt(0);
    }

    private static IReadOnlyList<string> BuildKeyterms() => new[]
    {
        "Clicky", "Claude", "Anthropic", "Windows", "OpenAI",
        "Visual Studio", "WinUI", "C#", "Microsoft"
    };

    public void SetSelectedModel(string modelId)
    {
        _selectedModel = modelId;
        // Persist to Windows app storage
        Windows.Storage.ApplicationData.Current.LocalSettings
            .Values["SelectedModel"] = modelId;
    }

    public string SelectedModel => _selectedModel;

    public void Dispose()
    {
        Stop();
        _ttsPlayer.Dispose();
        _pushToTalkManager.Dispose();
        _hotkeyMonitor.Dispose();
        _claudeCts.Dispose();
    }
}
