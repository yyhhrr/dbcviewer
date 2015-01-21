﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WowDataFileParser.Definitions;

namespace WowDataFileParser
{
    internal class SqlTable
    {
        public static void CreateStructure(StreamWriter writer, Definition definition)
        {
            var db_name = "wdb";
            if (definition.Build > 0)
                db_name += "_" + definition.Build;

            writer.WriteLine("-- DB structure");
            writer.WriteLine("CREATE DATABASE IF NOT EXISTS `{0}` CHARACTER SET utf8 COLLATE utf8_general_ci;", db_name);
            writer.WriteLine("USE `{0}`;", db_name);
            writer.WriteLine();

            foreach (var file in definition.Files)
            {
                if (string.IsNullOrWhiteSpace(file.Table))
                    throw new NullReferenceException("Table name missing or empty for " + file.Name);

                var keys = new List<string> { "locale" };

                writer.WriteLine("CREATE TABLE IF NOT EXISTS `{0}` (", file.Table);
                writer.WriteLine("    `locale` CHAR(4) NOT NULL DEFAULT 'xxXX',");

                foreach (var field in file.Fields)
                    CreateFieldByType(writer, keys, field, "");

                writer.WriteLine("    PRIMARY KEY (" + string.Join(", ", keys.Select(key => "`" + key + "`")) + ")");
                writer.WriteLine(") ENGINE = MyISAM DEFAULT CHARSET = utf8;");
                writer.WriteLine();
            }
        }

        private static void CreateFieldByType(StreamWriter writer, List<string> keys, Field field, string suffix)
        {
            if (field.Type == DataType.None)
                return;

            if (field.Key)
                keys.Add(field.Name);


            #region Type
            switch (field.Type)
            {
                case DataType.Long:
                    writer.WriteLine("    `{0}` BIGINT NOT NULL DEFAULT '0',", field.Name.ToLower() + suffix);
                    break;
                case DataType.Ulong:
                    writer.WriteLine("    `{0}` BIGINT UNSIGNED NOT NULL DEFAULT '0',", field.Name.ToLower() + suffix);
                    break;
                case DataType.Int:
                    writer.WriteLine("    `{0}` INT NOT NULL DEFAULT '0',", field.Name.ToLower() + suffix);
                    break;
                case DataType.Uint:
                    writer.WriteLine("    `{0}` INT UNSIGNED NOT NULL DEFAULT '0',", field.Name.ToLower() + suffix);
                    break;
                case DataType.Short:
                    writer.WriteLine("    `{0}` SMALLINT NOT NULL DEFAULT '0',", field.Name.ToLower() + suffix);
                    break;
                case DataType.Ushort:
                    writer.WriteLine("    `{0}` SMALLINT UNSIGNED NOT NULL DEFAULT '0',", field.Name.ToLower() + suffix);
                    break;
                case DataType.Byte:
                    writer.WriteLine("    `{0}` TINYINT NOT NULL DEFAULT '0',", field.Name.ToLower() + suffix);
                    break;
                case DataType.SByte:
                    writer.WriteLine("    `{0}` TINYINT UNSIGNED NOT NULL DEFAULT '0',", field.Name.ToLower() + suffix);
                    break;
                case DataType.Float:
                    writer.WriteLine("    `{0}` FLOAT NOT NULL DEFAULT '0',", field.Name.ToLower() + suffix);
                    break;
                case DataType.Double:
                    writer.WriteLine("    `{0}` DOUBLE NOT NULL DEFAULT '0',", field.Name.ToLower() + suffix);
                    break;
                case DataType.String:
                case DataType.String2:
                case DataType.Pstring:
                    if (field.Maxsize > 0)
                        writer.WriteLine("    `{0}` VARCHAR({1}),", field.Name.ToLower() + suffix, field.Maxsize);
                    else
                        writer.WriteLine("    `{0}` TEXT,", field.Name.ToLower() + suffix);
                    break;
                case DataType.List:
                    {
                        if (field.Size > 0)
                        {
                            var fname = field.Name.ToLower();
                            if (!char.IsDigit(fname[fname.Length - 1]))
                            {
                                if (suffix.Length > 0 && suffix[0] == '_')
                                    fname += suffix.Substring(1);
                                else
                                    fname += suffix;
                            }
                            else { fname += suffix; }

                            writer.WriteLine("    `{0}` INT NOT NULL DEFAULT '0',", fname);
                        }

                        for (int i = 0; i < field.Maxsize; ++i)
                        {
                            var m_suffix = suffix + "_" + (i + 1);
                            foreach (var element in field.Fields)
                                CreateFieldByType(writer, keys, element, m_suffix);
                        }
                    }
                    break;
                default:
                    throw new Exception("Unknown field type " + field.Type);
            }
            #endregion
        }
    }
}