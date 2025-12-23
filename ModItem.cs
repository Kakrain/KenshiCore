using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace KenshiCore
{
    public class ModItem
    {
        public string Name { get; set; }
        public string Language { get; set; } = "detecting...";
        public bool InGameDir { get; set; }
        public bool Selected { get; set; }
        public long WorkshopId { get; set; }
        private static Dictionary<int, Image> iconCache = new();
        public bool IsBaseGame { get; set; }

        public static Image? gameDirIcon = ResourceLoader.LoadImage("KenshiCore.icons.kenshiicon.png");
        public static Image? workshopIcon = ResourceLoader.LoadImage("KenshiCore.icons.steamicon.png");
        private static Image? image = ResourceLoader.LoadImage("KenshiCore.icons.selectedicon.png");
        public static Image? selectedIcon = image;
        public ModItem(string name)
        {
            InGameDir = false;
            Selected = false;
            WorkshopId = -1;
            Name = name ?? throw new ArgumentNullException(nameof(name)); ;
        }
        public Image CreateCompositeIcon()
        {
            if (IsBaseGame)
            {
                using (Bitmap tempBmp = new Bitmap(48, 16))
                {
                    using (Graphics g = Graphics.FromImage(tempBmp))
                    {
                        g.DrawImage(gameDirIcon!, 0, 0);
                        g.DrawImage(gameDirIcon!, 16, 0);
                        g.DrawImage(gameDirIcon!, 32, 0);
                    }
                    Image finalImage = (Image)tempBmp.Clone();
                    return finalImage;
                }

            }
            int key = (Convert.ToInt32(InGameDir) * 100) +
                      (Convert.ToInt32(WorkshopId != -1) * 10) +
                      Convert.ToInt32(Selected);

            if (iconCache.TryGetValue(key, out var cached))
                return cached;

            using (Bitmap blank = new Bitmap(16, 16))
            using (Bitmap tempBmp = new Bitmap(48, 16))
            {
                using (Graphics g = Graphics.FromImage(tempBmp))
                {
                    g.DrawImage(InGameDir ? gameDirIcon! : blank, 0, 0);
                    g.DrawImage(WorkshopId != -1 ? workshopIcon! : blank, 16, 0);
                    g.DrawImage(Selected ? selectedIcon! : blank, 32, 0);
                }

                Image finalImage = (Image)tempBmp.Clone();
                iconCache[key] = finalImage;
                return finalImage;
            }
        }
        public static void DisposeIconCache()
        {
            foreach (var image in iconCache.Values)
            {
                image.Dispose();
            }
            iconCache.Clear();
        }
        public string getBackupFilePath()
        {
            return Path.Combine(Path.GetDirectoryName(getModFilePath())!, Path.GetFileNameWithoutExtension(Name) + ".backup");
        }
        public string getDictFilePath()
        {
            return Path.Combine(Path.GetDirectoryName(getModFilePath())!, Path.GetFileNameWithoutExtension(Name) + ".dict");
        }
        public string? getGamedirModPath()
        {
            if (InGameDir)
            {
                return Path.Combine(ModManager.gamedirModsPath!, Path.GetFileNameWithoutExtension(Name), Name);
            }
            return null;
        }
        public string? getWorkshopModPath()
        {
            if (WorkshopId != -1)
            {
                return Path.Combine(ModManager.workshopModsPath!, WorkshopId.ToString(), Name);
            }
            return null;
        }
        public string? getModFilePath()
        {
            if (IsBaseGame)
            {
                string dataDir = Path.Combine(Path.GetDirectoryName(ModManager.gamedirModsPath!)!, "data");
                return Path.Combine(dataDir, Name);
            }
            if (InGameDir)
            {
                return getGamedirModPath();//Path.Combine(ModManager.gamedirModsPath!, Path.GetFileNameWithoutExtension(Name), Name);
            }
            if (WorkshopId != -1)
            {
                return getWorkshopModPath();//Path.Combine(ModManager.workshopModsPath!, WorkshopId.ToString(), Name);
            }
            CoreUtils.Print($"Error getting mod file path for {Name}",1);
            return null;
        }
        public string GetPatchTargetPath()
        {
            // Base game mods: patch in place
            if (IsBaseGame)
                return getModFilePath()!;

            // Already in game dir: patch in place
            if (InGameDir)
                return getGamedirModPath()!;

            // Workshop mod: must be copied first
            if (WorkshopId != -1)
            {
                string workshopFolder = Path.Combine(
                    ModManager.workshopModsPath!,
                    WorkshopId.ToString()
                );

                string gameDirFolder = Path.Combine(
                    ModManager.gamedirModsPath!,
                    Path.GetFileNameWithoutExtension(Name)
                );

                string targetModPath = Path.Combine(gameDirFolder, Name);

                // Copy only if not already present
                if (!Directory.Exists(gameDirFolder))
                {
                    CoreUtils.Print($"Copying workshop mod '{Name}' to game dir");
                    CoreUtils.CopyDirectory(workshopFolder, gameDirFolder);
                    InGameDir = true;
                }

                return targetModPath;
            }

            throw new InvalidOperationException($"Cannot determine patch target for mod '{Name}'");
        }
    }
}
