using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Api;

public class DbClientService
{
    private readonly string _host;
    private readonly int _port;

    public DbClientService(string host = "127.0.0.1", int port = 9090)
    {
        _host = host;
        _port = port;
    }

    private string Serialize(Dictionary<string, object> obj)
    {
        var sb = new StringBuilder();
        foreach (var kvp in obj)
        {
            var value = kvp.Value;
            if (value is List<Dictionary<string, object>> list)
            {
                value = JsonSerializer.Serialize(list);
            }
            else if (value is Dictionary<string, object> dict)
            {
                value = JsonSerializer.Serialize(dict);
            }
            sb.AppendLine($"{kvp.Key}:>{value}");
        }
        return sb.ToString();
    }

   
    
}
