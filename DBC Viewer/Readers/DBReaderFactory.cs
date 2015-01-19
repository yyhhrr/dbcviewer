﻿using System;
using System.IO;

namespace DBCViewer
{
    class DBReaderFactory
    {
        public static IWowClientDBReader GetReader(string file)
        {
            IWowClientDBReader reader;

            var ext = Path.GetExtension(file).ToUpperInvariant();
            if (ext == ".DBC")
                reader = new DBCReader(file);
            else if (ext == ".DB2")
                reader = new DB2Reader(file);
            else if (ext == ".ADB")
                reader = new ADBReader(file);
            else if (ext == ".WDB")
                reader = new WDBReader(file);
            else
                throw new InvalidDataException(String.Format("Unknown file type {0}", ext));

            return reader;
        }
    }
}
