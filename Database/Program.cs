using Database;

// Entry point for the database server
var host = Environment.GetEnvironmentVariable("DBMS_HOST") ?? "0.0.0.0";
var portStr = Environment.GetEnvironmentVariable("DBMS_PORT");
int port = int.TryParse(portStr, out var p) ? p : 9090;

Console.WriteLine($"Starting DBMS Server on {host}:{port}...");
var server = new ConnectionHandler(host, port);
await server.Run();
