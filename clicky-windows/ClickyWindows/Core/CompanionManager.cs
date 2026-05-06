// Core/CompanionManager.cs
// Central state machine for Clicky. Receives hotkey events and drives the
// full voice pipeline. Phase 02 wires up just the hotkey; other pipeline
// components are added in later phases.

namespace ClickyWindows;

internal class CompanionManager
{
    private readonly GlobalHotkeyMonitor _hotkeyMonitor = new();

    public void Start()
    {
        _hotkeyMonitor.OnShortcutTransition += HandleShortcutTransition;
        _hotkeyMonitor.Start();
        System.Diagnostics.Debug.WriteLine("Clicky: CompanionManager started");
    }

    public void Stop()
    {
        _hotkeyMonitor.Dispose();
    }

    private void HandleShortcutTransition(PushToTalkTransition transition)
    {
        switch (transition)
        {
            case PushToTalkTransition.Pressed:
                System.Diagnostics.Debug.WriteLine("Clicky: PTT pressed");
                // Phase 03+ will start microphone capture here
                break;
            case PushToTalkTransition.Released:
                System.Diagnostics.Debug.WriteLine("Clicky: PTT released");
                // Phase 03+ will stop mic and finalize transcript here
                break;
        }
    }
}
