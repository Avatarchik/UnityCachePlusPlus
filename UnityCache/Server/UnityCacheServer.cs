// <copyright file="UnityCacheServer.cs" company="Gabe Brown">
//     Copyright (c) Gabe Brown. All rights reserved.
// </copyright>

namespace Com.Yocero.UnityCache.Server
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using Com.Yocero.UnityCache.Properties;
    using NLog;

    /// <summary>
    /// The Unity cache server 
    /// </summary>
    public class UnityCacheServer
    {
        /// <summary>
        /// Stores a reference to the log manager
        /// </summary>
        private static Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// The protocol version of the server
        /// </summary>
        private int protocolVersion = 255;

        /// <summary>
        /// The socket used to communicate with clients
        /// </summary>
        private TcpListener socket;

        /// <summary>
        /// The servers current status
        /// </summary>
        private ServerStatus status = ServerStatus.Stopped;

        /// <summary>
        /// The file cache manager that keeps track of files
        /// </summary>
        private FileCacheManager fileManager;

        /// <summary>
        /// The stream block size used to reading and writing data
        /// </summary>
        private int streamBlockSize = 1024;

        /// <summary>
        /// Initializes a new instance of the UnityCacheServer class.
        /// </summary>
        public UnityCacheServer()
        {
            this.fileManager = new FileCacheManager(
                Path.GetFullPath(Settings.Default.CacheRootPath),
                Settings.Default.MaxCacheSizeMB);
        }

        /// <summary>
        /// An event that is triggered when a put is processed from a client
        /// </summary>
        public event EventHandler OnPutProcessed;

        /// <summary>
        /// An event that is triggered when a get is processed from a client
        /// </summary>
        public event EventHandler OnGetProcessed;

        /// <summary>
        /// Gets the server status
        /// </summary>
        public ServerStatus Status
        {
            get
            {
                return this.status;
            }
        }

        /// <summary>
        /// Start this instance.
        /// </summary>
        public void Start() 
        {
            this.status = ServerStatus.Running;
            this.socket = new TcpListener(IPAddress.Any, Settings.Default.Port);
            this.socket.Start();
            this.StartAccept();

            logger.Info(CultureInfo.CurrentCulture, "Server listening for connections on port {0}...", Settings.Default.Port);
        }

        /// <summary>
        /// Stop this instance.
        /// </summary>
        public void Stop() 
        {
            this.status = ServerStatus.Stopped;
            this.socket.Stop();
        }

        /// <summary>
        /// Used to accept sockets
        /// </summary>
        private void StartAccept()
        {
            this.socket.BeginAcceptTcpClient(this.DoAcceptTcpClientCallback, this.socket);
        }

        /// <summary>
        /// Process the client connection
        /// </summary>
        /// <param name="ar">The result to process</param>
        private void DoAcceptTcpClientCallback(IAsyncResult ar)
        {
            if (this.status == ServerStatus.Stopped)
            {
                // This is called when the server is shutting down
                return;
            }

            // Allow other threads to listen for connections while this one is processed
            this.StartAccept();

            try
            {
                TcpClient client = this.socket.EndAcceptTcpClient(ar);

                logger.Info("Accepting connection from " + client.Client.RemoteEndPoint.ToString());

                NetworkStream stream = client.GetStream();

                // Read client version
                byte[] versionBytes = new byte[2];
                stream.Read(versionBytes, 0, 2);
                string versionHex = Encoding.UTF8.GetString(versionBytes);
                int clientVersion = Convert.ToInt32(versionHex, 16);
                logger.Info(CultureInfo.CurrentCulture, "Client Version {0}", clientVersion);

                // Tell the client our version number as a 32-bit integer
                string serverVersion = this.protocolVersion.ToString("x8", CultureInfo.InvariantCulture);
                logger.Info(serverVersion);
                byte[] serverVersionBytes = Encoding.UTF8.GetBytes(serverVersion);
                stream.Write(serverVersionBytes, 0, serverVersionBytes.Length);

                int command = 0;
                
                while ((command = stream.ReadByte()) != -1)
                {
                    switch (command)
                    {
                        case 112: 
                            this.ProcessPut(stream);
                            break;

                        case 103:
                            this.ProcessGet(stream);
                            break;

                        default:
                            // No command was sent, go ahead and close the connection.
                            break;
                    }
                }

                stream.Close();
                client.Close();
            }
            catch (IOException)
            {
                logger.Info("Connection was closed by the client.");
                this.StartAccept();
            }
        }

        /// <summary>
        /// Processes the get command
        /// </summary>
        /// <param name="stream">The stream to the client</param>
        private void ProcessGet(NetworkStream stream) 
        {
            // Read ID
            Guid id = UnityCacheUtilities.ReadGuid(stream);
            string hash = UnityCacheUtilities.ReadHash(stream);

            if (!CacheFile.IsFileCached(this.fileManager.Root, id, hash))
            {
                logger.Info("GET: Cache miss. {0} {1}", id, hash);

                // File is not cached
                // Send command it's not cached
                byte[] code = new byte[1];
                code[0] = 45;
                stream.Write(code, 0, 1);

                // Send id and hash 
                UnityCacheUtilities.SendIdAndHashOnStream(stream, id, hash);
            } 
            else
            {
                logger.Info("GET: Cache hit. {0} {1}", id, hash);

                using (MemoryStream memoryStream = new MemoryStream(49))
                {
                    // File is cached, send the response
                    byte[] code = new byte[1];
                    code[0] = 43;
                    memoryStream.Write(code, 0, 1);

                    // Send the file size in bytes
                    ulong bytesToBeWritten = CacheFile.GetFileSizeBytes(this.fileManager.Root, id, hash);   // Dumb off by 1 hack
                    byte[] fileSizeBytes = UnityCacheUtilities.GetUlongAsAsciiBytes(bytesToBeWritten);
                    memoryStream.Write(fileSizeBytes, 0, fileSizeBytes.Length);

                    // Send id and hash 
                    UnityCacheUtilities.SendIdAndHashOnStream(memoryStream, id, hash);

                    // Send the file bytes
                    FileStream fileStream = this.fileManager.GetReadFileStream(id, hash);
                    byte[] buffer = new byte[this.streamBlockSize];

                    // Workaround to get enough bytes into a single packet so the Unity client doesn't choke
                    byte[] header = memoryStream.GetBuffer();
                    stream.Write(header, 0, header.Length);

                    while (bytesToBeWritten > 0)
                    {
                        int byteCount = (bytesToBeWritten > (ulong)this.streamBlockSize) ? this.streamBlockSize : (int)bytesToBeWritten;
                        fileStream.Read(buffer, 0, byteCount);
                        bytesToBeWritten -= (ulong)byteCount;
                        stream.Write(buffer, 0, byteCount);
                    }

                    fileStream.Close();
                }
            }

            // Notify listeners a get was processed
            if (this.OnGetProcessed != null)
            {
                this.OnGetProcessed(this, new EventArgs());
            }
        }

        /// <summary>
        /// Process the client put request
        /// </summary>
        /// <param name="stream">The stream to the client requesting the put</param>
        private void ProcessPut(NetworkStream stream) 
        {
            byte[] buffer = new byte[16];
            stream.Read(buffer, 0, 16);
            ulong bytesToBeRead = UnityCacheUtilities.GetAsciiBytesAsUInt64(buffer);

            // Read ID
            Guid id = UnityCacheUtilities.ReadGuid(stream);
            
            // Read HASH
            string hash = UnityCacheUtilities.ReadHash(stream);

            logger.Info("PUT: {0} {1}", id, hash);

            FileStream fileStream = this.fileManager.GetTemporaryFile(id, hash);
            buffer = new byte[this.streamBlockSize];

            while (bytesToBeRead > 0)
            {
                int len = (bytesToBeRead > (ulong)this.streamBlockSize) ? this.streamBlockSize : (int)bytesToBeRead;
                ulong bytesReturned = (ulong)stream.Read(buffer, 0, len);
                fileStream.Write(buffer, 0, (int)bytesReturned);
                bytesToBeRead -= (ulong)bytesReturned;
            }

            fileStream.Close();

            this.fileManager.CompleteFile(id, hash);

            // Notify listeners a get was processed
            if (this.OnPutProcessed != null)
            {
                this.OnPutProcessed(this, new EventArgs());
            }
        }
    }
}