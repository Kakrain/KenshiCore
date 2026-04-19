using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;

namespace KenshiCore.UI
{
    public static class UiService
    {
        public static string? PickFolder(string description)
        {
            using var dialog = new FolderBrowserDialog { Description = description };
            if (dialog.ShowDialog() == DialogResult.OK)
                return dialog.SelectedPath;
            return null;
        }
        /*
        public static void ShowMessage(string text, string caption = "", MessageBoxIcon icon = MessageBoxIcon.None)
        {
            MessageBox.Show(text, caption, MessageBoxButtons.OK, icon);
        }*/
        public static void ShowMessage(string message, string caption = "", MessageBoxIcon icon = MessageBoxIcon.None)
        {
            Form msg = new Form
            {
                Text = string.IsNullOrEmpty(caption) ? "Message" : caption,
                Width = 420,
                Height = 180,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(10)
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            PictureBox? picture = null;

            if (icon != MessageBoxIcon.None)
            {
                picture = new PictureBox
                {
                    SizeMode = PictureBoxSizeMode.AutoSize,
                    Margin = new Padding(0, 5, 10, 0)
                };

                picture.Image = icon switch
                {
                    MessageBoxIcon.Information => SystemIcons.Information.ToBitmap(),
                    MessageBoxIcon.Warning => SystemIcons.Warning.ToBitmap(),
                    MessageBoxIcon.Error => SystemIcons.Error.ToBitmap(),
                    MessageBoxIcon.Question => SystemIcons.Question.ToBitmap(),
                    _ => null
                };

                layout.Controls.Add(picture, 0, 0);
            }

            var label = new Label
            {
                Text = message,
                Dock = DockStyle.Fill,
                AutoSize = true
            };

            layout.Controls.Add(label, 1, 0);

            var ok = new Button
            {
                Text = "OK",
                AutoSize = true,
                Anchor = AnchorStyles.Right
            };

            ok.Click += (s, e) => msg.Close();

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true
            };

            buttonPanel.Controls.Add(ok);
            layout.Controls.Add(buttonPanel, 0, 1);
            layout.SetColumnSpan(buttonPanel, 2);

            msg.Controls.Add(layout);

            ThemeManager.ApplyTheme(msg);

            msg.ShowDialog();
        }

        public static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
        }

        public static void OpenFolderInExplorer(string path)
        {
            if (Directory.Exists(path))
            {
                try
                {
                    Process.Start("explorer.exe", path);
                }
                catch { }
            }
        }
    }
}
