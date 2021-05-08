using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

using LoadTestUO;

namespace LoadTestUO
{
    class Program
    {
        private static System.Timers.Timer aTimer;

        private static bool IsLoadTestActive = true;
        private static string LoadTestFailureMessage = "";

        private static int NumberOfLoadTestClients = 0;
        private static int NumberOfConnectedLoginClients = 0;

        //load test start/end is a region inside green acres
        public const int LOAD_TEST_START_X = 5500;
        public const int LOAD_TEST_START_Y = 1100;

        public const int LOAD_TEST_END_X = 5600;
        public const int LOAD_TEST_END_Y = 1200;

        static void Main(string[] args)
        {
            string IpAddress = "192.168.1.4";
            int Port = 2593;

            bool PromptUser = true;

            if (PromptUser)
            {
                string serverAddress = "";
                do
                {
                    Console.Write("Is the load test server ip address: " + IpAddress + " correct? (y/n) ");

                    string yesNoResponse = Console.ReadLine();

                    if (yesNoResponse.Contains("n") || yesNoResponse.Contains("N"))
                    {
                        Console.Write("Enter the correct server ip address: ");

                        IpAddress = Console.ReadLine();
                    }
                    else if (yesNoResponse.Contains("y") || yesNoResponse.Contains("Y"))
                    {
                        serverAddress = IpAddress;
                    }
                } while (string.IsNullOrEmpty(serverAddress));

                string serverPort = "";
                do
                {
                    Console.Write("Is the load test server port: " + Port + " correct? (y/n) ");

                    string yesNoResponse = Console.ReadLine();

                    if (yesNoResponse.Contains("n") || yesNoResponse.Contains("N"))
                    {
                        Console.Write("Enter the correct server Port: ");

                        string portResponse = Console.ReadLine();
                        Int32.TryParse(portResponse, out Port);
                    }
                    else if (yesNoResponse.Contains("y") || yesNoResponse.Contains("Y"))
                    {
                        serverPort = "" + Port;
                    }
                } while (string.IsNullOrEmpty(serverPort));

                Int32.TryParse(serverPort, out Port);
            }
            Console.Write("Enter number of load test clients: ");

            string input = Console.ReadLine();
            NumberOfLoadTestClients = 0;
            Int32.TryParse(input, out NumberOfLoadTestClients);
            if (NumberOfLoadTestClients <= 0)
            {
                Console.Write("Invalid number of clients exiting");
                Console.ReadLine();
                return;
            }

            Console.Write("Enter load test group letter (A, B, C, D, etc): ");

            string group = Console.ReadLine().Replace(" ", "");

            if (group.Length > 3)
            {
                group = group.Substring(0, 3);
            }

            ClientVersion clientVersion = ClientVersion.CV_705301;
            PacketsTable.AdjustPacketSizeByVersion(clientVersion);

            PacketHandlers.Load();
            PacketHandlers.ServerListReceivedEvent += ServerListReceived;
            PacketHandlers.ReceiveServerRelayEvent += ReceiveServerRelay;
            PacketHandlers.ReceiveCharacterListEvent += CharacterListReceived;
            PacketHandlers.EnterWorldEvent += EnterWorld;
            PacketHandlers.UpdatePlayerEvent += UpdatePlayer;
            PacketHandlers.ReceiveLoginRejectionEvent += ReceiveLoginRejection;

            //for (int i = 0; i < NumberOfLoadTestClients; i++)
            {
                NetClient loginClient = new NetClient(true);
                loginClient.Name = "" + 0;
                loginClient.Group = group;
                loginClient.Version = clientVersion;
                //loginClient.Connected += NetClient_ConnectedToServer;

                ConnectNetLoginClientToServer(loginClient, IpAddress, Port);
            }

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
        }

        public static Direction NextWalkDirection = Direction.Up;

        private static Direction GetNextWalkDirection()
        {
            if (NextWalkDirection == Direction.Up) { NextWalkDirection = Direction.North; }
            else if (NextWalkDirection == Direction.North) { NextWalkDirection = Direction.Right; }
            else if (NextWalkDirection == Direction.Right) { NextWalkDirection = Direction.East; }
            else if (NextWalkDirection == Direction.East) { NextWalkDirection = Direction.Down; }
            else if (NextWalkDirection == Direction.Down) { NextWalkDirection = Direction.South; }
            else if (NextWalkDirection == Direction.South) { NextWalkDirection = Direction.Left; }
            else if (NextWalkDirection == Direction.Left) { NextWalkDirection = Direction.West; }
            else if (NextWalkDirection == Direction.West) { NextWalkDirection = Direction.Up; }

            return NextWalkDirection;
        }

        private static bool FlipFlop = false;

        private static int DestroyedGameClients = 0;

        private static void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            if (IsLoadTestActive == false)
            {
                return;
            }
            Random rnd = new Random();
            int NumLoggedIn = 0;
            int NumEnteredWorld = 0;
            int NumInitPosition = 0;
            int NumWalking = 0;
            int NumWaitingForWalkConfirm = 0;

            for (int i = 0; i < NetClient.LoadTestNetClientsGame.Count; i++)
            {
                NetClient nc = NetClient.LoadTestNetClientsGame[i];

                if (nc.IsConnected)
                {
                    NumLoggedIn++;

                    if (nc.HasEnteredWorld)
                    {
                        NumEnteredWorld++;

                        if (nc.Walker.IsPlayerAtTeleportLocation())
                        {
                            NumInitPosition++;

                            if (nc.CanRequestWalk)
                            {
                                NumWalking++;
                            }
                            else
                            {
                                NumWaitingForWalkConfirm++;
                            }
                        }
                    }
                }
                else
                {
                    DestroyedGameClients++;
                    NetClient.LoadTestNetClientsGame.RemoveAt(i);
                }
            }

            // Console.Clear();
            Console.WriteLine("LoggedIn {0} EnteredWorld {1} InitPosition {2} CurrentlyWalking {3} NumWaitingForWalkConfirm {4} DestroyedGameClients {5}", NumLoggedIn, NumEnteredWorld, NumInitPosition, NumWalking, NumWaitingForWalkConfirm, DestroyedGameClients);

            for (int i = 0; i < NetClient.LoadTestNetClientsGame.Count; i++)
            {
                NetClient nc = NetClient.LoadTestNetClientsGame[i];

                if (nc.HasEnteredWorld)
                {
                    if (nc.Walker.IsPlayerAtTeleportLocation())
                    {
                        if (nc.CanRequestWalk)
                        {
                            nc.CanRequestWalk = false;//PacketHandler resets this once a ConfirmWalk or DenyWalk is received

                            //First Step points player in direction
                            TakeStep(nc, NextWalkDirection, false);

                            //Second step walks one tile in that direction
                            //TakeStep(nc, NextWalkDirection, false);
                        }
                    }
                    else
                    {
                        nc.RequestTeleportToInitialPositionCount++;

                        if (nc.RequestTeleportToInitialPositionCount == 1 || nc.RequestTeleportToInitialPositionCount % 5 == 0)
                        {
                            int x = rnd.Next(LOAD_TEST_START_X, LOAD_TEST_END_X);
                            int y = rnd.Next(LOAD_TEST_START_Y, LOAD_TEST_END_Y);
                            nc.Walker.TeleportPlayerX = x;
                            nc.Walker.TeleportPlayerY = y;
                            String message = String.Format("[go {0} {1}", x, y);

                            //Console.WriteLine(message);

                            if (nc.Version >= ClientVersion.CV_200)
                            {
                                nc.Send(new PUnicodeSpeechRequest(message, (byte)0, (byte)3, (ushort)690, "ENU"));
                            }
                            else
                            {
                                nc.Send(new PASCIISpeechRequest(message, (byte)0, (byte)3, (ushort)690));
                            }
                        }
                    }
                }
            }

            if (FlipFlop)
            {
                NextWalkDirection = GetNextWalkDirection();
            }

            FlipFlop = !FlipFlop;
        }

        private static void TakeStep(NetClient netClient, Direction direction, bool run)
        {
            /*
            if (direction == Direction.Up) { Console.WriteLine("Direction.North"); }
            if (direction == Direction.North) { Console.WriteLine("Direction.Right"); }
            if (direction == Direction.Right) { Console.WriteLine("Direction.East"); }
            if (direction == Direction.East) { Console.WriteLine("Direction.Down"); }
            if (direction == Direction.Down) { Console.WriteLine("Direction.South"); }
            if (direction == Direction.South) { Console.WriteLine("Direction.Left"); }
            if (direction == Direction.Left) { Console.WriteLine("Direction.West"); }
            if (direction == Direction.West) { Console.WriteLine("Direction.Up"); }
            */
            netClient.Send(new PWalkRequest(direction, netClient.Walker.WalkSequence, run, netClient.Walker.FastWalkStack.GetValue()));
        }

        static void ReceiveLoginRejection(object sender, ReceiveLoginRejectionEventArgs e)
        {
            NetClient Client = (NetClient)sender;
            //IsLoadTestActive = false;
            //LoadTestFailureMessage = e.ErrorMessage;
        }

        static async void ConnectNetLoginClientToServer(NetClient loginClient, string ip, int port)
        {
            if (await loginClient.Connect(ip, (ushort)port))
            {
                if (loginClient.IsConnected)
                {
                    NumberOfConnectedLoginClients++;
                    NetClient.LoadTestNetClientsLogin.Add(loginClient);

                    if (NumberOfConnectedLoginClients < NumberOfLoadTestClients)
                    {
                        NetClient newLoginClient = new NetClient(true);
                        newLoginClient.Name = "" + NumberOfConnectedLoginClients;
                        newLoginClient.Group = loginClient.Group;
                        newLoginClient.Version = loginClient.Version;
                        //newLoginClient.Connected += NetClient_ConnectedToServer;

                        ConnectNetLoginClientToServer(newLoginClient, ip, port);
                    }
                    else
                    {
                        Console.WriteLine("NumberOfConnectedLoginClients " + NumberOfConnectedLoginClients);

                        DetermineNextClientToSendSeedPacket();
                    }
                }
                else
                {
                    Console.WriteLine("Login NetClient failed to connect " + ip);
                }
                //
            }
        }

        static void DetermineNextClientToSendSeedPacket()
        {
            bool foundClientThatHasntSendSeedPacket = false;

            for (int i = 0; i < NetClient.LoadTestNetClientsLogin.Count; i++)
            {
                if (NetClient.LoadTestNetClientsLogin[i].HasSentSeedPacket == false)
                {
                    SendSeedPacket(NetClient.LoadTestNetClientsLogin[i]);
                    NetClient.LoadTestNetClientsLogin[i].HasSentSeedPacket = true;
                    foundClientThatHasntSendSeedPacket = true;
                    break;
                }
            }

            if (foundClientThatHasntSendSeedPacket == false)
            {
                DetermineNextClientToSendServerPacket();
            }
        }

        static void DetermineNextClientToSendServerPacket()
        {
            bool foundClientThatHasntSendServerPacket = false;

            for (int i = 0; i < NetClient.LoadTestNetClientsLogin.Count; i++)
            {
                if (NetClient.LoadTestNetClientsLogin[i].HasSentServerPacket == false)
                {
                    NetClient.LoadTestNetClientsLogin[i].Send(new PSelectServer(0));
                    NetClient.LoadTestNetClientsLogin[i].HasSentServerPacket = true;
                    foundClientThatHasntSendServerPacket = true;
                    break;
                }
            }

            if (foundClientThatHasntSendServerPacket == false)
            {
                DetermineNextClientToSendCharacterPacket();
            }
        }

        static void DetermineNextClientToSendCharacterPacket()
        {
            bool foundClientThatHasntSentCharacterPacket = false;

            for (int i = 0; i < NetClient.LoadTestNetClientsLogin.Count; i++)
            {
                if (NetClient.LoadTestNetClientsLogin[i].HasSentCharacterPacket == false)
                {
                    NetClient.LoadTestNetClientsLogin[i].HasSentCharacterPacket = true;
                    foundClientThatHasntSentCharacterPacket = true;
                    break;
                }
            }

            if (foundClientThatHasntSentCharacterPacket)
            {

            }
        }

        static void SendSeedPacket(NetClient loginClient)
        {
            if (loginClient.Version >= ClientVersion.CV_6040)
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

        static void NetClient_ConnectedToServer(object sender, EventArgs e)
        {
            NetClient netClient = (NetClient)sender;


        }

        static void ServerListReceived(object sender, ServerListReceivedEventArgs e)
        {
            /*
            NetClient netClient = (NetClient)sender;

            foreach(ServerListReceivedEntry entry in e.Servers)
            {
                //select first server from list
                netClient.Send(new PSelectServer(0));
                break;
            }
            */
            DetermineNextClientToSendSeedPacket();
        }

        static void ReceiveServerRelay(object sender, ReceiveServerRelayEventArgs e)
        {
            NetClient loginClient = (NetClient)sender;
            loginClient.Disconnect();
            NetClient.LoadTestNetClientsLogin.Remove(loginClient);

            DetermineNextClientToSendServerPacket();

            bool isLoginClient = false;//VERY IMPORTANT
            NetClient gameNetClient = new NetClient(isLoginClient);
            gameNetClient.Name = loginClient.Name;
            gameNetClient.Group = loginClient.Group;
            gameNetClient.Version = loginClient.Version;
            gameNetClient.Connect(e.ip, e.port)
                            .ContinueWith
                            (t => {
                                if (!t.IsFaulted) { ConnectNetGameClientToServer(gameNetClient, e.seed); }
                            },
                                  TaskContinuationOptions.ExecuteSynchronously
                            );
        }

        static void ConnectNetGameClientToServer(NetClient gameNetClient, uint seed)
        {
            NetClient.LoadTestNetClientsGame.Add(gameNetClient);
            gameNetClient.EnableCompression();

            gameNetClient.Walker.owningNetClient = gameNetClient;

            byte[] ss = new byte[4] { (byte)(seed >> 24), (byte)(seed >> 16), (byte)(seed >> 8), (byte)(seed) };
            gameNetClient.Send(new PacketWriter(ss, 4));

            string Account = gameNetClient.Group + "Account" + gameNetClient.Name;
            string Password = gameNetClient.Group + "Password" + gameNetClient.Name;
            gameNetClient.Send(new PSecondLogin(Account, Password, seed));
        }

        static void CharacterListReceived(object sender, ReceiveCharacterListEventArgs e)
        {
            NetClient Client = (NetClient)sender;
            // Console.WriteLine("Char List Received");

            foreach (string characterName in e.Characters)
            {
                if (string.IsNullOrEmpty(characterName) == false)
                {
                    //login to the first character in the list
                    //Console.WriteLine("CharacterName " + characterName);
                    Client.Send(new PSelectCharacter(0, characterName, NetClient.ClientAddress, (uint)Client.GetClientProtocol()));
                    return;
                }
            }

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
        }

        static void EnterWorld(object sender, EnterWorldEventArgs e)
        {
            NetClient Client = (NetClient)sender;
            Client.HasEnteredWorld = true;
            Client.UniqueId = e.UniqueId;
        }

        static void UpdatePlayer(object sender, UpdatePlayerEventArgs e)
        {
            NetClient Client = (NetClient)sender;
            if (e.Serial == Client.UniqueId)
            {
                Client.Walker.PlayerX = e.X;
                Client.Walker.PlayerY = e.Y;
                Client.Walker.PlayerZ = e.Z;
            }
        }

        static void OnNetworkUpdate()
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

            for (int i = 0; i < NetClient.LoadTestNetClientsGame.Count; i++)
            {
                NetClient nc = NetClient.LoadTestNetClientsGame[i];
                if (nc == null)
                {
                    continue;
                }
                if (!nc.IsDisposed)
                {
                    nc.Update();
                }
            }
        }

        //convert the load test bots number into a unique character name

        private static String[] units = { "Zero", "One", "Two", "Three",
            "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven",
            "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen",
            "Seventeen", "Eighteen", "Nineteen" };

        private static String[] tens = { "", "", "Twenty", "Thirty", "Forty",
            "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

        public static String Convert(Int64 i)
        {
            if (i < 20)
            {
                return units[i];
            }
            if (i < 100)
            {
                return tens[i / 10] + ((i % 10 > 0) ? " " + Convert(i % 10) : "");
            }
            if (i < 1000)
            {
                return units[i / 100] + " Hundred"
                        + ((i % 100 > 0) ? " And " + Convert(i % 100) : "");
            }
            if (i < 100000)
            {
                return Convert(i / 1000) + " Thousand "
                + ((i % 1000 > 0) ? " " + Convert(i % 1000) : "");
            }
            if (i < 10000000)
            {
                return Convert(i / 100000) + " Lakh "
                        + ((i % 100000 > 0) ? " " + Convert(i % 100000) : "");
            }
            if (i < 1000000000)
            {
                return Convert(i / 10000000) + " Crore "
                        + ((i % 10000000 > 0) ? " " + Convert(i % 10000000) : "");
            }
            return Convert(i / 1000000000) + " Arab "
                    + ((i % 1000000000 > 0) ? " " + Convert(i % 1000000000) : "");
        }
    }
}
