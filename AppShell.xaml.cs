namespace AABridgeWireless
{
    public partial class AppShell : Shell
    {
        public AppShell(string pageSelection)
        {
            InitializeComponent();
            if (!string.IsNullOrEmpty(pageSelection))
            {
                GoToAsync("//" + pageSelection);
            }
        }
    }
}
