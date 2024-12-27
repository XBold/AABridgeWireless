namespace AABridgeWireless
{
    public partial class AppShell : Shell
    {
        private bool cleanupInProgress = false;

        public AppShell(string pageSelection)
        {
            InitializeComponent();

            if (!string.IsNullOrEmpty(pageSelection))
            {
                GoToAsync("//" + pageSelection);
            }
        }

        protected override void OnNavigating(ShellNavigatingEventArgs args)
        {
            if (cleanupInProgress)
                return;

            if (CurrentPage is IPageCleanup cleanupPage)
            {
                cleanupInProgress = true;

                // Blocca temporaneamente la navigazione
                args.Cancel();

                // Esegui la pulizia
                Task.Run(async () =>
                {
                    await cleanupPage.CleanupAsync();

                    // Riprendi la navigazione
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        cleanupInProgress = false;
                        //await Current.GoToAsync(args.Target.Location);
                        await ((AppShell)Current).NavigateToPage(args.Target.Location.OriginalString);
                    });
                });
            }
        }

        public async Task NavigateToPage(string targetRoute)
        {
            var currentRoute = Current.CurrentState.Location.OriginalString;

            if (currentRoute != targetRoute)
            {
                await Current.GoToAsync(targetRoute);
            }
        }
    }

}
