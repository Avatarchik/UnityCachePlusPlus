// <copyright file="CacheFile.cs" company="Gabe Brown">
//     Copyright (c) Yocero, LLC All rights reserved.
// </copyright>

namespace Com.Yocero.UnityCache.Server
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Stores details and helper functions about the cache file
    /// </summary>
    public class CacheFile
    {
        /// <summary>
        /// Gets or sets the Id of the cache file
        /// </summary>
        public Guid Id
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the hash of the file
        /// </summary>
        public string Hash
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the date/time that the file was last accessed
        /// </summary>
        public DateTime LastAccessed
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the length of the file
        /// </summary>
        public int Length
        {
            get;
            set;
        }

        /// <summary>
        /// Determines if the given file is cached or not
        /// </summary>
        /// <param name="root">The root folder of the cache</param>
        /// <param name="id">The Id of the file</param>
        /// <param name="hash">The hash of the file</param>
        /// <returns>True if the file is cached, false otherwise</returns>
        public static bool IsFileCached(string root, Guid id, string hash)
        {
            return File.Exists(CacheFile.GetFullFilePath(root, id, hash));
        }

        /// <summary>
        /// Returns a file name for the given id and hash combination
        /// </summary>
        /// <param name="id">The Id of the file</param>
        /// <param name="hash">The has of the file</param>
        /// <returns>The filename of the file</returns>
        public static string GetFileName(Guid id, string hash)
        {
            return string.Format(CultureInfo.CurrentCulture, "{0}_{1}.data", id, hash);
        }

        /// <summary>
        /// Gets the file size of a given file
        /// </summary>
        /// <param name="root">The root folder of the cache</param>
        /// <param name="id">The Id of the file to get the size of</param>
        /// <param name="hash">The has of the file</param>
        /// <returns>The file size in bytes</returns>
        public static ulong GetFileSizeBytes(string root, Guid id, string hash)
        {
            // TODO: Replace all string.Format with Path.Join
            FileInfo info = new FileInfo(CacheFile.GetFullFilePath(root, id, hash));
            return (ulong)info.Length;
        }

        /// <summary>
        /// Returns the folder where the hash should be stored
        /// </summary>
        /// <param name="hash">The hash of the folder</param>
        /// <returns>The file's folder name</returns>
        public static string GetFolder(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                throw new ArgumentException("Hash cannot be empty or null.");
            }

            return hash.Substring(0, 2);
        }

        /// <summary>
        /// Takes a filename and returns the id and hash of the item
        /// </summary>
        /// <param name="fileName">The filename of the file</param>
        /// <param name="id">The id of the file</param>
        /// <param name="hash">The hash of the file</param>
        /// <returns>true if the file was a valid file, false if the file isn't a cache item</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1021:AvoidOutParameters", MessageId = "1#", Justification = "I'm lazy"),
        System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1021:AvoidOutParameters", MessageId = "2#", Justification = "I'm lazy")]
        public static bool ParseFileName(string fileName, out Guid id, out string hash)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("filename cannot be null or empty");
            }

            id = new Guid();
            hash = string.Empty;

            // See if the formate matches the 
            if (Regex.IsMatch(fileName, ".*\x5F.*.data"))
            {
                string[] splitSections = { "_", ".data" };
                string[] split = fileName.Split(splitSections, StringSplitOptions.RemoveEmptyEntries);
                id = Guid.Parse(split[0]);
                hash = split[1];

                return true;
            }

            // Not a valid file
            return false;
        }

        /// <summary>
        /// Returns the full path of a given file
        /// </summary>
        /// <param name="root">The root folder of the cache</param>
        /// <param name="id">The Id of the file</param>
        /// <param name="hash">The hash of the file</param>
        /// <returns>The full path of the file</returns>
        public static string GetFullFilePath(string root, Guid id, string hash)
        {
            return Path.Combine(root, CacheFile.GetFolder(hash), CacheFile.GetFileName(id, hash));
        }

        /// <summary>
        /// Loads the details of the files
        /// </summary>
        /// <param name="root">The root folder of the cache</param>
        /// <param name="id">The id of the file</param>
        /// <param name="hash">The hash of the file</param>
        public void Load(string root, Guid id, string hash)
        {
            string path = CacheFile.GetFullFilePath(root, id, hash);

            FileInfo info = new FileInfo(path);
            this.Id = id;
            this.Hash = hash;
            this.LastAccessed = File.GetLastAccessTime(path);
            this.Length = (int)info.Length;
        }
    }
}
