// <copyright file="UnityCacheUtilities.cs" company="Gabe Brown">
//     Copyright (c) Gabe Brown. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Com.Gabosgab.UnityCache
{
    public static class UnityCacheUtilities
    {
        /// <summary>
        /// Read the hash off the stream from a binary format to a string
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <returns>The hash off the stream</returns>
        public static String ReadHash(Stream stream)
        {
            CheckStreamIsNotNull(stream);

            byte[] buffer = new byte[16];
            stream.Read(buffer, 0, 16);
            String hash = ByteArrayToString(buffer);

            return hash;
        }

        /// <summary>
        /// Convert an array of bytes representing an ASCII encoded 64-bit unsigned integer
        /// </summary>
        /// <param name="value">An array of 16 bytes to be converted to an unsigned 64 bit integer</param>
        /// <returns>The value of the number represented in bytes</returns>
        public static UInt64 GetAsciiBytesAsUInt64(byte[] value)
        {
            String lenCount = Encoding.ASCII.GetString(value);
            ulong bytesToBeRead = UInt64.Parse(lenCount, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
            return bytesToBeRead;
        }

        public static byte[] GetUInt64AsAsciiBytes(UInt64 number)
        {
            string sizeHex = number.ToString("X16", CultureInfo.InvariantCulture);
            byte[] fileSizeBytes = Encoding.ASCII.GetBytes(sizeHex);
            return fileSizeBytes;
        }

        /// <summary>
        /// Read GUID off the stream
        /// </summary>
        /// <param name="stream">The stream to id from</param>
        /// <returns>A GUID that represents the bytes read from the stream</returns>
        public static Guid ReadGuid(Stream stream)
        {
            CheckStreamIsNotNull(stream);

            byte[] buffer = new byte[16];
            stream.Read(buffer, 0, 16);
            Guid id = new Guid(buffer);

            return id;

        }

        /// <summary>
        /// Sends the ID and has on the network stream
        /// </summary>
        /// <param name="stream">The stream to send the id and hash on</param>
        /// <param name="id">The id to send</param>
        /// <param name="hash">The hash to send</param>
        public static void SendIdAndHashOnStream(Stream stream, Guid id, string hash)
        {
            CheckStreamIsNotNull(stream);

            // Respond with id and items
            byte[] idBytes = id.ToByteArray();
            stream.Write(idBytes, 0, idBytes.Length);

            // Respond with hash
            byte[] hashBytes = ConvertHexStringToByteArray(hash);
            stream.Write(hashBytes, 0, hashBytes.Length);
        }

        /// <summary>
        /// Converts a byte array of hex to string
        /// </summary>
        /// <param name="ba">The byte array of the items to convert to string</param>
        /// <returns>Returns a hex representation of the string</returns>
        public static string ByteArrayToString(byte[] ba)
        {
            if(ba == null)
            {
                throw new ArgumentNullException(Resource.ByteArrayIsNullException);
            }

            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
            {
                hex.AppendFormat("{0:x2}", b);
            }
            return hex.ToString();
        }

        /// <summary>
        /// Convers the string representation of hex to binary
        /// </summary>
        /// <param name="hexString">The string to be converted</param>
        /// <returns>A byte array representing the string in hex.  If null or empty string was passed, null is returned.</returns>
        public static byte[] ConvertHexStringToByteArray(string hexString)
        {
            if(String.IsNullOrEmpty(hexString))
            {
                // An empty or null array just returns null
                return null;
            }

            if (hexString.Length % 2 != 0)
            {
                throw new ArgumentException(String.Format(CultureInfo.InvariantCulture, "The binary key cannot have an odd number of digits: {0}", hexString));
            }

            byte[] hexAsBytes = new byte[hexString.Length / 2];
            for (int index = 0; index < hexAsBytes.Length; index++)
            {
                string byteValue = hexString.Substring(index * 2, 2);
                hexAsBytes[index] = byte.Parse(byteValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return hexAsBytes;
        }

        /// <summary>
        /// Throws an exception is the stream is null
        /// </summary>
        /// <param name="stream">Checks the string is not null</param>
        private static void CheckStreamIsNotNull(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(Resource.StreamIsNullException);
            }
        }
    }
}
