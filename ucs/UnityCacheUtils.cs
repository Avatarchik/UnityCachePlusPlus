using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Com.Gabosgab.UnityCache
{
    public class UnityCacheUtils
    {
        /// <summary>
        /// Read the hash off the stream
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static String ReadHash(Stream stream)
        {
            byte[] buffer = new byte[16];
            stream.Read(buffer, 0, 16);
            String hash = ByteArrayToString(buffer);

            return hash;
        }

        public static UInt64 GetASCIIBytesAsUInt64(byte[] bytes)
        {
            String lenCount = Encoding.ASCII.GetString(bytes);

            // TODO: Fix this parsing of asset sizes to the proper value
            ulong bytesToBeRead = UInt64.Parse(lenCount, NumberStyles.AllowHexSpecifier);

            return bytesToBeRead;
        }

        public static byte[] GetUInt64AsASCIIBytes(UInt64 num)
        {
            string sizeHex = num.ToString("X16");
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

            byte[] buffer = new byte[16];
            stream.Read(buffer, 0, 16);
            Guid id = new Guid(buffer);

            return id;

        }

        /// <summary>
        /// Sends the ID and has on the network stream
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="id"></param>
        /// <param name="hash"></param>
        public static void SendIdAndHashOnStream(Stream stream, Guid id, string hash)
        {
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
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }

        /// <summary>
        /// Convers the string representation of hex to binary
        /// </summary>
        /// <param name="hexString"></param>
        /// <returns></returns>
        public static byte[] ConvertHexStringToByteArray(string hexString)
        {
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
