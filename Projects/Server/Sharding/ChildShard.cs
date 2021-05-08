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
        public static string ParentIp = "192.168.1.172";
        public static int ParentPort = 2583;

        private static readonly ILogger logger = LogFactory.GetLogger(typeof(ChildShard));

        public static void Initialize()
        {
            Timer.DelayCall(TimeSpan.FromSeconds(3.0), Run);
        }

        public static void Run()
        {
            if (Core.IsChildShard)
            {
                logger.Information("Server is configured as a child shard with parent server at {0}:{1} ", ParentIp, ParentPort);
            }
        }

        public static void HandleParentShardGameLoginRequest(NetState state, int authID)
        {
            logger.Information("ChildShard: Received login request pinging parent server with change server authID {0}", authID);

            LoadTestUO.ClientVersion clientVersion = LoadTestUO.ClientVersion.CV_705301;
            PacketsTable.AdjustPacketSizeByVersion(clientVersion);

            PacketHandlers.Load();

            NetClient loginClient = new NetClient(true);
            loginClient.Name = "" + 0;
            loginClient.AuthId = authID;
            loginClient.Group = "";// group;
            loginClient.Version = clientVersion;
            //loginClient.Connected += NetClient_ConnectedToServer;

            ConnectNetLoginClientToServer(loginClient, ParentIp, ParentPort);
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
