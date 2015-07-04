// <copyright file="UnityCacheClient.cs" company="Gabe Brown">
//     Copyright (c) Gabe Brown. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Com.Gabosgab.UnityCache.Client
{
    public class UnityCacheClient
    {
        /// <summary>
        /// The server hostname to connect to
        /// </summary>
        private string server = null;

        /// <summary>
        /// The port to connect to
        /// </summary>
        private int port = 8125;

        /// <summary>
        /// The TCP Client the client uses to connect
        /// </summary>
        private TcpClient client;

        /// <summary>
        /// The network string that the client communicates with
        /// </summary>
        private NetworkStream stream;

        /// <summary>
        /// The block size to use for copying data to/from the stream
        /// </summary>
        private int streamBlockSize = 1024;

        /// <summary>
        /// Initializes a new instance of the UnityCacheClient class.
        /// </summary>
        /// <param name="connection">The host of the server to connect to.</param>
        public UnityCacheClient(String connection)
        {
            if (String.IsNullOrEmpty(connection))
            {
                throw new ArgumentNullException("Cannot pass a null server address.");
            }

            // TODO: Make it so the user can specify the connection port number
            this.server = connection;
            this.client = new TcpClient();
        }

        public void Connect()
        {
            this.client.Connect(this.server, this.port);
            this.stream = this.client.GetStream();

            // Send the preamble
            WriteClientVersion(this.stream);
            ReadServerVersion(this.stream);

            this.IsConnected = true;
        }

        /// <summary>
        /// Closes the unity cache client
        /// </summary>
        public void Close()
        {
            this.client.Close();
            this.stream.Close();

            IsConnected = false;
        }

        /// <summary>
        /// Gets a value indicating whether the client is connected to the server.
        /// </summary>
        public bool IsConnected
        {
            get;
            private set;
        }

        public void Put(Guid id, string hash, byte[] data)
        {           
            byte[] command = Encoding.ASCII.GetBytes("p");
            stream.WriteByte(command[0]);

            // Send the length as ASCII
            ulong uDataLen = (ulong)data.Length;
            String lengthStr = uDataLen.ToString("X16");
            byte[] lenBytes = Encoding.ASCII.GetBytes(lengthStr);
            stream.Write(lenBytes, 0, lenBytes.Length);

            UnityCacheUtilities.SendIdAndHashOnStream(stream, id, hash);

            stream.Write(data, 0, data.Length);

        }


        /// <summary>
        /// Performs a get synchronously
        /// </summary>
        /// <param name="id">The ID of the file to get</param>
        /// <param name="hash">The hash of the files contents</param>
        /// <returns>The result of the get operation</returns>
        public UnityCacheClientGetResult Get(Guid id, String hash)
        {
            UnityCacheClientGetResult result = new UnityCacheClientGetResult();

            result.Id = id;
            result.Hash = hash;

            byte[] command = Encoding.ASCII.GetBytes("g");
            stream.WriteByte(command[0]);

            UnityCacheUtilities.SendIdAndHashOnStream(stream, id, hash);

            stream.Read(command, 0, command.Length);
            String strResult = Encoding.ASCII.GetString(command);

            if(strResult == "-")
            {
                result.Result = CacheResult.CacheMiss;

                // Read and toss the hash since we don't need it
                UnityCacheUtilities.ReadGuid(stream);
                UnityCacheUtilities.ReadHash(stream);
            }
            else if(strResult == "+")
            {
                result.Result = CacheResult.CacheHit;

                // Read the length of the file
                byte[] buffer = new byte[16];
                stream.Read(buffer, 0, 16);
                ulong bytesToBeRead = UnityCacheUtilities.GetAsciiBytesAsUInt64(buffer);

                // Read the ID and hash.  Toss this, we don't need it
                UnityCacheUtilities.ReadGuid(stream);
                UnityCacheUtilities.ReadHash(stream);

                // Read the reply from the server
                buffer = new byte[bytesToBeRead];
                result.Data = buffer;
                int offset = 0;

                while (bytesToBeRead > 0)
                {
                    int len = (bytesToBeRead > (ulong)streamBlockSize) ? streamBlockSize : (int)bytesToBeRead;
                    ulong bytesReturned = (ulong)stream.Read(buffer, offset, len);
                    bytesToBeRead -= (ulong)len;
                    offset += len;
                }
            }

            return result;
        }

        private void ReadServerVersion(NetworkStream stream)
        {
            byte[] data = new byte[8];
            stream.Read(data, 0, data.Length);
            string version = Encoding.UTF8.GetString(data);
            Console.WriteLine("Server version {0}", version);
        }

        private static void WriteClientVersion(NetworkStream stream)
        {
            byte[] version = Encoding.UTF8.GetBytes("fe");
            stream.Write(version, 0, version.Length);
        }
    }
}
