using System;
using System.Collections.Generic;
using Server.Logging;
using LoadTestUO;

namespace Server.Sharding
{
    public static class ChildShard
    {
        private static readonly ILogger logger = LogFactory.GetLogger(typeof(ChildShard));

        public static void Initialize()
        {
            Timer.DelayCall(TimeSpan.FromSeconds(3.0), Run);
        }

        public static void Run()
        {
            if (Core.IsChildShard)
            {
                logger.Information("ChildShard: Pinging parent server {0}:{1} to let them know we exist.", Core.ParentIp, Core.ParentPort);

                string IpAddress = Core.ParentIp;
                int Port = Core.ParentPort;

                LoadTestUO.ClientVersion clientVersion = LoadTestUO.ClientVersion.CV_705301;
                PacketsTable.AdjustPacketSizeByVersion(clientVersion);

                PacketHandlers.Load();
                /*
                PacketHandlers.ServerListReceivedEvent += ServerListReceived;
                PacketHandlers.ReceiveServerRelayEvent += ReceiveServerRelay;
                PacketHandlers.ReceiveCharacterListEvent += CharacterListReceived;
                PacketHandlers.EnterWorldEvent += EnterWorld;
                PacketHandlers.UpdatePlayerEvent += UpdatePlayer;
                PacketHandlers.ReceiveLoginRejectionEvent += ReceiveLoginRejection;
                */
                //for (int i = 0; i < NumberOfLoadTestClients; i++)
                {
                    NetClient loginClient = new NetClient(true);
                    loginClient.Name = "" + 0;
                    loginClient.Group = "";// group;
                    loginClient.Version = clientVersion;
                    //loginClient.Connected += NetClient_ConnectedToServer;

                    ConnectNetLoginClientToServer(loginClient, IpAddress, Port);
                }
                /*
                // Create a timer with a one second interval.
                aTimer = new System.Timers.Timer(1000);
                aTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
                aTimer.Enabled = true;

                while (IsLoadTestActive)
                {
                    OnNetworkUpdate();
                }

                if (string.IsNullOrEmpty(LoadTestFailureMessage) == false)
                {
                    Console.WriteLine("Test failed: " + LoadTestFailureMessage);
                    Console.ReadLine();
                }
                */
            }
        }

        static async void ConnectNetLoginClientToServer(NetClient loginClient, string ip, int port)
        {
            if (await loginClient.Connect(ip, (ushort)port))
            {
                if (loginClient.IsConnected)
                {
                    NetClient.LoadTestNetClientsLogin.Add(loginClient);

                    Console.WriteLine("Connected to parent server successfully!");

                    SendSeedPacket(loginClient);
                }
                else
                {
                    Console.WriteLine("Login NetClient failed to connect " + ip);
                }
                //
            }
        }

        static void SendSeedPacket(NetClient loginClient)
        {
            if (loginClient.Version >= LoadTestUO.ClientVersion.CV_6040)
            {
                uint clientVersion = (uint)loginClient.Version;

                byte major = (byte)(clientVersion >> 24);
                byte minor = (byte)(clientVersion >> 16);
                byte build = (byte)(clientVersion >> 8);
                byte extra = (byte)clientVersion;

                PSeed packet = new PSeed
                (
                    NetClient.ClientAddress,
                    major,
                    minor,
                    build,
                    extra
                );

                loginClient.Send(packet.ToArray(), packet.Length, true, true);
            }
            else
            {
                uint address = NetClient.ClientAddress;

                // TODO: stackalloc
                byte[] packet = new byte[4];
                packet[0] = (byte)(address >> 24);
                packet[1] = (byte)(address >> 16);
                packet[2] = (byte)(address >> 8);
                packet[3] = (byte)address;

                loginClient.Send(packet, packet.Length, true, true);
            }

            string Account = loginClient.Group + "Account" + loginClient.Name;
            string Password = loginClient.Group + "Password" + loginClient.Name;
            loginClient.Send(new PFirstLogin(Account, Password));
        }
    }
}
