using Tools;

namespace AABridgeWireless;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        string mode = string.Empty;

        try
        {
           mode = Preferences.Get("AppMode", "None");
        }
        catch(Exception ex)
        {
            Logger.Log("Error when loading startup page", 2);
            mode = string.Empty;
        }
        Page startPage;

        if (mode == "server")
        {
            startPage = new AppShell("//server");
        }
        else if (mode == "client")
        {
            startPage = new AppShell("//client");
        }
        else
        {
            startPage = new ModeSelectionPage();
        }

        return new Window(startPage);
    }
}