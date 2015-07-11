// <copyright file="MainClass.cs" company="Gabe Brown">
//     Copyright (c) Yocero, LLC All rights reserved.
// </copyright>

namespace Com.Yocero.UnityCache
{
    using System;
    using Com.Yocero.UnityCache.Server;
using NLog;

    /// <summary>
    /// Main runtime class
    /// </summary>
    public static class MainClass
    {
        /// <summary>
        /// Stores the current class log manager
        /// </summary>
        private static Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// The main runtime class
        /// </summary>
        public static void Main()
        {
            UnityCacheServer server = new UnityCacheServer();

            server.Start();

            logger.Info("Press any key to shutdown...");
            Console.ReadKey();
            logger.Info("Shutting down server...");
            server.Stop();
         }
    }
}
