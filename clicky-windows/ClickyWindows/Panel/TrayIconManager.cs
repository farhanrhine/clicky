// Panel/TrayIconManager.cs
// Creates and manages the system tray icon. Clicking the icon opens or
// closes the floating companion panel. Uses H.NotifyIcon.WinUI.

using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Runtime.InteropServices;
using Windows.Foundation;

namespace ClickyWindows;

internal class TrayIconManager
{
    private TaskbarIcon? _taskbarIcon;
    private CompanionPanelWindow? _companionPanel;
    private bool _isPanelVisible = false;

    public void Initialize()
    {
        _taskbarIcon = new TaskbarIcon
        {
            IconSource = new BitmapIconSource
            {
                UriSource = new Uri("ms-appx:///Assets/clicky.ico")
            },
            ToolTipText = "Clicky"
        };

        _taskbarIcon.TrayMouseDoubleClick += OnTrayIconClicked;
        _taskbarIcon.LeftClickCommand =
            new RelayCommand(() => OnTrayIconClicked(null, null));

        _taskbarIcon.ForceCreate(enablesRichTooltips: false);
    }

    private void OnTrayIconClicked(object? sender, object? args)
    {
        if (_isPanelVisible)
        {
            HidePanel();
        }
        else
        {
            ShowPanel();
        }
    }

    private void ShowPanel()
    {
        _companionPanel ??= new CompanionPanelWindow();
        _companionPanel.OnDismissRequested += HidePanel;

        PositionPanelNearTrayIcon();
        _companionPanel.Activate();
        _isPanelVisible = true;
    }

    private void HidePanel()
    {
        _companionPanel?.Close();
        _companionPanel = null;
        _isPanelVisible = false;
    }

    private void PositionPanelNearTrayIcon()
    {
        if (_companionPanel == null) return;

        // Position panel in the bottom-right corner, above the taskbar.
        // Windows 11 default taskbar is at the bottom.
        var workArea = SystemInformation.WorkingArea;
        var panelWidth = 320;
        var panelHeight = 420;

        int x = workArea.Right - panelWidth - 12;
        int y = workArea.Bottom - panelHeight - 12;

        _companionPanel.Move(x, y);
    }

    public void Dispose()
    {
        _taskbarIcon?.Dispose();
    }

    // Win32 helper for screen work area
    private static class SystemInformation
    {
        [DllImport("user32.dll")]
        private static extern bool SystemParametersInfo(
            uint uiAction, uint uiParam,
            ref RECT pvParam, uint fWinIni);

        private const uint SPI_GETWORKAREA = 0x0030;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        { public int Left, Top, Right, Bottom; }

        public static (int Right, int Bottom) WorkingArea
        {
            get
            {
                var rect = new RECT();
                SystemParametersInfo(SPI_GETWORKAREA, 0, ref rect, 0);
                return (rect.Right, rect.Bottom);
            }
        }
    }
}
