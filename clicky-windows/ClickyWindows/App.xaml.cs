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

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Do NOT create a MainWindow — Clicky is a tray-only app.
        // TrayIconManager owns the system tray icon and the floating panel.
        ClickyAnalytics.Configure();

        _companionManager = new CompanionManager();
        _companionManager.Start();

        _trayIconManager = new TrayIconManager(_companionManager);
        _trayIconManager.Initialize();

        // First launch detection & onboarding
        bool isFirstLaunch = !Windows.Storage.ApplicationData.Current
            .LocalSettings.Values.ContainsKey("HasCompletedOnboarding");

        if (isFirstLaunch)
        {
            // Register for startup on first launch
            StartupRegistration.RegisterForStartup();

            // Show onboarding welcome in overlay
            await System.Threading.Tasks.Task.Delay(1500); // Let the tray icon settle
            _companionManager?.ShowOnboardingWelcome();

            Windows.Storage.ApplicationData.Current.LocalSettings
                .Values["HasCompletedOnboarding"] = true;

            ClickyAnalytics.TrackOnboardingStarted();
        }

        ClickyAnalytics.TrackAppOpened();
    }
}
