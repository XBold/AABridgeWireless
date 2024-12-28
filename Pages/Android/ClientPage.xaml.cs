using Android.Content;
using Android.Net.Wifi;
using System.Net.Sockets;
using System.Text;
using Tools;
using Tools.Network;
using Tools.ObjectHandlers;

namespace AABridgeWireless;

public partial class ClientPage : ContentPage, IPageCleanup
{
    private bool stopPageRequest;
    private bool connectionRunning;
    private readonly string connectText = "Connect to server";
    private readonly string disconnectText = "Disconnect from server";
    private CancellationTokenSource _cancellationTokenSource;

    public ClientPage()
    {
        InitializeComponent();
        Console.WriteLine("Selezionata modalità client");
        Preferences.Set("AppMode", "client");
    }

    public async Task CleanupAsync()
    {
        Logger.Log($"Closing {this.GetType().Name}", 0);
        await StopPage();
    }

    private async Task StopPage()
    {
        stopPageRequest = true;
        await StopConnection();
    }

    private void InitializePage()
    {
        stopPageRequest = false;
        Logger.Log("Client mode selected", 0);
        Preferences.Set("AppMode", "client");
        btCnct.Text = connectText;
        lblRxMsg.Text = string.Empty;
        entIpDst.Text = Preferences.Get("ServerIp", string.Empty);
        int port = Preferences.Get("ServerPort", -1);
        if (port == -1)
        {
            entPort.Text = "";
        }
        else
        {
            entPort.Text = port.ToString();
        }
    }

    private void PageAppearing(object sender, EventArgs e)
    {
        InitializePage();
        _ = UpdateWifiData();
    }

    private async Task UpdateWifiData()
    {
        while (!stopPageRequest)
        {
            var (signalStrenght, ip) = WiFiData();
            if (signalStrenght > 0)
            {
                lblInfo.Text = "Signal strenght: " + signalStrenght.ToString() + "%";
                lblInfo.Text += "\n";
                lblInfo.Text += $"Ip address: {ip}";
            }
            else
            {
                lblInfo.Text = "WiFi not connected";
            }
            await Task.Delay(100);
        }
    }

    private (int signal, string ipAddress) WiFiData()
    {
#if ANDROID
        try
        {

            var wifiManager = (WifiManager)Android.App.Application.Context.GetSystemService(Context.WifiService);
            var info = wifiManager.ConnectionInfo;
            int ipAddressRaw = info.IpAddress;
            string ip = "";
            try
            {
                ip = string.Format(
                    "{0}.{1}.{2}.{3}",
                    (ipAddressRaw & 0xff),
                    (ipAddressRaw >> 8 & 0xff),
                    (ipAddressRaw >> 16 & 0xff),
                    (ipAddressRaw >> 24 & 0xff));
            }
            catch (Exception ex)
            {
                Logger.Log("Error while formatting the IP address", 2);
                ip = string.Empty;
            }

            return (WifiManager.CalculateSignalLevel(info.Rssi, 101), ip);
        }
        catch (Exception ex)
        {
            Logger.Log($"Error while getting signal strength. Error: {ex.Message}", 2);
            return (0, "");
        }
#else
        return (0, "");
#endif
    }

    private async Task ConnectToServer(string ip, int port, CancellationToken token)
    {
        TcpClient client = new TcpClient();
        ToogleUiElements(false);
        connectionRunning = true;
        Logger.Log("Waiting connection...", 0);
        Preferences.Set("ServerIp", ip);
        Preferences.Set("ServerPort", port);
        while (!client.Connected)
        {
            try
            {
                await client.ConnectAsync(ip, port, token);
            }
            catch (SocketException socketEx)
            {
                if (socketEx.Message != "Connection refused")
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Log("Request stop of waiting connection by token", 0);
                break;
            }
            catch (Exception ex)
            {
                Logger.Log($"Server error: {ex.Message}", 3);
                break;
            }
        }

        if (client.Connected)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    Logger.Log("Client is connected to server", 0);
                    _ = HandleConnectionAsync(client, token);
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Log("Request stop of connection by token", 0);
            }
            catch (Exception ex)
            {
                Logger.Log($"Server error: {ex.Message}", 3);
            }
            finally
            {

                client.Close();
                ToogleUiElements(true);
                connectionRunning = false;
                Logger.Log("Connection succesfully stopped", 0);
            }
        }
        else
        {
            client.Close();
            ToogleUiElements(true);
            connectionRunning = false;
            Logger.Log("Connection succesfully stopped", 0);
        }
    }

    private async Task HandleConnectionAsync(TcpClient client, CancellationToken token)
    {
        if (client.Connected)
        {
            try
            {
                using var stream = client.GetStream();
                byte[] buffer = Encoding.UTF8.GetBytes("Client connected to server");
                await stream.WriteAsync(buffer, 0, buffer.Length, token);
                Logger.Log("Welcome message sent to client", 0);

                buffer = new byte[1024];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                lblRxMsg.Text += message;
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
                Logger.Log("Server closed", 0);
            }
        }
    }

    private void ToogleUiElements(bool enable)
    {
        entIpDst.IsEnabled = enable;
        entPort.IsEnabled = enable;
        entTxMsg.IsEnabled = !enable;
        btCnct.Text = (enable ? connectText : disconnectText);
    }

    private void ManageWiFi()
    {
        // WiFi management logic
    }

    private void HandleUSBCommunication()
    {
        // USB communication logic
    }

    private void ConnectOrDisconnect(object sender, EventArgs e)
    {
        if (sender is Button btConnection)
        {
            if (btConnection.Text == connectText)
            {
                if (!string.IsNullOrEmpty(entIpDst.Text) && !string.IsNullOrEmpty(entPort.Text))
                {
                    if (int.TryParse(entPort.Text, out var port))
                    {
                        if (NetCheck.PortInRange(port))
                        {
                            string ip = entIpDst.Text;
                            _cancellationTokenSource = new CancellationTokenSource();
                            _ = ConnectToServer(ip, port, _cancellationTokenSource.Token);
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
                _ = StopConnection();
            }
        }
    }

    private async Task StopConnection()
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

        while (connectionRunning)
        {
            await Task.Delay(50);
            if (DateTime.Now - timeRequestStop > breakDuration)
            {
                Logger.Log("Forced stop when server is still running", 2);
                break;
            }
        }
    }

    private void RestoreColor(object sender, EventArgs e)
    {
        Properties.RestoreColor(sender, e);
    }
}
