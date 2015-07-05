// <copyright file="UnityCacheClientGetResult.cs" company="Gabe Brown">
//     Copyright (c) Gabe Brown. All rights reserved.
// </copyright>

namespace Com.Yocero.UnityCache.Client
{
    using System;

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

    /// <summary>
    /// The UnityClient get result return type
    /// </summary>
    public class UnityCacheClientGetResult
    {
        /// <summary>
        /// Gets or sets the requested id 
        /// </summary>
        public Guid Id
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the requested hash
        /// </summary>
        public string Hash
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the cache result
        /// </summary>
        public CacheResult Result
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the data received from the request
        /// </summary>
        public byte[] Data
        {
            get;
            set;
        }
    }
}
