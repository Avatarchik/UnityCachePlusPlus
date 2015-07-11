// <copyright file="UnityCacheServerTest.cs" company="Gabe Brown">
//     Copyright (c) Yocero, LLC All rights reserved.
// </copyright>

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Security.Cryptography;
using System.Text;
using Com.Yocero.UnityCache.Client;
using Com.Yocero.UnityCache;
using Com.Yocero.UnityCache.Server;
using System.Threading;

namespace UnityCacheServerTests
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable"), TestClass]
    public class UnityCacheServerTest
    {
        private ManualResetEvent signal;

        [TestMethod]
        public void EndToEndTest()
        {

            signal = new ManualResetEvent(false);
            UnityCacheServer server = new UnityCacheServer();
            using (UnityCacheClient client = new UnityCacheClient("localhost"))
            {

                server.OnPutProcessed += server_OnPutProcessed;

                try
                {

                    // Start the server
                    Assert.AreEqual<ServerStatus>(server.Status, ServerStatus.Stopped);
                    server.Start();
                    Assert.AreEqual<ServerStatus>(server.Status, ServerStatus.Running);

                    client.Connect();

                    Guid id = Guid.NewGuid();
                    MD5 md5Hash;
                    string hash;

                    using (md5Hash = MD5.Create()) 
                    {
                         hash = UnityCacheUtilities.ByteArrayToString(md5Hash.ComputeHash(Encoding.UTF8.GetBytes(DateTime.Now.ToString())));
                    }

                    Console.WriteLine("Requesting ID: {0}, Hash {1}", id, hash);
                    UnityCacheClientGetResult result;

                    // Perform a get
                    for (int x = 0; x < 100; x++)
                    {
                        // Verify that sending the same command over and over works correctly
                        result = client.Get(id, hash);
                        Assert.AreEqual<CacheResult>(result.Result, CacheResult.CacheMiss);
                    }

                    // Perform a put
                    int dataLen = 1024 * 10 + DateTime.Now.Second % 2;          // Test that even/odd file lengths work correctly randomly
                    byte[] data = new byte[dataLen];
                    Random r = new Random();
                    for (int x = 0; x < data.Length; x++)
                    {
                        data[x] = (byte)r.Next();
                    }
                    client.Put(id, hash, data);

                    // Wait for the server to process the request since there isn't an ACK
                    signal.WaitOne();

                    // Fetch the file we just put
                    result = client.Get(id, hash);

                    Assert.AreEqual<CacheResult>(result.Result, CacheResult.CacheHit);
                    Assert.AreEqual(result.Data.Length, dataLen);

                    for (int x = 0; x < data.Length; x++)
                    {
                        Assert.AreEqual<byte>(data[x], result.Data[x], "Data does not match at position {0}", x);
                    }
                }
                finally
                {
                    if (server.Status == ServerStatus.Running)
                        server.Stop();
                }
            }
        }

        void server_OnPutProcessed(object sender, EventArgs e)
        {
            // Let the thread rock and roll
            signal.Set();
        }
    }
}
