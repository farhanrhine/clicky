// Core/CompanionManager.cs
// Central state machine for Clicky. Receives hotkey events and drives the
// full voice pipeline. Phase 02 wires up just the hotkey; other pipeline
// components are added in later phases.

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ClickyWindows;

internal class CompanionManager
{
    private readonly GlobalHotkeyMonitor _hotkeyMonitor = new();
    private readonly PushToTalkManager _pushToTalkManager = new();
    private readonly ITranscriptionProvider _transcriptionProvider
        = new AssemblyAIStreamingProvider();
    private ITranscriptionSession? _activeTranscriptionSession;
    private readonly List<ClaudeConversationMessage> _conversationHistory = new();
    private CancellationTokenSource? _activeClaudeCancellationToken;

    public void Start()
    {
        _hotkeyMonitor.OnShortcutTransition += HandleShortcutTransition;
        _hotkeyMonitor.Start();
        System.Diagnostics.Debug.WriteLine("Clicky: CompanionManager started");
    }

    public void Stop()
    {
        _hotkeyMonitor.Dispose();
        _pushToTalkManager.Dispose();
        _activeTranscriptionSession?.Dispose();
    }

    private async void HandleShortcutTransition(PushToTalkTransition transition)
    {
        switch (transition)
        {
            case PushToTalkTransition.Pressed:
                System.Diagnostics.Debug.WriteLine("Clicky: PTT pressed");
                
                // Cancel any existing session
                _activeTranscriptionSession?.Cancel();
                _activeTranscriptionSession?.Dispose();

                try
                {
                    _activeTranscriptionSession = await _transcriptionProvider.StartSessionAsync(
                        keyterms: BuildKeyterms(),
                        onTranscriptUpdate: text => System.Diagnostics.Debug.WriteLine($"Transcript: {text}"),
                        onFinalTranscriptReady: OnFinalTranscriptReady,
                        onError: ex => System.Diagnostics.Debug.WriteLine($"ASR error: {ex.Message}"));

                    _pushToTalkManager.StartCapture(chunk =>
                        _activeTranscriptionSession?.AppendPCM16AudioChunk(chunk));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Clicky: Failed to start transcription session: {ex.Message}");
                }
                break;

            case PushToTalkTransition.Released:
                System.Diagnostics.Debug.WriteLine("Clicky: PTT released");
                _pushToTalkManager.StopCapture();
                _activeTranscriptionSession?.RequestFinalTranscript();
                break;
        }
    }

    private async void OnFinalTranscriptReady(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript)) return;

        // Phase 07 will provide real screenshots
        IReadOnlyList<ClaudeImageAttachment>? screenshots = null;

        var fullResponseBuilder = new StringBuilder();

        // Cancel any existing Claude request
        _activeClaudeCancellationToken?.Cancel();
        _activeClaudeCancellationToken?.Dispose();
        _activeClaudeCancellationToken = new CancellationTokenSource();

        try
        {
            await foreach (var textChunk in ClaudeApiClient.StreamResponseAsync(
                transcript, _conversationHistory, screenshots, 
                cancellationToken: _activeClaudeCancellationToken.Token))
            {
                fullResponseBuilder.Append(textChunk);
                // Phase 09+ will stream text to overlay UI here
            }

            var fullResponse = fullResponseBuilder.ToString();

            // Store in history
            _conversationHistory.Add(new ClaudeConversationMessage(
                ClaudeMessageRole.User, transcript));
            _conversationHistory.Add(new ClaudeConversationMessage(
                ClaudeMessageRole.Assistant, fullResponse));

            var pointTag = PointTagParser.ExtractFirstTag(fullResponse);
            var displayText = PointTagParser.StripAllTags(fullResponse);

            Debug.WriteLine($"Claude response: {displayText}");
            if (pointTag != null)
                Debug.WriteLine($"Point tag: {pointTag}");

            // Phase 06+ will play TTS here
            // Phase 09+ will display text in overlay here
            // Phase 10+ will animate cursor to point tag here
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("Claude request cancelled");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Claude API error: {ex.Message}");
        }
    }

    private static IReadOnlyList<string> BuildKeyterms() => new[]
    {
        "Clicky", "Claude", "Anthropic", "Windows", "OpenAI"
    };

    private void OnAudioChunkCaptured(byte[] pcm16AudioChunk)
    {
        // Handled directly in HandleShortcutTransition via lambda
    }
}
