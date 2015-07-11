// <copyright file="FileCacheManager.cs" company="Gabe Brown">
//     Copyright (c) Yocero, LLC All rights reserved.
// </copyright>

namespace Com.Yocero.UnityCache.Server
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using Com.Yocero.UnityCache.Properties;
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
        /// Stores if the cache eviction is underway
        /// </summary>
        private bool evictingCache = false;

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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2208:InstantiateArgumentExceptionsCorrectly", Justification = "Using correct exception for settings file validation")]
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
                logger.Warn(CultureInfo.CurrentCulture, "Creating incoming folder at: {0}", this.incoming);
                Directory.CreateDirectory(this.incoming);
            }

            if (Settings.Default.CacheFreePercentage < 0 && Settings.Default.CacheFreePercentage > 1)
            {
                throw new ArgumentOutOfRangeException("CacheFreePercentage", "CacheFreePercentage must be between 0 and 1.");
            }

            this.EmptyIncomingFolder();
            
            logger.Warn(CultureInfo.CurrentCulture, "Setting max cache size to {0} MB", this.maxSizeMB);

            // Queue a background task to size the cache folder
            ThreadPool.QueueUserWorkItem(new WaitCallback(this.CalculateFolderSize));
        }

        /// <summary>
        /// Gets a value indicating the root folder
        /// </summary>
        public string Root
        {
            get
            {
                return this.root;
            }
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
            string path = Path.Combine(this.incoming, CacheFile.GetFileName(id, hash));
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
            string path = Path.Combine(this.root, CacheFile.GetFolder(hash), CacheFile.GetFileName(id, hash));
            File.SetLastAccessTime(path, DateTime.Now);
            return File.OpenRead(path);
        } 

        /// <summary>
        /// Moves the file from the incoming temporary folder and moves it to permanent storage.
        /// The file is automatically marked as recently accessed in order to prevent it from being evicted.
        /// </summary>
        /// <param name="id">The Id of the file</param>
        /// <param name="hash">The hash of the file</param>
        public void CompleteFile(Guid id, string hash)
        {
            string fileName = CacheFile.GetFileName(id, hash);

            string src = Path.Combine(this.incoming, fileName);
            string dest = CacheFile.GetFullFilePath(this.root, id, hash);

            if (!Directory.Exists(Path.GetDirectoryName(dest))) 
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
            }

            // For some reason the cache server is asking to overwrite the file, 
            if (CacheFile.IsFileCached(this.root, id, hash))
            {
                File.Delete(CacheFile.GetFullFilePath(this.root, id, hash));
            }

            File.Move(src, dest);
            File.SetLastAccessTime(dest, DateTime.Now);

            FileInfo info = new FileInfo(dest);
            lock (this.cacheSizeBytesLock)
            {
                // Increment the cache size by adding a file
                this.cacheSizeBytes += (ulong)info.Length;
                int limit = Settings.Default.MaxCacheSizeMB * 1048576;

                // Check we haven't exceeded the cap
                if (this.cacheSizeBytes > (ulong)limit && !this.evictingCache)
                {
                    // We've exceeded the cache cap, request a cleanup
                    this.evictingCache = true;
                    ThreadPool.QueueUserWorkItem(new WaitCallback(this.Evict));
                }
            }

            // Store a hit on the ojbect
            File.SetLastAccessTime(dest, DateTime.Now);

            logger.Info(CultureInfo.CurrentCulture, "Moving {0} to permanent cache", fileName);
        }

        /// <summary>
        /// Clears out all the files in the incoming folder
        /// </summary>
        private void EmptyIncomingFolder()
        {
            logger.Warn("Emptying incoming folder of any incomplete downloads.");
            DirectoryInfo downloadedMessageInfo = new DirectoryInfo(this.incoming);

            foreach (FileInfo file in downloadedMessageInfo.GetFiles())
            {
                file.Delete();
            }
        }

        /// <summary>
        /// Walks through all assets in the file system and evicts any items that occur in the past
        /// to take the cache file system below the requested limit.
        /// This should only be called in a background thread since this is a time intensive operation.
        /// </summary>
        /// <param name="state">Ignored, object state</param>
        private void Evict(object state)
        {
            ulong cacheLimitBytes = (ulong)Settings.Default.MaxCacheSizeMB * 1048576;

            if (this.cacheSizeBytes < cacheLimitBytes)
            {
                // Cache isn't big enough to evict
                logger.Info(
                    "Cache isn't large enough to require eviction.  Max: {0} MB, Current: {1} MB",
                    Settings.Default.MaxCacheSizeMB,
                    this.cacheSizeBytes / 1048576);
                return;
            }

            logger.Warn(
                "Cache eviction is starting to prune.  Max: {0} MB, Current: {1} MB", 
                Settings.Default.MaxCacheSizeMB,
                this.cacheSizeBytes / 1048576);

            List<CacheFile> fileData = this.EnumerateAllCacheFiles();

            // Run the list of elements and delete the files that were accessed the furthest in the past
            var filesSorted = from a in fileData
                        orderby a.LastAccessed ascending
                        select a;
            IEnumerator<CacheFile> fileEnumerator = filesSorted.GetEnumerator();
            fileEnumerator.MoveNext();

            // Delete files until we are within 90% of the limit
            while (this.cacheSizeBytes > (cacheLimitBytes * Settings.Default.CacheFreePercentage))
            {
                CacheFile file = fileEnumerator.Current;

                lock (this.cacheSizeBytesLock)
                {
                    this.cacheSizeBytes -= (ulong)file.Length;
                }

                logger.Warn(
                    "Deleting last accessed {1} file {0}", 
                    file.LastAccessed, 
                    CacheFile.GetFileName(file.Id, file.Hash));
                File.Delete(CacheFile.GetFullFilePath(this.root, file.Id, file.Hash));

                if (!fileEnumerator.MoveNext())
                {
                    // There are no more files to clean
                    break;
                }
            }

            logger.Warn(
                "Cache eviction is complete.  Max: {0} MB, Current: {1} MB",
                Settings.Default.MaxCacheSizeMB,
                this.cacheSizeBytes / 1048576);
            this.evictingCache = false;
        }

        /// <summary>
        /// Runs a full calculation of the cache folder size
        /// </summary>
        /// <param name="stateInfo">Unused, ignored</param>
        private void CalculateFolderSize(object stateInfo)
        {
            logger.Warn("Determining cache folder size and marking items for eviction");
            lock (this.cacheSizeBytesLock)
            {
                this.cacheSizeBytes = 0;
            }

            List<CacheFile> files = this.EnumerateAllCacheFiles();

            foreach (CacheFile file in files)
            {
                lock (this.cacheSizeBytesLock)
                {
                    this.cacheSizeBytes += (ulong)file.Length;
                }
            }

            logger.Warn(CultureInfo.CurrentCulture, "Folder sizing complete, cache size: {0} MB", this.cacheSizeBytes / (1024 * 1024));

            // Request eviction since we might be oversize
            ThreadPool.QueueUserWorkItem(new WaitCallback(this.Evict));
        }

        /// <summary>
        /// Produces a list of all the files currently in the cache
        /// </summary>
        /// <returns>A list of all files in the cache</returns>
        private List<CacheFile> EnumerateAllCacheFiles()
        {
            IEnumerable<string> files = Directory.EnumerateFiles(this.root, "*.data", SearchOption.AllDirectories);
            List<CacheFile> fileData = new List<CacheFile>();

            // Build a list of objects we can query
            foreach (string file in files)
            {
                Guid id;
                string hash = string.Empty;

                if (file.Contains(this.incoming))
                {
                    // This is in the incoming folder, skip it
                    continue;
                }

                if (!CacheFile.ParseFileName(Path.GetFileName(file), out id, out hash))
                {
                    // Not a cache file, skip it
                    continue;
                }

                CacheFile cacheFile = new CacheFile();
                cacheFile.Load(this.root, id, hash);
                fileData.Add(cacheFile);
            }

            return fileData;
        }
    }
}
