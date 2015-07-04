// <copyright file="ServerStatus.cs" company="Gabe Brown">
//     Copyright (c) Gabe Brown. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.Gabosgab.UnityCache
{
    /// <summary>
    /// Describes the current server state
    /// </summary>
    public enum ServerStatus
    {
        /// <summary>
        /// The server is stopped and not accepting connections
        /// </summary>
        Stopped,

        /// <summary>
        /// The server is running and accepting connections
        /// </summary>
        Running
    }
}
