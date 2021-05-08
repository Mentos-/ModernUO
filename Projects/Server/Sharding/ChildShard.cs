using System;
using System.Collections.Generic;
using Server.Logging;
using System.Net;

using Server.Accounting;
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

        public static void HandleLoginServerAuth(NetState state, CircularBufferReader reader, ref int packetLength)
        {
            int authID = reader.ReadInt32();
            //this is the unused ClientVersion of the client trying to switch between Parent and Child shards
            //at this time we are only communicating with ChildShard's packet handler so we do not need it
            //state.Version = new ClientVersion(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());

            logger.Information("ChildShard: Received login request pinging parent server with authID {0}", authID);

            //this is the ClientVersion of our PacketHandler that talks from ChildShard to ParentShard
            LoadTestUO.ClientVersion clientVersion = LoadTestUO.ClientVersion.CV_705301;
            PacketsTable.AdjustPacketSizeByVersion(clientVersion);
            PacketHandlers.Load();

            NetClient loginClient = new NetClient(true);
            loginClient.Name = "" + 0;
            loginClient.AuthId = authID;
            loginClient.Group = "";// group;
            loginClient.Version = clientVersion;

            ConnectChildShardToParentShard(loginClient, ParentIp, ParentPort);
        }

        static async void ConnectChildShardToParentShard(NetClient loginClient, string ip, int port)
        {
            if (await loginClient.Connect(ip, (ushort)port))
            {
                if (loginClient.IsConnected)
                {
                    NetClient.LoadTestNetClientsLogin.Add(loginClient);

                    Console.WriteLine("Connected to parent server successfully!");

                    SendAuthIdPacket(loginClient);
                }
                else
                {
                    Console.WriteLine("Connected to parent server failed " + ip);
                }
            }
        }

        static void SendAuthIdPacket(NetClient loginClient)
        {
            PSeedChildShard SeedChildShard = new PSeedChildShard(loginClient.AuthId);

            loginClient.Send(SeedChildShard.ToArray(), SeedChildShard.Length, true, true);
        }

        public static void ReceiveAccountInfo(string account, string password)
        {
            logger.Information("ChildShard: ReceiveAccountInfo account: {0} password: {1}", account, password);
            /*
            if (!(Accounts.GetAccount(account) is Account acct))
            {
                // To prevent someone from making an account of just '' or a bunch of meaningless spaces
                if (AutoAccountCreation && un.Trim().Length > 0)
                {
                    e.State.Account = acct = CreateAccount(e.State, un, pw);
                    e.Accepted = acct?.CheckAccess(e.State) ?? false;

                    if (!e.Accepted)
                    {
                        e.RejectReason = ALRReason.BadComm;
                    }
                }
                else
                {
                    logger.Information("Login: {0}: Invalid username '{1}'", e.State, un);
                    e.RejectReason = ALRReason.Invalid;
                }
            }*/
        }

        public static void Tick()
        {
            for (int i = 0; i < NetClient.LoadTestNetClientsLogin.Count; i++)
            {
                NetClient nc = NetClient.LoadTestNetClientsLogin[i];
                if (nc == null)
                {
                    continue;
                }
                if (nc.IsDisposed && nc.IsConnected)
                {
                    nc.Disconnect();
                }
                else if (nc.IsConnected)
                {
                    nc.Update();
                }
                else
                {
                    Console.WriteLine("LoginTestNetClient is not connected " + nc.Name);
                }
            }
        }

    }
}
