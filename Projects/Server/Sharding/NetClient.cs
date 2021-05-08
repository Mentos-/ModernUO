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
using System.Threading.Tasks;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

//using ClassicUO.Network.Encryption;
//using ClassicUO.Utility;
//using ClassicUO.Utility.Logging;

namespace LoadTestUO
{
    enum ClientSocketStatus
    {
        Disconnected,
        Connecting,
        Connected,
    }

    internal sealed class NetClient
    {
        //Load test additions
        public string Name;
        public string Group;//prefix for account creation so that multiple instances of the application don't try to log into the same accounts
        public int AuthId;//authid received from clients wanting to log into child shards that we send to the parent shard to get character data 
        public ClientVersion Version = ClientVersion.CV_705301;
        public string VersionString = "7.0.59.5";
        internal WalkerManager Walker { get; } = new WalkerManager();
        public bool HasSentSeedPacket = false;
        public bool HasSentServerPacket = false;
        public bool HasSentCharacterPacket = false;
        public bool HasEnteredWorld = false;
        public int RequestTeleportToInitialPositionCount = 0;
        public bool CanRequestWalk = true;
        public uint UniqueId;

        private const int BUFF_SIZE = 0x80000;

        private int _incompletePacketLength;
        private bool _isCompressionEnabled;
        private byte[] _recvBuffer, _incompletePacketBuffer, _decompBuffer;
        private CircularBuffer _circularBuffer;
        private ConcurrentQueue<byte[]> _pluginRecvQueue = new ConcurrentQueue<byte[]>();
        private readonly bool _is_login_socket;
        private TcpClient _tcpClient;
        private NetworkStream _netStream;


        public NetClient(bool is_login_socket)
        {
            _is_login_socket = is_login_socket;
            //Statistics = new NetStatistics(this);
        }

        public static List<NetClient> LoadTestNetClientsLogin = new List<NetClient>();
        public static List<NetClient> LoadTestNetClientsGame = new List<NetClient>(); 

        public bool IsConnected => _tcpClient != null && _tcpClient.Connected;

        public bool IsDisposed { get; private set; }

        public ClientSocketStatus Status { get; private set; }

       // public NetStatistics Statistics { get; }

        private static uint? _client_address;


        public static uint ClientAddress
        {
            get
            {
                if (!_client_address.HasValue)
                {
                    try
                    {
                        _client_address = 0x100007f;

                        //var address = GetLocalIpAddress();

                        //_client_address = ((address & 0xff) << 0x18) | ((address & 65280) << 8) | ((address >> 8) & 65280) | ((address >> 0x18) & 0xff);
                    }
                    catch
                    {
                        _client_address = 0x100007f;
                    }
                }

                return _client_address.Value;
            }
        }

        public event EventHandler Connected;
        public event EventHandler<SocketError> Disconnected;

        public static event EventHandler<PacketWriter> PacketSent;

        private static readonly Task<bool> TaskCompletedFalse = new Task<bool>(() => false);

        public static void EnqueuePacketFromPlugin(byte[] data, int length)
        {

        }

        public Task<bool> Connect(string ip, ushort port)
        {
            IsDisposed = false;
            IPAddress address = ResolveIP(ip);

            if (address == null)
            {
                return TaskCompletedFalse;
            }

            return Connect(address, port);
        }

        public Task<bool> Connect(IPAddress address, ushort port)
        {
            IsDisposed = false;

            if (Status != ClientSocketStatus.Disconnected)
            {
                //Log.Warn($"Socket status: {Status}");

                return TaskCompletedFalse;
            }

            _tcpClient = new TcpClient
            {
                ReceiveBufferSize = BUFF_SIZE,
                SendBufferSize = BUFF_SIZE,
                NoDelay = true,
                ReceiveTimeout = 0,
                SendTimeout = 0
            };

            _recvBuffer = new byte[BUFF_SIZE];
            _incompletePacketBuffer = new byte[BUFF_SIZE];
            _decompBuffer = new byte[BUFF_SIZE];
            _circularBuffer = new CircularBuffer();
            _pluginRecvQueue = new ConcurrentQueue<byte[]>();
           // Statistics.Reset();

            Status = ClientSocketStatus.Connecting;

            return InternalConnect(address, port);
        }

        private Task<bool> InternalConnect(IPAddress address, ushort port)
        {
            try
            {
                return _tcpClient.ConnectAsync(address, port)
                                 .ContinueWith
                                 (
                                     (t) =>
                                     {
                                         if (!t.IsFaulted && _tcpClient.Connected)
                                         {
                                             _netStream = _tcpClient.GetStream();
                                             Status = ClientSocketStatus.Connected;
                                             Connected?.Invoke(this, EventArgs.Empty);
                                             //Statistics.ConnectedFrom = DateTime.Now;

                                             return true;
                                         }


                                         Status = ClientSocketStatus.Disconnected;
                                         //Log.Error("socket not connected");

                                         return false;
                                     },
                                     TaskContinuationOptions.ExecuteSynchronously
                                 );
            }
            catch (SocketException e)
            {
                //Log.Error($"Socket error when connecting:\n{e}");
                Disconnect(e.SocketErrorCode);

                return TaskCompletedFalse;
            }
        }

        public void Disconnect()
        {
            Disconnect(SocketError.Success);
        }

        private void Disconnect(SocketError error)
        {
            if (IsDisposed)
            {
                return;
            }

            Status = ClientSocketStatus.Disconnected;
            IsDisposed = true;

            if (_tcpClient == null)
            {
                return;
            }

            try
            {
                _tcpClient.Close();
            }
            catch
            {
            }

            try
            {
                _netStream?.Dispose();
            }
            catch
            {
            }

            //Log.Trace($"Disconnected [{(_is_login_socket ? "login socket" : "game socket")}]");

            _incompletePacketBuffer = null;
            _incompletePacketLength = 0;
            _recvBuffer = null;
            _isCompressionEnabled = false;
            _tcpClient = null;
            _netStream = null;
            _circularBuffer = null;

            if (error != 0)
            {
                Disconnected.Raise(error);
            }

           // Statistics.Reset();
        }

        public void EnableCompression()
        {
            //_isCompressionEnabled = true;
        }

        public void Send(PacketWriter p)
        {
            ref byte[] data = ref p.ToArray();
            int length = p.Length;
            PacketSent.Raise(p);
            Send(data, length, false);
        }

        public void Send(byte[] data, int length, bool ignorePlugin = false, bool skip_encryption = false)
        {
            PacketSent.Raise(new PacketWriter(data, length));
            Send(data, length, skip_encryption);
        }

        private void Send(byte[] data, int length, bool skip_encryption)
        {
            if (_tcpClient == null || IsDisposed)
            {
                return;
            }

            if (_netStream == null || !_tcpClient.Connected)
            {
                return;
            }

            if (data != null && data.Length != 0 && length > 0)
            {
                // if (CUOEnviroment.PacketLog)
                {
                    //LogPacket(data, length, true);
                }

                if (!skip_encryption)
                {
                    EncryptionHelper.Encrypt(_is_login_socket, ref data, ref data, length);
                }

                try
                {
                    _netStream.Write(data, 0, length);
                    _netStream.Flush();

                   // Statistics.TotalBytesSent += (uint)length;
                   // Statistics.TotalPacketsSent++;
                }
                catch (SocketException ex)
                {
                    //Log.Error("socket error when sending:\n" + ex);
                    Disconnect(ex.SocketErrorCode);
                }
                catch (Exception ex)
                {
                    if (ex.InnerException is SocketException socketEx)
                    {
                        //Log.Error("socket error when sending:\n" + socketEx);
                        Disconnect(socketEx.SocketErrorCode);
                    }
                    else
                    {
                        //Log.Error("fatal error when receiving:\n" + ex);
                        Disconnect();
                    }
                }
            }
        }

        public void Update()
        {
            ProcessRecv();

            while (_pluginRecvQueue.TryDequeue(out byte[] data) && data != null && data.Length != 0)
            {
                int length = PacketsTable.GetPacketLength(data[0]);
                int offset = 1;

                if (length == -1)
                {
                    if (data.Length < 3)
                    {
                        continue;
                    }

                    //length = data[2] | (data[1] << 8);
                    offset = 3;
                }

                PacketHandlers.Handlers.AnalyzePacket(this, data, offset, data.Length);
            }
        }

        private void ExtractPackets()
        {
            if (!IsConnected || _circularBuffer == null || _circularBuffer.Length <= 0)
            {
                return;
            }

            lock (_circularBuffer)
            {
                int length = _circularBuffer.Length;

                while (length > 0 && IsConnected)
                {
                    if (!GetPacketInfo(_circularBuffer, length, out int offset, out int packetlength))
                    {
                        break;
                    }

                    if (packetlength > 0)
                    {
                        // Patch to maintain a retrocompatibiliy with older cuoapi
                        byte[] data = new byte[packetlength]; // _packetBuffer;

                        _circularBuffer.Dequeue(data, 0, packetlength);

                        // if (CUOEnviroment.PacketLog)
                        {
                            //LogPacket(data, packetlength, false);
                        }


                        PacketHandlers.Handlers.AnalyzePacket(this, data, offset, packetlength);

                        // Statistics.TotalPacketsReceived++;
                    }

                    length = _circularBuffer?.Length ?? 0;
                }
            }
        }

        private static bool GetPacketInfo(CircularBuffer buffer, int bufferLength, out int offset, out int length)
        {
            if (buffer == null || bufferLength <= 0)
            {
                length = 0;
                offset = 0;

                return false;
            }

            length = PacketsTable.GetPacketLength(buffer.GetID());
            offset = 1;

            if (length == -1)
            {
                if (bufferLength < 3)
                {
                    return false;
                }

                length = buffer.GetLength();
                offset = 3;
            }

            return bufferLength >= length;
        }

        public ClientFlags GetClientProtocol()
        {
            ClientFlags Protocol = ClientFlags.CF_T2A;

            if (Version >= ClientVersion.CV_200)
            {
                Protocol |= ClientFlags.CF_RE;
            }

            if (Version >= ClientVersion.CV_300)
            {
                Protocol |= ClientFlags.CF_TD;
            }

            if (Version >= ClientVersion.CV_308)
            {
                Protocol |= ClientFlags.CF_LBR;
            }

            if (Version >= ClientVersion.CV_308Z)
            {
                Protocol |= ClientFlags.CF_AOS;
            }

            if (Version >= ClientVersion.CV_405A)
            {
                Protocol |= ClientFlags.CF_SE;
            }

            if (Version >= ClientVersion.CV_60144)
            {
                Protocol |= ClientFlags.CF_SA;
            }

            return Protocol;
        }

        private void ProcessRecv()
        {
            if (IsDisposed || Status != ClientSocketStatus.Connected)
            {
                return;
            }

            if (!IsConnected && !IsDisposed)
            {
                Disconnect();

                return;
            }

            if (!_netStream.DataAvailable)
            {
                return;
            }

            int available = _tcpClient.Available;

            if (available <= 0)
            {
                return;
            }

            try
            {
                int received = _netStream.Read(_recvBuffer, 0, available);

                if (received > 0)
                {
                    //Statistics.TotalBytesReceived += (uint)received;

                    byte[] buffer = _recvBuffer;

                    if (!_is_login_socket)
                    {
                        EncryptionHelper.Decrypt(ref buffer, ref buffer, received);
                    }

                    if (_isCompressionEnabled)
                    {
                        DecompressBuffer(ref buffer, ref received);
                    }

                    lock (_circularBuffer)
                    {
                        _circularBuffer.Enqueue(buffer, 0, received);
                    }

                    ExtractPackets();
                }
                else
                {
                    //Log.Warn("Server sent 0 bytes. Closing connection");
                    Disconnect(SocketError.SocketError);
                }
            }
            catch (SocketException socketException)
            {
                //Log.Error("socket error when receiving:\n" + socketException);
                Disconnect(socketException.SocketErrorCode);
            }
            catch (Exception ex)
            {
                if (ex.InnerException is SocketException socketEx)
                {
                    //Log.Error("socket error when receiving:\n" + socketEx);
                    Disconnect(socketEx.SocketErrorCode);
                }
                else
                {
                    //Log.Error("fatal error when receiving:\n" + ex);
                    Disconnect();
                }
            }
        }

        private void DecompressBuffer(ref byte[] buffer, ref int length)
        {
            byte[] source = _decompBuffer;
            int incompletelength = _incompletePacketLength;
            int sourcelength = incompletelength + length;

            if (incompletelength > 0)
            {
                Buffer.BlockCopy
                (
                    _incompletePacketBuffer,
                    0,
                    source,
                    0,
                    _incompletePacketLength
                );

                _incompletePacketLength = 0;
            }

            // if outbounds exception, BUFF_SIZE must be increased
            Buffer.BlockCopy
            (
                buffer,
                0,
                source,
                incompletelength,
                length
            );

            int processedOffset = 0;
            int sourceOffset = 0;
            int offset = 0;

            while (Huffman.DecompressChunk
            (
                ref source,
                ref sourceOffset,
                sourcelength,
                ref buffer,
                offset,
                out int outSize
            ))
            {
                processedOffset = sourceOffset;
                offset += outSize;
            }

            length = offset;

            if (processedOffset < sourcelength)
            {
                int l = sourcelength - processedOffset;

                Buffer.BlockCopy
                (
                    source,
                    processedOffset,
                    _incompletePacketBuffer,
                    _incompletePacketLength,
                    l
                );

                _incompletePacketLength += l;
            }
        }

        private static IPAddress ResolveIP(string addr)
        {
            IPAddress result = IPAddress.None;

            if (string.IsNullOrEmpty(addr))
            {
                return result;
            }

            if (!IPAddress.TryParse(addr, out result))
            {
                try
                {
                    IPHostEntry hostEntry = Dns.GetHostEntry(addr);

                    if (hostEntry.AddressList.Length != 0)
                    {
                        result = hostEntry.AddressList[hostEntry.AddressList.Length - 1];
                    }
                }
                catch
                {
                }
            }

            return result;
        }

        //private static LogFile _logFile;

        private static void LogPacket(byte[] buffer, int length, bool toServer)
        {
           // if (_logFile == null)
           //     _logFile = new LogFile(FileSystemHelper.CreateFolderIfNotExists(CUOEnviroment.ExecutablePath, "Logs", "Network"), "packets.log");

            int pos = 0;

            StringBuilder output = new StringBuilder();

            output.AppendFormat("{0}   -   ID {1:X2}   Length: {2}\n", (toServer ? "Client -> Server" : "Server -> Client"), buffer[0], buffer.Length);

            if (buffer[0] == 0x80 || buffer[0] == 0x91)
            {
                output.AppendLine("[ACCOUNT CREDENTIALS HIDDEN]");
            }
            else
            {
                output.AppendLine("        0  1  2  3  4  5  6  7   8  9  A  B  C  D  E  F");
                output.AppendLine("       -- -- -- -- -- -- -- --  -- -- -- -- -- -- -- --");

                int byteIndex = 0;

                int whole = length >> 4;
                int rem = length & 0xF;

                for (int i = 0; i < whole; ++i, byteIndex += 16)
                {
                    StringBuilder bytes = new StringBuilder(49);
                    StringBuilder chars = new StringBuilder(16);

                    for (int j = 0; j < 16; ++j)
                    {
                        int c = buffer[pos++];

                        bytes.Append(c.ToString("X2"));

                        if (j != 7)
                        {
                            bytes.Append(' ');
                        }
                        else
                        {
                            bytes.Append("  ");
                        }

                        if (c >= 0x20 && c < 0x80)
                        {
                            chars.Append((char)c);
                        }
                        else
                        {
                            chars.Append('.');
                        }
                    }

                    output.Append(byteIndex.ToString("X4"));
                    output.Append("   ");
                    output.Append(bytes);
                    output.Append("  ");
                    output.AppendLine(chars.ToString());
                }

                if (rem != 0)
                {
                    StringBuilder bytes = new StringBuilder(49);
                    StringBuilder chars = new StringBuilder(rem);

                    for (int j = 0; j < 16; ++j)
                    {
                        if (j < rem)
                        {
                            int c = buffer[pos++];

                            bytes.Append(c.ToString("X2"));

                            if (j != 7)
                            {
                                bytes.Append(' ');
                            }
                            else
                            {
                                bytes.Append("  ");
                            }

                            if (c >= 0x20 && c < 0x80)
                            {
                                chars.Append((char)c);
                            }
                            else
                            {
                                chars.Append('.');
                            }
                        }
                        else
                        {
                            bytes.Append("   ");
                        }
                    }

                    output.Append(byteIndex.ToString("X4"));
                    output.Append("   ");
                    output.Append(bytes);
                    output.Append("  ");
                    output.AppendLine(chars.ToString());
                }
            }


            output.AppendLine();
            output.AppendLine();

            //_logFile.Write(output.ToString());
            Console.WriteLine(output.ToString());
        }
    }

    internal struct StepInfo
    {
        public byte Direction;
        public byte OldDirection;
        public byte Sequence;
        public bool Accepted;
        public bool Running;
        public bool NoRotation;
        public long Timer;
        public ushort X, Y;
        public sbyte Z;

        public StepInfo(int empty)
        {
            Direction = 0;
            OldDirection = 0;
            Sequence = 0;
            Accepted = false;
            Running = false;
            NoRotation = false;
            Timer = 0;
            X = 0;
            Y = 0;
            Z = 0;
        }
    }

    

    internal class FastWalkStack
    {
        public const int MAX_FAST_WALK_STACK_SIZE = 5;

        private readonly uint[] _keys = new uint[MAX_FAST_WALK_STACK_SIZE];

        public void SetValue(int index, uint value)
        {
            if (index >= 0 && index < MAX_FAST_WALK_STACK_SIZE)
            {
                _keys[index] = value;
            }
        }

        public void AddValue(uint value)
        {
            for (int i = 0; i < MAX_FAST_WALK_STACK_SIZE; i++)
            {
                if (_keys[i] == 0)
                {
                    _keys[i] = value;

                    break;
                }
            }
        }

        public uint GetValue()
        {
            for (int i = 0; i < MAX_FAST_WALK_STACK_SIZE; i++)
            {
                uint key = _keys[i];

                if (key != 0)
                {
                    _keys[i] = 0;

                    return key;
                }
            }

            return 0;
        }
    }

    internal struct Step
    {
        public int X, Y;
        public sbyte Z;
        public byte Direction;
        public bool Run;

        public Step(int empty)
        {
            X = 0;
            Y = 0;
            Z = 0;
            Direction = 0;
            Run = false;
        }
    }

    internal class WalkerManager
    {
        public NetClient owningNetClient;

        public FastWalkStack FastWalkStack { get; } = new FastWalkStack();
        public ushort CurrentPlayerZ = 0;
        public byte CurrentWalkSequence = 0;
        public long LastStepRequestTime = 0;
        public ushort NewPlayerZ = 0;
        public bool ResendPacketResync;
        public const int MAX_STEP_COUNT = 5;
        public StepInfo[] StepInfos = new StepInfo[MAX_STEP_COUNT]
        {
            new StepInfo(), new StepInfo(), new StepInfo(),
            new StepInfo(), new StepInfo()
        };
        public int StepsCount;
        public int UnacceptedPacketsCount;
        public bool WalkingFailed;
        public byte WalkSequence;
        public bool WantChangeCoordinates = false;

        public int PlayerX;
        public int PlayerY;
        public int PlayerZ;

        public int TeleportPlayerX = 0;
        public int TeleportPlayerY = 0;
        public int TeleportPlayerZ = 0;

        public bool IsPlayerAtTeleportLocation()
        {
            return PlayerX == TeleportPlayerX && PlayerY == TeleportPlayerY;
        }

        public int OffsetX;
        public int OffsetY;
        public int OffsetZ;
        public Deque<Step> Steps { get; } = new Deque<Step>(MAX_STEP_COUNT);

        public void DenyWalk(byte sequence, int x, int y, sbyte z)
        {
            Steps.Clear();
            OffsetX = 0;
            OffsetY = 0;
            OffsetZ = 0;

            Reset();

            if (x != -1)
            {
                PlayerX = (ushort)x;
                PlayerY = (ushort)y;
                PlayerZ = z;
            }
        }

        public void ConfirmWalk(byte sequence)
        {
            if (UnacceptedPacketsCount != 0)
            {
                UnacceptedPacketsCount--;
            }

            int stepIndex = 0;

            for (int i = 0; i < StepsCount; i++)
            {
                if (StepInfos[i].Sequence == sequence)
                {
                    break;
                }

                stepIndex++;
            }

            bool isBadStep = stepIndex == StepsCount;


            if (!isBadStep)
            {
                if (stepIndex >= CurrentWalkSequence)
                {
                    StepInfos[stepIndex].Accepted = true;
                }
                else if (stepIndex == 0)
                {
                    for (int i = 1; i < StepsCount; i++)
                    {
                        StepInfos[i - 1] = StepInfos[i];
                    }

                    StepsCount--;
                    CurrentWalkSequence--;
                }
                else
                {
                    isBadStep = true;
                }
            }

            if (isBadStep)
            {
                if (!ResendPacketResync)
                {
                    owningNetClient.Send(new PResend());
                    ResendPacketResync = true;
                }

                WalkingFailed = true;
                StepsCount = 0;
                CurrentWalkSequence = 0;
            }
        }

        public void Reset()
        {
            UnacceptedPacketsCount = 0;
            StepsCount = 0;
            WalkSequence = 0;
            CurrentWalkSequence = 0;
            WalkingFailed = false;
            ResendPacketResync = false;
            LastStepRequestTime = 0;
        }
    }
}
