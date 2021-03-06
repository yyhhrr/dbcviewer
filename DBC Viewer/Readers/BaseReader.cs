﻿using System;
using System.Collections.Generic;
using System.IO;

namespace DBCViewer
{
    interface BaseReader
    {
        int RecordsCount { get; }
        int FieldsCount { get; }
        int RecordSize { get; }
        int StringTableSize { get; }
        Dictionary<int, string> StringTable { get; }
        byte[] GetRowAsByteArray(int row);
        BinaryReader this[int row] { get; }
    }

    class DBReaderFactory
    {
        public static BaseReader GetReader(string file)
        {
            BaseReader reader = null;

            var ext = Path.GetExtension(file).ToUpperInvariant();
            switch (ext)
            {
                case ".DBC":
                    reader = new DBCReader(file);
                    break;
                case ".DB2":
                    reader = new DB2Reader(file);
                    break;
                case ".ADB":
                    reader = new ADBReader(file);
                    break;
                case ".WDB":
                    reader = new WDBReader(file);
                    break;
                default:
                    throw new InvalidDataException(String.Format("Unknown file type {0}", ext));
            }

            return reader;
        }
    }
}
