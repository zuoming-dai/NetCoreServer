using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NetCoreServer;
using Serilog;
using Serilog.Core;

namespace A2SServer
{
    internal class A2SServer : UdpServer
    {
        private Logger log;
        private const byte A2SInfoRequest = 0x54;
        private const byte A2SInfoResponse = 0x49;
        private const byte A2SPlayerRequest = 0x55;
        private const byte A2SPlayerResponse = 0x44;
        private const byte A2SRuleRequest = 0x56;
        private const byte A2SRuleResponse = 0x45;
        private const byte A2SChallengeRequest = 0x57;
        private const byte A2SChallengeResponse = 0x41;

        public A2SServer(Logger log, IPAddress address, int port) : base(address, port)
        {
            this.log = log;
        }

        protected override void OnStarted()
        {
            // Start receive datagrams
            ReceiveAsync();
        }

        private byte[] Info()
        {
            log.Information("Returning A2S_INFO");
            using var stream = new MemoryStream();
            using var binWriter = new BinaryWriter(stream);
            // package header
            binWriter.Write(-1);

            // header
            binWriter.Write(A2SInfoResponse);
            // protocol
            binWriter.Write((byte) 3);
            // name
            binWriter.Write(Encoding.UTF8.GetBytes("TestGameServer1"));
            binWriter.Write((byte) 0);

            // map
            binWriter.Write(Encoding.UTF8.GetBytes("MapA"));
            binWriter.Write((byte) 0);

            // folder
            binWriter.Write(Encoding.UTF8.GetBytes("-"));
            binWriter.Write((byte) 0);

            // game
            binWriter.Write(Encoding.UTF8.GetBytes("TestGame"));
            binWriter.Write((byte) 0);

            // id
            binWriter.Write((short) 1);

            // players
            binWriter.Write((byte) 10);

            // maxPlayers
            binWriter.Write((byte) 64);

            // bots
            binWriter.Write((byte) 0);

            // serverType
            binWriter.Write((byte) 'd');

            // environment
            binWriter.Write((byte) 'l');

            // visibility
            binWriter.Write((byte) 1);

            // VAC
            binWriter.Write((byte) 0);

            // version
            binWriter.Write(Encoding.UTF8.GetBytes("1.0.1"));
            binWriter.Write((byte) 0);

            return stream.ToArray();
        }

        private  byte[] Player()
        {
            log.Information("Returning A2S_PLAYER");
            using var stream = new MemoryStream();
            using var binWriter = new BinaryWriter(stream);
            // package header
            binWriter.Write(-1);

            // header
            binWriter.Write(A2SPlayerResponse);

            byte count = 10;
            // count
            binWriter.Write(count);

            // players
            for (byte i = 0; i < count; i++)
            {
                // index
                binWriter.Write(i);

                var name = "player" + i;
                // name
                binWriter.Write(Encoding.UTF8.GetBytes(name));
                binWriter.Write((byte) 0);

                // score
                binWriter.Write(32);

                // duration
                binWriter.Write(3.45f);
            }

            return stream.ToArray();
        }

        private byte[] Rule()
        {
            log.Information("Returning A2S_RULE");
            using var stream = new MemoryStream();
            using var binWriter = new BinaryWriter(stream);
            // package header
            binWriter.Write(-1);

            // header
            binWriter.Write(A2SRuleResponse);

            // count
            short count = 16;
            binWriter.Write(count);
            for (short i = 0; i < count; i++)
            {
                // name
                var name = "rule" + i;
                binWriter.Write(Encoding.UTF8.GetBytes(name));
                binWriter.Write((byte) 0);

                // value
                var value = "value" + i;
                binWriter.Write(Encoding.UTF8.GetBytes(value));
                binWriter.Write((byte) 0);
            }

            return stream.ToArray();
        }

        private byte[] Challenge()
        {
            log.Information("Returning A2S_SERVERQUERY_GETCHALLENGE");
            using var stream = new MemoryStream();
            using var binWriter = new BinaryWriter(stream);

            // package header
            binWriter.Write(-1);

            // header
            binWriter.Write(A2SChallengeResponse);

            // challenge
            binWriter.Write(1);
            return stream.ToArray();
        }

        protected override void OnReceived(EndPoint endpoint, byte[] buffer, long offset, long size)
        {
            var response = buffer[4] switch
            {
                A2SInfoRequest =>
                    // info
                    Info(),
                A2SPlayerRequest =>
                    // player
                    Player(),
                A2SRuleRequest =>
                    // rule
                    Rule(),
                A2SChallengeRequest =>
                    // challenge
                    Challenge(),
                _ => buffer
            };

            // Send back
            SendAsync(endpoint, response, 0, response.Length);
        }

        protected override void OnSent(EndPoint endpoint, long sent)
        {
            // Continue receive datagrams
            ReceiveAsync();
        }

        protected override void OnError(SocketError error)
        {
            log.Error($"A2S UDP server caught an error with code {error}");
        }
    }

    class Program
    {
        // Sample args:
        // /usr/local/games/TestLinux/builda/A2SServer -config=23097/serverconfig.json -log=23097/logs -server_host_port=55101
        static void Main(string[] args)
        {
            // UDP server port
            int port = 3333; // default static port
            const int testLinuxQueryPortOffset = 3;
            const string serverHostPortArgPrefix = "-server_host_port=";

            var logPath = "/a2s_server.log";
            const string logArgPrefix = "-log=";
            
            if (args != null && args.Length > 0)
            {
                foreach (string arg in args)
                {
                    if (arg.StartsWith(serverHostPortArgPrefix))
                    {
                        int gamePort = Int32.Parse(arg.Substring(serverHostPortArgPrefix.Length));
                        port = gamePort + testLinuxQueryPortOffset;
                    }
                    
                    if (arg.StartsWith(logArgPrefix))
                    {
                        string logPathFromInput = arg.Substring(logArgPrefix.Length);
                        if (logPath.EndsWith('/'))
                        {
                            logPathFromInput = logPathFromInput.Substring(0, logPathFromInput.Length - 1);
                        }
                        logPath = logPathFromInput + logPath;
                    }
                }
            }
            Logger log = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Hour)
                .CreateLogger();
            log.Debug($"Log path is {logPath}");

            log.Debug($"UDP server port: {port}");

            // Create a new UDP a2s server
            var server = new A2SServer(log, IPAddress.Any, port);

            // Start the server
            log.Debug("Server starting...");
            server.Start();
            log.Debug("Done!");

            log.Debug("Press Enter to stop the server or '!' to restart the server...");

            // Perform text input
            for (;;)
            {
                var line = Console.ReadLine();
                if (string.IsNullOrEmpty(line))
                    break;

                // Restart the server
                if (line == "!")
                {
                    log.Debug("Server restarting...");
                    server.Restart();
                    log.Debug("Done!");
                }
            }

            // Stop the server
            log.Debug("Server stopping...");
            server.Stop();
            log.Debug("Done!");
        }
    }
}
