using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KenshiCore
{
    public class ModManager
    {
        private readonly ReverseEngineer _re;
        private readonly object _lock = new();

        private static string? steamInstallPath;
        private static string? kenshiPath;
        public static string? gamedirModsPath;
        public static string? workshopModsPath;
        public ModManager(ReverseEngineer re)
        {

            solvePaths();
            _re = re ?? throw new ArgumentNullException(nameof(re)); ;
        }
        private static string FindSteamInstallPath()
        {
            // Look in HKCU first (preferred for Steam)
            string? steamPath = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
            if (!string.IsNullOrEmpty(steamPath))
                return steamPath;

            // Fallback to HKLM for older installs
            steamPath = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) as string;
            if (!string.IsNullOrEmpty(steamPath))
                return steamPath;

            steamPath = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam", "InstallPath", null) as string;
            return steamPath ?? string.Empty;
        }
        private static string? FindKenshiInstallDir(string steamPath)
        {

            if (!string.IsNullOrEmpty(steamPath))
            {
                string defaultPath = Path.Combine(steamPath, "steamapps", "common", "Kenshi");
                if (Directory.Exists(defaultPath))
                    return defaultPath;
            }

            // 2. If not found, ask the user
            using var dialog = new FolderBrowserDialog
            {
                Description = "Please select your Kenshi installation folder (it should contain data/mods.cfg)."
            };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                if (File.Exists(Path.Combine(dialog.SelectedPath, "data", "mods.cfg")))
                    return dialog.SelectedPath;
                else
                    MessageBox.Show("That folder doesn’t look like a Kenshi install (mods.cfg not found).");
            }

            return null;
        }
        public void solvePaths()
        {
            
            steamInstallPath=FindSteamInstallPath();
            kenshiPath=FindKenshiInstallDir(steamInstallPath);
            if (string.IsNullOrEmpty(kenshiPath))
            {
                MessageBox.Show("Kenshi installation not found!");
                return;
            }
            gamedirModsPath = Path.Combine(kenshiPath, "mods");
            workshopModsPath = Path.Combine(steamInstallPath!, "steamapps", "workshop", "content", "233860");
        }
        public List<string> LoadGameDirMods()
        {
            var result = new List<string>(); 
            if (string.IsNullOrEmpty(gamedirModsPath) || !Directory.Exists(gamedirModsPath))
            {
                MessageBox.Show("gamedir folder not found!");
                return result;
            }
            // Get all subdirectories in the mods folder
            foreach (var folder in Directory.GetDirectories(gamedirModsPath))
            {
                // Get all .mod files in the current folder
                var files = Directory.GetFiles(folder, "*.mod");
                foreach (var file in files)
                {
                    result.Add(Path.GetFileName(file));
                }
            }
            return result;
        }
        public void SetManualSteamPath(string path)
        {
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException(path);

            steamInstallPath = path;
            workshopModsPath = Path.Combine(steamInstallPath, "steamapps", "workshop", "content", "233860");
        }
        public void SetManualKenshiPath(string path)
        {
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException(path);

            kenshiPath = path;
            gamedirModsPath = Path.Combine(kenshiPath, "mods");

            // If steam path was found, set workshop, otherwise leave null
            if (!string.IsNullOrEmpty(steamInstallPath))
                workshopModsPath = Path.Combine(steamInstallPath!, "steamapps", "workshop", "content", "233860");
            else
                workshopModsPath = null;
        }
        public List<string> LoadWorkshopMods()
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(workshopModsPath) || !Directory.Exists(workshopModsPath))
            {
                return result;
            }
            /*if (!Directory.Exists(workshopModsPath))
            {
                MessageBox.Show("workshop folder not found!");
                return result;
            }*/
            foreach (var folder in Directory.GetDirectories(workshopModsPath))
            {
                var files = Directory.GetFiles(folder, "*.mod");
                foreach (var file in files)
                {
                    string parentFolder = new DirectoryInfo(Path.GetDirectoryName(file)!).Name;
                    string fileName = Path.GetFileName(file);
                    string relativeName = Path.Combine(parentFolder, fileName);
                    result.Add(relativeName);
                }
            }
            return result;
        }
        public List<string> LoadSelectedMods()
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(kenshiPath))
                return result;
            string cfgPath = Path.Combine(kenshiPath, "data", "mods.cfg");

            if (!File.Exists(cfgPath))
            {
                MessageBox.Show("mods.cfg not found!");
                return result;
            }

            foreach (var line in File.ReadAllLines(cfgPath))
            {
                if (!string.IsNullOrWhiteSpace(line))
                    result.Add(line.Trim());
            }
            return result;
        }
        public void LoadModFile(string modPath)
        {
            if (string.IsNullOrEmpty(modPath) || !File.Exists(modPath))
            {
                MessageBox.Show($"Mod file path invalid: {modPath}");
                return;
            }
            lock (_lock)
            {
                _re.LoadModFile(modPath);
            }
        }

        public ReverseEngineer GetReverseEngineer() => _re;
    }

}
