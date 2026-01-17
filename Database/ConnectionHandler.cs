using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Database;

/// <summary>
/// TCP Server for handling database client connections
/// </summary>
public class ConnectionHandler
{
    private readonly string _host;
    private readonly int _port;
    private TcpListener? _server;

    public ConnectionHandler(string host = "localhost", int port = 9090)
    {
        _host = host;
        _port = port;
    }

    /// <summary>
    /// Serialize dictionary to custom protocol format (key:>value\n)
    /// </summary>
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

    /// <summary>
    /// Deserialize custom protocol format to dictionary
    /// </summary>
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

    /// <summary>
    /// Handle individual client connection
    /// </summary>
    private async Task HandleConnection(TcpClient client, string db)
    {
        var endpoint = client.Client.RemoteEndPoint;
        Console.WriteLine($"Connected With: {endpoint}");
        
        var executor = new SqlExecution(db);
        var stream = client.GetStream();
        var buffer = new byte[1024];

        try
        {
            while (client.Connected)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                string textBody = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                var deserializedObj = Deserialize(textBody);
                
                if (!deserializedObj.TryGetValue("query", out var queryStr))
                    continue;

                var queries = queryStr.Split(';', StringSplitOptions.RemoveEmptyEntries);
                object? response = null;

                foreach (var query in queries)
                {
                    if (!string.IsNullOrWhiteSpace(query))
                    {
                        try
                        {
                            var parser = new SqlParser(query.Trim());
                            executor.Execute(parser);
                            response = executor.Results;
                        }
                        catch (Exception ex)
                        {
                            response = $"Error: {ex.Message}";
                        }
                    }
                }

                var responseBody = new Dictionary<string, object>
                {
                    ["messages"] = response ?? "No result",
                    ["is_json"] = (response is List<Dictionary<string, object>> || response is Dictionary<string, object>) ? 1 : 0
                };

                var responseBytes = Encoding.UTF8.GetBytes(Serialize(responseBody));
                await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection error: {ex.Message}");
        }
        finally
        {
            client.Close();
            Console.WriteLine($"Disconnected: {endpoint}");
        }
    }

    /// <summary>
    /// Read database name from initial client message
    /// </summary>
    private async Task<(string? db, bool connected)> ReadDb(TcpClient client)
    {
        var stream = client.GetStream();
        var buffer = new byte[1024];
        
        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
        var body = Deserialize(Encoding.UTF8.GetString(buffer, 0, bytesRead));

        if (!body.TryGetValue("db", out var dbName) || string.IsNullOrEmpty(dbName))
        {
            var errorResponse = Serialize(new Dictionary<string, object>
            {
                ["message"] = "Invalid Connection",
                ["is_json"] = 0,
                ["con"] = 0
            });
            await stream.WriteAsync(Encoding.UTF8.GetBytes(errorResponse));
            return (null, false);
        }

        var successResponse = Serialize(new Dictionary<string, object>
        {
            ["message"] = $"Connected to {dbName} Successfully !",
            ["is_json"] = 0,
            ["con"] = 1
        });
        await stream.WriteAsync(Encoding.UTF8.GetBytes(successResponse));

        return (dbName, true);
    }

    /// <summary>
    /// Start the TCP server
    /// </summary>
    public async Task Run()
    {
        _server = new TcpListener(IPAddress.Parse("127.0.0.1"), _port);
        _server.Start();
        
        Console.WriteLine($"Server Is Listening on: {_host}:{_port}");

        try
        {
            while (true)
            {
                var client = await _server.AcceptTcpClientAsync();
                var (db, connected) = await ReadDb(client);
                
                if (connected && db != null)
                {
                    _ = Task.Run(() => HandleConnection(client, db));
                }
                else
                {
                    client.Close();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Server error: {ex.Message}");
        }
        finally
        {
            _server.Stop();
            Console.WriteLine("Bye Bye...");
        }
    }
}
