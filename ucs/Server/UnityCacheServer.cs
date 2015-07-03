// ------------------------------------------------------------------------------
//  <autogenerated>
//      This code was generated by a tool.
//      Mono Runtime Version: 4.0.30319.1
// 
//      Changes to this file may cause incorrect behavior and will be lost if 
//      the code is regenerated.
//  </autogenerated>
// ------------------------------------------------------------------------------
using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using Com.Gabosgab.UnityCache.Properties;
using System.IO;
using System.Globalization;
using System.Threading;


namespace Com.Gabosgab.UnityCache.Server
{
	public class UnityCacheServer
	{
        private int PROTOCOL_VERSION = 255;
		private TcpListener socket;
        private ServerStatus status = ServerStatus.Stopped;
        private FileCacheManager fileManager;
        private int streamBlockSize = 1024;

        /// <summary>
        /// Returns the server status
        /// </summary>
        public ServerStatus Status
        {
            get 
            {
                return status;
            }
        }

        /// <summary>
        /// Class constructor
        /// </summary>
		public UnityCacheServer ()
		{
            fileManager = new FileCacheManager(
                Path.GetFullPath(Settings.Default.CacheRootPath),
                Settings.Default.MaxCacheSizeMB);
		}

        // TODO: Have the event share what was put
        public event EventHandler OnPutProcessed;

        // TODO: Have the event share what was get
        public event EventHandler OnGetProcessed;

		/// <summary>
		/// Start this instance.
		/// </summary>
		public void Start() {
            status = ServerStatus.Running;
			socket = new TcpListener (IPAddress.Any, Settings.Default.Port);
			socket.Start ();
			StartAccept ();

            Console.WriteLine("Server listening for conenctions on port {0}...", Settings.Default.Port);
		}

        /// <summary>
        /// Used to accept sockets
        /// </summary>
		private void StartAccept() {
			socket.BeginAcceptTcpClient(DoAcceptTcpClientCallback, socket);
		}

		/// <summary>
		/// Stop this instance.
		/// </summary>
		public void Stop() {
            status = ServerStatus.Stopped;
            socket.Stop();
        }

        /// <summary>
        /// Process the client connection
        /// </summary>
        /// <param name="ar"></param>
        private void DoAcceptTcpClientCallback(IAsyncResult ar)
        {
            if (status == ServerStatus.Stopped)
            {
                // This is called when the server is shutting down
                return;
            }

            // Allow other threads to listen for connections while this one is processed
            StartAccept();

            try
            {

                TcpClient client = socket.EndAcceptTcpClient(ar);

                Console.WriteLine("Accepting connection from " + client.Client.RemoteEndPoint.ToString());

                NetworkStream stream = client.GetStream();

                // Read client version
                byte[] versionBytes = new byte[2];
                stream.Read(versionBytes, 0, 2);
                String versionHex = Encoding.UTF8.GetString(versionBytes);
                int clientVersion = Convert.ToInt32(versionHex, 16);
                Console.WriteLine("Client Version {0}", clientVersion);

                // Tell the client our version number as a 32-bit integer
                string serverVersion = PROTOCOL_VERSION.ToString("x8");
                Console.WriteLine(serverVersion);
                byte[] serverVersionBytes = Encoding.UTF8.GetBytes(serverVersion);
                stream.Write(serverVersionBytes, 0, serverVersionBytes.Length);

                int command = 0;

                
                while ((command = stream.ReadByte()) != -1)
                {
                    switch (command)
                    {

                        case 112: //'p'
                            Console.WriteLine("Process Put");
                            ProcessPut(stream);
                            break;

                        case 103:   // 'g'
                            Console.WriteLine("Process GET");
                            ProcessGet(stream);
                            break;

                        default:
                            // No command was sent, go ahead and close the connection.
                            break;

                    }
                }

                stream.Close();
                client.Close();
            }
            catch (IOException ex)
            {
                Console.WriteLine("Connection was closed by the client.");
                StartAccept();
            }
        }


		/// <summary>
		/// Processes the get.
		/// 
		/// client --- 'g' (id <128bit GUID><128bit HASH>) --> server
		/// </summary>
		/// <param name="stream">Stream.</param>
		private void ProcessGet(NetworkStream stream) {

			// Read ID
            Guid id = UnityCacheUtilities.ReadGuid(stream);

			// Read HASH
            String hash = UnityCacheUtilities.ReadHash(stream);

			Console.WriteLine ("GET: {0} => {1}", id, hash);

            if (!this.fileManager.IsFileCached(id, hash))
            {
                Console.WriteLine("GET: Cache miss. {0} {1}", id, hash);

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
                Console.WriteLine("GET: Cache hit. {0} {1}", id, hash);

                MemoryStream mStream = new MemoryStream(49);

                // File is cached, send the response
                // client <-- '+' (size <uint64>) (id <128bit GUID><128bit HASH>) + size bytes  --- server    (found in cache)
                // Send command the file is cached
                byte[] code = new byte[1];
                code[0] = 43;
                mStream.Write(code, 0, 1);

                // Send the file size in bytes
                ulong bytesToBeWritten = fileManager.GetFileSizeBytes(id, hash);   // Dumb off by 1 hack
                byte[] fileSizeBytes = UnityCacheUtilities.GetUInt64AsAsciiBytes(bytesToBeWritten);
                mStream.Write(fileSizeBytes, 0, fileSizeBytes.Length);

                // Send id and hash 
                UnityCacheUtilities.SendIdAndHashOnStream(mStream, id, hash);

                // Send the file bytes
                FileStream fileStream = fileManager.GetReadFileStream(id, hash);
                byte[] buffer = new byte[streamBlockSize];

                // Workaround to get enough bytes into a single packet so the Unity client doesn't choke
                byte[] header = mStream.GetBuffer();
                stream.Write(header, 0, header.Length);

                while (bytesToBeWritten > 0)
                {
                    int byteCount = (bytesToBeWritten > (ulong)streamBlockSize) ? streamBlockSize : (int)bytesToBeWritten;
                    fileStream.Read(buffer, 0, byteCount);
                    bytesToBeWritten -= (ulong)byteCount;
                    stream.Write(buffer, 0, byteCount);
                }
                fileStream.Close();
            }


            // Notify listeners a get was processed
            if(OnGetProcessed != null)
                OnGetProcessed(this, new EventArgs());

		}

		/// <summary>
		/// client <-- '+' (size <uint64>) (id <128bit GUID><128bit HASH>) + size bytes  --- server    (found in cache)
		/// 
		/// 
		/// </summary>
		/// <param name="stream">Stream.</param>
		private void ProcessPut(NetworkStream stream) {

			byte[] buffer = new byte[16];
			stream.Read(buffer, 0, 16);
            ulong bytesToBeRead = UnityCacheUtilities.GetAsciiBytesAsUInt64(buffer);

			// Read ID
            Guid id = UnityCacheUtilities.ReadGuid(stream);
			
			// Read HASH
			String hash = UnityCacheUtilities.ReadHash (stream);

            Console.WriteLine("PUT: {0} {1}", id, hash);

            FileStream fileStream = fileManager.GetTemporaryFile(id, hash);
            buffer = new byte[streamBlockSize];

            while (bytesToBeRead > 0)
            {
                int len = (bytesToBeRead > (ulong)streamBlockSize) ? streamBlockSize : (int)bytesToBeRead;
                ulong bytesReturned = (ulong)stream.Read(buffer, 0, len);
                fileStream.Write(buffer, 0, (int)bytesReturned);
                bytesToBeRead -= (ulong)bytesReturned;
            }
            fileStream.Close();

            fileManager.CompleteFile(id, hash);

            // Notify listeners a get was processed
            if(OnPutProcessed != null)
                OnPutProcessed(this, new EventArgs());
		}
	}
}

