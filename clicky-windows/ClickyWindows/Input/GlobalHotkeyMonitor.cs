// Input/GlobalHotkeyMonitor.cs
// Detects system-wide Ctrl+Alt press and release using two Win32 mechanisms:
//   1. RegisterHotKey — fires a WM_HOTKEY message when both keys are down.
//      This is the "press" event.
//   2. Low-level keyboard hook (WH_KEYBOARD_LL) — fires on every key event
//      system-wide. Used to detect when Ctrl or Alt is released, ending the
//      push-to-talk session.
//
// The hook runs on a dedicated background thread with its own message loop
// so it doesn't block the UI thread.

using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace ClickyWindows;

internal class GlobalHotkeyMonitor : IDisposable
{
    // Published to CompanionManager when the shortcut state changes.
    public event Action<PushToTalkTransition>? OnShortcutTransition;

    private const int HOTKEY_ID = 9001;
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_HOTKEY = 0x0312;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYUP = 0x0105;

    private Thread? _messageLoopThread;
    private IntPtr _hookHandle = IntPtr.Zero;
    private bool _isShortcutCurrentlyPressed = false;
    private bool _isDisposed = false;

    // Keep a reference to the delegate to prevent GC collection
    private LowLevelKeyboardProc? _hookCallback;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc fn, IntPtr mod, uint threadId);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string? lpModuleName);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode, scanCode, flags, time;
        public IntPtr dwExtraInfo;
    }

    public void Start()
    {
        _messageLoopThread = new Thread(RunMessageLoop)
        {
            IsBackground = true,
            Name = "ClickyHotkeyThread"
        };
        _messageLoopThread.Start();
    }

    private void RunMessageLoop()
    {
        // Install the keyboard hook on this thread
        _hookCallback = OnLowLevelKeyboardEvent;
        _hookHandle = SetWindowsHookEx(
            WH_KEYBOARD_LL,
            _hookCallback,
            GetModuleHandle(null),
            0
        );

        // Create a message-only window to receive WM_HOTKEY
        // (simplified: use thread message loop with PeekMessage)
        RegisterHotKey(IntPtr.Zero, HOTKEY_ID,
            PushToTalkShortcut.Win32ModifierFlags,
            PushToTalkShortcut.TriggerVirtualKey);

        // Standard Win32 message loop
        while (GetMessage(out var msg, IntPtr.Zero, 0, 0))
        {
            if (msg.message == WM_HOTKEY && msg.wParam.ToInt32() == HOTKEY_ID)
            {
                HandleHotkeyPressed();
            }
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }

    private IntPtr OnLowLevelKeyboardEvent(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _isShortcutCurrentlyPressed)
        {
            if (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)
            {
                var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                bool isCtrlOrAltRelease =
                    hookStruct.vkCode == PushToTalkShortcut.VK_LCONTROL ||
                    hookStruct.vkCode == PushToTalkShortcut.VK_RCONTROL ||
                    hookStruct.vkCode == PushToTalkShortcut.VK_LMENU    ||
                    hookStruct.vkCode == PushToTalkShortcut.VK_RMENU;

                if (isCtrlOrAltRelease)
                {
                    HandleHotkeyReleased();
                }
            }
        }
        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private void HandleHotkeyPressed()
    {
        if (_isShortcutCurrentlyPressed) return;
        _isShortcutCurrentlyPressed = true;
        OnShortcutTransition?.Invoke(PushToTalkTransition.Pressed);
    }

    private void HandleHotkeyReleased()
    {
        if (!_isShortcutCurrentlyPressed) return;
        _isShortcutCurrentlyPressed = false;
        OnShortcutTransition?.Invoke(PushToTalkTransition.Released);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        UnregisterHotKey(IntPtr.Zero, HOTKEY_ID);
        if (_hookHandle != IntPtr.Zero)
            UnhookWindowsHookEx(_hookHandle);
        
        if (_messageLoopThread != null && _messageLoopThread.IsAlive)
        {
            PostThreadMessage(
                (uint)_messageLoopThread.ManagedThreadId,
                0x0012, // WM_QUIT
                IntPtr.Zero, IntPtr.Zero);
        }
    }

    [DllImport("user32.dll")] static extern bool GetMessage(out MSG msg, IntPtr hWnd, uint min, uint max);
    [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref MSG msg);
    [DllImport("user32.dll")] static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wp, IntPtr lp);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam, lParam;
        public uint time;
        public System.Drawing.Point pt;
    }
}
