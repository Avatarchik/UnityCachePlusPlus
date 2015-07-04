// <copyright file="MainClass.cs" company="Gabe Brown">
//     Copyright (c) Gabe Brown. All rights reserved.
// </copyright>

using System;
using Com.Gabosgab.UnityCache.Server;

namespace Com.Gabosgab.UnityCache
{
    public static class MainClass
    {
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
