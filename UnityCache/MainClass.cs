// <copyright file="MainClass.cs" company="Gabe Brown">
//     Copyright (c) Gabe Brown. All rights reserved.
// </copyright>

namespace Com.Yocero.UnityCache
{
    using System;
    using Com.Yocero.UnityCache.Server;

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
