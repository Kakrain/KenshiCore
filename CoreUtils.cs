using System.Text;

namespace KenshiCore
{
    public static class CoreUtils
    {
        private static StreamWriter? _logWriter;
        private static readonly object _lock = new object();
        private static string? _currentLogPath;
        private static bool _logEnabled = false;
        public static event Action<string, int>? OnPrint;

        public static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), overwrite: true);

            foreach (var dir in Directory.GetDirectories(sourceDir))
                CopyDirectory(dir, Path.Combine(targetDir, Path.GetFileName(dir)));
        }
        public static List<string> SplitModList(string? modList)
        {
            if (string.IsNullOrWhiteSpace(modList))
                return new List<string>();

            var result = new List<string>();
            var parts = modList.Split(',');
            var currentPart = new StringBuilder();

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                if (currentPart.Length > 0)
                    currentPart.Append(",");

                currentPart.Append(trimmed);

                if (IsCompleteModFilename(currentPart.ToString()))
                {
                    result.Add(currentPart.ToString());
                    currentPart.Clear();
                }
            }

            if (currentPart.Length > 0)
                result.Add(currentPart.ToString());

            return result
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
        public static void Print(string s, int verbose = 0)
        {
            // Optional message box
            if (verbose > 0)
                MessageBox.Show(s);

            System.Diagnostics.Debug.WriteLine(s);
            OnPrint?.Invoke(s, verbose);

            if (_logEnabled)
                WriteDirect($"[{DateTime.Now:HH:mm:ss}] {s}");
        }
        private static bool IsCompleteModFilename(string filename)
        {
            // A complete mod filename should end with .mod or .base
            // and have at least one character before the extension
            if (filename.EndsWith(".mod", StringComparison.Ordinal) ||
                filename.EndsWith(".base", StringComparison.Ordinal))
            {
                var withoutExtension = Path.GetFileNameWithoutExtension(filename);
                return !string.IsNullOrWhiteSpace(withoutExtension);
            }
            return false;
        }

        public static string AddModToList(string? modList, string modName)
        {
            if (string.IsNullOrWhiteSpace(modName))
                return modList ?? string.Empty;
            var mods = SplitModList(modList);
            if (string.Join(",", mods)!.Contains(",.base"))
            {
                MessageBox.Show($"old Dependency:{string.Join(",", modList)} \nfound weird\n {modName}");
                MessageBox.Show($"new Dependency:{string.Join(",", mods)} \nfound weird\n {modName}");
            }
            if (!mods.Any(d => d.Equals(modName, StringComparison.Ordinal)))
                mods.Add(modName);
            if (modName == ".base")
                MessageBox.Show("found .base");
            
            return string.Join(",", mods);
        }
        public static void StartLog(string modName, string outputDir)
        {
            try
            {
                Directory.CreateDirectory(outputDir);
                _currentLogPath = Path.Combine(outputDir, $"{modName}_patch.log");
                _logWriter?.Dispose();
                _logWriter = new StreamWriter(_currentLogPath, append: false, Encoding.UTF8);
                _logEnabled = true;

                WriteDirect($"[{DateTime.Now}] Starting patch for {modName}");
                WriteDirect(new string('-', 60));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CoreUtils] Failed to start log: {ex.Message}");
                _logEnabled = false;
            }
        }
        public static void EndLog(string? summary = null)
        {
            if (!_logEnabled) return;

            lock (_lock)
            {
                WriteDirect(new string('-', 60));
                if (!string.IsNullOrWhiteSpace(summary))
                    WriteDirect(summary);
                WriteDirect($"[{DateTime.Now}] Patch process finished");
                _logWriter?.Flush();
                _logWriter?.Close();
                _logWriter = null;
                _logEnabled = false;
            }
        }
        private static void WriteDirect(string msg)
        {
            if (!_logEnabled || _logWriter == null) return;
            lock (_lock)
            {
                _logWriter.WriteLine(msg);
                _logWriter.Flush();
            }
        }

    }

}