using KenshiCore.Mods;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KenshiCore.ReverseEngineering
{
    public class ModTemplate
    {
        private readonly ModData _template;
        private static string ConfigPath =>Path.Combine(AppContext.BaseDirectory, "templates", "TEMPLATES");

        public ModTemplate()
        {
            var reader = new ModReader();

            if (!reader.Read(ConfigPath))
                throw new InvalidDataException($"Failed to load template: {ConfigPath}");

            _template = reader.data
                ?? throw new InvalidDataException($"Template contains no data: {ConfigPath}");
        }

        public ModRecord? FindRecord(int recordType)
        {
            return _template.GetRecordsByType(recordType).ElementAtOrDefault(0);
        }

        public ModRecord? GetWhole(ModRecord partial,int filetype)
        {
            var template = FindRecord(partial.getRecordTypeCode());
            if (template == null)
                return null;
            var result = partial.deepClone();

            result.SetAsNew(filetype);

            result.CompleteFieldsFrom(template);

            return result;
        }
    }
}
