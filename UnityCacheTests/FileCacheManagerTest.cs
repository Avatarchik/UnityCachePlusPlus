// <copyright file="FileCacheManagerTest.cs" company="Gabe Brown">
//     Copyright (c) Gabe Brown. All rights reserved.
// </copyright>

using Com.Yocero.UnityCache.Server;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace UnityCacheServerTests
{
    [TestClass]
    public class FileCacheManagerTest
    {
        [TestMethod]
        public void ConstructorTest()
        {
            string path = Path.Combine(Environment.CurrentDirectory, "cache");
            Console.WriteLine("Path {0}", path);
            FileCacheManager ft = new FileCacheManager(path, 200 * 1024 * 1024);

            Assert.IsNotNull(ft);
        }

        // TODO: Implement rest of tests
    }
}
