// <copyright file="FileCacheManager.cs" company="Gabe Brown">
//     Copyright (c) Gabe Brown. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Com.Gabosgab.UnityCache.Server
{
    public class FileCacheManager
    {
        /// <summary>
        /// Stores the location of the root folder for the cache
        /// </summary>
        private String root; 

        /// <summary>
        /// Stores the location of the incoming assets folder
        /// </summary>
        private String incoming;

        /// <summary>
        /// Stores the maximum allowed size of the cache in megabytes
        /// </summary>
        private int maxSizeMB;

        /// <summary>
        /// Stores the current size of cache on disk in bytes
        /// </summary>
        private UInt64 cacheSizeBytes;

        /// <summary>
        /// A lock used to allow synchronous access to this.cacheSizeBytes
        /// </summary>
        private object cacheSizeBytesLock = new object();

        /// <summary>
        /// Initializes a new instance of the FileCacheManager class.
        /// </summary>
        /// <param name="rootPath">The root path where the cache should be kept</param>
        /// <param name="newMaxSizeMB">The maximum size of the cache in megabytes.  This server will exceed the limit in order to optimize asset throughput, so please allow a 10-20% buffer.</param>
        public FileCacheManager(String rootPath, int newMaxSizeMB)
        {
            this.root = rootPath;
            this.incoming = Path.Combine(this.root, "incoming");
            this.maxSizeMB = newMaxSizeMB;

            if(!Directory.Exists(this.root))
            {
                Console.WriteLine("Initializing cache folder: {0}", this.root);
                Directory.CreateDirectory(this.root);
            }
            Console.WriteLine("Cache folder is ready: {0}", this.root);

            if(!Directory.Exists(incoming)) 
            {
                Directory.CreateDirectory(incoming);
            }

            // TODO: Flush the incoming folder of any dead files
            Console.WriteLine("Setting max cache size to {0} MB", this.maxSizeMB);

            // Queue a background task to size the cache folder
            ThreadPool.QueueUserWorkItem(new WaitCallback(this.CalculateFolderSize));

            // TODO: Setup tracking of the file cleanup and additions and trigger cleanups as needed
        }

        /// <summary>
        /// Opens a file stream to the file that represents the temporary cache file.  The caller should 
        /// close the file and then call CompleteFile to move it to permanent storage.
        /// </summary>
        /// <param name="id">The id of the file</param>
        /// <param name="hash">The hash of the file</param>
        /// <returns>A file stream to the temporary file</returns>
        public FileStream GetTemporaryFile(Guid id, string hash)
        {
            string path = Path.Combine(incoming, GetFileName(id, hash));

            return File.OpenWrite(path);
        }

        /// <summary>
        /// Opens a file stream to the given asset
        /// </summary>
        /// <param name="id">The Id of the file</param>
        /// <param name="hash">The hash of the file</param>
        /// <returns>A read only file handle to the asset</returns>
        public FileStream GetReadFileStream(Guid id, string hash)
        {
            string path = Path.Combine(this.root, GetFolder(hash), GetFileName(id, hash));
            return File.OpenRead(path);
        } 

        /// <summary>
        /// Moves the file from the incoming temporary folder and moves it to permanent storage
        /// </summary>
        /// <param name="id">The Id of the file</param>
        /// <param name="hash">The hash of the file</param>
        public void CompleteFile(Guid id, string hash)
        {
            string fileName = GetFileName(id, hash);

            String src = Path.Combine(incoming, fileName);
            String dest = GetFullFilePath(id, hash);

            if (!Directory.Exists(Path.GetDirectoryName(dest))) 
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
            }

            // For some reason the cache server is asking to overwrite the file, 
            if (this.IsFileCached(id, hash))
            {
                File.Delete(this.GetFullFilePath(id, hash));
            }

            File.Move(src, dest);

            Console.WriteLine("Moving {0} to permanent cache", fileName);
        }

        /// <summary>
        /// Returns the full path of a given file
        /// </summary>
        /// <param name="id">The Id of the file</param>
        /// <param name="hash">The hash of the file</param>
        /// <returns>The full path of the file</returns>
        public string GetFullFilePath(Guid id, string hash) 
        {
            return Path.Combine(this.root, GetFolder(hash), GetFileName(id, hash));
        }

        /// <summary>
        /// Determines if the given file is cached or not
        /// </summary>
        /// <param name="id">The Id of the file</param>
        /// <param name="hash">The hash of the file</param>
        /// <returns>True if the file is cached, false otherwise</returns>
        public bool IsFileCached(Guid id, string hash)
        {
            return File.Exists(GetFullFilePath(id, hash));
        }

        /// <summary>
        /// Returns a file name for the given id and hash combination
        /// </summary>
        /// <param name="id">The Id of the file</param>
        /// <param name="hash">The has of the file</param>
        /// <returns>The filename of the file</returns>
        private static string GetFileName(Guid id, string hash)
        {
            return String.Format(CultureInfo.CurrentCulture, "{0}_{1}.data", id, hash);
        }

        /// <summary>
        /// Returns the folder where the hash should be stored
        /// </summary>
        /// <param name="hash">The hash of the folder</param>
        /// <returns>The file's folder name</returns>
        private static string GetFolder(string hash)
        {
            return hash.Substring(0, 2);
        }

        /// <summary>
        /// Runs a full calculation of the cache folder size
        /// </summary>
        /// <param name="stateInfo">Unused, ignored</param>
        private void CalculateFolderSize(Object stateInfo)
        {
            Console.WriteLine("Determining cache folder size");

            IEnumerable<string> files = Directory.EnumerateFiles(this.root, "*", SearchOption.AllDirectories);

            foreach (string file in files)
            {
                FileInfo info = new FileInfo(file);
                lock (this.cacheSizeBytesLock)
                {
                    this.cacheSizeBytes += (ulong)info.Length;
                }
            }

            Console.WriteLine("Folder sizing complete, cache size: {0} MB", this.cacheSizeBytes / (1024 * 1024));
        }

        /// <summary>
        /// Gets the file size of a given file
        /// </summary>
        /// <param name="id">The Id of the file to get the size of</param>
        /// <param name="hash">The has of the file</param>
        /// <returns>The file size in bytes</returns>
        internal ulong GetFileSizeBytes(Guid id, string hash)
        {
            // TODO: Replace all String.Format with Path.Join
            FileInfo info = new FileInfo(GetFullFilePath(id, hash));
            return (ulong)info.Length;
        }
    }
}
