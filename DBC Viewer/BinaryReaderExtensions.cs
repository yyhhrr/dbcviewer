using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DBCViewer
{
    static class BinaryReaderExtensions
    {
        #region ReadStringNull
        /// <summary>
        ///  Reads the NULL terminated string from the current stream and advances the current position of the stream by string length + 1.
        /// <seealso cref="GenericReader.ReadStringNumber"/>
        /// </summary>
        public static string ReadStringNull(this BinaryReader reader)
        {
            byte num;
            string text = String.Empty;
            System.Collections.Generic.List<byte> temp = new System.Collections.Generic.List<byte>();

            while ((num = reader.ReadByte()) != 0)
                temp.Add(num);

            text = Encoding.UTF8.GetString(temp.ToArray());

            return text;
        }
        #endregion
    }
}
