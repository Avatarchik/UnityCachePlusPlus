using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.Gabosgab.UnityCache
{
    public enum ServerStatus
    {
        /// <summary>
        /// The server is stopped and not accepting connections
        /// </summary>
        Stopped,

        /// <summary>
        /// The server is running and accetping connections
        /// </summary>
        Running
    }
}
