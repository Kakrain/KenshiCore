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
            return Path.Combine(Path.GetDirectoryName(getModFilePath()), Path.GetFileNameWithoutExtension(Name) + ".backup");

            //return Path.Combine(ModManager.gamedirModsPath!, Path.GetFileNameWithoutExtension(Name), Path.GetFileNameWithoutExtension(Name)+".backup");
        }
        public string getDictFilePath()
        {
           return Path.Combine(Path.GetDirectoryName(getModFilePath()), Path.GetFileNameWithoutExtension(Name) + ".dict");
           // getModFilePath();gfdr
           //return Path.Combine(ModManager.gamedirModsPath!, Path.GetFileNameWithoutExtension(Name), Path.GetFileNameWithoutExtension(Name) + ".dict");
        }
        public string? getModFilePath()
        {
            if (InGameDir)
            {
                return Path.Combine(ModManager.gamedirModsPath!, Path.GetFileNameWithoutExtension(Name), Name);
            }
            if (WorkshopId != -1)
            {
                return Path.Combine(ModManager.workshopModsPath!, WorkshopId.ToString(), Name);
            }
            Debug.WriteLine($"Error getting mod file path for {Name}");
            return null;
        }
    }
}
