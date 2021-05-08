using System;
using System.Collections.Generic;
using Server.Logging;
using System.Net;
using Server.Network;

using LoadTestUO;

namespace Server.Sharding
{
    public static class ChildShard
    {
        private static readonly ILogger logger = LogFactory.GetLogger(typeof(ChildShard));

        private const int m_AuthIDWindowSize = 128;
        private static readonly Dictionary<int, AuthIDPersistence> m_AuthIDWindow =
            new(m_AuthIDWindowSize);

        public static void Initialize()
        {
            Timer.DelayCall(TimeSpan.FromSeconds(3.0), Run);
        }

        public static void Run()
        {
            if (Core.IsChildShard)
            {
                return;
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


        public static bool CheckTravel(Mobile caster, Map map, Point3D loc)
        {
            return true;
        }

        public static IPEndPoint GetIpEndpointForLocation(Point3D Location)
        {
            IPAddress expected = System.Net.IPAddress.Parse("192.168.1.4");
            IPEndPoint address = new System.Net.IPEndPoint(expected, 2593);
            return address;
        }

        public static void OnPlayerMobileLocationChange(Mobile m, Point3D oldLocation)
        {
            Console.WriteLine("PlayerMobile changed location from {0} to {1}", m.Location, oldLocation);

            IPEndPoint childShardEndpoint = GetIpEndpointForLocation(m.Location);
            ServerInfo info = new ServerInfo("child", 0, TimeZoneInfo.Utc, childShardEndpoint);

            NetState state = m.NetState;
            state._authId = GenerateAuthID(state);
            state.SentFirstPacket = false;
            state.SendPlayServerAck(info, state._authId);
        }
        internal struct AuthIDPersistence
        {
            public DateTime Age;
            public readonly ClientVersion Version;

            public AuthIDPersistence(ClientVersion v)
            {
                Age = Core.Now;
                Version = v;
            }
        }

        private static int GenerateAuthID(this NetState state)
        {
            if (m_AuthIDWindow.Count == m_AuthIDWindowSize)
            {
                var oldestID = 0;
                var oldest = DateTime.MaxValue;

                foreach (var kvp in m_AuthIDWindow)
                {
                    if (kvp.Value.Age < oldest)
                    {
                        oldestID = kvp.Key;
                        oldest = kvp.Value.Age;
                    }
                }

                m_AuthIDWindow.Remove(oldestID);
            }

            int authID;

            do
            {
                authID = Utility.Random(1, int.MaxValue - 1);

                if (Utility.RandomBool())
                {
                    authID |= 1 << 31;
                }
            } while (m_AuthIDWindow.ContainsKey(authID));

            m_AuthIDWindow[authID] = new AuthIDPersistence(state.Version);

            return authID;
        }

        public static void HandleParentShardGameLoginRequest(NetState state, int authID)
        {
            logger.Information("Pinging parent server {0}:{1} with child shard login request authID {2}", Core.ParentIp, Core.ParentPort, authID);

            string IpAddress = Core.ParentIp;
            int Port = Core.ParentPort;

            LoadTestUO.ClientVersion clientVersion = LoadTestUO.ClientVersion.CV_705301;
            PacketsTable.AdjustPacketSizeByVersion(clientVersion);

            PacketHandlers.Load();

            NetClient loginClient = new NetClient(true);
            loginClient.Name = "" + 0;
            loginClient.AuthId = authID;
            loginClient.Group = "";// group;
            loginClient.Version = clientVersion;
            //loginClient.Connected += NetClient_ConnectedToServer;

            ConnectNetLoginClientToServer(loginClient, IpAddress, Port);
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
            PSeedChildShard SeedChildShard = new PSeedChildShard(loginClient.AuthId);

            loginClient.Send(SeedChildShard.ToArray(), SeedChildShard.Length, true, true);

            /*
            string Account = loginClient.Group + "Account" + loginClient.Name;
            string Password = loginClient.Group + "Password" + loginClient.Name;
            loginClient.Send(new PFirstLogin(Account, Password));
            */
        }
    }
}
