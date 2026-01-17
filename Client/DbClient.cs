using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Client;

/// <summary>
/// Interactive console client for the DBMS server
/// </summary>
public class DbClient
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private readonly string _host;
    private readonly int _port;

    public DbClient(string host = "localhost", int port = 9090)
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
            sb.AppendLine($"{kvp.Key}:>{kvp.Value}");
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
    /// Connect to the database server
    /// </summary>
    public bool Connect(string database)
    {
        try
        {
            Console.WriteLine($"Trying to connect to database: {database}");
            
            _client = new TcpClient(_host, _port);
            _stream = _client.GetStream();

            // Send database connection request
            var connectMsg = $"db:>{database}\n";
            var connectBytes = Encoding.UTF8.GetBytes(connectMsg);
            _stream.Write(connectBytes, 0, connectBytes.Length);

            // Read connection response
            var buffer = new byte[1024];
            int bytesRead = _stream.Read(buffer, 0, buffer.Length);
            var response = Deserialize(Encoding.UTF8.GetString(buffer, 0, bytesRead));

            if (response.TryGetValue("con", out var conValue) && conValue == "1")
            {
                Console.WriteLine($"Connected to database: {database}");
                return true;
            }
            else
            {
                Console.WriteLine($"Could not connect to database: {database}");
                Console.WriteLine($"Reason: {response.GetValueOrDefault("message", "Unknown Error")}");
                Close();
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Send a query and receive results
    /// </summary>
    public void SendQuery(string query)
    {
        if (_stream == null || _client == null || !_client.Connected)
        {
            Console.WriteLine("Not connected to server");
            return;
        }

        try
        {
            // Send query
            var queryMsg = $"query:>{query}\n";
            var queryBytes = Encoding.UTF8.GetBytes(queryMsg);
            _stream.Write(queryBytes, 0, queryBytes.Length);

            // Read response
            var buffer = new byte[4096];
            int bytesRead = _stream.Read(buffer, 0, buffer.Length);
            var response = Deserialize(Encoding.UTF8.GetString(buffer, 0, bytesRead));

            // Parse and display results
            if (response.TryGetValue("is_json", out var isJson) && isJson == "1")
            {
                if (response.TryGetValue("messages", out var messages))
                {
                    try
                    {
                        var jsonDoc = JsonDocument.Parse(messages);
                        Console.WriteLine(JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true }));
                    }
                    catch
                    {
                        Console.WriteLine(messages);
                    }
                }
            }
            else
            {
                Console.WriteLine(response.GetValueOrDefault("messages", "No response"));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Query error: {ex.Message}");
        }
    }

    /// <summary>
    /// Close the connection
    /// </summary>
    public void Close()
    {
        _stream?.Close();
        _client?.Close();
    }

    /// <summary>
    /// Run interactive REPL
    /// </summary>
    public void RunRepl(string database)
    {
        if (!Connect(database))
        {
            return;
        }

        while (true)
        {
            Console.Write($"{database} >>> ");
            var query = Console.ReadLine();

            if (string.IsNullOrEmpty(query))
                continue;

            if (query.ToLower() == "exit")
            {
                Close();
                break;
            }

            SendQuery(query);
        }
    }
}
