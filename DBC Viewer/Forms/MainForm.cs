﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;

namespace DBCViewer
{

    public partial class MainForm : Form
    {
        // Fields
        private DataTable m_dataTable;
        private BaseReader m_reader;
        private DefinitionSelect m_selector;
        private XmlDocument m_definitions;
        private XmlNodeList m_fields;
        private DirectoryCatalog m_catalog;
        private XmlElement m_definition;        // definition for current file
        private string m_dbcName;               // file name without extension
        private string m_dbcFile;               // path to current file
        private DateTime m_startTime;
        private string m_workingFolder;

        // Properties
        public DataTable DataTable { get { return m_dataTable; } }
        public string WorkingFolder { get { return m_workingFolder; } }
        public XmlElement Definition { get { return m_definition; } }
        public string DBCName { get { return m_dbcName; } }
        public int DefinitionIndex { get { return m_selector != null ? m_selector.DefinitionIndex : 0; } }
        public string DBCFile { get { return m_dbcFile; } }

        // Delegates
        delegate void SetDataViewDelegate(DataView view);

        // MainForm
        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            WindowState = Properties.Settings.Default.WindowState;
            Size = Properties.Settings.Default.WindowSize;
            Location = Properties.Settings.Default.WindowLocation;

            m_workingFolder = Application.StartupPath;
            dataGridView1.AutoGenerateColumns = true;

            LoadDefinitions();
            Compose();

            // 支持文件拖入加载
            string[] cmds = Environment.GetCommandLineArgs();
            if (cmds.Length > 1)
                LoadFile(cmds[1]);

            // 启用dataGridView的双缓冲,解决dataGridView高频刷新闪烁
            Type _type = dataGridView1.GetType();
            PropertyInfo _info = _type.GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            _info.SetValue(dataGridView1, true, null);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Properties.Settings.Default.WindowState = WindowState;

            if (WindowState == FormWindowState.Normal)
            {
                Properties.Settings.Default.WindowSize = Size;
                Properties.Settings.Default.WindowLocation = Location;
            }
            else
            {
                Properties.Settings.Default.WindowSize = RestoreBounds.Size;
                Properties.Settings.Default.WindowLocation = RestoreBounds.Location;
            }

            Properties.Settings.Default.Save();
        }

        // 菜单
        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() != DialogResult.OK)
                return;

            LoadFile(openFileDialog1.FileName);
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CloseFile();
        }

        private void reloadDefinitionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LoadDefinitions();
        }

        private void runPluginToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (m_dataTable == null)
            {
                ShowErrorMessageBox("Nothing loaded yet!");
                return;
            }
            m_catalog.Refresh();

            Export2SQL _Sql = new Export2SQL();
            _Sql.Run2SQL(m_dataTable);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void resetColumnsFilterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewColumn col in dataGridView1.Columns)
            {
                col.Visible = true;
                ((ToolStripMenuItem)columnsFilterToolStripMenuItem.DropDownItems[col.Name]).Checked = false;
            }
        }

        private void difinitionEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (m_dbcName == null)
                return;

            StartEditor();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("DBC Viewer @ 2013 TOM_RUS\nDBC Viewer @ 2015 ZWJ Qq：41782992", "关于 DBC Viewer", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void LoadFile(string file)
        {
            m_dbcFile = file;
            SetDataSource(null);

            m_dbcName = Path.GetFileName(file); // 带上扩展名，区分4种不同的格式文件，因为有重名的文件，但是结构不同，让软件自动区分

            LoadDefinitions(); // reload in case of modification
            m_definition = GetDefinition();

            if (m_definition == null)
            {
                StartEditor();
                return;
            }

            toolStripProgressBar1.Visible = true;
            toolStripStatusLabel1.Text = "Loading...";

            m_startTime = DateTime.Now;
            backgroundWorker1.RunWorkerAsync(file);
        }

        private void CloseFile()
        {
            SetDataSource(null);
            m_definition = null;
            m_dataTable = null;
            columnsFilterToolStripMenuItem.DropDownItems.Clear();
        }

        private void StartEditor()
        {
            DefinitionEditor editor = new DefinitionEditor();
            var result = editor.ShowDialog(this);
            editor.Dispose();
            if (result == DialogResult.Abort)
                return;
            if (result == DialogResult.OK)
                LoadFile(m_dbcFile);
            else
                MessageBox.Show("Editor canceled! You can't open that file until you add proper definitions");
        }

        private XmlElement GetDefinition()
        {
            XmlNodeList definitions = m_definitions["DBFilesClient"].GetElementsByTagName(m_dbcName);

            if (definitions.Count == 0)
            {
                definitions = m_definitions["DBFilesClient"].GetElementsByTagName(Path.GetFileName(m_dbcFile));
            }

            if (definitions.Count == 0)
            {
                var msg = String.Format(CultureInfo.InvariantCulture, "{0} missing definition!", m_dbcName);
                ShowErrorMessageBox(msg);
                return null;
            }
            else if (definitions.Count == 1)
            {
                return ((XmlElement)definitions[0]);
            }
            else
            {
                m_selector = new DefinitionSelect();
                m_selector.SetDefinitions(definitions);
                var result = m_selector.ShowDialog(this);
                if (result != DialogResult.OK || m_selector.DefinitionIndex == -1)
                    return null;
                return ((XmlElement)definitions[m_selector.DefinitionIndex]);
            }
        }

        private static void ShowErrorMessageBox(string format, params object[] args)
        {
            var msg = String.Format(CultureInfo.InvariantCulture, format, args);
            MessageBox.Show(msg, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void CreateIndexes()
        {
            XmlNodeList indexes = m_definition.GetElementsByTagName("index");
            var columns = new DataColumn[indexes.Count];
            var idx = 0;
            foreach (XmlElement index in indexes)
                columns[idx++] = m_dataTable.Columns[index["primary"].InnerText];
            m_dataTable.PrimaryKey = columns;
        }

        private void CreateColumns()
        {
            foreach (XmlElement field in m_fields)
            {
                var colName = field.Attributes["name"].Value;

                switch (field.Attributes["type"].Value)
                {
                    case "long":
                        m_dataTable.Columns.Add(colName, typeof(long));
                        break;
                    case "ulong":
                        m_dataTable.Columns.Add(colName, typeof(ulong));
                        break;
                    case "int":
                        m_dataTable.Columns.Add(colName, typeof(int));
                        break;
                    case "uint":
                        m_dataTable.Columns.Add(colName, typeof(uint));
                        break;
                    case "short":
                        m_dataTable.Columns.Add(colName, typeof(short));
                        break;
                    case "ushort":
                        m_dataTable.Columns.Add(colName, typeof(ushort));
                        break;
                    case "sbyte":
                        m_dataTable.Columns.Add(colName, typeof(sbyte));
                        break;
                    case "byte":
                        m_dataTable.Columns.Add(colName, typeof(byte));
                        break;
                    case "float":
                        m_dataTable.Columns.Add(colName, typeof(float));
                        break;
                    case "double":
                        m_dataTable.Columns.Add(colName, typeof(double));
                        break;
                    case "string":
                        m_dataTable.Columns.Add(colName, typeof(string));
                        break;
                    default:
                        throw new ArgumentException(String.Format(CultureInfo.InvariantCulture, "Unknown field type {0}!", field.Attributes["type"].Value));
                }
            }
        }

        private void InitColumnsFilter()
        {
            columnsFilterToolStripMenuItem.DropDownItems.Clear();

            foreach (XmlElement field in m_fields)
            {
                var colName = field.Attributes["name"].Value;
                var type = field.Attributes["type"].Value;
                var format = field.Attributes["format"] != null ? field.Attributes["format"].Value : String.Empty;
                var visible = field.Attributes["visible"] != null ? field.Attributes["visible"].Value == "true" : true;
                var width = field.Attributes["width"] != null ? Convert.ToInt32(field.Attributes["width"].Value, CultureInfo.InvariantCulture) : 100;

                var item = new ToolStripMenuItem(colName);
                item.Click += new EventHandler(columnsFilterEventHandler);
                item.CheckOnClick = true;
                item.Name = colName;
                item.Checked = !visible;
                columnsFilterToolStripMenuItem.DropDownItems.Add(item);

                dataGridView1.Columns[colName].Visible = visible;
                dataGridView1.Columns[colName].Width = width;
                dataGridView1.Columns[colName].AutoSizeMode = GetColumnAutoSizeMode(type, format);
                dataGridView1.Columns[colName].SortMode = DataGridViewColumnSortMode.Automatic;
            }
        }

        private static DataGridViewAutoSizeColumnMode GetColumnAutoSizeMode(string type, string format)
        {
            switch (type)
            {
                case "string":
                    return DataGridViewAutoSizeColumnMode.NotSet;
                default:
                    break;
            }

            if (String.IsNullOrEmpty(format))
                return DataGridViewAutoSizeColumnMode.DisplayedCells;

            switch (format.Substring(0, 1).ToUpper(CultureInfo.InvariantCulture))
            {
                case "X":
                case "B":
                case "O":
                    return DataGridViewAutoSizeColumnMode.DisplayedCells;
                default:
                    return DataGridViewAutoSizeColumnMode.ColumnHeader;
            }
        }

        public void SetDataSource(DataView dataView)
        {
            bindingSource1.DataSource = dataView;
        }

        private void LoadDefinitions()
        {
            m_definitions = new XmlDocument();
            m_definitions.Load(Path.Combine(m_workingFolder, "Structure.xml"));
        }

        private void Compose()
        {
            m_catalog = new DirectoryCatalog(m_workingFolder);
            var container = new CompositionContainer(m_catalog);
            container.ComposeParts(this);
        }

        private static int GetFieldsCount(XmlNodeList fields)
        {
            int count = 0;
            foreach (XmlElement field in fields)
            {
                switch (field.Attributes["type"].Value)
                {
                    case "long":
                    case "ulong":
                    case "double":
                        count += 2;
                        break;
                    default:
                        count++;
                        break;
                }
            }
            return count;
        }

        private void dataGridView1_CellToolTipTextNeeded(object sender, DataGridViewCellToolTipTextNeededEventArgs e)
        {
            if (e.RowIndex == -1)
                return;

            ulong val = 0;

            Type dataType = m_dataTable.Columns[e.ColumnIndex].DataType;
            CultureInfo culture = CultureInfo.InvariantCulture;
            object value = dataGridView1[e.ColumnIndex, e.RowIndex].Value;

            if (dataType != typeof(string))
            {
                if (dataType == typeof(int))
                    val = (uint)Convert.ToInt32(value, culture);
                else if (dataType == typeof(uint))
                    val = Convert.ToUInt32(value, culture);
                else if (dataType == typeof(long))
                    val = (ulong)Convert.ToInt64(value, culture);
                else if (dataType == typeof(ulong))
                    val = Convert.ToUInt64(value, culture);
                else if (dataType == typeof(float))
                    val = BitConverter.ToUInt32(BitConverter.GetBytes((float)value), 0);
                else if (dataType == typeof(double))
                    val = BitConverter.ToUInt64(BitConverter.GetBytes((double)value), 0);
                else
                    val = Convert.ToUInt32(value, culture);
            }
            else
            {
                if (!(m_reader is WDBReader))
                    val = (uint)(from k in m_reader.StringTable where string.Compare(k.Value, (string)value, StringComparison.Ordinal) == 0 select k.Key).FirstOrDefault();
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat(culture, "Integer: {0:D}{1}", val, Environment.NewLine);
            sb.AppendFormat(new BinaryFormatter(), "HEX: {0:X}{1}", val, Environment.NewLine);
            sb.AppendFormat(new BinaryFormatter(), "BIN: {0:B}{1}", val, Environment.NewLine);
            sb.AppendFormat(culture, "Float: {0}{1}", BitConverter.ToSingle(BitConverter.GetBytes(val), 0), Environment.NewLine);
            sb.AppendFormat(culture, "Double: {0}{1}", BitConverter.ToDouble(BitConverter.GetBytes(val), 0), Environment.NewLine);

            try
            {
                sb.AppendFormat(culture, "String: {0}{1}", !(m_reader is WDBReader) ? m_reader.StringTable[(int)val] : String.Empty, Environment.NewLine);
            }
            catch
            {
                sb.AppendFormat(culture, "String: <empty>{0}", Environment.NewLine);
            }

            e.ToolTipText = sb.ToString();
        }

        private void dataGridView1_CurrentCellChanged(object sender, EventArgs e)
        {
            if (dataGridView1.CurrentCell != null)
                label1.Text = String.Format(CultureInfo.InvariantCulture, "Current Cell: {0}x{1}", dataGridView1.CurrentCell.RowIndex, dataGridView1.CurrentCell.ColumnIndex);
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            string file = (string)e.Argument;

            try
            {
                m_reader = DBReaderFactory.GetReader(file);
            }
            catch (Exception ex)
            {
                ShowErrorMessageBox(ex.Message);
                e.Cancel = true;
                return;
            }

            m_fields = m_definition.GetElementsByTagName("field");

            string[] types = new string[m_fields.Count];

            for (int j = 0; j < m_fields.Count; ++j)
                types[j] = m_fields[j].Attributes["type"].Value;

            // hack for *.adb files (because they don't have FieldsCount)
            bool notADB = !(m_reader is ADBReader);
            // hack for *.wdb files (because they don't have FieldsCount)
            bool notWDB = !(m_reader is WDBReader);

            if (GetFieldsCount(m_fields) != m_reader.FieldsCount && notADB && notWDB)
            {
                string msg = String.Format(CultureInfo.InvariantCulture, "{0} has invalid definition!\nFields count mismatch: got {1}, expected {2}", Path.GetFileName(file), m_fields.Count, m_reader.FieldsCount);
                ShowErrorMessageBox(msg);
                e.Cancel = true;
                return;
            }

            m_dataTable = new DataTable(Path.GetFileName(file));
            m_dataTable.Locale = CultureInfo.InvariantCulture;

            CreateColumns();                                // Add columns

            CreateIndexes();                                // Add indexes

            for (int i = 0; i < m_reader.RecordsCount; ++i) // Add rows
            {
                DataRow dataRow = m_dataTable.NewRow();

                BinaryReader br = m_reader[i];

                for (int j = 0; j < m_fields.Count; ++j)    // Add cells
                {
                    switch (types[j])
                    {
                        case "long":
                            dataRow[j] = br.ReadInt64();
                            break;
                        case "ulong":
                            dataRow[j] = br.ReadUInt64();
                            break;
                        case "int":
                            dataRow[j] = br.ReadInt32();
                            break;
                        case "uint":
                            dataRow[j] = br.ReadUInt32();
                            break;
                        case "short":
                            dataRow[j] = br.ReadInt16();
                            break;
                        case "ushort":
                            dataRow[j] = br.ReadUInt16();
                            break;
                        case "sbyte":
                            dataRow[j] = br.ReadSByte();
                            break;
                        case "byte":
                            dataRow[j] = br.ReadByte();
                            break;
                        case "float":
                            dataRow[j] = br.ReadSingle();
                            break;
                        case "double":
                            dataRow[j] = br.ReadDouble();
                            break;
                        case "string":
                            dataRow[j] = m_reader is WDBReader ? br.ReadStringNull() : m_reader.StringTable[br.ReadInt32()];
                            break;
                        default:
                            throw new ArgumentException(String.Format(CultureInfo.InvariantCulture, "Unknown field type {0}!", types[j]));
                    }
                }

                m_dataTable.Rows.Add(dataRow);

                int percent = (int)((float)m_dataTable.Rows.Count / (float)m_reader.RecordsCount * 100.0f);
                (sender as BackgroundWorker).ReportProgress(percent);
            }

            if (dataGridView1.InvokeRequired)
            {
                SetDataViewDelegate d = new SetDataViewDelegate(SetDataSource);
                Invoke(d, new object[] { m_dataTable.DefaultView });
            }
            else
                SetDataSource(m_dataTable.DefaultView);

            e.Result = file;
        }

        private void columnsFilterEventHandler(object sender, EventArgs e)
        {
            ToolStripMenuItem item = (ToolStripMenuItem)sender;

            dataGridView1.Columns[item.Name].Visible = !item.Checked;

            ((ToolStripMenuItem)item.OwnerItem).ShowDropDown();
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            toolStripProgressBar1.Value = e.ProgressPercentage;
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            toolStripProgressBar1.Visible = false;
            toolStripProgressBar1.Value = 0;

            if (e.Error != null)
            {
                ShowErrorMessageBox(e.Error.ToString());
                toolStripStatusLabel1.Text = "Error.";
            }
            else if (e.Cancelled == true)
            {
                toolStripStatusLabel1.Text = "Error in definitions.";
                StartEditor();
            }
            else
            {
                TimeSpan total = DateTime.Now - m_startTime;
                toolStripStatusLabel1.Text = String.Format(CultureInfo.InvariantCulture, "Ready. Loaded in {0} sec", total.TotalSeconds);
                Text = String.Format(CultureInfo.InvariantCulture, "DBC Viewer - {0}", e.Result.ToString());
                InitColumnsFilter();
            }
        }

        private void dataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            XmlAttribute attribute = m_fields[e.ColumnIndex].Attributes["format"];

            if (attribute == null)
                return;

            string fmtStr = "{0:" + attribute.Value + "}";
            e.Value = String.Format(new BinaryFormatter(), fmtStr, e.Value);
            e.FormattingApplied = true;
        }

        private void dataGridView1_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            label2.Text = String.Format(CultureInfo.InvariantCulture, "Rows Displayed: {0}", dataGridView1.RowCount);
        }
    }
}
