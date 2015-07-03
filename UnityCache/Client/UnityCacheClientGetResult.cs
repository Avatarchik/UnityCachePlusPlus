using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.Gabosgab.UnityCache.Client
{
    /// <summary>
    /// Describes what the status of the cache get request was
    /// </summary>
    public enum CacheResult
    {
        /// <summary>
        /// The cache doesn't contain the file
        /// </summary>
        CacheMiss,

        /// <summary>
        /// The cache does contain the file
        /// </summary>
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
