
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

public static class LogstashLogger
{
    private static readonly string host = "logstash"; // service name in docker-compose
    private static readonly int port = 5044;

    public static void Log(object logEntry)
    {
        var json = JsonSerializer.Serialize(logEntry);
        var data = Encoding.UTF8.GetBytes(json + "\n");

        using var client = new TcpClient(host, port);
        client.GetStream().Write(data, 0, data.Length);
    }
}

