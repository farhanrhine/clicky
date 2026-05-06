// Core/CompanionManager.cs
// Central state machine for Clicky. Receives hotkey events and drives the
// full voice pipeline. Phase 02 wires up just the hotkey; other pipeline
// components are added in later phases.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ClickyWindows;

internal class CompanionManager
{
    private readonly GlobalHotkeyMonitor _hotkeyMonitor = new();
    private readonly PushToTalkManager _pushToTalkManager = new();
    private readonly ITranscriptionProvider _transcriptionProvider
        = new AssemblyAIStreamingProvider();
    private ITranscriptionSession? _activeTranscriptionSession;

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

    private void OnFinalTranscriptReady(string transcript)
    {
        System.Diagnostics.Debug.WriteLine($"Final transcript: {transcript}");
        // Phase 05+ sends this to Claude
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
