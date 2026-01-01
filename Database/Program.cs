using Database;

// Entry point for the database server
Console.WriteLine("Starting DBMS Server...");
var server = new ConnectionHandler();
await server.Run();
