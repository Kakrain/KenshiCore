using KenshiCore.Mods;
using KenshiCore.ReverseEngineering;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace KenshiCore.UI
{
    public class ModFormatter
    {

        public List<(string Text, Color Color)> ValidateAllDataAsBlocks(ModData data)
        {
            var blocks = new List<(string, Color)>();

            if (data.Header != null)
            {
                int filetype = data.Header.FileType;
                blocks.Add(($"--- FOLLOWING NON CONFORMING: v{filetype} ---", Color.Yellow));
                foreach (var rec in data.GetRecords())
                {
                    if (!rec.ValidateChangeTypeAssumptions(filetype))
                        blocks.Add(($"--- RECORD: {rec.Name} {rec.StringId} ({rec.getRecordType()}) ({rec.getChangeType()})---", Color.Red));
                    if (!rec.ValidateDataTypeAssumptions())
                        blocks.Add(($"--- RECORD: {rec.Name} {rec.StringId} ({rec.RecordType}) ({rec.getRecordType()})---", Color.Red));
                }
            }
            return blocks;
        }

        public List<(string Text, Color Color)> GetHeaderAsBlocks(ModData data,string? recordTypeFilter = null, List<string>? fieldFilter = null)
        {
            var blocks = new List<(string, Color)>();

            // Header
            if (data.Header != null)
            {
                blocks.Add(("--- MOD HEADER ---", Color.LightBlue));
                blocks.Add(($"FileType: {data.Header.FileType}", Color.Gray));
                blocks.Add(($"ModVersion: {data.Header.ModVersion}", Color.Gray));
                if (!string.IsNullOrEmpty(data.Header.Author))
                    blocks.Add(($"Author: {data.Header.Author}", Color.LightGreen));
                if (!string.IsNullOrEmpty(data.Header.Description))
                    blocks.Add(($"Description: {data.Header.Description}", Color.LightGreen));
                if (!string.IsNullOrEmpty(data.Header.Dependencies))
                    blocks.Add(($"Dependencies: {data.Header.Dependencies}", Color.LightCyan));
                if (!string.IsNullOrEmpty(data.Header.References))
                    blocks.Add(($"References: {data.Header.References}", Color.LightCyan));
                blocks.Add(($"RecordCount: {data.Count}", Color.Gray));
            }
            return blocks;
        }
        public string GetHeaderAsString(ModData data)
        {
            StringBuilder sb = new StringBuilder();

            // Header
            if (data.Header != null)
            {
                sb.AppendLine(("--- MOD HEADER ---"));
                sb.AppendLine(($"FileType: {data.Header.FileType}"));
                sb.AppendLine(($"ModVersion: {data.Header.ModVersion}"));
                if (!string.IsNullOrEmpty(data.Header.Author))
                    sb.AppendLine(($"Author: {data.Header.Author}"));
                if (!string.IsNullOrEmpty(data.Header.Description))
                    sb.AppendLine(($"Description: {data.Header.Description}"));
                if (!string.IsNullOrEmpty(data.Header.Dependencies))
                    sb.AppendLine(($"Dependencies: {data.Header.Dependencies}"));
                if (!string.IsNullOrEmpty(data.Header.References))
                    sb.AppendLine(($"References: {data.Header.References}"));
                sb.AppendLine(($"RecordCount: {data.Count}"));
            }
            return sb.ToString();
        }
        public List<(string Text, Color Color)> GetRecordsAsBlocks(ModData data,string? recordTypeFilter = null, List<string>? fieldFilter = null)
        {
            int typeCompare = ModRecord.getRecordTypeInt(recordTypeFilter ?? "");
            var blocks = new List<(string, Color)>();
            foreach (var rec in data.GetRecords())
            {
                if (typeCompare != -1 && rec.RecordType != typeCompare)
                    continue;
                blocks.AddRange(getDataAsBlock(rec,fieldFilter));
            }
            return blocks;
        }


        public string GetRecordNamesAsString(ModData data,string? recordTypeFilter = null, List<string>? fieldFilter = null)
        {
            int typeCompare = ModRecord.getRecordTypeInt(recordTypeFilter ?? "");
            StringBuilder sb = new StringBuilder();
            foreach (var rec in data.GetRecords())
            {
                //if (recordTypeFilter != null && !rec.getRecordType().Equals(recordTypeFilter, StringComparison.Ordinal))
                if (typeCompare != -1 && rec.RecordType != typeCompare)
                    continue;
                sb.AppendLine($"--- RECORD: {rec.Name} ({rec.getRecordType()}) ---");
                sb.AppendLine($"ID: {rec.Id}, StringID: {rec.StringId}, ChangeType: {rec.getChangeType()}");
            }
            return sb.ToString();
        }
        public string GetRecordsAsString(ModData data, string? recordTypeFilter = null, List<string>? fieldFilter = null)
        {
            int typeCompare = ModRecord.getRecordTypeInt(recordTypeFilter ?? "");
            StringBuilder sb = new();
            foreach (var rec in data.GetRecords())
            {
                if (typeCompare != -1 && rec.RecordType != typeCompare)
                    continue;
                sb.Append(getDataAsString(rec,fieldFilter));
            }
            return sb.ToString();
        }

        public List<(string, Color)> getNameOnlyAsBlock(ModRecord record)
        {
            var blocks = new List<(string, Color)>();
            blocks.Add(($"--- RECORD: {record.Name} ({record.getRecordType()}) ---", Color.Orange));
            blocks.Add(($"ID: {record.Id}, StringID: {record.StringId}, ChangeType: {record.getChangeType()}", Color.Gray));
            return blocks;
        }
        public List<(string, Color)> getDataAsBlock(ModRecord record,List<string>? fieldFilter = null)
        {
            var blocks = new List<(string, Color)>();
            // Record header
            blocks.Add(($"--- RECORD: {record.Name} ({record.getRecordType()}) ---", Color.Orange));
            blocks.Add(($"ID: {record.Id}, StringID: {record.StringId}, ChangeType: {record.getChangeType()}", Color.Gray));

            bool filterActive = fieldFilter != null && fieldFilter.Count > 0;

            bool ShouldInclude(string fieldName) =>
            !filterActive || fieldFilter!.Any(f => f.Equals(fieldName, StringComparison.Ordinal));

            // Basic fields
            foreach (var kv in record.BoolFields ?? Enumerable.Empty<KeyValuePair<string, bool>>())
                if (ShouldInclude(kv.Key))
                    blocks.Add(($"Bool: {kv.Key} = {kv.Value}", Color.LightCyan));

            foreach (var kv in record.FloatFields ?? Enumerable.Empty<KeyValuePair<string, float>>())
                if (ShouldInclude(kv.Key))
                    blocks.Add(($"Float: {kv.Key} = {kv.Value}", Color.LightCyan));

            foreach (var kv in record.LongFields ?? Enumerable.Empty<KeyValuePair<string, int>>())
                if (ShouldInclude(kv.Key))
                    blocks.Add(($"Long: {kv.Key} = {kv.Value}", Color.LightCyan));

            foreach (var kv in record.StringFields ?? Enumerable.Empty<KeyValuePair<string, string>>())
                if (ShouldInclude(kv.Key))
                    blocks.Add(($"String: {kv.Key} = {kv.Value}", Color.LightYellow));

            foreach (var kv in record.FilenameFields ?? Enumerable.Empty<KeyValuePair<string, string>>())
                if (ShouldInclude(kv.Key))
                    blocks.Add(($"Filename: {kv.Key} = {kv.Value}", Color.LightPink));

            //if (fieldFilter != null)
            if(filterActive)
                return blocks;
            // ExtraData
            if (record.ExtraDataFields != null)
            {
                foreach (var cat in record.ExtraDataFields)
                {
                    blocks.Add(($"ExtraData Category: {cat.Key}", Color.LightSalmon));
                    foreach (var item in cat.Value)
                        blocks.Add(($"  {item.Key} = [{string.Join(",", item.Value)}]", Color.LightSalmon));
                }
            }

            // Instances
            if (record.InstanceFields != null)
            {
                foreach (var inst in record.InstanceFields)
                {
                    blocks.Add(($"Instance: Id={inst.Id}, Target={inst.Target}, Pos=({inst.Tx},{inst.Ty},{inst.Tz}), Rot=({inst.Rx},{inst.Ry},{inst.Rz},{inst.Rw})", Color.LightGray));
                    if (inst.States != null && inst.States.Count > 0)
                        blocks.Add(($"  States: {string.Join(",", inst.States)}", Color.LightGray));
                }
            }
            return blocks;
        }
        public string getDataAsString(ModRecord record , List<string>? fieldFilter = null)
        {
            StringBuilder sb = new StringBuilder();
            // Record header
            sb.AppendLine($"--- RECORD: {record.Name} ({record.getRecordType()}) ---");
            sb.AppendLine($"ID: {record.Id}, StringID: {record.StringId}, ChangeType: {record.getChangeType()}");

            bool filterActive = fieldFilter != null && fieldFilter.Count > 0;

            bool ShouldInclude(string fieldName) =>
            !filterActive || fieldFilter!.Any(f => f.Equals(fieldName, StringComparison.Ordinal));

            // Basic fields
            foreach (var kv in record.BoolFields ?? Enumerable.Empty<KeyValuePair<string, bool>>())
                if (ShouldInclude(kv.Key))
                    sb.AppendLine($"Bool: {kv.Key} = {kv.Value}");

            foreach (var kv in record.FloatFields ?? Enumerable.Empty<KeyValuePair<string, float>>())
                if (ShouldInclude(kv.Key))
                    sb.AppendLine($"Float: {kv.Key} = {kv.Value}");

            foreach (var kv in record.LongFields ?? Enumerable.Empty<KeyValuePair<string, int>>())
                if (ShouldInclude(kv.Key))
                    sb.AppendLine($"Long: {kv.Key} = {kv.Value}");

            foreach (var kv in record.StringFields ?? Enumerable.Empty<KeyValuePair<string, string>>())
                if (ShouldInclude(kv.Key))
                    sb.AppendLine($"String: {kv.Key} = {kv.Value}");

            foreach (var kv in record.FilenameFields ?? Enumerable.Empty<KeyValuePair<string, string>>())
                if (ShouldInclude(kv.Key))
                    sb.AppendLine($"Filename: {kv.Key} = {kv.Value}");

            if (fieldFilter != null)
                return sb.ToString();
            // ExtraData
            if (record.ExtraDataFields != null)
            {
                foreach (var cat in record.ExtraDataFields)
                {
                    sb.AppendLine($"ExtraData Category: {cat.Key}");
                    foreach (var item in cat.Value)
                        sb.AppendLine($"  {item.Key} = [{string.Join(",", item.Value)}]");
                }
            }

            // Instances
            if (record.InstanceFields != null)
            {
                foreach (var inst in record.InstanceFields)
                {
                    sb.AppendLine($"Instance: Id={inst.Id}, Target={inst.Target}, Pos=({inst.Tx},{inst.Ty},{inst.Tz}), Rot=({inst.Rx},{inst.Ry},{inst.Rz},{inst.Rw})");
                    if (inst.States != null && inst.States.Count > 0)
                        sb.AppendLine($"  States: {string.Join(",", inst.States)}");
                }
            }
            return sb.ToString();
        }
    }
}
