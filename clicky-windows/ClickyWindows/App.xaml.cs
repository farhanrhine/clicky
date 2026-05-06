// App.xaml.cs
// Entry point for Clicky Windows. Creates no main window — the app lives
// entirely in the system tray. The tray icon is created here and torn down
// on exit. All other components are owned by CompanionManager.

using Microsoft.UI.Xaml;

namespace ClickyWindows;

public partial class App : Application
{
    private TrayIconManager? _trayIconManager;
    private CompanionManager? _companionManager;

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Do NOT create a MainWindow — Clicky is a tray-only app.
        // TrayIconManager owns the system tray icon and the floating panel.
        _companionManager = new CompanionManager();
        _companionManager.Start();

        _trayIconManager = new TrayIconManager();
        _trayIconManager.Initialize();
    }
}
