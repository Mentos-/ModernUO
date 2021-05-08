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
using System.Linq;
using System.Text;

namespace LoadTestUO
{
    internal sealed class PSeed : PacketWriter
    {
        public PSeed(byte[] version) : base(0xEF)
        {
            const uint SEED = 0x1337BEEF;
            WriteUInt(SEED);

            for (int i = 0; i < 4; i++)
            {
                WriteUInt(version[i]);
            }
        }

        public PSeed(uint v, byte major, byte minor, byte build, byte extra) : base(0xEF)
        {
            WriteUInt(v);

            WriteUInt(major);
            WriteUInt(minor);
            WriteUInt(build);
            WriteUInt(extra);
        }
    }

    internal sealed class PFirstLogin : PacketWriter
    {
        public PFirstLogin(string account, string password) : base(0x80)
        {
            WriteASCII(account, 30);
            WriteASCII(password, 30);
            WriteByte(0xFF);
        }
    }

    internal sealed class PSelectServer : PacketWriter
    {
        public PSelectServer(byte index) : base(0xA0)
        {
            WriteByte(0);
            WriteByte(index);
        }
    }

    internal sealed class PSecondLogin : PacketWriter
    {
        public PSecondLogin(string account, string password, uint seed) : base(0x91)
        {
            WriteUInt(seed);
            WriteASCII(account, 30);
            WriteASCII(password, 30);
        }
    }

    internal enum CreateCharacterSkillLock : byte
    {
        Up = 0,
        Down = 1,
        Locked = 2
    }

    internal sealed class CreateCharacterSkill
    {
        public CreateCharacterSkill(string name, int index, ushort valueFixed)
        {
            Name = name;
            Index = index;
            ValueFixed = valueFixed;
        }

        public CreateCharacterSkillLock SkillLock { get; internal set; }

        public ushort ValueFixed { get; internal set; }

        public ushort BaseFixed { get; internal set; }

        public ushort CapFixed { get; internal set; }

        public float Value => ValueFixed / 10.0f;

        public float Base => BaseFixed / 10.0f;

        public float Cap => CapFixed / 10.0f;

        public bool IsClickable { get; }

        public string Name { get; }

        public int Index { get; }

        public override string ToString()
        {
            return Name + Value;
        }
    }

    internal sealed class PCreateCharacter : PacketWriter
    {
        public PCreateCharacter
        (
            ClientVersion clientVersion,
            uint clientProtocol,
            string characterName,
            byte characterStrength,
            byte characterIntelligence,
            byte characterDexterity,
            List<CreateCharacterSkill> characterSkills,
            Flags characterFlags,
            byte characterRace,
            ushort characterHue,
            ushort characterHairHue,
            ushort characterHairGraphic,
            ushort characterBeardHue,
            ushort characterBeardGraphic,
            ushort characterShirtHue,
            ushort characterPantsHue,
            int cityIndex,
            uint clientIP,
            int serverIndex,
            uint slot,
            byte profession
        ) : base(0x00)
        {
            int skillcount = 3;

            if (clientVersion >= ClientVersion.CV_70160)
            {
                skillcount++;
                this[0] = 0xF8;
            }

            WriteUInt(0xEDEDEDED);
            WriteUShort(0xFFFF);
            WriteUShort(0xFFFF);
            WriteByte(0x00);
            WriteASCII(characterName, 30);
            WriteUShort(0x00);

            WriteUInt(clientProtocol);
            WriteUInt(0x01);
            WriteUInt(0x0);
            WriteByte(profession); // Profession
            Skip(15);
            byte val;

            if (clientVersion < ClientVersion.CV_4011D)
            {
                val = Convert.ToByte(characterFlags.HasFlag(Flags.Female));
            }
            else
            {
                val = characterRace;

                if (clientVersion < ClientVersion.CV_7000)
                {
                    val--;
                }

                val = (byte)(val * 2 + Convert.ToByte(characterFlags.HasFlag(Flags.Female)));
            }

            WriteByte(val);
            WriteByte((byte)characterStrength);
            WriteByte((byte)characterDexterity);
            WriteByte((byte)characterIntelligence);

            List<CreateCharacterSkill> skills = characterSkills.OrderByDescending(o => o.Value).Take(skillcount).ToList();

            foreach (CreateCharacterSkill skill in skills)
            {
                WriteByte((byte)skill.Index);
                WriteByte((byte)skill.ValueFixed);
            }

            WriteUShort(characterHue);

            WriteUShort(characterHairGraphic);
            WriteUShort(characterHairHue);

            WriteUShort(characterBeardGraphic);
            WriteUShort(characterBeardHue);

            WriteUShort((ushort)cityIndex);
            WriteUShort(0x0000);
            WriteUShort((ushort)slot);

            WriteUInt(clientIP);

            WriteUShort(characterShirtHue);

            WriteUShort(characterPantsHue);
        }
    }

    internal sealed class PSelectCharacter : PacketWriter
    {
        public PSelectCharacter(uint index, string name, uint ipclient, uint clientProtocol) : base(0x5D)
        {
            WriteUInt(0xEDEDEDED);
            WriteASCII(name, 30);
            Skip(2);
            WriteUInt(clientProtocol);
            Skip(24);
            WriteUInt(index);
            WriteUInt(ipclient);
        }
    }

    internal sealed class PClientVersion : PacketWriter
    {
        public PClientVersion(byte[] version) : base(0xBD)
        {
            WriteASCII
            (
                string.Format
                (
                    "{0}.{1}.{2}.{3}",
                    version[0],
                    version[1],
                    version[2],
                    version[3]
                )
            );
        }

        public PClientVersion(string v) : base(0xBD)
        {
            WriteASCII(v);
        }
    }

    internal sealed class PResend : PacketWriter
    {
        public PResend() : base(0x22)
        {
        }
    }

    internal sealed class PWalkRequest : PacketWriter
    {
        public PWalkRequest(Direction direction, byte seq, bool run, uint fastwalk) : base(0x02)
        {
            if (run)
            {
                direction |= Direction.Running;
            }

            WriteByte((byte)direction);
            WriteByte(seq);
            WriteUInt(fastwalk);
        }
    }

    internal sealed class PASCIISpeechRequest : PacketWriter
    {
        public PASCIISpeechRequest(string text, byte type, byte font, ushort hue) : base(0x03)
        {
            WriteByte((byte)type);
            WriteUShort(hue);
            WriteUShort(font);
            WriteASCII(text);
        }
    }

    internal sealed class PUnicodeSpeechRequest : PacketWriter
    {
        public PUnicodeSpeechRequest(string text, byte type, byte font, ushort hue, string lang) : base(0xAD)
        {
            WriteByte(type);
            WriteUShort(hue);
            WriteUShort(font);
            WriteASCII(lang, 4);

            WriteUnicode(text);
        }
    }


    internal class PacketWriter : PacketBase
    {
        private byte[] _data;

        public PacketWriter(byte id)
        {
            this[0] = id;
        }

        public PacketWriter(byte[] data, int length)
        {
            Array.Resize(ref _data, length);

            for (int i = 0; i < length; i++)
            {
                _data[i] = data[i];
            }
        }

        public override byte this[int index]
        {
            get => _data[index];
            set
            {
                if (index == 0)
                {
                    SetPacketId(value);
                }
                else
                {
                    _data[index] = value;
                }
            }
        }

        public override int Length => _data.Length;

        private void SetPacketId(byte id)
        {
            short len = PacketsTable.GetPacketLength(id);
            IsDynamic = len < 0;
            _data = new byte[IsDynamic ? 32 : len];
            _data[0] = id;
            Position = IsDynamic ? 3 : 1;
        }

        public override ref byte[] ToArray()
        {
            if (IsDynamic && Length != Position)
            {
                Array.Resize(ref _data, Position);
            }

            WriteSize();

            return ref _data;
        }

        public void WriteSize()
        {
            if (IsDynamic)
            {
                this[1] = (byte) (Position >> 8);
                this[2] = (byte) Position;
            }
        }

        protected override bool EnsureSize(int length)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            if (IsDynamic)
            {
                while (Position + length > Length)
                {
                    Array.Resize(ref _data, Length + length * 2);
                }

                return false;
            }

            return Position + length > Length;
        }
    }
}