using KenshiCore.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace KenshiCore.Utilities
{
    public static class Logger
    {
        private static readonly HashSet<string> Muted_files = new();

        public static void Mute(string file)
        {
            Muted_files.Add(file);
        }
        public static void Print(
            string message,
            [CallerFilePath] string file = "",
            [CallerMemberName] string member = "",
            [CallerLineNumber] int line = 0)
        {
            if (Muted_files.Contains(Path.GetFileName(file)))
                return;
            //CoreUtils.Print($"[{file}] {member}:{line} -> {message}");
            CoreUtils.Print($" {message}");
        }
        public static void Prompt(
            string message,
            [CallerFilePath] string file = "",
            [CallerMemberName] string member = "",
            [CallerLineNumber] int line = 0)
        {
            if (Muted_files.Contains(Path.GetFileName(file)))
                return;

            UiService.ShowMessage($" {message}");
        }
    }
}
