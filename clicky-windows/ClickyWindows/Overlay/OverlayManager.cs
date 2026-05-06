// Overlay/OverlayManager.cs
// Manages multiple OverlayWindow instances (one per monitor).
// Routes state updates from CompanionManager to all overlays.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ClickyWindows;

internal class OverlayManager
{
    private readonly List<OverlayWindow> _windows = new();

    public void Initialize()
    {
        // Create one overlay window per monitor
        foreach (var monitor in EnumerateMonitors())
        {
            var overlay = new OverlayWindow();
            overlay.CreateForMonitor(monitor);
            _windows.Add(overlay);
        }
    }

    public void UpdateVoiceState(CompanionVoiceState state)
        => _windows.ForEach(w => w.UpdateVoiceState(state));

    public void UpdateCursorPosition(System.Drawing.Point pos)
        => _windows.ForEach(w => w.UpdateCursorPosition(pos));

    public void StartCursorFlight(System.Drawing.PointF start, System.Drawing.PointF target, string label)
        => _windows.ForEach(w => w.StartCursorFlight(start, target, label));

    public void AppendResponseText(string chunk)
        => _windows.ForEach(w => w.AppendResponseText(chunk));

    public void ClearResponseText()
        => _windows.ForEach(w => w.ClearResponseText());

    public void UpdateAudioPowerLevel(double level)
        => _windows.ForEach(w => w.UpdateAudioPowerLevel(level));

    // Re-use Win32 enumeration logic from ScreenCaptureUtility
    private static IEnumerable<MonitorBounds> EnumerateMonitors()
    {
        var monitors = new List<MonitorBounds>();

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(hMonitor, ref info))
            {
                monitors.Add(new MonitorBounds(
                    X: info.rcMonitor.left,
                    Y: info.rcMonitor.top,
                    Width: info.rcMonitor.right - info.rcMonitor.left,
                    Height: info.rcMonitor.bottom - info.rcMonitor.top
                ));
            }
            return true;
        }, IntPtr.Zero);

        return monitors;
    }

    [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);
    [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor, rcWork;
        public uint dwFlags;
    }
}
