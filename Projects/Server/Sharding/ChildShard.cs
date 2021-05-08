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

        public static void HandleLoginServerAuth(NetState state, int authID)
        {
            logger.Information("ChildShard: Received login request pinging parent server with change server authID {0}", authID);

            LoadTestUO.ClientVersion clientVersion = LoadTestUO.ClientVersion.CV_705301;
            PacketsTable.AdjustPacketSizeByVersion(clientVersion);

            PacketHandlers.Load();
            PacketHandlers.ReceiveCharacterListEvent += CharacterListReceived;
            PacketHandlers.ReceiveLoginRejectionEvent += ReceiveLoginStatus;

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

        static void ReceiveLoginStatus(object sender, ReceiveLoginRejectionEventArgs e)
        {
            NetClient Client = (NetClient)sender;
            Console.WriteLine("ReceiveLoginStatus " + e.ErrorMessage);
            //IsLoadTestActive = false;
            //LoadTestFailureMessage = e.ErrorMessage;
        }

        static void CharacterListReceived(object sender, ReceiveCharacterListEventArgs e)
        {
            NetClient Client = (NetClient)sender;
            logger.Information("Char List Received " + e.ToString());

            foreach (string characterName in e.Characters)
            {
                if (string.IsNullOrEmpty(characterName) == false)
                {
                    logger.Information("characterName: " + characterName);
                }
            }
            /*
            //otherwise create a new character
            ClientVersion clientVersion = Client.Version;
            uint clientProtocol = (uint)Client.GetClientProtocol();
            string newCharacterName = "" + Client.Name;// Convert(Int64.Parse(Client.Name));//convert the load test bots number into a unique character name
            byte characterStrength = 20;
            byte characterIntelligence = 20;
            byte characterDexterity = 20;
            List<CreateCharacterSkill> characterSkills = new List<CreateCharacterSkill>();
            characterSkills.Add(new CreateCharacterSkill("Alchemy", 0, 0));
            characterSkills.Add(new CreateCharacterSkill("Magery", 25, 30));
            characterSkills.Add(new CreateCharacterSkill("Meditation", 46, 30));
            characterSkills.Add(new CreateCharacterSkill("Wrestling", 43, 30));
            Flags characterFlags = 0;
            byte characterRace = 0;
            ushort characterHue = 0;
            ushort characterHairHue = 0;
            ushort characterHairGraphic = 0;
            ushort characterBeardHue = 0;
            ushort characterBeardGraphic = 0;
            ushort characterShirtHue = 0;
            ushort characterPantsHue = 0;
            int cityIndex = 0;
            uint clientIP = NetClient.ClientAddress;
            int serverIndex = 0;
            uint slot = 0;
            byte profession = 0;

            PCreateCharacter newCharacter = new PCreateCharacter
                (
                clientVersion,
                clientProtocol,
                newCharacterName,
                characterStrength,
                characterIntelligence,
                characterDexterity,
                characterSkills,
                characterFlags,
                characterRace,
                characterHue,
                characterHairHue,
                characterHairGraphic,
                characterBeardHue,
                characterBeardGraphic,
                characterShirtHue,
                characterPantsHue,
                cityIndex,
                clientIP,
                serverIndex,
                slot,
                profession);

            Client.Send(newCharacter);
            */
        }
    }
}
