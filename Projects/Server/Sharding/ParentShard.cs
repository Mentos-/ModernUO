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
            do
            {
                state._lastChildShardAuthId = Utility.Random(1, int.MaxValue - 1);
            } while (NetStateChildShardAuthId.ContainsKey(state._lastChildShardAuthId));//make sure we don't hand out the same authId to two different clients

            NetStateChildShardAuthId.Add(state._lastChildShardAuthId, state);
            
            state.SentFirstPacket = false;
            state.SendPlayServerAck(info, state._lastChildShardAuthId);

            logger.Information("Sent change to child shard request. authId: " + state._lastChildShardAuthId);
        }

        //After ChildShard receives authId from client it sends it to us to begin transfer
        public static bool HandleChildShardLoginRequest(NetState childShardNetState, CircularBufferReader reader, ref int packetLength)
        {
            if (ChildShardIpAddresses.Contains(childShardNetState.Address.ToString()) == false)
            {
                return false;
            }

            int authIdFromRequest = reader.ReadInt32();
            logger.Information("LoginServerRequest is from child shard: {0} with authId {1}", childShardNetState.Address, authIdFromRequest);

            NetState parentShardNetState;

            if (NetStateChildShardAuthId.TryGetValue(authIdFromRequest, out parentShardNetState))
            {
                NetStateChildShardAuthId.Remove(authIdFromRequest);//make sure we remove the request so that our list doesn't grow indefinitely

                if (parentShardNetState._lastChildShardAuthId == authIdFromRequest)
                {
                    logger.Information("LoginServerSeed authId {0} successfully found for netstate {1}!", authIdFromRequest, parentShardNetState._lastChildShardAuthId);

                    string account = parentShardNetState.Account.Username;
                    string password = "" + authIdFromRequest;
                    childShardNetState.SendChildShardAck(account, password);
                    logger.Information("Sending SendChildShardAck()");
                }
                else
                {
                    logger.Information("LoginServerSeed authId {0} failed to find netstate authid match {1}!", authIdFromRequest, parentShardNetState._lastChildShardAuthId);

                    childShardNetState.SendChildShardAck("","");
                }
            }
            else
            {
                logger.Information("LoginServerSeed authId {0} failed to find netstate in authId->Netstate dictionary!", authIdFromRequest);
            }

            return true;
        }
    }
}
