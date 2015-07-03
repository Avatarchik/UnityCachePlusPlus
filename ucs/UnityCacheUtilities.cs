using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Com.Gabosgab.UnityCache
{
    public class UnityCacheUtilities
    {
        /// <summary>
        /// Read the hash off the stream
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static String ReadHash(Stream stream)
        {
            CheckStreamIsNotNull(stream);

            byte[] buffer = new byte[16];
            stream.Read(buffer, 0, 16);
            String hash = ByteArrayToString(buffer);

            return hash;
        }

        /// <summary>
        /// Convert an array of bytes representing an ASCII encoded UInt64 and converts them to a UInt64
        /// </summary>
        /// <param name="value">An array of 16 bytes to be converted to an UInt64</param>
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
        /// Read Guid off the stream
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static Guid ReadGuid(Stream stream)
        {
            CheckStreamIsNotNull(stream);

            byte[] buffer = new byte[16];
            stream.Read(buffer, 0, 16);
            Guid id = new Guid(buffer);

            return id;

        }

        /// <summary>
        /// Throws an exception is the stream is null
        /// </summary>
        /// <param name="stream"></param>
        private static void CheckStreamIsNotNull(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(Resource.StreamIsNullException);
            }
        }

        /// <summary>
        /// Sends the ID and has on the network stream
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="id"></param>
        /// <param name="hash"></param>
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
        /// Converst a byte array of hex to string
        /// </summary>
        /// <param name="ba"></param>
        /// <returns></returns>
        public static string ByteArrayToString(byte[] ba)
        {
            if(ba == null)
            {
                throw new ArgumentNullException(Resource.ByteArrayIsNullException);
            }

            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
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

            byte[] HexAsBytes = new byte[hexString.Length / 2];
            for (int index = 0; index < HexAsBytes.Length; index++)
            {
                string byteValue = hexString.Substring(index * 2, 2);
                HexAsBytes[index] = byte.Parse(byteValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return HexAsBytes;
        }
    }
}
