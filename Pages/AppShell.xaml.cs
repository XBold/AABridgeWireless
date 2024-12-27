using Android.Graphics.Pdf.Models.Selection;

namespace AABridgeWireless
{
    public partial class AppShell : Shell
    {
        private bool cleanupInProgress = false;

        public AppShell(string pageSelection)
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(ServerPage), typeof(ServerPage));
            Routing.RegisterRoute(nameof(ClientPage), typeof(ClientPage));

            if (!string.IsNullOrEmpty(pageSelection))
            {
                GoToAsync(pageSelection);
            }
        }

        protected override void OnNavigating(ShellNavigatingEventArgs args)
        {
            if (cleanupInProgress)
                return;

            if (CurrentPage is IPageCleanup cleanupPage)
            {
                cleanupInProgress = true;
                args.Cancel(); // Annulla la navigazione in corso

                Task.Run(async () =>
                {
                    await cleanupPage.CleanupAsync(); // Esegui la pulizia

                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await Current.GoToAsync(args.Target.Location);
                        cleanupInProgress = false;
                    });
                });
            }
            else
            {
                base.OnNavigating(args);
            }
        }
    }

}
