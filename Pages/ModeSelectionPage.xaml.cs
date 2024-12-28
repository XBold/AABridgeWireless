using Tools;

namespace AABridgeWireless;

public partial class ModeSelectionPage : ContentPage
{
    public ModeSelectionPage()
    {
        InitializeComponent();
    }

    private void OnModeSelected(object sender, EventArgs e)
    {
        if (sender is Button button)
        {
            string mode = button.Text;
            Preferences.Set("AppMode", mode);

            if (Application.Current?.Windows.Count > 0)
            {
                if (mode == "Server")
                {
                    Application.Current.Windows[0].Page = new AppShell(mode);
                }
                else if (mode == "Client")
                {
                    Application.Current.Windows[0].Page = new AppShell(mode);
                }
            }
            else
            {
                Logger.Log("No mode selected, check the code!", 3);
            }
        }
    }
}
