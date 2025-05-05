#pragma warning disable IDE1006 // Naming Styles
using System.Data.Common;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TsvEditor
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            dataGridView1.Visible = false;
            SetTitleText();
            toolStripStatusLabel1.Text = "Open a .txt file";
            toolStripTextBoxSearchText.TextBox.PlaceholderText = "search text";
            toolStripTextBoxFilterText.TextBox.PlaceholderText = "filter text";
            this.AllowDrop = true;
            this.DragEnter += new DragEventHandler(Form1_DragEnter);
            this.DragDrop += new DragEventHandler(Form1_DragDrop);
        }

        void Form1_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) ?? false)
                e.Effect = DragDropEffects.Copy;
        }

        void Form1_DragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data != null)
            {
                var fileData = e.Data.GetData(DataFormats.FileDrop);
                if (fileData != null)
                {
                    string[] files = (string[])fileData;
                    if (files.Length > 0)
                    {
                        LoadFile(files[0]);
                    }
                }
            }
        }

        enum SortType
        {
            Text,
            Number,
        }

        private const string selectAColumn = "(select a column)";
        private FileInfo? _currentFile;
        private readonly Dictionary<string, SortType> _columnSorts = [];

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var openDialog = new OpenFileDialog()
            {
                Filter = "Text files (*.txt)|*.txt",
                Title = "Open text file",
                CheckFileExists = true,
                RestoreDirectory = true,
            };
            if (_currentFile != null)
            {
                var dir = Path.GetDirectoryName(_currentFile.FilePath);
                openDialog.InitialDirectory = dir;
            }
            if (openDialog.ShowDialog() == DialogResult.OK)
            {
                var filePath = openDialog.FileName;
                try
                {
                    LoadFile(filePath);
                }
                catch (Exception ex)
                {

                    MessageBox.Show(this, $"Error opening {filePath}:\n\n{ex.Message}", "Error", MessageBoxButtons.OK);
                }
            }
        }

        private void LoadFile(string filePath)
        {
            var grid = dataGridView1;

            SetStatusText($"1 {filePath}");

            var lines = File.ReadAllLines(filePath);
            if (lines.Length == 0)
            {
                SetStatusText($"Too few lines in {filePath}");
                return;
            }

            var columnNames = $"#\t{lines[0]}".Split('\t'); // prepend fake column for row number
            if (columnNames.Length < 3)
            {
                SetStatusText($"Too few columns in {filePath}");
                return;
            }

            //
            // speed-up ideas from https://10tec.com/articles/why-datagridview-slow.aspx
            //

            // Double buffering can make DGV slow in remote desktop
            if (!SystemInformation.TerminalServerSession)
            {
                Type dgvType = grid.GetType();
                var pi = dgvType.GetProperty("DoubleBuffered",
                  System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                pi?.SetValue(grid, true, null);
            }

            var isSameFilename = _currentFile != null && string.Equals(Path.GetFileName(_currentFile.FilePath) , Path.GetFileName(filePath), StringComparison.OrdinalIgnoreCase);
            var selectedColumnText = isSameFilename ? toolStripComboBoxColumnFilter.Text : selectAColumn;
            if (!isSameFilename)
            {
                toolStripComboBoxFilterType.SelectedIndex = 0;
                toolStripTextBoxFilterText.Text = string.Empty;
            }
            toolStripComboBoxColumnFilter.Items.Clear();

            grid.Rows.Clear();
            grid.Columns.Clear();
            _columnSorts.Clear();

            SetStatusText($"2 {filePath}");

            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            grid.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            grid.RowHeadersVisible = false;


            ((System.ComponentModel.ISupportInitialize)grid).BeginInit();
            foreach (var columnName in columnNames)
            {
                AddColumn(grid, columnName);
            }

            toolStripComboBoxColumnFilter.Items.Add(selectAColumn);
            toolStripComboBoxColumnFilter.Items.AddRange(columnNames);
            toolStripComboBoxColumnFilter.Text = selectedColumnText;

            SetStatusText($"3 {filePath}");

            var rows = new List<DataGridViewRow>();

            int rowNum = 0;
            foreach (var line in lines.Skip(1))
            {
                ++rowNum;
                var cellValues = $"{rowNum}\t{line}".Split('\t');
                var row = new DataGridViewRow();
                row.CreateCells(grid, cellValues);
                rows.Add(row);
                var maxIndex = Math.Min(columnNames.Length, cellValues.Length);
                for (int i = 0; i < maxIndex; i++)
                {
                    if (!string.IsNullOrEmpty(cellValues[i]) && !int.TryParse(cellValues[i], out int _))
                        _columnSorts[columnNames[i]] = SortType.Text;
                }
            }

            SetStatusText($"4 {filePath}");

            grid.Rows.AddRange([.. rows]);
            grid.Columns[0].Frozen = true;
            grid.Columns[1].Frozen = true;

            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            grid.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.AutoSizeToAllHeaders;
            grid.RowHeadersVisible = true;

            ((System.ComponentModel.ISupportInitialize)grid).EndInit();

            grid.Visible = true;

            _currentFile = new FileInfo
            {
                FilePath = filePath,
            };

            var fileName = Path.GetFileName(filePath);
            var fileDirectory = Path.GetDirectoryName(filePath);
            SetTitleText($"{fileName} ({fileDirectory})");
            SetStatusText(string.Empty);

            void AddColumn(DataGridView grid, string columnName)
            {
                grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = columnName,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                    Name = columnName,
                });
                _columnSorts[columnName] = SortType.Number; // Assume numeric for now
            }
        }

        private void SetStatusText(string s)
        {
            toolStripStatusLabel1.Text = s;
            statusStrip1.Refresh();
        }

        private void SetTitleText(string? s = null)
        {
            if (string.IsNullOrWhiteSpace(s))
                this.Text = "Txt/Tsv Editor";
            else
                this.Text = $"{s} - Txt/Tsv Editor";
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_currentFile == null)
                return;

            MaybeDoBackup(_currentFile);

            var savePath = _currentFile.FilePath;

            try
            {
                var grid = dataGridView1;
                SaveFile(grid, savePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error saving {savePath}:\n\n{ex.Message}", "Error", MessageBoxButtons.OK);
            }
        }

        private void SaveFile(DataGridView grid, string savePath)
        {
            var lines = new List<string>();

            var cells = new List<string>();
            foreach (DataGridViewTextBoxColumn column in grid.Columns)
                cells.Add(column.HeaderText);

            AddLine(cells);

            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow)
                    continue;
                cells = [];
                foreach (DataGridViewTextBoxCell cell in row.Cells)
                    cells.Add(cell.Value?.ToString() ?? string.Empty);
                AddLine(cells);
            }

            File.WriteAllLines(savePath, lines);
            SetStatusText($"Saved {savePath}");

            void AddLine(List<string> cells)
            {
                var line = string.Join("\t", cells.Skip(1));
                lines.Add(line);
            }
        }

        private void MaybeDoBackup(FileInfo file)
        {
            if (file.HaveMadeBackup)
                return;

            if (!File.Exists(file.FilePath))
                return;

            try
            {
                var backupPath = file.FilePath + ".bak";
                File.Copy(file.FilePath, backupPath, true);
                file.HaveMadeBackup = true;
            }
            catch (Exception ex)
            {
                SetStatusText($"Error: {ex.Message}");
            }
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_currentFile == null)
                return;

            var saveDialog = new SaveFileDialog()
            {
                Filter = "Text files (*.txt)|*.txt",
                Title = "Save text file",
                FileName = _currentFile.FilePath,
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                var savePath = saveDialog.FileName;
                try
                {
                    var grid = dataGridView1;
                    SaveFile(grid, savePath);
                    _currentFile.FilePath = savePath;
                    var fileName = Path.GetFileName(savePath);
                    SetTitleText(fileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Error saving {savePath}:\n\n{ex.Message}", "Error", MessageBoxButtons.OK);
                }
            }

        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void findToolStripMenuItem_Click(object sender, EventArgs e)
        {
            toolStripTextBoxSearchText.SelectAll();
            toolStripTextBoxSearchText.Focus();
        }

        private void findNextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoFind();
        }

        private void toolStripTextBox1_Leave(object sender, EventArgs e)
        {
            DoFind();
        }

        private void DoFind()
        {
            var findText = toolStripTextBoxSearchText.Text;
            if (string.IsNullOrEmpty(findText))
            {
                SetStatusText("Nothing to search for");
                return;
            }

            DataGridView grid = dataGridView1;

            SetStatusText("Searching...");

            var currentCell = grid.CurrentCell;

            if (currentCell == null)
            {
                SetStatusText("Error: No current cell");
                return;
            }

            var xStart = currentCell.ColumnIndex;
            var yStart = currentCell.RowIndex;
            var x = xStart;
            var y = yStart;

            bool found;
            do
            {
                ++x;
                if (x >= grid.Columns.Count)
                {
                    x = 0;
                    ++y;
                }
                if (y >= grid.Rows.Count || grid.Rows[y].IsNewRow)
                {
                    x = 0;
                    y = 0;
                }
                found = grid.Rows[y].Cells[x].Value?.ToString()?.Contains(findText, StringComparison.OrdinalIgnoreCase) ?? false;
            }
            while (!found && (x != xStart || y != yStart));

            if (found)
            {
                grid.CurrentCell = grid.Rows[y].Cells[x];
                SetStatusText(string.Empty);
                return;
            }

            SetStatusText($"'{findText}' not found");
        }

        private void toolStripTextBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SelectNextControl(ActiveControl, true, true, true, true);
            }
        }

        private void dataGridView1_SortCompare(object sender, DataGridViewSortCompareEventArgs e)
        {

            if (!_columnSorts.TryGetValue(e.Column.HeaderText, out var sortType))
                sortType = SortType.Text;

            if (sortType == SortType.Number)
            {
                e.SortResult = GetCellInt(e.CellValue1).CompareTo(GetCellInt(e.CellValue2));
                e.Handled = true;//pass by the default sorting
            }

            static int GetCellInt(object? obj)
            { 
                var s = obj as string;
                if (int.TryParse(s, out var i))
                    return i;
                return -1;
            }
        }

        private void newWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(Application.ExecutablePath);
        }

        enum FilterType
        {
            NoSelection = -1,
            None = 0,
            Contains = 1,
            Exact = 2,
            Regex = 3,
        }

        private void ApplyFilter()
        {
            var grid = dataGridView1;
            var filterType = (FilterType)toolStripComboBoxFilterType.SelectedIndex;

            if (filterType == FilterType.None || filterType == FilterType.NoSelection)
            {
                ClearFilter();
                return;
            }

            var columnName = toolStripComboBoxColumnFilter.Text;
            if (string.IsNullOrWhiteSpace(columnName) || columnName == selectAColumn)
            {
                ClearFilter();
                return;
            }

            var columnIndex = grid.Columns[columnName]?.Index ?? -1;
            if (columnIndex < 1)
            {
                ClearFilter();
                return;
            }

            var filterText = toolStripTextBoxFilterText.Text;

            Regex? filterRegex = null;
            if (filterType == FilterType.Regex)
            {
                try
                {
                    filterRegex = new Regex(filterText, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                }
                catch
                {
                    SetStatusText("Invalid regex");
                    return;
                }
            }

            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            grid.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            grid.RowHeadersVisible = false;
            ShowColumns(false);

            int counter = 0;
            int shown = 0;
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow)
                    continue;
                row.Visible = RowMatches(row);
                ++counter;
                if (RowMatches(row))
                    ++shown;
                if (counter % 10 == 0)
                    SetStatusText($"Showing {shown} / {counter} rows");
            }
            SetStatusText($"Showing {shown} / {counter} rows");

            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            grid.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.AutoSizeToAllHeaders;
            grid.RowHeadersVisible = true;
            ShowColumns(true);

            bool RowMatches(DataGridViewRow row)
            {
                if (string.IsNullOrEmpty(filterText) && filterType != FilterType.Exact)
                    return true;
                var cell = row.Cells[columnIndex];
                var cellText = cell?.Value?.ToString() ?? string.Empty;

                return filterType switch
                {
                    FilterType.Exact => string.Equals(cellText, filterText, StringComparison.InvariantCultureIgnoreCase),
                    FilterType.Contains => cellText.Contains(filterText, StringComparison.InvariantCultureIgnoreCase),
                    FilterType.Regex => filterRegex!.IsMatch(cellText),
                    _ => true,
                };
            }

            void ShowColumns(bool showColumns)
            {
                foreach (DataGridViewColumn column in grid.Columns)
                {
                    column.Visible = showColumns;
                }
            }

            void ClearFilter()
            {
                grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
                grid.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
                grid.RowHeadersVisible = false;
                ShowColumns(false);

                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (row.IsNewRow)
                        continue;
                    row.Visible = true;
                }

                grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
                grid.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.AutoSizeToAllHeaders;
                grid.RowHeadersVisible = true;
                ShowColumns(true);

                SetStatusText(string.Empty);
            }
        }

        private void toolStripButtonApplyFilter_Click(object sender, EventArgs e)
        {
            ApplyFilter();
        }
    }

    public class FileInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public bool HaveMadeBackup { get; set; } = false;
    }
}