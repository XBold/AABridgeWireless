using Android.Content;
using Android.Net.Wifi;
using System.Net.Sockets;
using System.Text;
using Tools;
using Tools.Network;
using Tools.ObjectHandlers;
using Tools.ValidationsAndExeptions;

namespace AABridgeWireless;

public partial class ServerPage : ContentPage, IPageCleanup
{

    private bool stopPageRequest;
    private bool confirmAddIp;
    TCP_Server server;
    private List<TcpClient> tcpClientsConnected = new();

    public ServerPage()
    {
        InitializeComponent();
    }

    private void InitializePage()
    {
        stopPageRequest = false;
        Logger.Log("Server mode selected", 0);
        Preferences.Set("AppMode", "server");
        btCnct.Text = Constants.connectText;
        lblRxMsg.Text = string.Empty;
        entIpDst.Text = Preferences.Get("ToClient.Ip", string.Empty);
        if (string.IsNullOrEmpty(entIpDst.Text))
        {
            entIpDst.IsEnabled = false;
        }
        int value = Preferences.Get("ClientPort", -1);
        if (value == -1)
        {
            entPort.Text = "";
        }
        else
        {
            entPort.Text = value.ToString();
        }
    }

    private void PageAppearing(object sender, EventArgs e)
    {
        InitializePage();
        _ = UpdateWifiData();
    }

    public async Task CleanupAsync()
    {
        Logger.Log($"Closing {this.GetType().Name}", 0);
        await StopPage();
    }

    private async Task StopPage()
    {
        stopPageRequest = true;
        server.StopServer();
        await Task.Delay(1000);
    }

    private async Task StartServer(string ip, int port)
    {
        ValidationResult result;
        if (string.IsNullOrEmpty(ip))
        {
            server = TCP_Server.Create(out result, port);
        }
        else
        {
            server = TCP_Server.Create(ip, out result, port);
        }
        if (server == null)
        {
            foreach (var error in result.Errors)
            {
                if (error.Key == NetworkConstants.IPName)
                {
                    entIpDst.BackgroundColor = Colors.Red;
                }
                else if (error.Key == NetworkConstants.PortName)
                {
                    entPort.BackgroundColor = Colors.Red;
                }
            }
            return;
        }

        AddEvents(true);

        try
        {
            await server.StartServerAsync();
        }
        catch (SocketException socketEx)
        {
            if (socketEx.Message != "Connection refused")
            {
                Logger.Log("Connection refused", 0);
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Log("Request stop of waiting connection by token - NOT USED, HANDLED INTERNALLY IN TOOL LIB", 3);
        }
        catch (ParameterValidationException ex)
        {
            Logger.Log("Failed to connect due to validation errors:", 1);
            foreach (var error in ex.ValidationResult.Errors)
            {
                Logger.Log($"{error.Key}: {error.Value}", 1);
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Unexpected error: {ex.Message}", 2);
        }
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

    private async Task OnMessageReceived(TcpClient client, byte[] data)
    {
        string message = Encoding.UTF8.GetString(data);
        lblRxMsg.Text = message;
    }

    private async Task OnClientConnected(TcpClient client)
    {
        tcpClientsConnected.Add(client);
        ToggleUiServerMessage(true);
    }

    private async Task OnClientDisconnected(TcpClient client)
    {
        tcpClientsConnected.Remove(client);
        if (tcpClientsConnected.Count == 0)
        {
            ToggleUiServerMessage(false);
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

    private void AddEvents(bool activate)
    {
        if (activate)
        {
            server.OnMessageReceived += OnMessageReceived;
            server.OnClientConnected += OnClientConnected;
            server.OnClientDisconnected += OnClientDisconnected;
        }
        else
        {
            server.OnMessageReceived -= OnMessageReceived;
            server.OnClientConnected -= OnClientConnected;
            server.OnClientDisconnected -= OnClientDisconnected;
        }
    }

    private void ToggleUiServerConfig(bool enable)
    {
        entPort.IsEnabled = enable;
        entIpDst.IsEnabled = enable;
        btCnct.Text = (enable ? Constants.connectText : Constants.disconnectText);
    }

    private void ToggleUiServerMessage(bool enable)
    {
        btTxMsg.IsEnabled = enable;
    }

    private void RestoreColor(object sender, EventArgs e)
    {
        Properties.RestoreColor(sender, e);
    }

    private void OptionalEntry(object sender, FocusEventArgs e)
    {
        if (sender is Entry entry)
        {
            if (string.IsNullOrEmpty(entry.Text) && !confirmAddIp)
            {
                confirmAddIp = true;
                entry.Placeholder = "OPTIONAL - Click again";
            }
            else if (string.IsNullOrEmpty(entry.Text) && confirmAddIp)
            {
                entry.Placeholder = "Server IP address";
                entry.IsEnabled = true;
            }
            RestoreColor(sender, e);
        }
    }

    private void BtStartStopServer(object sender, EventArgs e)
    {
        if (sender is Button btConnection)
        {
            if (btConnection.Text == Constants.connectText)
            {
                if (!string.IsNullOrEmpty(entPort.Text))
                {
                    if (int.TryParse(entPort.Text, out var port))
                    {
                        if (NetCheck.PortInRange(port))
                        {
                            ToggleUiServerConfig(false);
                            _ = StartServer(entIpDst.Text, port);
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
                AddEvents(false);
                server.StopServer();
            }
        }
    }

    private async void BtTxMessage(object sender, EventArgs e)
    {
        btTxMsg.IsEnabled = false;
        if (!string.IsNullOrEmpty(entTxMsg.Text))
        {
            await server.BroadcastMessageAsync(entTxMsg.Text);
        }
        btCnct.IsEnabled = true;
    }
}