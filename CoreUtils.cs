using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace KenshiCore
{
    public static class CoreUtils
    {
        
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
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsCompleteModFilename(string filename)
        {
            // A complete mod filename should end with .mod or .base
            // and have at least one character before the extension
            if (filename.EndsWith(".mod", StringComparison.OrdinalIgnoreCase) ||
                filename.EndsWith(".base", StringComparison.OrdinalIgnoreCase))
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
            if (!mods.Any(d => d.Equals(modName, StringComparison.OrdinalIgnoreCase)))
                mods.Add(modName);
            if (modName == ".base")
                MessageBox.Show("found .base");
            
            return string.Join(",", mods);
        }
    }

}