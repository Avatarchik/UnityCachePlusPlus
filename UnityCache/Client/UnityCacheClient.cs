// <copyright file="UnityCacheClient.cs" company="Gabe Brown">
//     Copyright (c) Gabe Brown. All rights reserved.
// </copyright>

namespace Com.Yocero.UnityCache.Client
{
    using System;
    using System.Globalization;
    using System.Net.Sockets;
    using System.Text;
    using NLog;

    /// <summary>
    /// The Unity cache client used to communicate to a Unity Cache server
    /// </summary>
    public class UnityCacheClient : IDisposable
    {
        /// <summary>
        /// Stores the current class log manager
        /// </summary>
        private static Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Determines if the class has been disposed or not
        /// </summary>
        private bool disposed = false;

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
        public UnityCacheClient(string connection)
        {
            if (string.IsNullOrEmpty(connection))
            {
                throw new ArgumentNullException("connection", "Cannot pass a null server address.");
            }

            // TODO: Make it so the user can specify the connection port number
            this.server = connection;
            this.client = new TcpClient();
        }

        /// <summary>
        /// Gets a value indicating whether the client is connected to the server.
        /// </summary>
        public bool IsConnected
        {
            get;
            private set;
        }

        /// <summary>
        /// Implements Dispose framework
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Connect to the server
        /// </summary>
        public void Connect()
        {
            this.client.Connect(this.server, this.port);
            this.stream = this.client.GetStream();

            // Send the preamble
            WriteClientVersion(this.stream);
            UnityCacheClient.ReadServerVersion(this.stream);

            this.IsConnected = true;
        }

        /// <summary>
        /// Closes the unity cache client
        /// </summary>
        public void Close()
        {
            this.client.Close();
            this.stream.Close();

            this.IsConnected = false;
        }

        /// <summary>
        /// Puts a file to the server
        /// </summary>
        /// <param name="id">The id of the file</param>
        /// <param name="hash">The hash of the file</param>
        /// <param name="data">The data contained in the file</param>
        public void Put(Guid id, string hash, byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException("data", "Argument cannot be null");
            }

            byte[] command = Encoding.ASCII.GetBytes("p");
            this.stream.WriteByte(command[0]);

            // Send the length as ASCII
            ulong dataLength = (ulong)data.Length;
            string lengthStr = dataLength.ToString("X16", CultureInfo.InvariantCulture);
            byte[] lenBytes = Encoding.ASCII.GetBytes(lengthStr);
            this.stream.Write(lenBytes, 0, lenBytes.Length);

            UnityCacheUtilities.SendIdAndHashOnStream(this.stream, id, hash);

            this.stream.Write(data, 0, data.Length);
        }

        /// <summary>
        /// Performs a get synchronously
        /// </summary>
        /// <param name="id">The ID of the file to get</param>
        /// <param name="hash">The hash of the files contents</param>
        /// <returns>The result of the get operation</returns>
        public UnityCacheClientGetResult Get(Guid id, string hash)
        {
            UnityCacheClientGetResult result = new UnityCacheClientGetResult();

            result.Id = id;
            result.Hash = hash;

            byte[] command = Encoding.ASCII.GetBytes("g");
            this.stream.WriteByte(command[0]);

            UnityCacheUtilities.SendIdAndHashOnStream(this.stream, id, hash);

            this.stream.Read(command, 0, command.Length);
            string strResult = Encoding.ASCII.GetString(command);

            if (strResult == "-")
            {
                result.Result = CacheResult.CacheMiss;

                // Read and toss the hash since we don't need it
                UnityCacheUtilities.ReadGuid(this.stream);
                UnityCacheUtilities.ReadHash(this.stream);
            }
            else if (strResult == "+")
            {
                result.Result = CacheResult.CacheHit;

                // Read the length of the file
                byte[] buffer = new byte[16];
                this.stream.Read(buffer, 0, 16);
                ulong bytesToBeRead = UnityCacheUtilities.GetAsciiBytesAsUInt64(buffer);

                // Read the ID and hash.  Toss this, we don't need it
                UnityCacheUtilities.ReadGuid(this.stream);
                UnityCacheUtilities.ReadHash(this.stream);

                // Read the reply from the server
                buffer = new byte[bytesToBeRead];
                result.Data = buffer;
                int offset = 0;

                while (bytesToBeRead > 0)
                {
                    int len = (bytesToBeRead > (ulong)this.streamBlockSize) ? this.streamBlockSize : (int)bytesToBeRead;
                    ulong bytesReturned = (ulong)this.stream.Read(buffer, offset, len);
                    bytesToBeRead -= (ulong)bytesReturned;
                    offset += (int)bytesReturned;
                }
            }

            return result;
        }

        /// <summary>
        /// Disposes of 
        /// </summary>
        /// <param name="disposing">Determines if the object should be disposed</param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                // Free any other managed objects here. 
                if (this.client.Connected)
                {
                    this.client.Close();
                }
            }

            // Free any unmanaged objects here. 
            this.disposed = true;
        }

        /// <summary>
        /// Writes the client version on the stream
        /// </summary>
        /// <param name="stream">The stream to write the version to</param>
        private static void WriteClientVersion(NetworkStream stream)
        {
            byte[] version = Encoding.UTF8.GetBytes("fe");
            stream.Write(version, 0, version.Length);
        }

        /// <summary>
        /// Reads the server version off the channel
        /// </summary>
        /// <param name="readStream">The stream to read the version off</param>
        private static void ReadServerVersion(NetworkStream readStream)
        {
            byte[] data = new byte[8];
            readStream.Read(data, 0, data.Length);
            string version = Encoding.UTF8.GetString(data);
            logger.Info(CultureInfo.CurrentCulture, "Server version {0}", version);
        }
    }
}
