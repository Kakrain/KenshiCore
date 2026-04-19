using KenshiCore.ReverseEngineering;
using KenshiCore.UI;
using KenshiCore.Utilities;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KenshiCore.Mods
{
    public class ModManager
    {

        private static ModManager? _instance;
        private AppConfig config;
        public static ModManager Instance => _instance ??= new ModManager();

        private readonly ReverseEngineer _re;
        private readonly object _lock = new();

        private static string? steamInstallPath;
        public static string? kenshiPath;
        public static string? gamedirModsPath;
        public static string? workshopModsPath;
        private ModManager()
        {
            _re = new ReverseEngineer();
            config = AppConfig.Load();
            solvePaths();
        }
        
        private static string FindSteamInstallPath()
        {
            string? steamPath =
                Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null)
                as string
                ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null)
                as string
                ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam", "InstallPath", null)
                as string
                ?? string.Empty;

            if (string.IsNullOrEmpty(steamPath))
                return string.Empty;

            // Normalize path if registry points to steamapps
            if (Path.GetFileName(steamPath)
                .Equals("steamapps", StringComparison.OrdinalIgnoreCase))
            {
                var parent = Directory.GetParent(steamPath);
                if (parent != null)
                    steamPath = parent.FullName;
            }

            return steamPath;
        }
        private static string? FindKenshiInstallDir(string steamPath)
        {

            if (!string.IsNullOrEmpty(steamPath))
            {
                string defaultPath = Path.Combine(steamPath, "steamapps", "common", "Kenshi");
                if (Directory.Exists(defaultPath))
                    return defaultPath;
            }
            string? folder = UiService.PickFolder("Please select your Kenshi installation folder (it should contain data/mods.cfg).");
            if (!string.IsNullOrEmpty(folder) && File.Exists(Path.Combine(folder, "data", "mods.cfg")))
                return folder;

            UiService.ShowMessage("That folder doesn’t look like a Kenshi install (mods.cfg not found).");
            return null;
        }
        public void solvePaths()
        {
            steamInstallPath = config.SteamPath ?? FindSteamInstallPath();
            kenshiPath = config.KenshiPath ?? FindKenshiInstallDir(steamInstallPath);
            if (string.IsNullOrEmpty(kenshiPath))
            {
                UiService.ShowMessage("Kenshi installation not found!\n Please set it manually by clicking the \"Browse\" button.");
                return;
            }
            gamedirModsPath = Path.Combine(kenshiPath, "mods");
            if (!string.IsNullOrEmpty(steamInstallPath))
                workshopModsPath = Path.Combine(steamInstallPath!, "steamapps", "workshop", "content", "233860");
        }
        public void SetManualSteamPath(string path)
        {
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException(path);

            steamInstallPath = path;
            workshopModsPath = Path.Combine(steamInstallPath, "steamapps", "workshop", "content", "233860");
            config.SteamPath = path;
            config.Save();
        }
        public void SetManualKenshiPath(string path)
        {
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException(path);

            kenshiPath = path;
            gamedirModsPath = Path.Combine(kenshiPath, "mods");

            config.KenshiPath = path;
            config.Save();

            // If steam path was found, set workshop, otherwise leave null
            if (!string.IsNullOrEmpty(steamInstallPath))
                workshopModsPath = Path.Combine(steamInstallPath!, "steamapps", "workshop", "content", "233860");
            else
                workshopModsPath = null;
        }
        public bool TrySetKenshiPath(string path, out string errorMessage)
        {
            errorMessage = "";
            if (!Directory.Exists(path))
            {
                errorMessage = "Selected folder does not exist.";
                return false;
            }

            bool hasExe = File.Exists(Path.Combine(path, "kenshi.exe")) || File.Exists(Path.Combine(path, "kenshi_x64.exe"));
            bool hasData = Directory.Exists(Path.Combine(path, "data"));

            if (!hasExe || !hasData)
            {
                errorMessage = "That folder doesn’t look like a Kenshi install (kenshi.exe or data/ missing).";
                return false;
            }

            SetManualKenshiPath(path);
            return true;
        }
        public bool PromptAndSetKenshiPath()
        {
            string? folder = UiService.PickFolder("Please select your Kenshi installation folder (it should contain kenshi.exe and data/).");
            if (string.IsNullOrEmpty(folder))
                return false;

            return TrySetKenshiPath(folder, out _);
        }
        public void LoadAllMods()
        {
            if (string.IsNullOrEmpty(gamedirModsPath) || string.IsNullOrEmpty(workshopModsPath)) return;

            ModRepository.Instance.LoadBaseGameMods(Path.Combine(kenshiPath!, "data"));
            ModRepository.Instance.LoadGameDirMods(gamedirModsPath);
            ModRepository.Instance.LoadWorkshopMods(workshopModsPath);
            ModRepository.Instance.LoadSelectedMods(Path.Combine(kenshiPath!, "data", "mods.cfg"));
        }
        public ReverseEngineer GetReverseEngineer() => _re;
    }

}
