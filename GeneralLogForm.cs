using ScintillaNET;
using System.Drawing;
using System.Text;

namespace KenshiCore
{
    public partial class GeneralLogForm : Form
    {
        private Scintilla? logBox;
        private Label? statusLabel;
        private Button? clearButton;
        private Button? saveButton;
        private string title = "General";
        private DateTime startTime;

        private readonly Dictionary<Color, int> styleCache = new();
        private int nextStyleIndex = 1; // 0 = default

        public GeneralLogForm()
        {
            InitializeComponent();
            startTime = DateTime.Now;
        }

        private void InitializeComponent(string t = "General")
        {
            this.Size = new Size(800, 600);
            this.title = t;
            this.Text = $"{title} Log";
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.StartPosition = FormStartPosition.CenterParent;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 2,
                Padding = new Padding(5)
            };

            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            // Status label
            statusLabel = new Label
            {
                Text = $"{title} Log - Ready",
                Font = new Font(Font.FontFamily, 10, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            layout.Controls.Add(statusLabel, 0, 0);
            layout.SetColumnSpan(statusLabel, 2);

            // Scintilla log box
            logBox = new Scintilla
            {
                Dock = DockStyle.Fill,
                Lexer = Lexer.Null,
                WrapMode = WrapMode.None,
                IndentationGuides = IndentView.None,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White
            };

            // Default style
            logBox.StyleResetDefault();
            logBox.Styles[Style.Default].Font = "Consolas";
            logBox.Styles[Style.Default].Size = 9;
            logBox.Styles[Style.Default].BackColor = Color.FromArgb(30, 30, 30);
            logBox.Styles[Style.Default].ForeColor = Color.White;
            logBox.StyleClearAll();

            logBox.Margins[0].Width = 0;
            logBox.Margins[1].Width = 0;
            logBox.Margins[2].Width = 0;

            logBox.KeyDown += (s, e) => e.SuppressKeyPress = true;

            layout.Controls.Add(logBox, 0, 1);
            layout.SetColumnSpan(logBox, 2);

            // Buttons
            clearButton = new Button { Text = "Clear Log", Dock = DockStyle.Fill, Height = 30 };
            clearButton.Click += (s, e) => { logBox.ClearAll(); UpdateStatus(); };

            saveButton = new Button { Text = "Save Log", Dock = DockStyle.Fill, Height = 30 };
            saveButton.Click += SaveLog_Click;

            layout.Controls.Add(clearButton, 0, 2);
            layout.Controls.Add(saveButton, 1, 2);

            this.Controls.Add(layout);

            this.FormClosing += (s, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    this.Hide();
                }
            };
        }
        public static string CombineBlocksToString(IEnumerable<(string Text, Color Color)> blocks)
        {
            if (blocks == null)
                return string.Empty;

            var sb = new StringBuilder();

            foreach (var (text, _) in blocks)
            {
                if (!string.IsNullOrEmpty(text))
                {
                    sb.AppendLine(text);
                }
            }

            return sb.ToString();
        }
        private void LogBlocks(IEnumerable<(string Text, Color Color)> blocks, Action<int, string>? reportProgress = null)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<IEnumerable<(string, Color)>, Action<int, string>?>(LogBlocks), blocks, reportProgress);
                return;
            }

            var blockList = blocks as IList<(string Text, Color Color)> ?? blocks.ToList();
            int total = blockList.Count;
            int count = 0;

            // Temporarily make editable so we can append text
            logBox!.ReadOnly = false;

            // Suspend layout to reduce flicker
            logBox.SuspendLayout();

            try
            {
                foreach (var (text, color) in blockList)
                {
                    int start = logBox.TextLength;
                    logBox.AppendText(text + "\r\n");
                    int end = logBox.TextLength;

                    logBox.StartStyling(start);
                    logBox.SetStyling(end - start, GetStyleForColor(color));

                    count++;
                    if (reportProgress != null && count % 50 == 0)
                        reportProgress(count, $"Logging {count}/{total} lines...");
                }

                reportProgress?.Invoke(total, $"Finished logging {total} lines.");
            }
            finally
            {
                logBox.ResumeLayout();
                logBox.Invalidate();
                logBox.ReadOnly = true;
            }
            UpdateStatus();
        }

        public void LogString(string text, Color? textColor = null)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string, Color?>(LogString), text, textColor);
                return;
            }

            if (string.IsNullOrEmpty(text))
                return;

            logBox!.ReadOnly = false;
            logBox.SuspendLayout();

            try
            {
                int start = logBox.TextLength;
                logBox.AppendText(text);
                int end = logBox.TextLength;

                // apply green style to all appended text
                logBox.StartStyling(start);
                logBox.SetStyling(end - start, GetStyleForColor(textColor ?? Color.LightGreen));
            }
            finally
            {
                logBox.ResumeLayout();
                logBox.Invalidate();
                logBox.ReadOnly = true;
            }
            UpdateStatus();
        }
        public void Log(string text, Color? textColor = null)
        {
            LogBlocks(new List<(string, Color)>
            {
                ($"[{DateTime.Now:HH:mm:ss}] {text}", textColor ?? Color.LightGreen)
            });
        }

        // Error log
        public void LogError(string error)
        {
            LogBlocks(new List<(string, Color)>
            {
                ($"[{DateTime.Now:HH:mm:ss}] ❌ ERROR: {error}", Color.Red)
            });
        }

        private int GetStyleForColor(Color color)
        {
            if (styleCache.TryGetValue(color, out int styleId))
                return styleId;

            styleId = nextStyleIndex++;
            logBox!.Styles[styleId].ForeColor = color;
            styleCache[color] = styleId;
            return styleId;
        }

        private void UpdateStatus()
        {
            var elapsed = DateTime.Now - startTime;
            statusLabel!.Text = $"{title} Log | Elapsed: {elapsed:hh\\:mm\\:ss}";
        }

        private void SaveLog_Click(object? sender, EventArgs e)
        {
            using var dialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"{title}_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                File.WriteAllText(dialog.FileName, logBox!.Text);
                MessageBox.Show($"Log saved to: {dialog.FileName}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        public void Reset()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(Reset));
                return;
            }

            logBox!.ReadOnly = false;
            logBox.ClearAll();      
            logBox.Text = string.Empty;  
            logBox.GotoPosition(0);       
            logBox.ReadOnly = true;      

            startTime = DateTime.Now;
            UpdateStatus();
        }
    }
}