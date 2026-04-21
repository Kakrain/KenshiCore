using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KenshiCore.Mods
{
    public class ModRepository
    {
        private static ModRepository? _instance;
        public static ModRepository Instance => _instance ??= new ModRepository();
        private ModRepository() { }
        private readonly List<string> _baseGameMods = new();
        private readonly List<string> _gameDirMods = new();
        private readonly List<string> _workshopMods = new();
        private readonly List<string> _selectedMods = new();
        public IReadOnlyList<string> BaseGameMods => _baseGameMods;
        public IReadOnlyList<string> GameDirMods => _gameDirMods;
        public IReadOnlyList<string> WorkshopMods => _workshopMods;
        public IReadOnlyList<string> SelectedMods => _selectedMods;

        public bool excludeUnselectedMods = false;

        public void LoadBaseGameMods()//string gamedirDataPath)
        {
            string gamedirDataPath = Path.Combine(ModManager.kenshiPath!, "data");
            _baseGameMods.Clear();
            if (!Directory.Exists(gamedirDataPath)) return;

            foreach (var file in Directory.GetFiles(gamedirDataPath, "*.mod"))
                _baseGameMods.Add(Path.GetFileName(file));
            foreach (var file in Directory.GetFiles(gamedirDataPath, "*.base"))
                _baseGameMods.Add(Path.GetFileName(file));
            
        }
        public void LoadGameDirMods()//string modsPath)
        {
            string modsPath = ModManager.gamedirModsPath!;
            _gameDirMods.Clear();
            if (!Directory.Exists(modsPath)) return;

            foreach (var folder in Directory.GetDirectories(modsPath))
                _gameDirMods.AddRange(Directory.GetFiles(folder, "*.mod").Select(Path.GetFileName)!);
        }
        public void LoadWorkshopMods()//string workshopPath)
        {
            string workshopPath = ModManager.workshopModsPath!;
            _workshopMods.Clear();
            if (!Directory.Exists(workshopPath)) return;

            foreach (var folder in Directory.GetDirectories(workshopPath))
            {
                _workshopMods.AddRange(Directory.GetFiles(folder, "*.mod")
                    .Select(f => Path.Combine(new DirectoryInfo(Path.GetDirectoryName(f)!).Name, Path.GetFileName(f))));
            }
        }
        public void LoadSelectedMods()//string cfgPath)
        {
            string cfgPath = Path.Combine(ModManager.kenshiPath!, "data", "mods.cfg");
            _selectedMods.Clear();
            if (!File.Exists(cfgPath)) return;

            foreach (var line in File.ReadAllLines(cfgPath))
            {
                if (!string.IsNullOrWhiteSpace(line))
                    _selectedMods.Add(line.Trim());
            }
        }
        public Dictionary<string, ModItem> GetMergedMods()
        {
            var merged = new Dictionary<string, ModItem>();

            // 1. Base game mods
            foreach (var mod in BaseGameMods)
            {
                if (!merged.ContainsKey(mod))
                    merged[mod] = new ModItem(mod)
                    {
                        IsBaseGame = true,
                        Selected = true
                    };
            }
            // 2. Selected mods (mods.cfg)
            foreach (var mod in SelectedMods)
            {
                if (!merged.ContainsKey(mod))
                    merged[mod] = new ModItem(mod);
                merged[mod].Selected = true;
            }

            // 3. GameDir mods
            foreach (var mod in GameDirMods)
            {
                if (!merged.ContainsKey(mod))
                    merged[mod] = new ModItem(mod);
                merged[mod].InGameDir = true;
            }
            // 4. Workshop mods
            foreach (var folderMod in WorkshopMods)
            {
                string? folderPart = Path.GetDirectoryName(folderMod);
                if (folderPart == null) continue;

                string filePart = Path.GetFileName(folderMod);
                if (!merged.ContainsKey(filePart))
                    merged[filePart] = new ModItem(filePart);

                string folderName = Path.GetFileName(folderPart);
                if (long.TryParse(folderName, out long workshopId))
                    merged[filePart].WorkshopId = workshopId;
            }
            // 5. Exclude unselected mods if the option is enabled
            if (excludeUnselectedMods) { 
                var unselectedKeys = merged.Where(kvp => !kvp.Value.Selected).Select(kvp => kvp.Key).ToList();
                foreach (var key in unselectedKeys)
                    merged.Remove(key);
            }
            return merged;
        }
        public Dictionary<string, ModItem> FilterSelectedMods(Dictionary<string, ModItem> mods)
        {
            var selected = SelectedMods;
            return mods
                .Where(kvp => selected.Contains(kvp.Key, StringComparer.Ordinal))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        public Dictionary<string, ModItem> Mods { get; private set; } = new();
        

    }
}
