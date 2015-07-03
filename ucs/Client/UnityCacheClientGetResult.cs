using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.Gabosgab.UnityCache.Client
{
    public enum CacheResult
    {
        CacheMiss,
        CacheHit
    }


    public class UnityCacheClientGetResult
    {
        public Guid Id
        {
            get;
            set;
        }

        public string Hash
        {
            get;
            set;
        }

        public CacheResult Result
        {
            get;
            set;
        }

        public byte[] Data
        {
            get;
            set;
        }

    }
}
