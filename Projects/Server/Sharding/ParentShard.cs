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
    }
}
