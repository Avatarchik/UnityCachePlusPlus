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
        private String root; 
        private String incoming;
        private int maxSizeMB;
        private UInt64 currSizeBytes;
        private object currSizeBytesLock = new object();

        /// <summary>
        /// Class constructor
        /// </summary>
        public FileCacheManager(String rootPath, int newMaxSizeMB)
        {
            root = rootPath;
            incoming = Path.Combine(root, "incoming");
            maxSizeMB = newMaxSizeMB;

            if(!Directory.Exists(root))
            {
                Console.WriteLine("Initializing cache folder: {0}", root);
                Directory.CreateDirectory(root);
            }
            Console.WriteLine("Cache folder is ready: {0}", root);

            if(!Directory.Exists(incoming)) {
                Directory.CreateDirectory(incoming);
            }

            // TODO: Flush the incoming folder of any dead files

            Console.WriteLine("Setting max cache size to {0} MB", maxSizeMB);

            // Queue a background task to size the cache folder
            ThreadPool.QueueUserWorkItem(new WaitCallback(CalculateFolderSize));
        }

        /// <summary>
        /// Opens a file stream to the file that represents the temporary cache file.  The caller should 
        /// close the file and then call CompleteFile to move it to permanent storage.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="hash"></param>
        public FileStream GetTemporaryFile(Guid id, string hash)
        {
            string path = Path.Combine(incoming, GetFileName(id, hash));

            return File.OpenWrite(path);
        }

        public FileStream GetReadFileStream(Guid id, string hash)
        {
            string path = Path.Combine(root, GetFolder(hash), GetFileName(id, hash));
            return File.OpenRead(path);
        } 

        /// <summary>
        /// Moves the file out of temporary storage and into an accepted state
        /// </summary>
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
            if (IsFileCached(id, hash))
            {
                File.Delete(GetFullFilePath(id, hash));
            }

            File.Move(src, dest);

            Console.WriteLine("Moving {0} to permanent cache", fileName);
        }

        public string GetFullFilePath(Guid id, string hash) 
        {
            return Path.Combine(root, GetFolder(hash), GetFileName(id, hash));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="hash"></param>
        /// <returns></returns>
        public bool IsFileCached(Guid id, string hash)
        {
            return File.Exists(GetFullFilePath(id, hash));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="hash"></param>
        /// <returns></returns>
        private static string GetFileName(Guid id, string hash)
        {
            return String.Format(CultureInfo.CurrentCulture, "{0}_{1}.data", id, hash);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        private static string GetFolder(string hash)
        {
            return hash.Substring(0, 2);
        }

        /// <summary>
        /// Runs a full calculation of the cache folder size
        /// </summary>
        /// <param name="stateInfo"></param>
        private void CalculateFolderSize(Object stateInfo)
        {
            Console.WriteLine("Determining cache folder size");

            IEnumerable<string> files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories);

            foreach (string file in files)
            {
                FileInfo info = new FileInfo(file);
                lock (this.currSizeBytesLock)
                {
                    this.currSizeBytes += (ulong)info.Length;
                }
            }

            Console.WriteLine("Folder sizing complete, cache size: {0} MB", this.currSizeBytes / (1024 * 1024));
        }

        internal ulong GetFileSizeBytes(Guid id, string hash)
        {
            // TODO: Replace all String.Format with Path.Join
            FileInfo info = new FileInfo(GetFullFilePath(id, hash));
            return (ulong)info.Length;
        }
    }
}
