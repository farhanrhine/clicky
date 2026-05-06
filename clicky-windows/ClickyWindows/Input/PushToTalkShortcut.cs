// Input/PushToTalkShortcut.cs
// Defines the push-to-talk shortcut (Ctrl+Alt, matching macOS Ctrl+Option)
// and the transition states used to drive the voice pipeline.

namespace ClickyWindows;

/// <summary>
/// The state transition published when the push-to-talk shortcut is
/// pressed or released. Mirrors BuddyPushToTalkShortcut.ShortcutTransition on macOS.
/// </summary>
internal enum PushToTalkTransition
{
    None,
    Pressed,
    Released
}

internal static class PushToTalkShortcut
{
    /// <summary>
    /// Human-readable display text shown in the panel UI.
    /// </summary>
    public const string DisplayText = "Ctrl + Alt";

    /// <summary>
    /// Win32 modifier flags for RegisterHotKey.
    /// MOD_CONTROL = 0x0002, MOD_ALT = 0x0001.
    /// </summary>
    public const uint Win32ModifierFlags = 0x0002 | 0x0001; // MOD_CONTROL | MOD_ALT

    /// <summary>
    /// Virtual key code for the modifier-only shortcut.
    /// 0 means "no extra key — modifiers alone trigger it."
    /// Because RegisterHotKey requires a VK, we use VK_MENU (Alt = 0x12)
    /// as the trigger key and handle the Ctrl check in the hook.
    /// </summary>
    public const uint TriggerVirtualKey = 0x12; // VK_MENU (Alt key)

    /// <summary>
    /// Virtual key code for Left Ctrl (used in the hook to validate
    /// that both Ctrl and Alt are held simultaneously).
    /// </summary>
    public const int VK_LCONTROL = 0xA2;
    public const int VK_RCONTROL = 0xA3;
    public const int VK_LMENU   = 0xA4; // Left Alt
    public const int VK_RMENU   = 0xA5; // Right Alt
}
