#region license

// Copyright (c) 2021, andreakarasho
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 1. Redistributions of source code must retain the above copyright
//    notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
//    notice, this list of conditions and the following disclaimer in the
//    documentation and/or other materials provided with the distribution.
// 3. All advertising materials mentioning features or use of this software
//    must display the following acknowledgement:
//    This product includes software developed by andreakarasho - https://github.com/andreakarasho
// 4. Neither the name of the copyright holder nor the
//    names of its contributors may be used to endorse or promote products
//    derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS ''AS IS'' AND ANY
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Net;
using System.Numerics;

namespace LoadTestUO
{
    [Flags]
    internal enum Flags : byte
    {
        None,
        Frozen = 0x01,
        Female = 0x02,
        Poisoned = 0x04,
        Flying = 0x04,
        YellowBar = 0x08,
        IgnoreMobiles = 0x10,
        Movable = 0x20,
        WarMode = 0x40,
        Hidden = 0x80
    }

    [Flags]
    internal enum Direction : byte
    {
        North = 0x00,
        Right = 0x01,
        East = 0x02,
        Down = 0x03,
        South = 0x04,
        Left = 0x05,
        West = 0x06,
        Up = 0x07,
        Mask = 0x7,
        Running = 0x80,
        NONE = 0xED
    }

    [Flags]
    internal enum ClientFlags : uint
    {
        CF_T2A = 0x00,
        CF_RE = 0x01,
        CF_TD = 0x02,
        CF_LBR = 0x04,
        CF_AOS = 0x08,
        CF_SE = 0x10,
        CF_SA = 0x20,
        CF_UO3D = 0x40,
        CF_RESERVED = 0x80,
        CF_3D = 0x100,
        CF_UNDEFINED = 0xFFFF
    }

    internal class ServerListReceivedEntry
    {
        public IPAddress IpAddress;
        public int Index;
        public string Name;
        public int PercentFull;
        public int Timezone;

        public ServerListReceivedEntry(ref PacketBufferReader p)
        {
            Index = p.ReadUShort();
            Name = p.ReadASCII(32);
            PercentFull = p.ReadByte();
            Timezone = p.ReadByte();
            IpAddress = new IPAddress(p.ReadUInt());
        }
    }

    internal class ServerListReceivedEventArgs : EventArgs
    {
        public byte Flags;
        public List<ServerListReceivedEntry> Servers;

        internal ServerListReceivedEventArgs(NetClient netClient, ref PacketBufferReader p)
        {
            Flags = p.ReadByte();
            ushort count = p.ReadUShort();
            Servers = new List<ServerListReceivedEntry>(count);
            for (int i = 0; i < count; i++)
            {
                Servers.Add(new ServerListReceivedEntry(ref p));
            }
        }
    }

    public sealed class ReceiveServerRelayEventArgs : EventArgs
    {
        public IPAddress ip;
        public ushort port;
        public uint seed;

        internal ReceiveServerRelayEventArgs(NetClient netClient, ref PacketBufferReader p)
        {
            byte[] ipBytes = { p.ReadByte(), p.ReadByte(), p.ReadByte(), p.ReadByte() };
            ip = new IPAddress(ipBytes);
            port = p.ReadUShort();
            seed = p.ReadUInt();
        }
    }

    internal class OldCityLocation
    {
        public int X;
        public int Y;

        public OldCityLocation(int inX, int inY)
        {
            X = inX;
            Y = inY;
        }
    }

    internal class CityInfo
    {
        public CityInfo
        (
            int index,
            string city,
            string building,
            uint descriptionCliloc,
            ushort x,
            ushort y,
            sbyte z,
            uint map,
            bool isNew
        )
        {
            Index = index;
            City = city;
            Building = building;
            DescriptionCliloc = descriptionCliloc;
            X = x;
            Y = y;
            Z = z;
            Map = map;
            IsNewCity = isNew;
        }

        public readonly string Building;
        public readonly string City;
        public readonly uint DescriptionCliloc;
        public readonly int Index;
        public readonly bool IsNewCity;
        public readonly uint Map;
        public readonly ushort X, Y;
        public readonly sbyte Z;
    }

    internal sealed class ReceiveCharacterListEventArgs : EventArgs
    {
        public List<string> Characters;
        public List<CityInfo> Cities;
        uint CharacterListFeatureFlag;

        internal ReceiveCharacterListEventArgs(NetClient netClient, ref PacketBufferReader p)
        {
            ParseCharacterList(ref p);
            ParseCities(netClient, ref p);
            CharacterListFeatureFlag = p.ReadUInt();
        }

        internal void ParseCharacterList(ref PacketBufferReader p)
        {
            int count = p.ReadByte();
            Characters = new List<string>(count);

            for (int i = 0; i < count; i++)
            {
                Characters.Add(p.ReadASCII(30).TrimEnd('\0'));

                p.Skip(30);
            }
        }

        private void ParseCities(NetClient netClient, ref PacketBufferReader p)
        {
            byte count = p.ReadByte();
            Cities = new List<CityInfo>(count);

            bool isNew = netClient.Version >= ClientVersion.CV_70130;

            for (int i = 0; i < count; i++)
            {
                CityInfo cityInfo;

                if (isNew)
                {
                    byte cityIndex = p.ReadByte();
                    string cityName = p.ReadASCII(32);
                    string cityBuilding = p.ReadASCII(32);
                    ushort cityX = (ushort)p.ReadUInt();
                    ushort cityY = (ushort)p.ReadUInt();
                    sbyte cityZ = (sbyte)p.ReadUInt();
                    uint cityMapIndex = p.ReadUInt();
                    uint cityDescriptionCliloc = p.ReadUInt();
                    p.Skip(4);

                    cityInfo = new CityInfo
                    (
                        cityIndex,
                        cityName,
                        cityBuilding,
                        cityDescriptionCliloc,
                        cityX,
                        cityY,
                        cityZ,
                        cityMapIndex,
                        isNew
                    );
                }
                else
                {
                    OldCityLocation[] oldtowns =
                    {
                        new OldCityLocation(105, 130), new OldCityLocation(245, 90),
                        new OldCityLocation(165, 200), new OldCityLocation(395, 160),
                        new OldCityLocation(200, 305), new OldCityLocation(335, 250),
                        new OldCityLocation(160, 395), new OldCityLocation(100, 250),
                        new OldCityLocation(270, 130), new OldCityLocation(0xFFFF, 0xFFFF)
                    };

                    byte cityIndex = p.ReadByte();
                    string cityName = p.ReadASCII(31);
                    string cityBuilding = p.ReadASCII(31);

                    cityInfo = new CityInfo
                    (
                        cityIndex,
                        cityName,
                        cityBuilding,
                        count,
                        (ushort)oldtowns[i % oldtowns.Length].X,
                        (ushort)oldtowns[i % oldtowns.Length].Y,
                        0,
                        0,
                        isNew
                    );
                }
                Cities.Add(cityInfo);
            }
        }
    }

    internal class EnterWorldEventArgs : EventArgs
    {
        public uint UniqueId;

        internal EnterWorldEventArgs(NetClient netClient, ref PacketBufferReader p)
        {
            UniqueId = p.ReadUInt();
        }
    }

    
    internal class ReceiveAccountInfoArgs : EventArgs
    {
        public string Account;
        public string Password;
        public NetClient NetClient;

        internal ReceiveAccountInfoArgs(NetClient inNetClient, string inAccount, string inPassword)
        {
            Account = inAccount;
            Password = inPassword;
            NetClient = inNetClient;
        }
    }

    internal class ReceiveLoginRejectionEventArgs : EventArgs
    {
        public string ErrorMessage;

        internal ReceiveLoginRejectionEventArgs(NetClient netClient, ref PacketBufferReader p)
        {
            byte code = p.ReadByte();
            ErrorMessage = ServerErrorMessages.GetError(p.ID, code);
        }
    }

    internal class UpdatePlayerEventArgs : EventArgs
    {
        public uint Serial;
        public ushort Graphic;
        public byte Graphic_inc;
        public ushort Hue;
        public Flags Flags;
        public ushort X ;
        public ushort Y;
        public sbyte Z;
        public ushort ServerID;
        public Direction Direction;

        internal UpdatePlayerEventArgs(NetClient netClient, ref PacketBufferReader p)
        {
            Serial = p.ReadUInt();
            Graphic = p.ReadUShort();
            Graphic_inc = p.ReadByte();
            Hue = p.ReadUShort();
            Flags = (Flags)p.ReadByte();
            X = p.ReadUShort();
            Y = p.ReadUShort();
            ServerID = p.ReadUShort();
            Direction = (Direction)p.ReadByte();
            Z = p.ReadSByte();
        }
    }

    internal class PacketHandlers
    {
        public delegate void OnPacketBufferReader(NetClient client, ref PacketBufferReader p);
        private readonly OnPacketBufferReader[] _handlers = new OnPacketBufferReader[0x100];

        public static PacketHandlers Handlers { get; } = new PacketHandlers();

        public static event EventHandler<ServerListReceivedEventArgs> ServerListReceivedEvent;
        public static event EventHandler<ReceiveServerRelayEventArgs> ReceiveServerRelayEvent;
        public static event EventHandler<ReceiveCharacterListEventArgs> ReceiveCharacterListEvent;
        public static event EventHandler<EnterWorldEventArgs> EnterWorldEvent;
        public static event EventHandler<ReceiveAccountInfoArgs> ReceiveAccountInfoEvent;
        public static event EventHandler<UpdatePlayerEventArgs> UpdatePlayerEvent;
        public static event EventHandler<ReceiveLoginRejectionEventArgs> ReceiveLoginRejectionEvent; 

        public void Add(byte id, OnPacketBufferReader handler)
        {
            _handlers[id] = handler;
        }

        public void AnalyzePacket(NetClient netClient, byte[] data, int offset, int length)
        {
            //Console.WriteLine("packetId 0x{0:X}", data[0]);
            OnPacketBufferReader bufferReader = _handlers[data[0]];

            if (bufferReader != null)
            {
                PacketBufferReader buffer = new PacketBufferReader(data, length)
                {
                    Position = offset
                };

                bufferReader(netClient, ref buffer);
            }
        }

        public static void Load()
        {
            // login
            Handlers.Add(0xA8, ServerListReceived);
            Handlers.Add(0x8C, ReceiveServerRelay);
            Handlers.Add(0xA9, ReceiveCharacterList);
            Handlers.Add(0x86, UpdateCharacterList);
            Handlers.Add(0x82, ReceiveLoginRejection);
            Handlers.Add(0x85, ReceiveLoginRejection);
            Handlers.Add(0x53, ReceiveLoginRejection);
            // child shard
            Handlers.Add(0x80, ReceiveAccountInfo);

            #region Game
            Handlers.Add(0x1B, EnterWorld);
            Handlers.Add(0x55, LoginComplete);
            Handlers.Add(0xBD, ClientVersion);
            Handlers.Add(0x03, ClientTalk);
            Handlers.Add(0x0B, Damage);
            Handlers.Add(0x11, CharacterStatus);
            Handlers.Add(0x15, FollowR);
            Handlers.Add(0x16, NewHealthbarUpdate);
            Handlers.Add(0x17, NewHealthbarUpdate);
            Handlers.Add(0x1A, UpdateItem);
            Handlers.Add(0x1C, Talk);
            Handlers.Add(0x1D, DeleteObject);
            Handlers.Add(0x20, UpdatePlayer);
            Handlers.Add(0x21, DenyWalk);
            Handlers.Add(0x22, ConfirmWalk);
            Handlers.Add(0x23, DragAnimation);
            Handlers.Add(0x24, OpenContainer);
            Handlers.Add(0x25, UpdateContainedItem);
            Handlers.Add(0x27, DenyMoveItem);
            Handlers.Add(0x28, EndDraggingItem);
            Handlers.Add(0x29, DropItemAccepted);
            Handlers.Add(0x2C, DeathScreen);
            Handlers.Add(0x2D, MobileAttributes);
            Handlers.Add(0x2E, EquipItem);
            Handlers.Add(0x2F, Swing);
            Handlers.Add(0x32, Unknown_0x32);
            Handlers.Add(0x38, Pathfinding);
            Handlers.Add(0x3A, UpdateSkills);
            Handlers.Add(0x3C, UpdateContainedItems);
            Handlers.Add(0x4E, PersonalLightLevel);
            Handlers.Add(0x4F, LightLevel);
            Handlers.Add(0x54, PlaySoundEffect);
            Handlers.Add(0x56, MapData);
            Handlers.Add(0x5B, SetTime);
            Handlers.Add(0x65, SetWeather);
            Handlers.Add(0x66, BookData);
            Handlers.Add(0x6C, TargetCursor);
            Handlers.Add(0x6D, PlayMusic);
            Handlers.Add(0x6F, SecureTrading);
            Handlers.Add(0x6E, CharacterAnimation);
            Handlers.Add(0x70, GraphicEffect);
            Handlers.Add(0x71, BulletinBoardData);
            Handlers.Add(0x72, Warmode);
            Handlers.Add(0x73, Ping);
            Handlers.Add(0x74, BuyList);
            Handlers.Add(0x77, UpdateCharacter);
            Handlers.Add(0x78, UpdateObject);
            Handlers.Add(0x7C, OpenMenu);
            Handlers.Add(0x88, OpenPaperdoll);
            Handlers.Add(0x89, CorpseEquipment);
            Handlers.Add(0x90, DisplayMap);
            Handlers.Add(0x93, OpenBook);
            Handlers.Add(0x95, DyeData);
            Handlers.Add(0x97, MovePlayer);
            Handlers.Add(0x98, UpdateName);
            Handlers.Add(0x99, MultiPlacement);
            Handlers.Add(0x9A, ASCIIPrompt);
            Handlers.Add(0x9E, SellList);
            Handlers.Add(0xA1, UpdateHitpoints);
            Handlers.Add(0xA2, UpdateMana);
            Handlers.Add(0xA3, UpdateStamina);
            Handlers.Add(0xA5, OpenUrl);
            Handlers.Add(0xA6, TipWindow);
            Handlers.Add(0xAA, AttackCharacter);
            Handlers.Add(0xAB, TextEntryDialog);
            Handlers.Add(0xAF, DisplayDeath);
            Handlers.Add(0xAE, UnicodeTalk);
            Handlers.Add(0xB0, OpenGump);
            Handlers.Add(0xB2, ChatMessage);
            Handlers.Add(0xB7, Help);
            Handlers.Add(0xB8, CharacterProfile);
            Handlers.Add(0xB9, EnableLockedFeatures);
            Handlers.Add(0xBA, DisplayQuestArrow);
            Handlers.Add(0xBB, UltimaMessengerR);
            Handlers.Add(0xBC, Season);
            Handlers.Add(0xBE, AssistVersion);
            Handlers.Add(0xBF, ExtendedCommand);
            Handlers.Add(0xC0, GraphicEffect);
            Handlers.Add(0xC1, DisplayClilocString);
            Handlers.Add(0xC2, UnicodePrompt);
            Handlers.Add(0xC4, Semivisible);
            Handlers.Add(0xC6, InvalidMapEnable);
            Handlers.Add(0xC7, GraphicEffect);
            Handlers.Add(0xC8, ClientViewRange);
            Handlers.Add(0xCA, GetUserServerPingGodClientR);
            Handlers.Add(0xCB, GlobalQueCount);
            Handlers.Add(0xCC, DisplayClilocString);
            Handlers.Add(0xD0, ConfigurationFileR);
            Handlers.Add(0xD1, Logout);
            Handlers.Add(0xD2, UpdateCharacter);
            Handlers.Add(0xD3, UpdateObject);
            Handlers.Add(0xD4, OpenBook);
            Handlers.Add(0xD6, MegaCliloc);
            Handlers.Add(0xD7, GenericAOSCommandsR);
            Handlers.Add(0xD8, CustomHouse);
            Handlers.Add(0xDB, CharacterTransferLog);
            Handlers.Add(0xDC, OPLInfo);
            Handlers.Add(0xDD, OpenCompressedGump);
            Handlers.Add(0xDE, UpdateMobileStatus);
            Handlers.Add(0xDF, BuffDebuff);
            Handlers.Add(0xE2, NewCharacterAnimation);
            Handlers.Add(0xE3, KREncryptionResponse);
            Handlers.Add(0xE5, DisplayWaypoint);
            Handlers.Add(0xE6, RemoveWaypoint);
            Handlers.Add(0xF0, KrriosClientSpecial);
            Handlers.Add(0xF1, FreeshardListR);
            Handlers.Add(0xF3, UpdateItemSA);
            Handlers.Add(0xF5, DisplayMap);
            Handlers.Add(0xF6, BoatMoving);
            Handlers.Add(0xF7, PacketList);
            #endregion
        }

        private static void ServerListReceived(NetClient netClient, ref PacketBufferReader p)
        {
            ServerListReceivedEvent?.Invoke(netClient, new ServerListReceivedEventArgs(netClient, ref p));
        }

        private static void ReceiveServerRelay(NetClient netClient, ref PacketBufferReader p)
        {
            ReceiveServerRelayEvent?.Invoke(netClient, new ReceiveServerRelayEventArgs(netClient, ref p));
        }

        private static void ReceiveCharacterList(NetClient netClient, ref PacketBufferReader p)
        {
            ReceiveCharacterListEvent?.Invoke(netClient, new ReceiveCharacterListEventArgs(netClient, ref p));
        }

        private static void ReceiveAccountInfo(NetClient netClient, ref PacketBufferReader p)
        {
            string acct = p.ReadASCII(30);
            string pwd = p.ReadASCII(30);
            ReceiveAccountInfoEvent?.Invoke(netClient, new ReceiveAccountInfoArgs(netClient, acct, pwd));
        }

        private static void ReceiveLoginRejection(NetClient netClient, ref PacketBufferReader p)
        {
            ReceiveLoginRejectionEvent?.Invoke(netClient, new ReceiveLoginRejectionEventArgs(netClient, ref p));
        }

        private static void LoginComplete(NetClient netClient, ref PacketBufferReader p)
        {
            //Console.WriteLine("PacketHandler.cs LoginComplete");
        }

        private static void EnterWorld(NetClient netClient, ref PacketBufferReader p)
        {
            //Console.WriteLine("EnterWorld");
            EnterWorldEvent?.Invoke(netClient, new EnterWorldEventArgs(netClient, ref p));
        }

        private static void DenyWalk(NetClient netClient, ref PacketBufferReader p)
        {
            netClient.CanRequestWalk = true;

            byte seq = p.ReadByte();
            ushort x = p.ReadUShort();
            ushort y = p.ReadUShort();
            Direction direction = (Direction)p.ReadByte();
            direction &= Direction.Up;
            sbyte z = p.ReadSByte();

            netClient.Walker.DenyWalk(seq, x, y, z);
        }

        private static void ConfirmWalk(NetClient netClient, ref PacketBufferReader p)
        {
            netClient.CanRequestWalk = true;

            byte seq = p.ReadByte();
            byte noto = (byte)(p.ReadByte() & ~0x40);

            if (noto == 0 || noto >= 8)
            {
                noto = 0x01;
            }

            netClient.Walker.ConfirmWalk(seq);
        }

        private static void UpdatePlayer(NetClient netClient, ref PacketBufferReader p)
        {
            UpdatePlayerEvent?.Invoke(netClient, new UpdatePlayerEventArgs(netClient, ref p));
        }

        public static void SendMegaClilocRequests()
        {

        }

        public static void AddMegaClilocRequest(uint serial)
        {

        }

        private static void TargetCursor(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void SecureTrading(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void ClientTalk(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void Damage(NetClient netClient, ref PacketBufferReader p)
        {
 
        }

        private static void CharacterStatus(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void FollowR(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void NewHealthbarUpdate(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void UpdateItem(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void Talk(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void DeleteObject(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void DragAnimation(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void OpenContainer(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void UpdateContainedItem(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void DenyMoveItem(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void EndDraggingItem(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void DropItemAccepted(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void DeathScreen(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void MobileAttributes(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void EquipItem(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void Swing(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void Unknown_0x32(NetClient netClient, ref PacketBufferReader p)
        {
        }

        private static void UpdateSkills(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void Pathfinding(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void UpdateContainedItems(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void PersonalLightLevel(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void LightLevel(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void PlaySoundEffect(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void PlayMusic(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void MapData(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void SetTime(NetClient netClient, ref PacketBufferReader p)
        {
        }

        private static void SetWeather(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void BookData(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void CharacterAnimation(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void GraphicEffect(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void ClientViewRange(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void BulletinBoardData(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void Warmode(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void Ping(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void BuyList(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void UpdateCharacter(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void UpdateObject(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void OpenMenu(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void OpenPaperdoll(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void CorpseEquipment(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void DisplayMap(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void OpenBook(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void DyeData(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void MovePlayer(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void UpdateName(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void MultiPlacement(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void ASCIIPrompt(NetClient netClient, ref PacketBufferReader p)
        {
 
        }

        private static void SellList(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void UpdateHitpoints(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void UpdateMana(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void UpdateStamina(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void OpenUrl(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void TipWindow(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void AttackCharacter(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void TextEntryDialog(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void UnicodeTalk(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void DisplayDeath(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void OpenGump(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void ChatMessage(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void Help(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void CharacterProfile(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void EnableLockedFeatures(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void DisplayQuestArrow(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void UltimaMessengerR(NetClient netClient, ref PacketBufferReader p)
        {
        }

        private static void Season(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void ClientVersion(NetClient netClient, ref PacketBufferReader p)
        {
            netClient.Send(new PClientVersion(netClient.VersionString));
        }

        private static void AssistVersion(NetClient netClient, ref PacketBufferReader p)
        {
            //uint version = p.ReadUInt();

            //string[] parts = Service.GetByLocalSerial<Settings>().ClientVersion.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            //byte[] clientVersionBuffer =
            //    {byte.Parse(parts[0]), byte.Parse(parts[1]), byte.Parse(parts[2]), byte.Parse(parts[3])};

            //NetClient.Socket.Send(new PAssistVersion(clientVersionBuffer, version));
        }

        private static void ExtendedCommand(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void DisplayClilocString(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void UnicodePrompt(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void Semivisible(NetClient netClient, ref PacketBufferReader p)
        {
        }

        private static void InvalidMapEnable(NetClient netClient, ref PacketBufferReader p)
        {
        }

        private static void ParticleEffect3D(NetClient netClient, ref PacketBufferReader p)
        {
        }

        private static void GetUserServerPingGodClientR(NetClient netClient, ref PacketBufferReader p)
        {
        }

        private static void GlobalQueCount(NetClient netClient, ref PacketBufferReader p)
        {
        }

        private static void ConfigurationFileR(NetClient netClient, ref PacketBufferReader p)
        {
        }

        private static void Logout(NetClient netClient, ref PacketBufferReader p)
        {
        }

        private static void MegaCliloc(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void GenericAOSCommandsR(NetClient netClient, ref PacketBufferReader p)
        {
        }

        private static void CustomHouse(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void CharacterTransferLog(NetClient netClient, ref PacketBufferReader p)
        {
        }

        private static void OPLInfo(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void OpenCompressedGump(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void UpdateMobileStatus(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void BuffDebuff(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void NewCharacterAnimation(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void KREncryptionResponse(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void DisplayWaypoint(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void RemoveWaypoint(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void KrriosClientSpecial(NetClient netClient, ref PacketBufferReader p)
        {
            
        }

        private static void FreeshardListR(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void UpdateItemSA(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void BoatMoving(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void PacketList(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void SelectServer(byte index)
        {
           

        }

        private static void UpdateCharacterList(NetClient netClient, ref PacketBufferReader p)
        {

        }

        private static void AddItemToContainer
        (
            uint serial,
            ushort graphic,
            ushort amount,
            ushort x,
            ushort y,
            ushort hue,
            uint containerSerial
        )
        {

        }

        private static void UpdateGameObject
        (
            uint serial,
            ushort graphic,
            byte graphic_inc,
            ushort count,
            ushort x,
            ushort y,
            sbyte z,
            Direction direction,
            ushort hue,
            Flags flagss,
            int UNK,
            byte type,
            ushort UNK_2
        )
        {

        }

        private static void UpdatePlayer
        (
            uint serial,
            ushort graphic,
            byte graph_inc,
            ushort hue,
            Flags flags,
            ushort x,
            ushort y,
            sbyte z,
            ushort serverID,
            Direction direction
        )
        {
            /*
            if (serial == World.Player)
            {
                World.Player.CloseBank();

                World.Player.Walker.WalkingFailed = false;

                World.Player.X = x;
                World.Player.Y = y;
                World.Player.Z = z;

                World.RangeSize.X = x;
                World.RangeSize.Y = y;

                bool olddead = World.Player.IsDead;
                ushort old_graphic = World.Player.Graphic;

                World.Player.Graphic = graphic;
                World.Player.Direction = direction & Direction.Mask;
                World.Player.FixHue(hue);

                World.Player.Flags = flags;

                World.Player.Walker.DenyWalk(0xFF, -1, -1, -1);
                GameScene gs = Client.Game.GetScene<GameScene>();

                if (gs != null)
                {
                    gs.Weather.Reset();
                    gs.UpdateDrawPosition = true;
                }

                if (old_graphic != 0 && old_graphic != World.Player.Graphic)
                {
                    if (World.Player.IsDead)
                    {
                        TargetManager.Reset();
                    }
                }

                if (olddead != World.Player.IsDead)
                {
                    if (World.Player.IsDead)
                    {
                        World.ChangeSeason(Game.Managers.Season.Desolation, 42);
                    }
                    else
                    {
                        World.ChangeSeason(World.OldSeason, World.OldMusicIndex);
                    }
                }

                World.Player.Walker.ResendPacketResync = false;
                World.Player.CloseRangedGumps();

                World.Player.UpdateScreenPosition();
                World.Player.AddToTile();
            }
            */
        }

        [Flags]
        private enum AffixType
        {
            Append = 0x00,
            Prepend = 0x01,
            System = 0x02
        }
    }
}
