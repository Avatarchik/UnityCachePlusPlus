﻿// <copyright file="FileCacheManager.cs" company="Gabe Brown">
//     Copyright (c) Gabe Brown. All rights reserved.
// </copyright>

namespace Com.Yocero.UnityCache.Server
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Threading;
    using NLog;

    /// <summary>
    /// The file cache manager used to managing all files in the cache
    /// </summary>
    public class FileCacheManager
    {
        /// <summary>
        /// Stores the current class log manager
        /// </summary>
        private static Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Stores the location of the root folder for the cache
        /// </summary>
        private string root; 

        /// <summary>
        /// Stores the location of the incoming assets folder
        /// </summary>
        private string incoming;

        /// <summary>
        /// Stores the maximum allowed size of the cache in megabytes
        /// </summary>
        private int maxSizeMB;

        /// <summary>
        /// Stores the current size of cache on disk in bytes
        /// </summary>
        private ulong cacheSizeBytes;

        /// <summary>
        /// A lock used to allow synchronous access to this.cacheSizeBytes
        /// </summary>
        private object cacheSizeBytesLock = new object();

        /// <summary>
        /// Initializes a new instance of the FileCacheManager class.
        /// </summary>
        /// <param name="rootPath">The root path where the cache should be kept</param>
        /// <param name="newMaxSizeMB">The maximum size of the cache in megabytes.  This server will exceed the limit in order to optimize asset throughput, so please allow a 10-20% buffer.</param>
        public FileCacheManager(string rootPath, int newMaxSizeMB)
        {
            this.root = rootPath;
            this.incoming = Path.Combine(this.root, "incoming");
            this.maxSizeMB = newMaxSizeMB;

            if (!Directory.Exists(this.root))
            {
                logger.Warn(CultureInfo.CurrentCulture, "Initializing cache folder: {0}", this.root);
                Directory.CreateDirectory(this.root);
            }

            logger.Warn(CultureInfo.CurrentCulture, "Cache folder is ready: {0}", this.root);

            if (!Directory.Exists(this.incoming)) 
            {
                Directory.CreateDirectory(this.incoming);
            }

            // TODO: Flush the incoming folder of any dead files
            logger.Warn(CultureInfo.CurrentCulture, "Setting max cache size to {0} MB", this.maxSizeMB);

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
            string path = Path.Combine(this.incoming, GetFileName(id, hash));

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

            string src = Path.Combine(this.incoming, fileName);
            string dest = this.GetFullFilePath(id, hash);

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

            logger.Info(CultureInfo.CurrentCulture, "Moving {0} to permanent cache", fileName);
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
            return File.Exists(this.GetFullFilePath(id, hash));
        }

        /// <summary>
        /// Gets the file size of a given file
        /// </summary>
        /// <param name="id">The Id of the file to get the size of</param>
        /// <param name="hash">The has of the file</param>
        /// <returns>The file size in bytes</returns>
        internal ulong GetFileSizeBytes(Guid id, string hash)
        {
            // TODO: Replace all string.Format with Path.Join
            FileInfo info = new FileInfo(this.GetFullFilePath(id, hash));
            return (ulong)info.Length;
        }

        /// <summary>
        /// Returns a file name for the given id and hash combination
        /// </summary>
        /// <param name="id">The Id of the file</param>
        /// <param name="hash">The has of the file</param>
        /// <returns>The filename of the file</returns>
        private static string GetFileName(Guid id, string hash)
        {
            return string.Format(CultureInfo.CurrentCulture, "{0}_{1}.data", id, hash);
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
        private void CalculateFolderSize(object stateInfo)
        {
            logger.Warn("Determining cache folder size");

            IEnumerable<string> files = Directory.EnumerateFiles(this.root, "*", SearchOption.AllDirectories);

            foreach (string file in files)
            {
                FileInfo info = new FileInfo(file);
                lock (this.cacheSizeBytesLock)
                {
                    this.cacheSizeBytes += (ulong)info.Length;
                }
            }

            logger.Warn(CultureInfo.CurrentCulture, "Folder sizing complete, cache size: {0} MB", this.cacheSizeBytes / (1024 * 1024));
        }
    }
}
