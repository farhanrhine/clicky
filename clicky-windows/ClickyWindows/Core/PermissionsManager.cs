// Core/PermissionsManager.cs
// Checks and requests Windows permissions needed by Clicky.
// On Windows, only microphone access needs an explicit request.
// Screen capture is handled by the capture picker (Phase 07).
// Accessibility (for global hotkey) does not require a permission prompt on Windows.

using Windows.Media.Capture;

namespace ClickyWindows;

internal static class PermissionsManager
{
    private static bool _hasMicPermission = false;
    private static bool _hasScreenCapturePermission = false;

    public static bool HasMicrophonePermission => _hasMicPermission;
    public static bool HasScreenCapturePermission => _hasScreenCapturePermission;
    public static bool AllPermissionsGranted
        => _hasMicPermission && _hasScreenCapturePermission;

    /// <summary>
    /// Requests microphone access if not already granted.
    /// Returns true if permission is granted after the call.
    /// </summary>
    public static async Task<bool> RequestMicrophonePermissionAsync()
    {
        try
        {
            var captureSettings = new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Audio
            };

            using var capture = new MediaCapture();
            await capture.InitializeAsync(captureSettings);
            _hasMicPermission = true;
            ClickyAnalytics.TrackPermissionGranted("microphone");
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            _hasMicPermission = false;
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Opens Windows Settings → Privacy → Microphone if permission was denied.
    /// </summary>
    public static async Task OpenMicrophoneSettingsAsync()
    {
        await Windows.System.Launcher.LaunchUriAsync(
            new Uri("ms-settings:privacy-microphone"));
    }

    /// <summary>
    /// Marks screen capture as permitted (called after first successful capture).
    /// </summary>
    public static void MarkScreenCaptureGranted()
    {
        if (!_hasScreenCapturePermission)
        {
            _hasScreenCapturePermission = true;
            ClickyAnalytics.TrackPermissionGranted("screen_capture");
        }

        if (AllPermissionsGranted)
            ClickyAnalytics.TrackAllPermissionsGranted();
    }
}
