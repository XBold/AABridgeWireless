using Android.Content;
using Android.Net.Wifi;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Tools.Classes;

namespace AABridgeWireless;

public partial class ServerPage : ContentPage
{
    private CancellationTokenSource _cancellationTokenSource;
    private readonly string startText = "Start server";
    private readonly string stopText = "Stop server";

    public ServerPage()
    {
        InitializeComponent();
        Logger.Log("Selezionata modalità server", 0);
        Preferences.Set("AppMode", "server");
        btStartStop.Text = startText;
        _ = CheckWiFiSignal();
    }

    private async Task StartServer(int port, CancellationToken token)
    {
        TcpListener server = new TcpListener(IPAddress.Any, port);
        server.Start();
        Logger.Log("Server avviato", 0);
        ToogleUiElements(false);

        try
        {
            while (!token.IsCancellationRequested)
            {
                Logger.Log("In attesa di una connessione...", 0);

                // Attendi una connessione o il token di cancellazione
                var acceptTask = server.AcceptTcpClientAsync();
                var completedTask = await Task.WhenAny(acceptTask, Task.Delay(Timeout.Infinite, token));

                // Se il token è stato segnalato, esci dal ciclo
                if (completedTask == acceptTask)
                {
                    var client = acceptTask.Result;
                    Logger.Log("Connessione accettata", 0);

                    // Gestisci il client in un'altra funzione
                    _ = HandleClientAsync(client, token);
                }
                else
                {
                    Logger.Log("Token di cancellazione ricevuto", 0);
                    ToogleUiElements(true);
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Log("Server interrotto dal token di cancellazione", 0);
        }
        catch (Exception ex)
        {
            Logger.Log($"Errore nel server: {ex.Message}", 3);
        }
        finally
        {
            server.Stop();
            ToogleUiElements(true);
            Logger.Log("Server arrestato", 0);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        try
        {
            using var stream = client.GetStream();
            byte[] buffer = Encoding.UTF8.GetBytes("Connesso al server.");
            await stream.WriteAsync(buffer, 0, buffer.Length, token);
            Logger.Log("Messaggio inviato al client", 0);

            // Leggi i dati dal client
            buffer = new byte[1024];
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Logger.Log($"Ricevuto: {message}", 0);
        }
        catch (OperationCanceledException)
        {
            Logger.Log("Operazione annullata", 0);
        }
        catch (Exception ex)
        {
            Logger.Log($"Errore nel client: {ex.Message}", 3);
        }
        finally
        {
            client.Close();
            Logger.Log("Connessione chiusa", 0);
        }
    }

    private async Task CheckWiFiSignal()
    {
        while (true)
        {
            lblShowSignal.Text = WiFiSignal().ToString();
            await Task.Delay(1000);
        }
    }

    private int WiFiSignal()
    {
        var wifiManager = (WifiManager)Android.App.Application.Context.GetSystemService(Context.WifiService);
        var info = wifiManager.ConnectionInfo;
        return WifiManager.CalculateSignalLevel(info.Rssi, 101);
    }

    private void StartStopServer(object sender, EventArgs e)
    {
        if (btStartStop.Text == startText)
        {
            if (!string.IsNullOrEmpty(entPort.Text))
            {
                if (int.TryParse(entPort.Text, out var port))
                {
                    if (port > 1020 && port <= 65535)
                    {
                        btStartStop.Text = stopText;
                        _cancellationTokenSource =  new CancellationTokenSource();
                        _ = StartServer(port, _cancellationTokenSource.Token);
                    }
                    else
                    {
                        entPort.BackgroundColor = Colors.Red;
                    }
                }
                else
                {
                    Logger.Log("Not possible to parse the input in INT format", 2);
                }
            }
        }
        else
        {
            StopServer();
        }
    }

    private void StopServer()
    {
        if (_cancellationTokenSource != null)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
            Logger.Log("Richiesta di arresto del server inviata", 0);
        }
    }

    private void ToogleUiElements(bool enable)
    {
        entPort.IsEnabled = enable;
        btStartStop.Text = (enable ? startText : stopText);
    }

    private void RestoreBackgroundColor(object sender, EventArgs e)
    {
        if (sender is Entry entry)
        {
            entry.BackgroundColor = Colors.Transparent;
        }
    }
}