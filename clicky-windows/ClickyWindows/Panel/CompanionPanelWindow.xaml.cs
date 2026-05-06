// Panel/CompanionPanelWindow.xaml.cs

using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace ClickyWindows;

public sealed partial class CompanionPanelWindow : Window
{
    public event Action? OnDismissRequested;

    public CompanionPanelWindow()
    {
        this.InitializeComponent();
        ConfigureWindowStyle();
        InstallClickOutsideHandler();
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
