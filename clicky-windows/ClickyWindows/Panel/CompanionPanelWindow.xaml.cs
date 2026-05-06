// Panel/CompanionPanelWindow.xaml.cs

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace ClickyWindows;

public sealed partial class CompanionPanelWindow : Window
{
    public event Action? OnDismissRequested;

    private readonly CompanionManager _companionManager;

    public CompanionPanelWindow(CompanionManager companionManager)
    {
        _companionManager = companionManager;
        this.InitializeComponent();
        ConfigureWindowStyle();
        InstallClickOutsideHandler();
        _ = CheckMicPermissionAsync();
    }

    private async Task CheckMicPermissionAsync()
    {
        bool hasMic = await PermissionsManager.RequestMicrophonePermissionAsync();
        
        this.DispatcherQueue.TryEnqueue(() => {
            if (hasMic)
            {
                MicStatusText.Text = "Allowed";
                MicStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 52, 211, 153));
                PermissionsSection.Visibility = Visibility.Collapsed;
                ModelPickerRow.Visibility = Visibility.Visible;
                UpdateModelSelectionUI();
            }
            else
            {
                MicStatusText.Text = "Denied";
                MicStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 229, 72, 77));
                MicGrantButton.Visibility = Visibility.Visible;
            }
        });
    }

    private void MicGrantButton_Click(object sender, RoutedEventArgs e)
    {
        _ = PermissionsManager.OpenMicrophoneSettingsAsync();
    }

    private void SonnetButton_Click(object sender, RoutedEventArgs e)
    {
        _companionManager.SetSelectedModel("claude-3-5-sonnet-20241022");
        UpdateModelSelectionUI();
    }

    private void OpusButton_Click(object sender, RoutedEventArgs e)
    {
        _companionManager.SetSelectedModel("claude-3-opus-20240229");
        UpdateModelSelectionUI();
    }

    private void UpdateModelSelectionUI()
    {
        bool isSonnet = _companionManager.SelectedModel.Contains("sonnet");
        
        SonnetButton.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            isSonnet ? Windows.UI.Color.FromArgb(255, 39, 42, 41) : Windows.UI.Color.FromArgb(0, 0, 0, 0));
        SonnetButton.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            isSonnet ? Windows.UI.Color.FromArgb(255, 236, 238, 237) : Windows.UI.Color.FromArgb(255, 107, 115, 111));

        OpusButton.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            !isSonnet ? Windows.UI.Color.FromArgb(255, 39, 42, 41) : Windows.UI.Color.FromArgb(0, 0, 0, 0));
        OpusButton.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            !isSonnet ? Windows.UI.Color.FromArgb(255, 236, 238, 237) : Windows.UI.Color.FromArgb(255, 107, 115, 111));
    }

    private void ConfigureWindowStyle()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var appWindow = AppWindow.GetFromWindowId(
            Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd));

        // Remove titlebar and make borderless
        appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        var presenter = appWindow.Presenter as OverlappedPresenter;
        if (presenter != null)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }
    }

    public void Move(int x, int y)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var appWindow = AppWindow.GetFromWindowId(
            Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd));
        appWindow.Move(new Windows.Graphics.PointInt32(x, y));
    }

    private void InstallClickOutsideHandler()
    {
        // Dismiss when app loses activation (user clicks elsewhere)
        this.Activated += (_, args) =>
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                OnDismissRequested?.Invoke();
            }
        };
    }

    private void QuitButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Exit();
    }
}
