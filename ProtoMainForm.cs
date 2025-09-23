using System;
using System.Collections;
using System.Diagnostics;
using System.Windows.Forms;

class ListViewColumnSorter : IComparer
{
    public int Column { get; set; } = 0;
    public SortOrder Order { get; set; } = SortOrder.None;

    public int Compare(object? x, object? y)
    {
        if (Order == SortOrder.None) return 0;

        if (x is not ListViewItem itemX || y is not ListViewItem itemY)
            return 0;

        string textX = itemX.SubItems[Column].Text;
        string textY = itemY.SubItems[Column].Text;

        int result = string.Compare(textX, textY, StringComparison.CurrentCultureIgnoreCase);
        return Order == SortOrder.Ascending ? result : -result;
    }
}

namespace KenshiCore
{
    public class ProtoMainForm : Form
    {
        public ListView modsListView;
        private ImageList modIcons = new ImageList();
        private Dictionary<string, ModItem> mergedMods = new Dictionary<string, ModItem>();
        private List<string> gameDirMods = new List<string>();
        private List<string> selectedMods = new List<string>();
        private List<string> workshopMods = new List<string>();
        private Dictionary<string, ListViewItem> modItemsLookup = new();
        private List<ListViewItem> originalOrder = new();
        protected TextBox? generalLog;
        protected ProgressBar progressBar;
        private Label progressLabel;
        private ModManager modM = new ModManager(new ReverseEngineer());
        private Button openGameDirButton;
        private Button openSteamLinkButton;
        private Button copyToGameDirButton;
        private Color secondary_color = Color.White;
        protected TableLayoutPanel mainlayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
        };
        protected Panel listContainer;
        private FlowLayoutPanel buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true
        };
        public ProtoMainForm()
        {
            Text = "Unnamed Proto Main Form";
            Width = 800;
            Height = 500;

            mainlayout.RowCount = 3;
            mainlayout.RowStyles.Clear();
            mainlayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
            mainlayout.RowStyles.Add(new RowStyle(SizeType.Percent, 70F));
            mainlayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30F));

            mainlayout.ColumnCount = 2;
            mainlayout.ColumnStyles.Clear();
            mainlayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            mainlayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Controls.Add(mainlayout);

            progressBar = new ProgressBar { Dock = DockStyle.Fill, Height = 20, Minimum = 0, Maximum = 0, Value = 0 };
            mainlayout.Controls.Add(progressBar, 0, 0);

            progressLabel = new Label { Dock = DockStyle.Fill, Height = 20, Text = "Ready", TextAlign = ContentAlignment.MiddleLeft };
            mainlayout.Controls.Add(progressLabel, 1, 0);

            modsListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true
            };
            modsListView.Columns.Add("Mod Name", -2, HorizontalAlignment.Left);
            mainlayout.Controls.Add(modsListView, 0, 1);

            modsListView.SelectedIndexChanged += SelectedIndexChanged;
            modsListView.ColumnClick += ModsListView_ColumnClick;
            modsListView.ListViewItemSorter = new ListViewColumnSorter();

            mainlayout.Controls.Add(buttonPanel, 1, 1);
            

            openGameDirButton = AddButton("Open Mod Directory", OpenGameDirButton_Click);
            openSteamLinkButton = AddButton("Open Steam Link", OpenSteamLinkButton_Click);
            copyToGameDirButton = AddButton("Copy to GameDir", CopyToGameDirButton_Click);




            AddButton("Generate Console.log", (sender, e) => GenerateTextFile(generalLog.Text, "Console.log"));

            _= InitializeAsync();
        }
        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await InitializeAsync();
        }
        protected void setColors(Color main, Color secondary)
        {
            this.BackColor = main;
            this.secondary_color = secondary;

            foreach (Control ctrl in buttonPanel.Controls)
            {
                if (ctrl is Button btn)
                {
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.BackColor = secondary_color;
                    btn.FlatAppearance.BorderSize = 0;
                }
            }

        }
        protected void EnableConsoleLog()
        {
            if (generalLog != null) return; // already enabled

            generalLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9),
                BackColor = Color.Black,
                ForeColor = Color.LightGreen
            };

            var logPanel = new Panel { Dock = DockStyle.Fill };
            logPanel.Controls.Add(generalLog);

            mainlayout.Controls.Add(logPanel, 0, 2);
            mainlayout.SetColumnSpan(logPanel, 2);
        }
        protected void LogMessage(string message)
        {
            if (generalLog == null) return;
            generalLog.AppendText($"{message}\n");
            generalLog.SelectionStart = 0;
            generalLog.ScrollToCaret();
        }
        protected void ReportProgress(int done,string labelText) {
            if (IsHandleCreated)
                Invoke((Action)(() =>
                {
                    progressBar.Value = done;
                    progressLabel.Text = labelText;
                }));
        }
        
        protected virtual void SetupColumns() { }

        protected Button AddButton(string text, EventHandler onClick)
        {

            var button = new Button
            {
                Text = text,
                AutoSize = true
            };
            button.Click += onClick;
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = secondary_color;
            button.FlatAppearance.BorderSize = 0;
            buttonPanel.Controls.Add(button);
            return button;
        }

        private void ModsListView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (modsListView.ListViewItemSorter is not ListViewColumnSorter sorter)
            {
                sorter = new ListViewColumnSorter();
                modsListView.ListViewItemSorter = sorter;
            }
            if (sorter.Column == e.Column)
            {
                sorter.Order = sorter.Order switch
                {
                    SortOrder.Ascending => SortOrder.Descending,
                    SortOrder.Descending => SortOrder.None,
                    SortOrder.None => SortOrder.Ascending,
                    _ => SortOrder.Ascending
                };
            }
            else
            {
                sorter.Column = e.Column;
                sorter.Order = SortOrder.Ascending;
            }
            if (sorter.Order == SortOrder.None)
            {
                modsListView.BeginUpdate();
                modsListView.Items.Clear();
                modsListView.Items.AddRange(originalOrder.ToArray());
                modsListView.EndUpdate();
            }
            else
            {
                modsListView.Sort();
            }
            modsListView.Invalidate();
        }

        private async Task InitializeAsync()
        {
            progressLabel.Text = "Loading mods...";
            progressBar.Style = ProgressBarStyle.Marquee;

            gameDirMods = await Task.Run(() => modM.LoadGameDirMods());
            selectedMods = await Task.Run(() => modM.LoadSelectedMods());
            workshopMods = await Task.Run(() => modM.LoadWorkshopMods());

            modIcons.ImageSize = new Size(48, 16);
            modsListView.SmallImageList = modIcons;
            modsListView.OwnerDraw = true;
            modsListView.DrawColumnHeader += ModsListView_DrawColumnHeader;
            modsListView.DrawItem += (s, e) => e.DrawDefault = true;
            modsListView.DrawSubItem += (s, e) => e.DrawDefault = true;

            this.Invoke((MethodInvoker)delegate {
                modIcons.ImageSize = new Size(48, 16);
                modsListView.SmallImageList = modIcons;
                PopulateModsListView();
                modsListView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);

                progressBar.Style = ProgressBarStyle.Continuous;
                progressLabel.Text = "Ready";
            });
        }
        private void ModsListView_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.DrawBackground();
            string text = e.Header.Text;
            if (modsListView.ListViewItemSorter is ListViewColumnSorter sorter && sorter.Column == e.ColumnIndex)
            {
                string marker = sorter.Order switch
                {
                    SortOrder.Ascending => " ▲",
                    SortOrder.Descending => " ▼",
                    SortOrder.None => " ■",
                    _=>""
                };
                text += marker;
            }

            TextRenderer.DrawText(
                e.Graphics,
                text,
                e.Font,
                e.Bounds,
                e.ForeColor,
                TextFormatFlags.Left
            );
        }
        private void SelectedIndexChanged(object? sender, EventArgs? e)
        {
            if (modsListView.SelectedItems.Count != 1)
            {
                openGameDirButton.Enabled = false;
                openSteamLinkButton.Enabled = false;
                copyToGameDirButton.Enabled = false;
                return;
            }

            string modName = modsListView.SelectedItems[0].Text;
            if (mergedMods.TryGetValue(modName, out var mod))
            {
                openGameDirButton.Enabled = mod.InGameDir || mod.WorkshopId != -1;
                copyToGameDirButton.Enabled = !mod.InGameDir && mod.WorkshopId != -1;
                openSteamLinkButton.Enabled = mod.WorkshopId != -1;
            }
        }
        protected ModItem getSelectedMod()
        {
            string modName = modsListView.SelectedItems[0].Text;
            return (ModItem)modsListView.SelectedItems[0].Tag!;

        }

        private void OpenGameDirButton_Click(object? sender, EventArgs e)
        {
            string? modpath = Path.GetDirectoryName(getSelectedMod().getModFilePath());
            if (modpath != null && Directory.Exists(modpath))
                Process.Start("explorer.exe", modpath);
            else
                MessageBox.Show($"{modpath} not found!");
        }

        private void OpenSteamLinkButton_Click(object? sender, EventArgs e)
        {
            string modName = modsListView.SelectedItems[0].Text;
            var mod = mergedMods[modName];
            if (mod != null && mod.WorkshopId != -1)
            {
                string url = $"https://steamcommunity.com/sharedfiles/filedetails/?id={mod.WorkshopId}";
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else
            {
                MessageBox.Show("This mod is not from the Steam Workshop.");
            }
        }

        private void CopyToGameDirButton_Click(object? sender, EventArgs e)
        {
            if (modsListView.SelectedItems.Count != 1) return;

            string modName = modsListView.SelectedItems[0].Text;
            if (!mergedMods.TryGetValue(modName, out var mod)) return;
            if (mod.WorkshopId == -1) return;

            string workshopFolder = Path.Combine(ModManager.workshopModsPath, mod.WorkshopId.ToString());
            string gameDirFolder = Path.Combine(ModManager.gamedirModsPath, Path.GetFileNameWithoutExtension(modName));

            if (Directory.Exists(gameDirFolder))
            {
                MessageBox.Show("Mod already exists in GameDir!");
                return;
            }

            CopyDirectory(workshopFolder, gameDirFolder);
            mod.InGameDir = true;
            modsListView.SelectedItems[0].ImageKey = mod.Name;
            MessageBox.Show($"{mod.Name} copied to GameDir!");
        }

        private void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)));

            foreach (var dir in Directory.GetDirectories(sourceDir))
                CopyDirectory(dir, Path.Combine(targetDir, Path.GetFileName(dir)));
        }
        public static void GenerateTextFile(string content, string filePath)
        {
            try
            {
                string? directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(filePath, content);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error writing file: {ex.Message}");
            }
        }

        private void PopulateModsListView()
        {
            modsListView.Items.Clear();
            originalOrder.Clear();

            foreach (var mod in selectedMods)
            {
                if (!mergedMods.ContainsKey(mod))
                    mergedMods[mod] = new ModItem(mod);
                mergedMods[mod].Selected = true;
            }
            foreach (var mod in gameDirMods)
            {
                if (!mergedMods.ContainsKey(mod))
                    mergedMods[mod] = new ModItem(mod);
                mergedMods[mod].InGameDir = true;
            }
            foreach (var folder_mod in workshopMods)
            {
                string? folderPart = Path.GetDirectoryName(folder_mod!);
                if (folderPart == null) continue;
                string filePart = Path.GetFileName(folder_mod);
                if (!mergedMods.ContainsKey(filePart))
                    mergedMods[filePart] = new ModItem(filePart);
                mergedMods[filePart].WorkshopId = Convert.ToInt64(folderPart);
            }
            foreach (var mod in mergedMods.Values)
            {
                Image icon = mod.CreateCompositeIcon();
                if (!modIcons.Images.ContainsKey(mod.Name))
                    modIcons.Images.Add(mod.Name, icon);
                // Add to ListView
                var item = new ListViewItem(new[] { mod.Name, mod.Language })
                {
                    Tag = mod,
                    ImageKey = mod.Name
                };
                item.UseItemStyleForSubItems = false;
                modsListView.Items.Add(item);
                originalOrder.Add(item);
            }

            
        }
    }
}
