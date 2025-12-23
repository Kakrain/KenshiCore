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

        int result;


        if (int.TryParse(textX, out int numX) && int.TryParse(textY, out int numY))
        {
            result = numX.CompareTo(numY);
        }
        else
        {
            result = string.Compare(textX, textY, StringComparison.CurrentCulture);
        }


        return Order == SortOrder.Ascending ? result : -result;
    }
}

namespace KenshiCore
{
    

    public class ProtoMainForm : Form
    {
        public ListView modsListView;
        private ImageList modIcons = new ImageList();
        protected Dictionary<string, ModItem> mergedMods = new Dictionary<string, ModItem>();
        private List<string> baseGameData = new List<string>();
        private List<string> gameDirMods = new List<string>();
        private List<string> selectedMods = new List<string>();
        private List<string> workshopMods = new List<string>();

        private Dictionary<Action<ModItem>, bool> showActionCache = new Dictionary<Action<ModItem>, bool>();
        private Dictionary<Button, Func<ModItem, bool>> ButtonCache = new Dictionary<Button,Func<ModItem, bool>>();

        protected Boolean shouldResetLog = true;
        protected Boolean shouldLoadBaseGameData = false;

        
        private Dictionary<string, ListViewItem> modItemsLookup = new();
        private List<ListViewItem> originalOrder = new();
        private ProgressBar progressBar;
        private Label progressLabel;
        private ModManager modM;
        private Color secondary_color = Color.White;
        private TextBox kenshiDirTextBox;
        private TextBox steamDirTextBox;
        protected Button ShowLogButton;
        protected Task? InitializationTask { get; private set; }
        private List<(ColumnHeader header, Func<ModItem, object> selector)> columnDefs= new();
        private GeneralLogForm? logForm;
        protected TableLayoutPanel mainlayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
        };
        protected Panel? listContainer;
        protected FlowLayoutPanel buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true
        };
        protected Panel listHost = new Panel
        {
            Dock = DockStyle.Fill
        };
        public ProtoMainForm()
        {
            Text = "Unnamed Proto Main Form";
            Width = 800;
            Height = 500;

            mainlayout.RowCount = 3;
            mainlayout.RowStyles.Clear();
            mainlayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));
            mainlayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
            mainlayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));   // row 2: list + buttons

            mainlayout.ColumnCount = 2;
            mainlayout.ColumnStyles.Clear();
            mainlayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            mainlayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Controls.Add(mainlayout);

            var dirTable = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 3,
                RowCount = 2
            };
            dirTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));  // label
            dirTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // textbox
            dirTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));  // button
    
            var kenshiLabel = new Label { Text = "Kenshi Directory:", AutoSize = true };
            kenshiDirTextBox = new TextBox { Width = 300 };
            var browseKenshi = new Button { Text = "Browse...", AutoSize = true };
            browseKenshi.Click += BrowseKenshi_Click;

            var steamLabel = new Label { Text = "Steam Directory:", AutoSize = true };
            steamDirTextBox = new TextBox { Width = 300 };
            var browseSteam = new Button { Text = "Browse...", AutoSize = true };
            browseSteam.Click += BrowseSteam_Click;

            dirTable.Controls.Add(kenshiLabel, 0, 0);
            dirTable.Controls.Add(kenshiDirTextBox, 1, 0);
            dirTable.Controls.Add(browseKenshi, 2, 0);

            dirTable.Controls.Add(steamLabel, 0, 1);
            dirTable.Controls.Add(steamDirTextBox, 1, 1);
            dirTable.Controls.Add(browseSteam, 2, 1);

            mainlayout.Controls.Add(dirTable, 0, 0);
            mainlayout.SetColumnSpan(dirTable, 2);



            progressBar = new ProgressBar { Dock = DockStyle.Fill, Height = 20, Minimum = 0, Maximum = 0, Value = 0 };
            mainlayout.Controls.Add(progressBar, 0, 1);

            progressLabel = new Label { Dock = DockStyle.Fill, Height = 20, Text = "Ready", TextAlign = ContentAlignment.MiddleLeft };
            mainlayout.Controls.Add(progressLabel, 1, 1);

            modsListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false
            };

            listHost.Controls.Add(modsListView);
            mainlayout.Controls.Add(listHost, 0, 2);
            modsListView.ColumnClick += ModsListView_ColumnClick!;
            modsListView.ListViewItemSorter = new ListViewColumnSorter();

            mainlayout.Controls.Add(buttonPanel, 1, 2);

            AddButton("Open Mod Directory", OpenGameDirButton_Click,mod=>mod.InGameDir||mod.WorkshopId!=-1);
            AddButton("Open Steam Link", OpenSteamLinkButton_Click,mod=>mod.WorkshopId!=-1);
            AddButton("Copy to GameDir", CopyToGameDirButton_Click,mod=>!mod.InGameDir&&mod.WorkshopId!=-1);
            ShowLogButton = AddButton("Show Log", ShowLogButton_Click);
            
            AddColumn("Mod Name", mod => mod.Name,300);
            modsListView.SelectedIndexChanged += ModsListView_SelectedIndexChanged;



            logForm = new GeneralLogForm();


            modM =new ModManager(new ReverseEngineer());

            foreach (var kvp in ButtonCache)
            {
                kvp.Key.Enabled = false;
            }

            if (!string.IsNullOrEmpty(ModManager.gamedirModsPath) && Directory.Exists(ModManager.gamedirModsPath))
                kenshiDirTextBox.Text = Path.GetDirectoryName(ModManager.gamedirModsPath);
            /*if (!string.IsNullOrEmpty(ModManager.workshopModsPath) && Directory.Exists(ModManager.workshopModsPath))
            {
                var steamRoot = Directory.GetParent(Directory.GetParent(Directory.GetParent(ModManager.workshopModsPath)!.FullName)!.FullName!)!.FullName;
                steamDirTextBox.Text = steamRoot;
            }
            else
            {
                steamDirTextBox.Text = ""; // leave blank if Steam/workshop not found
            }*/
            if (!string.IsNullOrEmpty(ModManager.workshopModsPath) && Directory.Exists(ModManager.workshopModsPath))
            {
                var steamRoot = GetSteamRootFromWorkshopPath(ModManager.workshopModsPath);
                if (!string.IsNullOrEmpty(steamRoot))
                    steamDirTextBox.Text = steamRoot;
                else
                    // fallback to workshopModsPath parent if helper failed
                    steamDirTextBox.Text = Directory.GetParent(ModManager.workshopModsPath)!.FullName;
            }
            else
            {
                steamDirTextBox.Text = ""; // leave blank if Steam/workshop not found
            }
            if (!string.IsNullOrEmpty(ModManager.gamedirModsPath) &&!string.IsNullOrEmpty(ModManager.workshopModsPath))
            {
                InitializationTask = InitializeAsync();
            }
            else
            {
                progressLabel.Text = "Please set Kenshi directory.";
            }

        }
        private string? GetSteamRootFromWorkshopPath(string? workshopPath)
        {
            if (string.IsNullOrEmpty(workshopPath)) return null;

            try
            {
                DirectoryInfo? dir = new DirectoryInfo(workshopPath);
                while (dir != null && !dir.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase))
                {
                    dir = dir.Parent;
                }

                // dir now points at the "steamapps" directory if found
                if (dir != null && dir.Parent != null)
                    return dir.Parent.FullName; // Steam root
            }
            catch { /* ignore and fallback below */ }

            // Fallback: if we can't find steamapps, try using the parent chain or return original path
            try
            {
                // If the path happens to already be Steam root (contains steamapps as child), use that.
                if (Directory.Exists(Path.Combine(workshopPath!, "steamapps")))
                    return workshopPath;

                // otherwise try a safer up-level normalization
                var p = new DirectoryInfo(workshopPath!);
                var parent = p.Parent?.Parent?.Parent?.Parent; // best-effort
                return parent?.FullName ?? workshopPath;
            }
            catch
            {
                return workshopPath;
            }
        }
        public GeneralLogForm getLogForm()
        {
            if (logForm == null || logForm.IsDisposed)
            {
                logForm = new GeneralLogForm();
            }
            return logForm;
        }
        protected virtual void ModsListView_SelectedIndexChanged(object? sender, EventArgs? e)
        {
            if (modsListView.SelectedItems.Count == 0)
                return;
            BeginInvoke((MethodInvoker)delegate
            {
                ModItem? selectedmod = getSelectedMod();
                if (selectedmod == null)
                    return;
                if(shouldResetLog)
                    getLogForm().Reset();
                modsListView.BeginUpdate();
                foreach (var kvp in ButtonCache)
                {
                    kvp.Key.Enabled = kvp.Value(selectedmod);
                }
                foreach (var kvp in showActionCache)
                {
                    if (kvp.Value)
                        kvp.Key(selectedmod);
                }
                
                modsListView.EndUpdate();
                modsListView.Refresh();
            });
        }
        private void ShowLogButton_Click(object? sender, EventArgs e)
        {
            logForm = getLogForm();

            if (logForm.Visible)
            {
                logForm.BringToFront();
            }
            else
            {
                logForm.Show(this);
            }
        }
        private void BrowseKenshi_Click(object? sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog { Description = "Select Kenshi installation folder" };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                string selected = dialog.SelectedPath;
                if ((File.Exists(Path.Combine(selected, "kenshi.exe")) || File.Exists(Path.Combine(selected, "kenshi_x64.exe"))) && Directory.Exists(Path.Combine(selected, "data")))
                {
                    kenshiDirTextBox.Text = selected;
                    modM.SetManualKenshiPath(selected);
                    TryInitialize();
                }
                else
                {
                    MessageBox.Show("That folder doesn’t look like a Kenshi install (kenshi.exe or data/ missing).");
                }
            }
        }

        private void BrowseSteam_Click(object? sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog { Description = "Select Steam installation folder" };
            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            string selected = dialog.SelectedPath;

            bool isSteamRoot = Directory.Exists(Path.Combine(selected, "steamapps"));
            bool isSteamApps = Path.GetFileName(selected)
                                    .Equals("steamapps", StringComparison.OrdinalIgnoreCase);

            if (!isSteamRoot && !isSteamApps)
            {
                MessageBox.Show("That folder doesn’t look like a Steam installation.\nPlease select the Steam folder OR the steamapps folder.");
                return;
            }

            // Normalize:
            // If user selected "steamapps", go one level up so we store the Steam root.
            if (isSteamApps)
                selected = Directory.GetParent(selected)!.FullName;

            steamDirTextBox.Text = selected;
            modM.SetManualSteamPath(selected);
            TryInitialize();
        }
        protected void TryInitialize()
        {
            if (!string.IsNullOrEmpty(ModManager.gamedirModsPath)) //&&!string.IsNullOrEmpty(ModManager.workshopModsPath))
            {
                InitializationTask = InitializeAsync();
            }
        }
        protected void AddColumn(string title, Func<ModItem, object> selector, int width = -2)
        {
            if (width == -2)
                width = title.Length*8;
            var col = new ColumnHeader
            {
                Text = title,
                Width = width,
                TextAlign = HorizontalAlignment.Left,
                Tag = title // store original title
            };
            modsListView.Columns.Add(col);
            columnDefs.Add((col, selector));
            
        }
        private void UpdateColumnSortMarker(int sortedColumn, SortOrder order)
        {
            for (int i = 0; i < modsListView.Columns.Count; i++)
            {
                var col = modsListView.Columns[i];
                string baseText = col.Tag?.ToString() ?? col.Text;

                if (i == sortedColumn)
                {
                    string arrow = order switch
                    {
                        SortOrder.Ascending => " ▲",
                        SortOrder.Descending => " ▼",
                        _ => " ■"
                    };
                    col.Text = baseText + arrow;
                }
                else
                {
                    col.Text = baseText; // reset other columns
                }
            }
        }
        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if(InitializationTask!=null)
                await InitializationTask;
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
        protected void InitializeProgress(int init, int total)
        {
            if (IsHandleCreated)
                Invoke((Action)(() =>
                {
                    progressBar.Value = init;
                    progressBar.Minimum = init;
                    progressBar.Maximum = total;
                    progressLabel.Text = "";
                }));
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

        protected Button AddButton(string text, EventHandler onClick,Func<ModItem,bool>? enabledFunc = null)
        {
            enabledFunc ??= m => true;
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


            ButtonCache.Add(button, enabledFunc);

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
            //modsListView.Invalidate();
            UpdateColumnSortMarker(sorter.Column, sorter.Order);
        }

        private async Task InitializeAsync()
        {
            progressLabel.Text = "Loading mods...";
            progressBar.Style = ProgressBarStyle.Marquee;
            baseGameData = await Task.Run(() => modM.LoadBaseGameData());
            gameDirMods = await Task.Run(() => modM.LoadGameDirMods());
            selectedMods = await Task.Run(() => modM.LoadSelectedMods());
            workshopMods = await Task.Run(() => modM.LoadWorkshopMods());

            modIcons.ImageSize = new Size(48, 16);
            modsListView.SmallImageList = modIcons;
            modsListView.OwnerDraw = false;//true;
            modsListView.DrawColumnHeader += ModsListView_DrawColumnHeader;
            modsListView.DrawItem += (s, e) => e.DrawDefault = true;
            modsListView.DrawSubItem += (s, e) => e.DrawDefault = true;

            this.Invoke((MethodInvoker)delegate {
                modIcons.ImageSize = new Size(48, 16);
                modsListView.SmallImageList = modIcons;
                PopulateModsListView();

                progressBar.Style = ProgressBarStyle.Continuous;
                progressLabel.Text = "Ready";
            });
        }
        private void ModsListView_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.DrawBackground();
            string text = e.Header!.Text;
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
        protected ModItem? getSelectedMod()
        {
            if (modsListView.SelectedItems.Count == 0)
            {
                CoreUtils.Print("⚠ No item is currently selected.");
                return null;
            }
            string modName = modsListView.SelectedItems[0].Text;
            return (ModItem)modsListView.SelectedItems[0].Tag!;
        }

        private void OpenGameDirButton_Click(object? sender, EventArgs e)
        {
            string? modpath = Path.GetDirectoryName(getSelectedMod()!.getModFilePath());
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
            if (string.IsNullOrEmpty(ModManager.workshopModsPath))
            {
                MessageBox.Show("Workshop folder not set. Please set Kenshi directory first.");
                return;
            }
            if (modsListView.SelectedItems.Count != 1) return;
            string modName = modsListView.SelectedItems[0].Text;
            if (!mergedMods.TryGetValue(modName, out var mod)) return;
            if (mod.WorkshopId == -1) return;

            string workshopFolder = Path.Combine(ModManager.workshopModsPath!, mod.WorkshopId.ToString());
            string gameDirFolder = Path.Combine(ModManager.gamedirModsPath!, Path.GetFileNameWithoutExtension(modName));

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
        private void RefreshColumn(int colIndex, Func<ModItem, object> selector)
        {
            modsListView.BeginUpdate();
            try
            {
                foreach (ListViewItem item in modsListView.Items)
                {
                    if (item.Tag is ModItem mod)
                    {
                        var newValue = selector(mod)?.ToString() ?? "";
                        item.SubItems[colIndex].Text = newValue;
                    }
                }
            }
            finally
            {
                modsListView.EndUpdate();
            }

            // Optional: force repaint of just that column
            modsListView.Invalidate(GetColumnBounds(modsListView, colIndex));
        }
        protected void RefreshColumn(int colIndex)
        {
            if (colIndex < 0 || colIndex >= columnDefs.Count)
                return;

            var selector = columnDefs[colIndex].selector;
            RefreshColumn(colIndex, selector);
        }
        private Rectangle GetColumnBounds(ListView list, int colIndex)
        {
            int x = 0;
            for (int i = 0; i < colIndex; i++)
                x += list.Columns[i].Width;

            return new Rectangle(x, 0, list.Columns[colIndex].Width, list.Height);
        }
        protected void LoadBaseGameData()
        {
            modsListView.BeginUpdate();
            try
            {
                // First, temporarily capture existing mergedMods
                var reordered = new Dictionary<string, ModItem>();

                // Add all base mods FIRST
                foreach (var mod in baseGameData)
                {
                    if (!mergedMods.ContainsKey(mod))
                    {
                        var m = new ModItem(mod) { IsBaseGame = true, Selected = true };
                        mergedMods[mod] = m;
                    }

                    reordered[mod] = mergedMods[mod];
                }

                // Then add the rest of the mods in the previous order
                foreach (var kv in mergedMods)
                {
                    if (!reordered.ContainsKey(kv.Key))
                        reordered[kv.Key] = kv.Value;
                }

                // Replace the mergedMods dictionary with the reordered one
                mergedMods = reordered;

                // Finally, ensure the ListView order matches
                foreach (var mod in baseGameData)
                {
                    AddModItemToListView(mergedMods[mod], insertFirst: true);
                }
            }
            finally
            {
                modsListView.EndUpdate();
                modsListView.Refresh();
            }
        }

        private void AddModItemToListView(ModItem mod, bool insertFirst = false)
        {
            Image icon = mod.CreateCompositeIcon();

            if (!modIcons.Images.ContainsKey(mod.Name))
                modIcons.Images.Add(mod.Name, icon);

            var item = new ListViewItem(new[] { columnDefs[0].selector(mod)?.ToString() ?? "" })
            {
                Tag = mod,
                ImageKey = mod.Name
            };

            for (int i = 1; i < columnDefs.Count; i++)
            {
                var value = columnDefs[i].selector(mod);
                item.SubItems.Add(value?.ToString() ?? "");
            }

            item.UseItemStyleForSubItems = false;

            if (insertFirst)
                modsListView.Items.Insert(0, item);
            else
                modsListView.Items.Add(item);

            if (insertFirst)
                originalOrder.Insert(0, item);
            else
                originalOrder.Add(item);
        }
        protected void ExcludeUnselectedMods()
        {
            // Remove mods from mergedMods that are not in selectedMods
            var toRemove = mergedMods.Keys
                .Where(modName => !selectedMods.Contains(modName, StringComparer.Ordinal))
                .ToList();

            foreach (var modName in toRemove)
                mergedMods.Remove(modName);

            // Update the ListView accordingly
            modsListView.BeginUpdate();
            try
            {
                foreach (ListViewItem item in modsListView.Items.Cast<ListViewItem>().ToList())
                {
                    if (item.Tag is ModItem mod && !selectedMods.Contains(mod.Name, StringComparer.Ordinal))
                    {
                        modsListView.Items.Remove(item);
                        originalOrder.Remove(item);
                    }
                }
            }
            finally
            {
                modsListView.EndUpdate();
                modsListView.Refresh();
            }

        }

        protected void AddToggle(string label, Action<ModItem> onToggled, bool initialState = false)
        {
            var checkbox = new CheckBox
            {
                Text = label,
                Checked = initialState,
                AutoSize = true,
                BackColor = secondary_color,
                Padding = new Padding(2),
                Margin = new Padding(3)
            };

            showActionCache[onToggled] = initialState;

            checkbox.CheckedChanged += (s, e) =>
            {
                showActionCache[onToggled] = ((CheckBox)s!).Checked;
                ModsListView_SelectedIndexChanged(null, null);
            };

            buttonPanel.Controls.Add(checkbox);
        }
        protected virtual void PopulateModsListView()
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
                string? folderName = Path.GetFileName(folderPart);
                if (long.TryParse(folderName, out long workshopId))//just in case some genius adds non-numeric folders in the workshop dir
                {
                    mergedMods[filePart].WorkshopId = workshopId;
                }
                //mergedMods[filePart].WorkshopId = Convert.ToInt64(folderPart);
            }
            modIcons.Images.Clear();
            foreach (var mod in mergedMods.Values)
            {
                
                Image icon = mod.CreateCompositeIcon();
                if (!modIcons.Images.ContainsKey(mod.Name))
                    modIcons.Images.Add(mod.Name, icon);
                var item = new ListViewItem(new[] { columnDefs[0].selector(mod)?.ToString() ?? "" })
                {
                    Tag = mod,
                    ImageKey = mod.Name
                };

                for (int i = 1; i < columnDefs.Count; i++)
                {
                    var value = columnDefs[i].selector(mod); // call your selector
                    var subItem = new ListViewItem.ListViewSubItem(item, value?.ToString() ?? "");
                    item.SubItems.Add(subItem);
                }

                item.UseItemStyleForSubItems = false;
                modsListView.Items.Add(item);
                originalOrder.Add(item);
            }
        }
        
    }
}
