using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

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
        public static bool ShowYesNoQuestion(string message, string caption = "Question")
        {
            var result = MessageBox.Show(
                message,
                caption,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            return result == DialogResult.Yes;
        }
        public static void ShowMessage(string message, string caption = "", MessageBoxIcon icon = MessageBoxIcon.None)
        {
            var owner = Form.ActiveForm ?? (Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null);
            if (owner != null && owner.InvokeRequired)
            {
                owner.Invoke((Action)(() => ShowMessage(message, caption, icon)));
                return;
            }
            Form msg = new Form
            {
                Text = string.IsNullOrEmpty(caption) ? "Message" : caption,
                Width = 420,
                Height = 180,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                TopMost = true
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
            var textBox = new TextBox
            {
                Text = message.Replace("\n", Environment.NewLine),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill
            };

            layout.Controls.Add(textBox, 1, 0);
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
            msg.MinimumSize = new Size(420, 180);
            msg.MaximumSize = new Size(1200, 800);
            ThemeManager.ApplyTheme(msg);

            if (owner != null)
                msg.Show(owner);
            else
                msg.Show();
        }
        public static string? PromptForString(string message, string caption = "Input",string defaultValue = "",bool allowEmpty = false)
        {
            var owner = Form.ActiveForm ??(Application.OpenForms.Count > 0? Application.OpenForms[0]: null);

            using var dialog = new Form
            {
                Text = caption,
                Width = 420,
                Height = 160,
                StartPosition = owner != null? FormStartPosition.CenterParent: FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false
            };

            var label = new Label
            {
                Text = message,
                Dock = DockStyle.Fill,
                AutoSize = true
            };

            var input = new TextBox
            {
                Text = defaultValue,
                Dock = DockStyle.Top
            };

            var ok = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                AutoSize = true
            };

            var cancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                AutoSize = true
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true
            };

            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 1,
                RowCount = 3
            };

            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            layout.Controls.Add(label, 0, 0);
            layout.Controls.Add(input, 0, 1);
            layout.Controls.Add(buttons, 0, 2);

            dialog.Controls.Add(layout);
            dialog.AcceptButton = ok;
            dialog.CancelButton = cancel;

            ok.Click += (_, _) =>
            {
                if (!allowEmpty && string.IsNullOrWhiteSpace(input.Text))
                {
                    MessageBox.Show(
                        dialog,
                        "Please enter a value.",
                        "Input required",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    dialog.DialogResult = DialogResult.None;
                }
            };

            ThemeManager.ApplyTheme(dialog);

            input.SelectAll();
            input.Focus();

            var result = owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();

            return result == DialogResult.OK  ? input.Text.Trim() : null;
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
