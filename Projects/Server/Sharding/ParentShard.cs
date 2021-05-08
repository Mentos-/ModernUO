using System;
using System.Collections.Generic;
using Server.Logging;
using System.Net;
using Server.Network;

using LoadTestUO;

namespace Server.Sharding
{
    public static class ParentShard
    {
        public static List<string> ChildShardIpAddresses = new List<string> { "192.168.1.4" };

        private static Dictionary<int, NetState> NetStateChildShardAuthId = new Dictionary<int, NetState>();

        private static readonly ILogger logger = LogFactory.GetLogger(typeof(ParentShard));

        private const int m_AuthIDWindowSize = 128;
        private static readonly Dictionary<int, AuthIDPersistence> m_AuthIDWindow =
            new(m_AuthIDWindowSize);

        public static void Initialize()
        {
            Timer.DelayCall(TimeSpan.FromSeconds(3.0), Run);
        }

        public static void Run()
        {

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
            logger.Information("PlayerMobile changed location from {0} to {1}", m.Location, oldLocation);

            SendChangeToChildShardRequest(m, oldLocation);
        }

        private static void SendChangeToChildShardRequest(Mobile m, Point3D oldLocation)
        {
            IPEndPoint childShardEndpoint = GetIpEndpointForLocation(m.Location);
            ServerInfo info = new ServerInfo("child", 0, TimeZoneInfo.Utc, childShardEndpoint);

            NetState state = m.NetState;
            state._authId = GenerateAuthID(state);
            state._lastChildShardAuthId = state._authId;
            NetStateChildShardAuthId.Add(state._authId, state);
            
            state.SentFirstPacket = false;
            state.SendPlayServerAck(info, state._authId);

            logger.Information("Sent change to child shard request. state._authId: " + state._authId);
        }

        public static bool HandleChildShardLoginRequest(NetState childShardNetState, CircularBufferReader reader, ref int packetLength)
        {
            if (ChildShardIpAddresses.Contains(childShardNetState.Address.ToString()) == false)
            {
                return false;
            }

            int authIdFromRequest = reader.ReadInt32();
            logger.Information("LoginServerSeed is from child shard: {0} with seed {1}", childShardNetState.Address, authIdFromRequest);

            NetState netstate;

            if (NetStateChildShardAuthId.TryGetValue(authIdFromRequest, out netstate))
            {
                if (netstate._lastChildShardAuthId == authIdFromRequest)
                {
                    logger.Information("LoginServerSeed authId {0} successfully found for netstate {1}!", authIdFromRequest, netstate._lastChildShardAuthId);
                }
                else
                {
                    logger.Information("LoginServerSeed authId {0} failed to find netstate authid match {1}!", authIdFromRequest, netstate._lastChildShardAuthId);
                }
            }
            else
            {
                logger.Information("LoginServerSeed authId {0} failed to find netstate in authId->Netstate dictionary!", authIdFromRequest);
            }

            return true;
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
    }
}
