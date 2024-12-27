namespace AABridgeWireless;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var mode = Preferences.Get("AppMode", "None");
        Page startPage;

        if (mode == "server")
        {
            startPage = new AppShell("server");
        }
        else if (mode == "client")
        {
            startPage = new AppShell("client");
        }
        else
        {
            startPage = new ModeSelectionPage();
        }

        return new Window(startPage);
    }
}