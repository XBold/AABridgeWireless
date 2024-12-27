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

        protected override void OnNavigating(ShellNavigatingEventArgs args)
        {
            base.OnNavigating(args);

            // Controlla se la pagina corrente implementa l'interfaccia IPageCleanup
            if (CurrentPage is IPageCleanup cleanupPage)
            {
                args.Cancel(); // Blocca temporaneamente la navigazione
                Task.Run(async () =>
                {
                    await cleanupPage.CleanupAsync(); // Esegui la pulizia
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await Shell.Current.GoToAsync(args.Target.Location); // Continua la navigazione
                    });
                });
            }
        }
    }
}
