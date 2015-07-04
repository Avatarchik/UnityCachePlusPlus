// <copyright file="MainClass.cs" company="Gabe Brown">
//     Copyright (c) Gabe Brown. All rights reserved.
// </copyright>

using Com.Gabosgab.UnityCache.Server;
using System;

namespace Com.Gabosgab.UnityCache
{
    /// <summary>
    /// Main runtime class
    /// </summary>
    public static class MainClass
    {
        /// <summary>
        /// The main runtime class
        /// </summary>
        public static void Main()
        {
            UnityCacheServer server = new UnityCacheServer();

            server.Start();

            Console.WriteLine("Press any key to shutdown...");
            Console.ReadKey();
            Console.WriteLine("Shutting down server...");
            server.Stop();
         }
    }
}
