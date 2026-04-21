using KenshiCore.Mods;
using KenshiCore.Utilities;
using System;
using System.Collections;
using System.Diagnostics;
using System.Text;
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

namespace KenshiCore.UI
{
    

    public class ProtoMainForm : Form
    {
        public ListView modsListView;
        private ImageList modIcons = new ImageList();
        protected Dictionary<string, ModItem> mergedMods = new Dictionary<string, ModItem>();

        private Dictionary<Action<ModItem>, bool> showActionCache = new Dictionary<Action<ModItem>, bool>();

        protected Boolean shouldResetLog = true;
        protected Boolean shouldLoadBaseGameData = false;

        private Dictionary<string, ListViewItem> modItemsLookup = new();
        private List<ListViewItem> originalOrder = new();
        private ModManager modM=ModManager.Instance;
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


            ProgressController.Instance.Build(mainlayout);

            modsListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = true
            };

            listHost.Controls.Add(modsListView);
            mainlayout.Controls.Add(listHost, 0, 2);
            modsListView.ColumnClick += ModsListView_ColumnClick!;
            modsListView.ListViewItemSorter = new ListViewColumnSorter();

            mainlayout.Controls.Add(buttonPanel, 1, 2);

            AddButton("Open Mod Directory", OpenGameDirButton_Click);
            AddButton("Open Steam Link", OpenSteamLinkButton_Click);
            AddButton("Copy to GameDir", CopyToGameDirButton_Click);
            ShowLogButton = AddButton("Show Log", ShowLogButton_Click);
            
            AddColumn("Mod Name", mod => mod.Name,300);
            modsListView.SelectedIndexChanged += ModsListView_SelectedIndexChanged;
            logForm = new GeneralLogForm();
            if (!string.IsNullOrEmpty(ModManager.gamedirModsPath) && Directory.Exists(ModManager.gamedirModsPath))
                kenshiDirTextBox.Text = Path.GetDirectoryName(ModManager.gamedirModsPath);
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
                ProgressController.Instance.setLabel("Please set Kenshi directory.");
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
            BeginInvoke((MethodInvoker)delegate
            {
                if(shouldResetLog)
                    getLogForm().Reset();
                var mods = getSelectedMods();
                foreach (var mod in mods)
                {
                    foreach (var kvp in showActionCache)
                    {
                        if (kvp.Value)
                            kvp.Key(mod);
                    }
                }
            });
            //modsListView.Refresh();
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
            if (!modM.PromptAndSetKenshiPath())
            {
                UiService.ShowMessage("That folder doesn’t look like a Kenshi install (kenshi.exe or data/ missing).");
                return;
            }
            kenshiDirTextBox.Text = ModManager.gamedirModsPath; // automatically updated
            TryInitialize();
        }

        private void BrowseSteam_Click(object? sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog { Description = "Select Steam installation folder" };
            if (dialog.ShowDialog() != DialogResult.OK)
                return;
            string selected = dialog.SelectedPath;
            bool isSteamRoot = Directory.Exists(Path.Combine(selected, "steamapps"));
            bool isSteamApps = Path.GetFileName(selected).Equals("steamapps", StringComparison.OrdinalIgnoreCase);
            if (!isSteamRoot && !isSteamApps)
            {
                UiService.ShowMessage("That folder doesn’t look like a Steam installation.\nPlease select the Steam folder OR the steamapps folder.");
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
            if (!string.IsNullOrEmpty(ModManager.gamedirModsPath))
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

            ThemeManager.ApplyTheme(this);
            if (InitializationTask!=null)
                await InitializationTask;
        }
        protected virtual void SetupColumns() { }

        protected Button AddButton(string text, EventHandler onClick)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = true,
                Enabled = true
            };
            button.Click += onClick;
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = secondary_color;
            button.FlatAppearance.BorderSize = 0;

            ThemeManager.ApplyThemeToControl(button);
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
            //modsListView.Invalidate();
            UpdateColumnSortMarker(sorter.Column, sorter.Order);
        }
        
        private async Task InitializeAsync()
        {
            await Task.Run(() => LoadMods());
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
            });

            await AfterModsLoadedAsync();
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
        protected List<ModItem> getSelectedMods()
        {
            var result = new List<ModItem>();
            foreach (ListViewItem item in modsListView.SelectedItems)
            {
                if (item.Tag is ModItem mod)
                    result.Add(mod);
            }
            return result;
        }

        private void OpenGameDirButton_Click(object? sender, EventArgs e)
        {
            var mods = getSelectedMods();
            foreach (var mod in mods)
            {
                string? modpath = Path.GetDirectoryName(mod.getModFilePath());
                if (modpath != null && Directory.Exists(modpath))
                    Process.Start("explorer.exe", modpath);
                else
                    UiService.ShowMessage($"{modpath} not found!");
            }
        }
        private void OpenSteamLinkButton_Click(object? sender, EventArgs e)
        {
            var mods= getSelectedMods().Where(m => m.WorkshopId!=-1).ToList();
            if (mods.Count == 0)
            {
                UiService.ShowMessage("No selected mod is from the Steam Workshop.");
                return;
            }
            foreach( var mod in mods)
            {
                string url = $"https://steamcommunity.com/sharedfiles/filedetails/?id={mod.WorkshopId}";
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        }
        private void CopyToGameDirButton_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(ModManager.workshopModsPath))
            {
                UiService.ShowMessage("Workshop folder not set. Please set Kenshi directory first.");
                return;
            }
            var mods = getSelectedMods().Where(m => !m.InGameDir && m.WorkshopId != -1).ToList();
            if (mods.Count == 0)
            {
                UiService.ShowMessage("No selected mods are from the Steam Workshop.");
                return;
            }
            foreach (var mod in mods)
            {
                string modName = mod.Name;
                string workshopFolder = Path.Combine(ModManager.workshopModsPath!, mod.WorkshopId.ToString());
                string gameDirFolder = Path.Combine(ModManager.gamedirModsPath!, Path.GetFileNameWithoutExtension(modName));
                if (!Directory.Exists(gameDirFolder))
                {
                    CopyDirectory(workshopFolder, gameDirFolder);
                    mod.InGameDir = true;
                    //modsListView.SelectedItems[0].ImageKey = mod.Name;
                    UpdateModIcon(mod);
                }
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendJoin(",", mods.ConvertAll(m => m.Name));
            UiService.ShowMessage($"{sb.ToString()} copied to GameDir!");
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
            mergedMods = ModRepository.Instance.FilterSelectedMods(mergedMods);
            RefreshListView();
        }
        private void RefreshListView()
        {
            modsListView.BeginUpdate();
            try
            {
                modsListView.Items.Clear();
                originalOrder.Clear();

                foreach (var mod in mergedMods.Values)
                {
                    AddModItemToListView(mod);
                }
            }
            finally
            {
                modsListView.EndUpdate();
            }
        }
        protected virtual void LoadMods()
        {
            throw new NotImplementedException("Please override LoadMods in your form to load mods into the ModManager.");
        }
        protected virtual Task AfterModsLoadedAsync()
        {
            return Task.CompletedTask;
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
        private void UpdateModIcon(ModItem mod)
        {
            Image icon = mod.CreateCompositeIcon();

            modIcons.Images.RemoveByKey(mod.Name);
            modIcons.Images.Add(mod.Name, icon);

            var item = modsListView.Items
                .Cast<ListViewItem>()
                .FirstOrDefault(i => i.Tag == mod);

            if (item != null)
                item.ImageKey = mod.Name;
        }
        protected virtual void PopulateModsListView()
        {
            modsListView.Items.Clear();
            originalOrder.Clear();
            mergedMods = ModRepository.Instance.GetMergedMods();
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
