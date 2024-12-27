using Android.Content;
using Android.Net.Wifi;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Tools.Classes;

namespace AABridgeWireless;

public partial class ServerPage : ContentPage, IPageCleanup
{
    public bool serverRunning { get; private set; }
    
    private CancellationTokenSource _cancellationTokenSource;
    private readonly string startText = "Start server";
    private readonly string stopText = "Stop server";
    private bool stopPageRequest;

    public ServerPage()
    {
        InitializeComponent();
        stopPageRequest = false;
        Logger.Log("Server mode selected", 0);
        Preferences.Set("AppMode", "server");
        btStartStop.Text = startText;
        _ = CheckWiFiSignal();
    }

    public async Task CleanupAsync()
    {
        await StopPage();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Shell.Current.Navigation.PopAsync(false);
        });
    }

    private async Task StopPage()
    {
        stopPageRequest = true;
        await StopServer();
    }

    private async Task StartServer(int port, CancellationToken token)
    {
        TcpListener server = new TcpListener(IPAddress.Any, port);
        server.Start();
        Logger.Log("Server started", 0);
        ToogleUiElements(false);
        serverRunning = true;

        try
        {
            while (!token.IsCancellationRequested)
            {
                Logger.Log("Waiting connection...", 0);

                var acceptTask = server.AcceptTcpClientAsync();
                var completedTask = await Task.WhenAny(acceptTask, Task.Delay(Timeout.Infinite, token));

                if (completedTask == acceptTask)
                {
                    var client = acceptTask.Result;
                    Logger.Log("Connection accepted", 0);

                    _ = HandleClientAsync(client, token);
                }
                else
                {
                    Logger.Log("Token for closing server received", 0);
                    ToogleUiElements(true);
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Log("Requested stop of server by token", 0);
        }
        catch (Exception ex)
        {
            Logger.Log($"Server error: {ex.Message}", 2);
        }
        finally
        {
            server.Stop();
            ToogleUiElements(true);
            serverRunning = false;
            Logger.Log("Server succesfully stopped", 0);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        try
        {
            using var stream = client.GetStream();
            byte[] buffer = Encoding.UTF8.GetBytes("Client connected");
            await stream.WriteAsync(buffer, 0, buffer.Length, token);
            Logger.Log("Welcome message sent to client", 0);

            buffer = new byte[1024];
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Logger.Log($"Message received: {message}", 0);
        }
        catch (OperationCanceledException)
        {
            Logger.Log("Server stop requested", 0);
        }
        catch (Exception ex)
        {
            Logger.Log($"Error receiving message: {ex.Message}", 2);
        }
        finally
        {
            client.Close();
            Logger.Log("Server closed", 0);
        }
    }

    private async Task CheckWiFiSignal()
    {
        
        while (!stopPageRequest)
        {
            lblShowSignal.Text = "Signal strenght: " + WiFiSignal().ToString() + "%";
            await Task.Delay(100);
        }
        lblShowSignal.Text = "Stop page requested!!";
    }

    private int WiFiSignal()
    {
#if ANDROID
        try
        {
            var wifiManager = (WifiManager)Android.App.Application.Context.GetSystemService(Context.WifiService);
            var info = wifiManager.ConnectionInfo;
            return WifiManager.CalculateSignalLevel(info.Rssi, 101);
        }
        catch (Exception ex)
        {
            Logger.Log($"Error while getting signal strength. Error: {ex.Message}", 2);
            return 0;
        }
#else
        return 0;
#endif
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
            _ = StopServer();
        }
    }

    private async Task StopServer()
    {
        DateTime timeRequestStop = DateTime.Now;
        TimeSpan breakDuration = TimeSpan.FromSeconds(2);
        if (_cancellationTokenSource != null)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
            Logger.Log("Request stop server", 0);
        }

        while (serverRunning)
        {
            await Task.Delay(50);
            if (DateTime.Now - timeRequestStop > breakDuration)
            {
                Logger.Log("Forced stop when server is still running", 2);
                break;
            }
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