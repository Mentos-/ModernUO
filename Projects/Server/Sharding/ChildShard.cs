using System;
using System.Collections.Generic;
using Server.Logging;

namespace Server.Sharding
{
    public static class ChildShard
    {
        private static readonly ILogger logger = LogFactory.GetLogger(typeof(ChildShard));

        public static void Initialize()
        {
            Timer.DelayCall(TimeSpan.FromSeconds(3.0), Run);
        }

        public static void Run()
        {
            if (Core.IsChildShard)
            {
                logger.Information("ChildShard: Pinging parent server {0}:{1} to let them know we exist.", Core.ParentIp, Core.ParentPort);

            }
        }
    }
}
