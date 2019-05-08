using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using PureWebSockets;

namespace DarkId.Papyrus.DebugAdapterProxy
{
    class Program
    {
        const string LogFileName = "debug-proxy-log.log";

        static void Main(string[] args)
        {
            var client = new PureWebSocket("ws://localhost:2077", new PureWebSocketOptions()
            {
                DebugMode = true
            });

            client.OnMessage += (message) =>
            {
                Console.Write(message);
            };

            client.OnClosed += (message) =>
            {
                Environment.Exit(0);
            };

            if (client.Connect())
            {
                while (client.State == System.Net.WebSockets.WebSocketState.Open)
                {
                    var headers = new Dictionary<string, string>();
                    while (true)
                    {
                        var headerInput = Console.ReadLine();
                        if (string.IsNullOrWhiteSpace(headerInput))
                        {
                            break;
                        }

                        var headerPair = headerInput.Split(':');
                        headers.Add(headerPair[0].Trim(), headerPair[1].Trim());
                    }

                    var contentLength = int.Parse(headers["Content-Length"]);
                    var message = new char[contentLength];

                    for (var i = 0; i < contentLength; i++)
                    {
                        message[i] = (char)Console.Read();
                    }

                    var completedMessage = new string(message);
                    client.Send(completedMessage);
                }
            }
            else
            {
                Environment.Exit(1);
            }
        }
    }
}
