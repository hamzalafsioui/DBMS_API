using Client;

// Entry point for the database client
Console.Write("Please Enter db: ");
var database = Console.ReadLine();

if (string.IsNullOrEmpty(database))
{
    Console.WriteLine("Database name is required");
    return;
}

var host = Environment.GetEnvironmentVariable("DBMS_HOST") ?? "localhost";
var portStr = Environment.GetEnvironmentVariable("DBMS_PORT");
int port = int.TryParse(portStr, out var p) ? p : 9090;

var client = new DbClient(host, port);
client.RunRepl(database);
