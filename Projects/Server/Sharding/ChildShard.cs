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

        private static Dictionary<int, NetState> NetStateChildShardAuthId = new Dictionary<int, NetState>();

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

        public static void OnPlayerMobileLocationChange(Mobile m, Point3D oldLocation)
        {
            logger.Information("PlayerMobile {0} changed location from {1} to {2}", m.Name, m.Location, oldLocation);
        }

        public static void HandleLoginServerAuth(NetState state, CircularBufferReader reader, ref int packetLength)
        {
            int authID = reader.ReadInt32();
            state._seed = authID;
            state.Seeded = true;

            if (state._seed == 0)
            {
                state.LogInfo("Invalid client detected, disconnecting");
                state.Disconnect("Invalid seed sent.");
                return;
            }

            var clientMaj = reader.ReadInt32();
            var clientMin = reader.ReadInt32();
            var clientRev = reader.ReadInt32();
            var clientPat = reader.ReadInt32();

            state.Version = new ClientVersion(clientMaj, clientMin, clientRev, clientPat);

            logger.Information("ChildShard: Received login request with authID {0} ClientVersion {1}.{2}.{3}.{4}", authID, clientMaj, clientMin, clientRev, clientPat);

            //this is the ClientVersion of our PacketHandler that talks from ChildShard to ParentShard
            LoadTestUO.ClientVersion clientVersion = LoadTestUO.ClientVersion.CV_705301;
            PacketsTable.AdjustPacketSizeByVersion(clientVersion);
            PacketHandlers.Load();
            PacketHandlers.ReceiveAccountInfoEvent += ReceiveAccountInfo;

            NetClient loginClient = new NetClient(true);
            loginClient.Name = "" + 0;
            loginClient.AuthId = authID;
            loginClient.Group = "";// group;
            loginClient.Version = clientVersion;

            NetStateChildShardAuthId.Add(authID, state);

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

        static void ReceiveAccountInfo(object sender, ReceiveAccountInfoArgs e)
        {
            string account = e.Account;
            string password = e.Password;
            NetClient netClient = e.NetClient;

            logger.Information("ChildShard: ReceiveAccountInfo account: {0} password: {1}", account, password);

            NetState childShardNetState;

            int authIdFromRequest = int.Parse(password);

            if (NetStateChildShardAuthId.TryGetValue(authIdFromRequest, out childShardNetState))
            {
                var accountLoginEventArgs = new AccountLoginEventArgs(childShardNetState, account, password);

                EventSink.InvokeAccountLogin(accountLoginEventArgs);

                if (accountLoginEventArgs.Accepted)
                {
                    logger.Information("ChildShard: Successfull account creation: {0} password: {1}", account, password);

                    var serverListEventArgs = new ServerListEventArgs(childShardNetState, childShardNetState.Account);

                    EventSink.InvokeServerList(serverListEventArgs);

                    if (serverListEventArgs.Rejected)
                    {
                        childShardNetState.Account = null;
                        childShardNetState.SendAccountLoginRejected(ALRReason.BadComm);
                        childShardNetState.Disconnect($"Account login rejected due to {ALRReason.BadComm}");
                    }
                    else
                    {
                        childShardNetState.ServerInfo = serverListEventArgs.Servers.ToArray();
                        childShardNetState.SendAccountLoginAck();
                    }

                    netClient.ClientNetState = childShardNetState;

                    CreateCharacter(netClient);
                }
                else
                {
                    logger.Information("ChildShard: Failed account creation: {0} password: {1}", account, password);
                }
            }
            else
            {
                logger.Information("ChildShard: Could not find valid NetState for authId {0}", authIdFromRequest);
            }
        }

        public static void CreateCharacter(NetClient netClient)
        {
            //otherwise create a new character
            string characterName = "George";
            bool female = false;
            byte characterStrength = 20;
            byte characterIntelligence = 20;
            byte characterDexterity = 20;
            SkillNameValue[]  skills = new[]
            {
                new SkillNameValue((SkillName)0, 0),
                new SkillNameValue((SkillName)25, 30),
                new SkillNameValue((SkillName)46, 30),
                new SkillNameValue((SkillName)43, 30)
            };

            Server.Race characterRace = Server.Race.Human;
            ushort characterHue = 0;
            ushort characterHairHue = 0;
            ushort characterHairGraphic = 0;
            ushort characterBeardHue = 0;
            ushort characterBeardGraphic = 0;
            ushort characterShirtHue = 0;
            ushort characterPantsHue = 0;
            CityInfo cityInfo = new CityInfo("Britain", "Sweet Dreams Inn", 1496, 1628, 10);
            uint clientIP = NetClient.ClientAddress;
            byte profession = 0;

            var args = new CharacterCreatedEventArgs(
                netClient.ClientNetState,
                netClient.ClientNetState.Account,
                characterName,
                female,
                characterHue,
                characterStrength,
                characterDexterity,
                characterIntelligence,
                cityInfo,
                skills,
                characterShirtHue,
                characterPantsHue,
                characterHairGraphic,
                characterHairHue,
                characterBeardGraphic,
                characterBeardHue,
                profession,
                characterRace
            );

            netClient.ClientNetState.SendClientVersionRequest();

            EventSink.InvokeCharacterCreated(args);

            var m = args.Mobile;

            if (m != null)
            {
                netClient.ClientNetState.Mobile = m;
                m.NetState = netClient.ClientNetState;

                netClient.DoCharacterLogin = true;
            }
            else
            {
                netClient.ClientNetState.BlockAllPackets = false;
                netClient.ClientNetState.Disconnect("Character creation blocked.");
            }

            logger.Information("ChildShard: Invoking character creation {0}", characterName);
        }

        public static void DoLogin(this NetState state, Mobile m)
        {
            state._protocolState = NetState.ProtocolState.GameServer_LoggedIn;

            state.SendLoginConfirmation(m);

            state.SendMapChange(m.Map);

            state.SendMapPatches();

            state.SendSeasonChange((byte)m.GetSeason(), true);

            state.SendSupportedFeature();

            state.Sequence = 0;

            state.SendMobileUpdate(m);
            state.SendMobileUpdate(m);

            m.CheckLightLevels(true);

            state.SendMobileUpdate(m);

            state.SendMobileIncoming(m, m);

            state.SendMobileStatus(m);
            state.SendSetWarMode(m.Warmode);

            m.SendEverything();

            state.SendSupportedFeature();
            state.SendMobileUpdate(m);

            state.SendMobileStatus(m);
            state.SendSetWarMode(m.Warmode);
            state.SendMobileIncoming(m, m);

            state.SendLoginComplete();
            state.SendCurrentTime();
            state.SendSeasonChange((byte)m.GetSeason(), true);
            state.SendMapChange(m.Map);

            EventSink.InvokeLogin(m);

/*            state.CompressionEnabled = true;
            state.PacketEncoder ??= NetworkCompression.Compress;*/
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

                    if (nc.DoCharacterLogin)
                    {
                        if(nc.ClientNetState != null && nc.ClientNetState.Mobile != null)
                        {
                            nc.DoCharacterLogin = false;
                            DoLogin(nc.ClientNetState, nc.ClientNetState.Mobile);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("LoginTestNetClient is not connected " + nc.Name);
                }
            }
        }

    }
}
