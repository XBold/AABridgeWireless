using System.Net.Sockets;
using System.Text;

namespace AABridgeWireless;

public partial class ClientPage : ContentPage
{
    public ClientPage()
    {
        InitializeComponent();
        Console.WriteLine("Selezionata modalità client");
        Preferences.Set("AppMode", "client");
        _ = ConnectToServer("192.168.50.1", 5555);
    }

    public async Task ConnectToServer(string ip, int port)
    {
        TcpClient client = new TcpClient();
        await client.ConnectAsync(ip, port);
        Console.WriteLine("Connesso al server.");

        var stream = client.GetStream();
        byte[] buffer = Encoding.UTF8.GetBytes("Messaggio dal client.");
        await stream.WriteAsync(buffer, 0, buffer.Length);

        // Legge i dati dal server
        buffer = new byte[1024];
        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
        Console.WriteLine($"Ricevuto dal server: {message}");
    }

    private void ManageWiFi()
    {
        // WiFi management logic
    }

    private void HandleUSBCommunication()
    {
        // USB communication logic
    }
}
