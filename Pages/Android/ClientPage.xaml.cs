using Android.Content;
using Android.Net.Wifi;
using System.Net.Sockets;
using System.Text;
using Tools;
using Tools.Network;
using Tools.ObjectHandlers;
using Tools.ValidationsAndExeptions;

namespace AABridgeWireless;

public partial class ClientPage : ContentPage, IPageCleanup
{
    private bool stopPageRequest;
    private bool connectionRunning;
    TCP_Client client = new TCP_Client();

    public ClientPage()
    {
        InitializeComponent();
        Console.WriteLine("Selezionata modalità client");
        Preferences.Set("AppMode", "client");

        client.OnMessageReceived += OnMessageReceived;
        client.OnConnectionStateChanged += ConnectionStateChanged;
    }

    private void InitializePage()
    {
        stopPageRequest = false;
        Logger.Log("Client mode selected", 0);
        Preferences.Set("AppMode", "client");
        btCnct.Text = Constants.connectText;
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
        entTxMsg.IsEnabled = false;
        btTxMsg.IsEnabled = false;
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
        client.Disconnect();
        await Task.Delay(1000);
    }

    private async Task ConnectToServer(string ip, int port)
    {
        try
        {
            await client.ConnectAsync(ip, port);
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

    private async Task OnMessageReceived(byte[] data)
    {
        string message = Encoding.UTF8.GetString(data);
        lblRxMsg.Text = message;
    }

    private async Task ConnectionStateChanged(bool isConnected)
    {
        entTxMsg.IsEnabled = isConnected;
        btTxMsg.IsEnabled = isConnected;
        ToogleUiElements(!isConnected);
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

    private void ToogleUiElements(bool enable)
    {
        entIpDst.IsEnabled = enable;
        entPort.IsEnabled = enable;
        btCnct.Text = (enable ? Constants.connectText : Constants.disconnectText);
    }

    private void RestoreColor(object sender, EventArgs e)
    {
        Properties.RestoreColor(sender, e);
    }

    private void BtConnectDisconnect(object sender, EventArgs e)
    {
        if (sender is Button btConnection)
        {
            if (btConnection.Text == Constants.connectText)
            {
                if (!string.IsNullOrEmpty(entIpDst.Text) && !string.IsNullOrEmpty(entPort.Text))
                {
                    if (int.TryParse(entPort.Text, out var port))
                    {
                        if (NetCheck.PortInRange(port))
                        {
                            ToogleUiElements(false);
                            _ = ConnectToServer(entIpDst.Text, port);
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
                client.Disconnect();
            }
        }
    }

    private async void BtTxMessage(object sender, EventArgs e)
    {
        btTxMsg.IsEnabled = false;
        if (!string.IsNullOrEmpty(entTxMsg.Text))
        {
            await client.SendMessageAsync(entTxMsg.Text);
        }
        btCnct.IsEnabled = true;
    }
}
