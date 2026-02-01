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

    private Dictionary<string, string> Deserialize(string body)
    {
        var obj = new Dictionary<string, string>();
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            var separatorIndex = line.IndexOf(":>");
            if (separatorIndex > 0)
            {
                var key = line.Substring(0, separatorIndex);
                var value = line.Substring(separatorIndex + 2).Trim();
                obj[key] = value;
            }
        }
        return obj;
    }

    public async Task<object> ExecuteQueryAsync(string dbName, string query)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(_host, _port);
            using var stream = client.GetStream();

            // 1) Send DB Name
            var dbInfo = new Dictionary<string, object> { ["db"] = dbName };
            var dbInfoBytes = Encoding.UTF8.GetBytes(Serialize(dbInfo));
            await stream.WriteAsync(dbInfoBytes, 0, dbInfoBytes.Length);

            // 2) Read DB Connection Response
            var buffer = new byte[4096];
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            var dbResponse = Deserialize(Encoding.UTF8.GetString(buffer, 0, bytesRead));

            if (!dbResponse.TryGetValue("con", out var con) || con != "1")
            {
                return new { error = dbResponse.TryGetValue("message", out var msg) ? msg : "Failed to connect to database" };
            }

            // 3) Send Query
            var queryInfo = new Dictionary<string, object> { ["query"] = query };
            var queryBytes = Encoding.UTF8.GetBytes(Serialize(queryInfo));
            await stream.WriteAsync(queryBytes, 0, queryBytes.Length);

            // 4) Read Query Response
            bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            var queryResponse = Deserialize(Encoding.UTF8.GetString(buffer, 0, bytesRead));

            if (queryResponse.TryGetValue("messages", out var results))
            {
                if (queryResponse.TryGetValue("is_json", out var isJson) && isJson == "1")
                {
                    try {
                        return JsonSerializer.Deserialize<List<Dictionary<string, object>>>(results) ?? new object();
                    } catch {
                        return results;
                    }
                }
                return results;
            }

            return "No result received from DBMS";
        }
        catch (Exception ex)
        {
            return new { error = $"Database connection error: {ex.Message}" };
        }
    }
}
