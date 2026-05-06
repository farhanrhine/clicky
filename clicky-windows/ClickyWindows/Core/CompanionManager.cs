// Core/CompanionManager.cs
// Central state machine for Clicky. Receives hotkey events and drives the
// full voice pipeline. Phase 02 wires up just the hotkey; other pipeline
// components are added in later phases.

namespace ClickyWindows;

internal class CompanionManager
{
    private readonly GlobalHotkeyMonitor _hotkeyMonitor = new();
    private readonly PushToTalkManager _pushToTalkManager = new();

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
    }

    private void HandleShortcutTransition(PushToTalkTransition transition)
    {
        switch (transition)
        {
            case PushToTalkTransition.Pressed:
                System.Diagnostics.Debug.WriteLine("Clicky: PTT pressed");
                _pushToTalkManager.StartCapture(OnAudioChunkCaptured);
                break;
            case PushToTalkTransition.Released:
                System.Diagnostics.Debug.WriteLine("Clicky: PTT released");
                _pushToTalkManager.StopCapture();
                // Phase 04+ will finalize transcript here
                break;
        }
    }

    private void OnAudioChunkCaptured(byte[] pcm16AudioChunk)
    {
        // Phase 04+ will forward these chunks to AssemblyAI WebSocket
        System.Diagnostics.Debug.WriteLine(
            $"Clicky: audio chunk {pcm16AudioChunk.Length} bytes");
    }
}
