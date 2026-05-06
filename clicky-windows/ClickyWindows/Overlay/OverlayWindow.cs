// Overlay/OverlayWindow.cs
// Full-screen transparent click-through window spanning all monitors.
// Hosts the blue cursor, response text bubble, waveform, and spinner.
//
// Win32 window style notes:
//   WS_EX_TRANSPARENT — hit-testing passes through to windows below.
//     This makes the overlay invisible to mouse events.
//   WS_EX_LAYERED     — required for full alpha transparency.
//   WS_EX_TOPMOST     — always rendered above all other windows.
//   WS_EX_NOACTIVATE  — prevents the overlay from stealing keyboard focus.
//
// One OverlayWindow per monitor is created and positioned to cover that monitor.
// The cursor monitor's overlay is the "primary" one that shows the text bubble.

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace ClickyWindows;

internal class OverlayWindow
{
    private Window? _window;
    private IntPtr _hwnd;
    private OverlayCanvas? _canvas;

    // Extended window style flags
    private const int WS_EX_TRANSPARENT   = 0x00000020;
    private const int WS_EX_LAYERED       = 0x00080000;
    private const int WS_EX_TOPMOST       = 0x00000008; // set via SetWindowPos
    private const int WS_EX_NOACTIVATE    = 0x08000000;
    private const int GWL_EXSTYLE         = -20;
    private const int HWND_TOPMOST        = -1;
    private const uint SWP_NOMOVE         = 0x0002;
    private const uint SWP_NOSIZE         = 0x0001;
    private const uint SWP_NOACTIVATE     = 0x0010;

    // Current blue cursor position in screen coordinates
    private System.Drawing.Point _cursorPosition;
    private CompanionVoiceState _voiceState = CompanionVoiceState.Idle;
    private string _responseText = "";
    private double _audioPowerLevel = 0;

    public void CreateForMonitor(MonitorBounds monitor)
    {
        _window = new Window();
        _hwnd = WindowNative.GetWindowHandle(_window);

        // Apply click-through + transparent + no-activate styles
        int exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE;
        SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle);

        // Make always-on-top
        SetWindowPos(_hwnd, new IntPtr(HWND_TOPMOST), 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

        // Remove titlebar
        var appWindow = AppWindow.GetFromWindowId(
            Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd));
        appWindow.TitleBar.ExtendsContentIntoTitleBar = true;

        // Size and position to cover the entire monitor
        appWindow.MoveAndResize(new Windows.Graphics.RectInt32(
            monitor.X, monitor.Y, monitor.Width, monitor.Height));

        // Install the canvas (WinUI 2D rendering surface)
        _canvas = new OverlayCanvas();
        _window.Content = _canvas;

        _window.Activate();
    }

    /// <summary>
    /// Called by CompanionManager when the voice state changes.
    /// Drives which visual is shown in the overlay.
    /// </summary>
    public void UpdateVoiceState(CompanionVoiceState state)
    {
        _voiceState = state;
        _canvas?.Refresh(state, _responseText, _audioPowerLevel, _cursorPosition);
    }

    /// <summary>
    /// Called with the cursor's current screen position so the blue
    /// cursor dot follows the mouse.
    /// </summary>
    public void UpdateCursorPosition(System.Drawing.Point screenPosition)
    {
        _cursorPosition = screenPosition;
        _canvas?.UpdateCursorPosition(screenPosition);
    }

    /// <summary>Called with each streaming response text chunk.</summary>
    public void AppendResponseText(string textChunk)
    {
        _responseText += textChunk;
        _canvas?.UpdateResponseText(_responseText);
    }

    public void ClearResponseText()
    {
        _responseText = "";
        _canvas?.UpdateResponseText("");
    }

    /// <summary>Called with audio power level during listening (0.0–1.0).</summary>
    public void UpdateAudioPowerLevel(double level)
    {
        _audioPowerLevel = level;
        _canvas?.UpdateWaveform(level);
    }

    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")] static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}

internal record MonitorBounds(int X, int Y, int Width, int Height);
