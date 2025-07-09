using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MainServer
{
    internal class Server
    {
        static void Main(string[] args)
        {
            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint hostingEndpoint = new IPEndPoint(IPAddress.Any, 11552);

            serverSocket.Bind(hostingEndpoint);
            serverSocket.Blocking = false;

            Console.WriteLine($"Server je pokrenut i ceka poruku na :{hostingEndpoint}");
        }
    }
}
