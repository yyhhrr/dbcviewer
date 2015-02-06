using System;
using System.ComponentModel.Composition;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace DBCViewer
{
    public class Export2SQL
    {
        public void Run2SQL(DataTable data)
        {
            StreamWriter sqlWriter = new StreamWriter(Path.GetFileNameWithoutExtension(data.TableName) + ".sql");

            WriteSqlStructure(sqlWriter, data);

            StringBuilder result = new StringBuilder();
            result.AppendFormat("INSERT INTO `dbc_{0}` VALUES", Path.GetFileNameWithoutExtension(data.TableName));
            result.AppendLine();

            //foreach (DataRow row in data.Rows)
            for (int x = 0; x < data.Rows.Count; ++x)
            {
                result.Append("(");
                int flds = 0;

                for (var i = 0; i < data.Columns.Count; ++i)
                {
                    switch (data.Columns[i].DataType.Name)
                    {
                        case "Int64":
                        case "UInt64":
                        case "Int32":
                        case "UInt32":
                        case "Int16":
                        case "UInt16":
                        case "SByte":
                        case "Byte":
                            result.Append(data.Rows[x][i]);
                            break;
                        case "Single":
                            result.Append(((float)data.Rows[x][i]).ToString(CultureInfo.InvariantCulture));
                            break;
                        case "Double":
                            result.Append(((double)data.Rows[x][i]).ToString(CultureInfo.InvariantCulture));
                            break;
                        case "String":
                            result.Append("\"" + StripBadCharacters((string)data.Rows[x][i]) + "\"");
                            break;
                        default:
                            throw new Exception(String.Format("未知的数据类型：{0}!", data.Columns[i].DataType.Name));
                    }

                    if (flds != data.Columns.Count - 1)
                        result.Append(", ");

                    flds++;
                }

                if (x < data.Rows.Count - 1)
                    result.Append("),");
                else
                    result.Append(");");

                sqlWriter.WriteLine(result);
                result.Clear();
            }

            sqlWriter.Flush();
            sqlWriter.Close();

            var msg = String.Format("提示：共计导出 {0} 条数据，SQL文件生成成功。", data.Rows.Count);
            MessageBox.Show(msg, "完成");
        }

        private void WriteSqlStructure(StreamWriter sqlWriter, DataTable data)
        {
            sqlWriter.WriteLine("DROP TABLE IF EXISTS `dbc_{0}`;", Path.GetFileNameWithoutExtension(data.TableName));
            sqlWriter.WriteLine("CREATE TABLE `dbc_{0}` (", Path.GetFileNameWithoutExtension(data.TableName));

            for (var i = 0; i < data.Columns.Count; ++i)
            {
                sqlWriter.Write("\t" + String.Format("`{0}`", data.Columns[i].ColumnName));

                switch (data.Columns[i].DataType.Name)
                {
                    case "Int64":
                        sqlWriter.Write(" BIGINT NOT NULL DEFAULT '0'");
                        break;
                    case "UInt64":
                        sqlWriter.Write(" BIGINT UNSIGNED NOT NULL DEFAULT '0'");
                        break;
                    case "Int32":
                        sqlWriter.Write(" INT NOT NULL DEFAULT '0'");
                        break;
                    case "UInt32":
                        sqlWriter.Write(" INT UNSIGNED NOT NULL DEFAULT '0'");
                        break;
                    case "Int16":
                        sqlWriter.Write(" SMALLINT NOT NULL DEFAULT '0'");
                        break;
                    case "UInt16":
                        sqlWriter.Write(" SMALLINT UNSIGNED NOT NULL DEFAULT '0'");
                        break;
                    case "SByte":
                        sqlWriter.Write(" TINYINT NOT NULL DEFAULT '0'");
                        break;
                    case "Byte":
                        sqlWriter.Write(" TINYINT UNSIGNED NOT NULL DEFAULT '0'");
                        break;
                    case "Single":
                        sqlWriter.Write(" FLOAT NOT NULL DEFAULT '0'");
                        break;
                    case "Double":
                        sqlWriter.Write(" DOUBLE NOT NULL DEFAULT '0'");
                        break;
                    case "String":
                        sqlWriter.Write(" TEXT NOT NULL");
                        break;
                    default:
                        throw new Exception(String.Format("Unknown field type {0}!", data.Columns[i].DataType.Name));
                }

                if (i < data.Columns.Count - 1 || data.PrimaryKey.Length > 0)
                    sqlWriter.WriteLine(",");
                else
                    sqlWriter.WriteLine();
            }

            foreach (DataColumn index in data.PrimaryKey)
            {
                sqlWriter.WriteLine("\tPRIMARY KEY (`{0}`)", index.ColumnName);
            }

            sqlWriter.WriteLine(") ENGINE=MyISAM DEFAULT CHARSET=utf8 COMMENT='Export of {0}';", data.TableName);
            sqlWriter.WriteLine();
        }

        static string StripBadCharacters(string input)
        {
            input = Regex.Replace(input, @"'", @"\'");
            input = Regex.Replace(input, @"\""", @"\""");
            return input;
        }
    }
}
