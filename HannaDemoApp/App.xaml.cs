namespace HannaDemoApp;

// Bootstraps the MAUI application and provides the root shell window.
public partial class App : Application
{
    private readonly AppShell _appShell;

    // Creates the application with the shared shell used as the root UI container.
    public App(AppShell appShell)
    {
        InitializeComponent();
        _appShell = appShell;
    }

    // Creates the main app window for the current activation.
    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(_appShell);
    }
}
