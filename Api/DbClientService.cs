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

   
   
    
}
