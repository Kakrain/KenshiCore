using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace KenshiCore
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

        public static void ShowMessage(string text, string caption = "", MessageBoxIcon icon = MessageBoxIcon.None)
        {
            MessageBox.Show(text, caption, MessageBoxButtons.OK, icon);
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
