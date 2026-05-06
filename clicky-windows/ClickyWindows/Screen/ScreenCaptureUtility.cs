// Screen/ScreenCaptureUtility.cs
// Captures all connected displays using Windows.Graphics.Capture.
// Returns JPEG images sorted cursor-screen-first, matching
// CompanionScreenCaptureUtility.swift's multi-monitor approach.
//
// Windows capture flow:
//   1. Enumerate displays via Win32 EnumDisplayMonitors
//   2. For each monitor, create a GraphicsCaptureItem
//   3. Capture one frame using Direct3D11CaptureFramePool
//   4. Convert D3D surface to bitmap, compress to JPEG
//   5. Sort: cursor screen first, then left-to-right

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WinRT;

namespace ClickyWindows;

internal static class ScreenCaptureUtility
{
    private const int MaxOutputDimension = 1280; // px
    private const int JpegQuality = 85; // matches macOS 0.85 compression

    /// <summary>
    /// Captures all connected monitors and returns them sorted cursor-screen-first.
    /// Returns an empty list if capture permission is not granted.
    /// </summary>
    public static async Task<IReadOnlyList<ScreenCaptureResult>> CaptureAllScreensAsync()
    {
        var monitors = EnumerateMonitors();
        var cursorPosition = GetCursorPosition();
        var captureResults = new List<ScreenCaptureResult>();

        foreach (var monitor in monitors)
        {
            var result = await CaptureMonitorAsync(monitor, cursorPosition);
            if (result != null)
                captureResults.Add(result);
        }

        // Sort cursor screen first, then by x-origin (left to right)
        captureResults.Sort((a, b) =>
        {
            if (a.IsCursorScreen && !b.IsCursorScreen) return -1;
            if (!a.IsCursorScreen && b.IsCursorScreen) return 1;
            return a.VirtualDesktopOrigin.X.CompareTo(b.VirtualDesktopOrigin.X);
        });

        return captureResults;
    }

    private static async Task<ScreenCaptureResult?> CaptureMonitorAsync(
        MonitorInfo monitor, System.Drawing.Point cursorPos)
    {
        try
        {
            // Create capture item for the specific monitor
            var captureItem = CreateCaptureItemForMonitor(monitor.Handle);
            if (captureItem == null) return null;

            // Use Direct3D11 to capture one frame
            var device = Direct3D11Helper.CreateDevice();
            using var framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                device,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                numberOfBuffers: 1,
                captureItem.Size);

            using var session = framePool.CreateCaptureSession(captureItem);

            var frameTcs = new TaskCompletionSource<Direct3D11CaptureFrame?>();

            framePool.FrameArrived += (pool, _) =>
            {
                frameTcs.TrySetResult(pool.TryGetNextFrame());
            };

            session.StartCapture();

            // Wait up to 2 seconds for the first frame
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var frame = await frameTcs.Task.WaitAsync(timeout.Token);

            session.Dispose();

            if (frame == null) return null;

            // Convert frame surface to JPEG bytes
            byte[] jpegBytes = await ConvertFrameToJpegAsync(
                frame, device, MaxOutputDimension);

            bool isCursorScreen =
                monitor.Bounds.Contains(cursorPos.X, cursorPos.Y);

            int monitorIndex = GetMonitorIndex(monitor.Handle);

            return new ScreenCaptureResult(
                JpegBytes: jpegBytes,
                Label: $"Screen {monitorIndex + 1}" +
                       (monitor.IsPrimary ? " (primary)" : ""),
                WidthInPixels: monitor.Bounds.Width,
                HeightInPixels: monitor.Bounds.Height,
                VirtualDesktopOrigin: new System.Drawing.Point(
                    monitor.Bounds.Left, monitor.Bounds.Top),
                IsCursorScreen: isCursorScreen
            );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Screen capture failed for monitor: {ex.Message}");
            return null;
        }
    }

    // ── Win32 Monitor Enumeration ────────────────────────────────────────────

    private record MonitorInfo(
        IntPtr Handle,
        System.Drawing.Rectangle Bounds,
        bool IsPrimary);

    private static List<MonitorInfo> EnumerateMonitors()
    {
        var monitors = new List<MonitorInfo>();

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(hMonitor, ref info))
            {
                var bounds = new System.Drawing.Rectangle(
                    info.rcMonitor.left,
                    info.rcMonitor.top,
                    info.rcMonitor.right - info.rcMonitor.left,
                    info.rcMonitor.bottom - info.rcMonitor.top);

                bool isPrimary = (info.dwFlags & 0x00000001) != 0;
                monitors.Add(new MonitorInfo(hMonitor, bounds, isPrimary));
            }
            return true;
        }, IntPtr.Zero);

        return monitors;
    }

    private static int GetMonitorIndex(IntPtr monitorHandle)
    {
        var all = EnumerateMonitors();
        return all.FindIndex(m => m.Handle == monitorHandle);
    }

    private static System.Drawing.Point GetCursorPosition()
    {
        GetCursorPos(out var pt);
        return new System.Drawing.Point(pt.X, pt.Y);
    }

    // Convert D3D11 surface → JPEG bytes
    private static async Task<byte[]> ConvertFrameToJpegAsync(
        Direct3D11CaptureFrame frame,
        IDirect3DDevice device,
        int maxDimension)
    {
        using var softwareBitmap =
            await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface);

        // Resize to maxDimension while preserving aspect ratio
        var (targetW, targetH) = ComputeTargetSize(
            softwareBitmap.PixelWidth,
            softwareBitmap.PixelHeight,
            maxDimension);

        using var outputStream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(
            BitmapEncoder.JpegEncoderId,
            outputStream);

        encoder.SetSoftwareBitmap(softwareBitmap);
        encoder.BitmapTransform.ScaledWidth = (uint)targetW;
        encoder.BitmapTransform.ScaledHeight = (uint)targetH;
        encoder.BitmapTransform.InterpolationMode =
            BitmapInterpolationMode.Fant;

        var properties = new BitmapPropertySet();
        properties["ImageQuality"] = new BitmapTypedValue(
            JpegQuality / 100.0, Windows.Foundation.PropertyType.Single);
        await encoder.BitmapProperties.SetPropertiesAsync(properties);

        await encoder.FlushAsync();
        
        var bytes = new byte[outputStream.Size];
        using (var reader = new DataReader(outputStream.GetInputStreamAt(0)))
        {
            await reader.LoadAsync((uint)outputStream.Size);
            reader.ReadBytes(bytes);
        }
        return bytes;
    }

    private static (int w, int h) ComputeTargetSize(int w, int h, int max)
    {
        if (w <= max && h <= max) return (w, h);
        double scale = Math.Min((double)max / w, (double)max / h);
        return ((int)(w * scale), (int)(h * scale));
    }

    // Wraps WinRT CaptureItem creation from a monitor HMONITOR
    private static GraphicsCaptureItem? CreateCaptureItemForMonitor(IntPtr hMonitor)
    {
        try
        {
            // .NET 8 modern way for interop
            var factory = WinRT.ActivationFactory<GraphicsCaptureItem>.As<IGraphicsCaptureItemInterop>();
            var iid = typeof(GraphicsCaptureItem).GUID;
            var ptr = factory.CreateForMonitor(hMonitor, ref iid);
            return GraphicsCaptureItem.FromAbi(ptr);
        }
        catch
        {
            return null;
        }
    }

    // ── Win32 P/Invoke ───────────────────────────────────────────────────────

    private delegate bool MonitorEnumProc(
        IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(
        IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);
    [DllImport("user32.dll")] static extern bool GetMonitorInfo(
        IntPtr hMonitor, ref MONITORINFO lpmi);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor, rcWork;
        public uint dwFlags;
    }

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        IntPtr CreateForWindow([In] IntPtr hWnd, [In] ref Guid iid);
        IntPtr CreateForMonitor([In] IntPtr hMonitor, [In] ref Guid iid);
    }
}

// ── Direct3D11 Helper ────────────────────────────────────────────────────────

internal static class Direct3D11Helper
{
    [DllImport("d3d11.dll", EntryPoint = "D3D11CreateDevice", SetLastError = true)]
    private static extern int D3D11CreateDevice(IntPtr adapter, int driverType, IntPtr software, uint flags, IntPtr featureLevels, uint featureLevelsCount, uint sdkVersion, out IntPtr device, out uint featureLevel, out IntPtr context);

    [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", SetLastError = true)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    public static IDirect3DDevice CreateDevice()
    {
        // 1 = D3D_DRIVER_TYPE_HARDWARE, 0x20 = D3D11_CREATE_DEVICE_BGRA_SUPPORT, 7 = D3D11_SDK_VERSION
        int hr = D3D11CreateDevice(IntPtr.Zero, 1, IntPtr.Zero, 0x20, IntPtr.Zero, 0, 7, out var d3dDevice, out _, out _);
        if (hr < 0) Marshal.ThrowExceptionForHR(hr);

        // Get IDXGIDevice
        var dxgiDeviceGuid = new Guid("70212c6d-5527-4ad3-af0d-db4673eca73e");
        hr = Marshal.QueryInterface(d3dDevice, ref dxgiDeviceGuid, out var dxgiDevice);
        if (hr < 0) Marshal.ThrowExceptionForHR(hr);

        hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out var graphicsDevice);
        if (hr < 0) Marshal.ThrowExceptionForHR(hr);

        // Correctly project from ABI pointer to WinRT object in .NET 8
        var device = WinRT.MarshalInterface<IDirect3DDevice>.FromAbi(graphicsDevice);

        Marshal.Release(d3dDevice);
        Marshal.Release(dxgiDevice);
        Marshal.Release(graphicsDevice);

        return device;
    }
}
