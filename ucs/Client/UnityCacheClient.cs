﻿using System;
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
        private string server = null;
        private int port = 8125;
        private TcpClient client;
        private NetworkStream stream;
        private int streamBlockSize = 1024;

        /// <summary>
        /// Constructs the Unity Cache Client
        /// </summary>
        /// <param name="newServer">The host and port of the server to connect to.  If port == 0, default of 8125 will be used.</param>
        public UnityCacheClient(String connection)
        {
            if (String.IsNullOrEmpty(connection))
                throw new ArgumentNullException("Cannot pass a null server address.");

            // TODO: Make it so the user can specify the connection port number
            this.server = connection;
            client = new TcpClient();
        }

        public void Connect()
        {
            client.Connect(server, port);
            stream = client.GetStream();

            // Send the preamble
            WriteClientVersion(stream);
            ReadServerVersion(stream);

            IsConnected = true;
        }

        public void Close()
        {
            client.Close();
            stream.Close();

            IsConnected = false;
        }

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

            UnityCacheUtils.SendIdAndHashOnStream(stream, id, hash);

            stream.Write(data, 0, data.Length);

        }


        /// <summary>
        /// Performs a get synchronusly
        /// </summary>
        /// <param name="id">The ID of the file to get</param>
        /// <param name="hash">The hash of the files contents</param>
        public UnityCacheClientGetResult Get(Guid id, String hash)
        {
            UnityCacheClientGetResult result = new UnityCacheClientGetResult();

            result.Id = id;
            result.Hash = hash;

            byte[] command = Encoding.ASCII.GetBytes("g");
            stream.WriteByte(command[0]);

            UnityCacheUtils.SendIdAndHashOnStream(stream, id, hash);

            stream.Read(command, 0, command.Length);
            String strResult = Encoding.ASCII.GetString(command);

            if (strResult == "-")
            {
                result.Result = CacheResult.CacheMiss;

                // Read and toss the hash since we don't need it
                UnityCacheUtils.ReadGuid(stream);
                UnityCacheUtils.ReadHash(stream);
            }
            else if(strResult == "+")
            {
                result.Result = CacheResult.CacheHit;

                // Read the length of the file
                byte[] buffer = new byte[16];
                stream.Read(buffer, 0, 16);
                ulong bytesToBeRead = UnityCacheUtils.GetASCIIBytesAsUInt64(buffer);

                // Read the ID and hash.  Toss this, we don't need it
                UnityCacheUtils.ReadGuid(stream);
                UnityCacheUtils.ReadHash(stream);

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