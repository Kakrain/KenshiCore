
using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace KenshiCore
{
    public class ReverseEngineer
    {
        public static int DELETED = 2147483647;
        public ModData modData;
        public ReverseEngineer()
        {
            modData = new ModData();
        }
        public string modname = "";
        private readonly Dictionary<int, List<ModRecord>> _recordsByType = new();
        public int ReadInt(BinaryReader reader) => reader.ReadInt32();
        public float ReadFloat(BinaryReader reader) => reader.ReadSingle();
        public bool ReadBool(BinaryReader reader) => reader.ReadBoolean();
        private const int NEW_V16 = unchecked((int)0x80000002u);
        private const int NEW_V17 = 0x00000020;
        public string ReadString(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            byte[] bytes = reader.ReadBytes(length);
            return Encoding.UTF8.GetString(bytes);
        }
        public List<string> GetModsNewRecords()
        {
            if (modData?.Records == null)
                return new List<string>();
            return modData.Records
                .Where(r => r.isNew())
                .Select(r => r.GetModName())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
        public void WriteInt(BinaryWriter writer, int v) => writer.Write(v);
        public void WriteFloat(BinaryWriter writer, float v) => writer.Write(v);
        public void WriteBool(BinaryWriter writer, bool v) => writer.Write(v);
        public List<string> getDependencies()
        {
            return CoreUtils.SplitModList(this.modData.Header?.Dependencies);
        }
        public void addDependencies(List<string> deps)
        {
            foreach (string d in deps)
            {
                this.modData.Header!.AddDependency(d);
            }
        }
        public void addReferences(List<string> refs)
        {
            foreach (string d in refs)
            {
                this.modData.Header!.AddReference(d);
            }
        }
        public void WriteString(BinaryWriter writer, string v)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(v);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
        public Dictionary<string, T> ReadDictionary<T>(BinaryReader reader, Func<BinaryReader, T> readValue)
        {
            int count = reader.ReadInt32();
            var dict = new Dictionary<string, T>();
            for (int i = 0; i < count; i++)
            {
                string key = ReadString(reader);
                dict[key] = readValue(reader);
            }
            return dict;
        }
        public void WriteDictionary<T>(BinaryWriter writer, Dictionary<string, T> dict, Action<BinaryWriter, T> writeValue)
        {
            writer.Write(dict.Count());
            foreach (var kv in dict)
            {
                WriteString(writer, kv.Key);
                writeValue(writer, kv.Value);
            }
        }
        public void LoadModFile(string path)
        {
            //modname
            modData = new ModData();
            using var fs = File.OpenRead(path);
            using var reader = new BinaryReader(fs, Encoding.UTF8);

            string fileName = Path.GetFileName(path);
            string extension = Path.GetExtension(fileName).ToLowerInvariant();

            if (extension != ".mod" && extension != ".base")
            {
                fileName = Path.GetFileNameWithoutExtension(fileName) + ".mod";
            }

            // Store canonical mod name (used for StringID creation etc.)
            this.modname = fileName;

            modData.Header = ParseHeader(reader);
            int recordCount = modData.Header.RecordCount;
            modData.Records = new List<ModRecord>();
            for (int i = 0; i < recordCount; i++)
            {
                modData.Records.Add(ParseRecord(reader));
            }
            TryParseDetails(modData.Header);
            long leftover = fs.Length - fs.Position;
            if (leftover > 0)
            {
                modData.Leftover = reader.ReadBytes((int)leftover);
                Console.WriteLine($"⚠ Warning: {leftover} leftover bytes detected.");
            }
        }
        public static int readJustVersion(string path)
        {
            using var fs = File.OpenRead(path);
            using var reader = new BinaryReader(fs, Encoding.UTF8);
            int filetype = reader.ReadInt32();
            if (filetype == 16)
                return reader.ReadInt32();
            if (filetype == 17)
            {
                reader.ReadInt32();
                return reader.ReadInt32();
            }
            throw new Exception($"Unexpected filetype: {filetype}");
        }
        public void enforceSanity()
        {
            this.modData.Header!.RecordCount = this.modData.Records!.Count();
        }
        public void SaveModFile(string path)
        {
            enforceSanity();
            using var fs = File.Create(path);// OpenWrite
            using var writer = new BinaryWriter(fs, Encoding.UTF8);
            modData.Header!.Details = BuildDetails(modData.Header!);
            modData.Header.DetailsLength = modData.Header.Details!.Length;

            WriteHeader(writer, modData.Header!);
            foreach (var record in modData.Records!)
                WriteRecord(writer, record);

            if (modData.Leftover != null)
                writer.Write(modData.Leftover);
        }
        public void TryParseDetails(ModHeader header)
        {
            if (header.FileType != 17 || header.Details == null)
                return;

            using var ms = new MemoryStream(header.Details);
            using var reader = new BinaryReader(ms, Encoding.UTF8);

            long lastGoodPos = ms.Position;

            T? TryRead<T>(Func<BinaryReader, T> func, out bool success)
            {
                long startPos = ms.Position;
                try
                {
                    var val = func(reader);
                    success = true;
                    lastGoodPos = ms.Position; // update last successfully read position
                    return val;
                }
                catch
                {
                    ms.Position = startPos;
                    success = false;
                    return default;
                }
            }

            bool ok;
            if (ms.Position < ms.Length)
                header.Author = TryRead(ReadString, out ok) ?? null;
            if (ms.Position < ms.Length)
                header.Description = TryRead(ReadString, out ok) ?? null;
            if (ms.Position < ms.Length)
                header.Dependencies = TryRead(ReadString, out ok) ?? null;
            if (ms.Position < ms.Length)
                header.References = TryRead(ReadString, out ok) ?? null;
            // Optional fields
            if (ms.Position < ms.Length)
                header.SaveCount = TryRead(r => r.ReadUInt32(), out ok);
            if (ms.Position < ms.Length)
                header.LastMerge = TryRead(r => r.ReadUInt32(), out ok);
            if (ms.Position < ms.Length)
                header.MergeEntries = TryRead(ReadMergeEntries, out ok) ?? null;
            if (ms.Position < ms.Length)
                header.DeleteRequests = TryRead(ReadDeleteRequests, out ok) ?? null;

            // Keep remaining bytes
            if (lastGoodPos != ms.Length)
                header.UnparsedDetails = ms.ToArray()[(int)lastGoodPos..];
        }
        public List<ModRecord> GetRecordsByTypeMUTABLE(string recordType)
        {
            if (!ModRecord.ModTypeNames.TryGetValue(recordType, out int code))
                throw new FormatException($"Invalid patch definition format: '{recordType}' is not a valid Record Type");
            return GetRecordsByTypeMUTABLE(code);

        }
        public List<ModRecord> GetRecordsByTypeMUTABLE(int recordType)
        {
            return modData.Records!.Where(r => r.RecordType == recordType).ToList();
        }
        public List<ModRecord> GetRecordsByTypeINMUTABLE(int recordType)
        {
            List<ModRecord>? result;
            if (!_recordsByType.TryGetValue(recordType, out result))
            {
                result = modData.Records!.Where(r => r.RecordType == recordType).ToList();
                _recordsByType[recordType] = result;
            }
            return result;
        }
        public List<ModRecord> GetRecordsByTypeINMUTABLE(string recordType)
        {
            if (!ModRecord.ModTypeNames.TryGetValue(recordType, out int code))
                throw new FormatException($"Invalid patch definition format: '{recordType}' is not a valid Record Type");
            return GetRecordsByTypeINMUTABLE(code);
        }
        public byte[] BuildDetails(ModHeader header)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, Encoding.UTF8);
            // Always write the "known" fields
            if (header.Author != null)
                WriteString(writer, header.Author);
            if (header.Description != null)
                WriteString(writer, header.Description);
            if (header.Dependencies != null)
            {
                //header.Dependencies = maxdeps;
                WriteString(writer, header.Dependencies);
            }
            if (header.References != null)
                WriteString(writer, header.References);
            if (header.SaveCount != null)
                writer.Write(header.SaveCount.Value);
            if (header.LastMerge != null)
                writer.Write(header.LastMerge.Value);
            if (header.MergeEntries != null)
                WriteMergeEntries(writer, header.MergeEntries);
            if (header.DeleteRequests != null)
                WriteDeleteRequests(writer, header.DeleteRequests);
            if (header.UnparsedDetails != null && header.UnparsedDetails.Length > 0)
            {
                writer.Write(header.UnparsedDetails);
            }
            return ms.ToArray();
        }
        private Dictionary<string, MergeEntry> ReadMergeEntries(BinaryReader reader)
        {
            byte count = reader.ReadByte();
            var dict = new Dictionary<string, MergeEntry>(count);
            for (int i = 0; i < count; i++)
            {
                string key = ReadString(reader);
                dict[key] = new MergeEntry(reader.ReadUInt32(), reader.ReadUInt32());
            }
            return dict;
        }
        public List<(string Text, Color Color)> ValidateAllDataAsBlocks()
        {
            var blocks = new List<(string, Color)>();

            if (modData.Header != null)
            {

                int filetype = modData.Header.FileType;
                blocks.Add(($"--- FOLLOWING NON CONFORMING: v{filetype} ---", Color.Yellow));
                if (modData.Records != null)
                {
                    foreach (var rec in modData.Records)
                    {
                        if (!rec.ValidateChangeTypeAssumptions(filetype))
                            blocks.Add(($"--- RECORD: {rec.Name} {rec.StringId} ({rec.getRecordType()}) ({rec.getChangeType()})---", Color.Red));
                        if (!rec.ValidateDataTypeAssumptions())
                            blocks.Add(($"--- RECORD: {rec.Name} {rec.StringId} ({rec.RecordType}) ({rec.getRecordType()})---", Color.Red));
                    }
                }
            }
            return blocks;
        }
        public List<(string Text, Color Color)> GetHeaderAsBlocks(string? recordTypeFilter = null, List<string>? fieldFilter = null)
        {
            var blocks = new List<(string, Color)>();

            // Header
            if (modData.Header != null)
            {
                blocks.Add(("--- MOD HEADER ---", Color.LightBlue));
                blocks.Add(($"FileType: {modData.Header.FileType}", Color.Gray));
                blocks.Add(($"ModVersion: {modData.Header.ModVersion}", Color.Gray));
                if (!string.IsNullOrEmpty(modData.Header.Author))
                    blocks.Add(($"Author: {modData.Header.Author}", Color.LightGreen));
                if (!string.IsNullOrEmpty(modData.Header.Description))
                    blocks.Add(($"Description: {modData.Header.Description}", Color.LightGreen));
                if (!string.IsNullOrEmpty(modData.Header.Dependencies))
                    blocks.Add(($"Dependencies: {modData.Header.Dependencies}", Color.LightCyan));
                if (!string.IsNullOrEmpty(modData.Header.References))
                    blocks.Add(($"References: {modData.Header.References}", Color.LightCyan));
                blocks.Add(($"RecordCount: {modData.Header.RecordCount}", Color.Gray));
            }
            return blocks;
        }
        public string GetHeaderAsString()
        {
            StringBuilder sb = new StringBuilder();

            // Header
            if (modData.Header != null)
            {
                sb.AppendLine(("--- MOD HEADER ---"));
                sb.AppendLine(($"FileType: {modData.Header.FileType}"));
                sb.AppendLine(($"ModVersion: {modData.Header.ModVersion}"));
                if (!string.IsNullOrEmpty(modData.Header.Author))
                    sb.AppendLine(($"Author: {modData.Header.Author}"));
                if (!string.IsNullOrEmpty(modData.Header.Description))
                    sb.AppendLine(($"Description: {modData.Header.Description}"));
                if (!string.IsNullOrEmpty(modData.Header.Dependencies))
                    sb.AppendLine(($"Dependencies: {modData.Header.Dependencies}"));
                if (!string.IsNullOrEmpty(modData.Header.References))
                    sb.AppendLine(($"References: {modData.Header.References}"));
                sb.AppendLine(($"RecordCount: {modData.Header.RecordCount}"));
            }
            return sb.ToString();
        }
        public List<(string Text, Color Color)> GetRecordsAsBlocks(string? recordTypeFilter = null, List<string>? fieldFilter = null)
        {
            var blocks = new List<(string, Color)>();
            // Records
            if (modData.Records != null)
            {
                foreach (var rec in modData.Records)
                {
                    if (recordTypeFilter != null &&
                    !rec.getRecordType().Equals(recordTypeFilter, StringComparison.Ordinal))
                        continue;
                    blocks.AddRange(rec.getDataAsBlock(fieldFilter));
                }
            }
            return blocks;
        }
        public string GetRecordsAsString(string? recordTypeFilter = null, List<string>? fieldFilter = null)
        {
            StringBuilder sb = new StringBuilder();
            if (modData.Records != null)
            {
                foreach (var rec in modData.Records)
                {
                    if (recordTypeFilter != null &&
                    !rec.getRecordType().Equals(recordTypeFilter, StringComparison.Ordinal))
                        continue;
                    sb.Append(rec.getDataAsString(fieldFilter));
                }
            }
            return sb.ToString();
        }
        public List<(string Text, Color Color)> CompareWith(ReverseEngineer other, string? recordTypeFilter = null, List<string>? fieldFilter = null)
        {
            var blocks = new List<(string, Color)>();

            if (this.modData?.Records == null || other.modData?.Records == null)
                return blocks;

            // Build quick lookup by record name (case-insensitive).
            // You can swap to StringId if you prefer a different matching key.
            var mineByName = this.modData.Records
                .Where(r => recordTypeFilter == null || r.getRecordType().Equals(recordTypeFilter, StringComparison.Ordinal))
                .ToDictionary(r => r.Name ?? "", StringComparer.Ordinal);

            var otherByName = other.modData.Records
                .Where(r => recordTypeFilter == null || r.getRecordType().Equals(recordTypeFilter, StringComparison.Ordinal))
                .ToDictionary(r => r.Name ?? "", StringComparer.Ordinal);

            // Intersection: only records present in both
            var commonNames = mineByName.Keys.Intersect(otherByName.Keys, StringComparer.Ordinal);
            double maxdif = -999;
            double mindif = 999;
            double avdif = 0;
            double num = 0;
            foreach (var name in commonNames.OrderBy(n => n, StringComparer.Ordinal))
            {
                var mine = mineByName[name];
                var theirs = otherByName[name];

                // Header for this record
                blocks.Add(($"--- RECORD: {mine.Name} ({mine.getRecordType()}) ---", Color.Orange));
                blocks.Add(($"This:   StringID: {mine.StringId}  ChangeType: {mine.getChangeType()}", Color.Gray));
                blocks.Add(($"Other:  StringID: {theirs.StringId}  ChangeType: {theirs.getChangeType()}", Color.Gray));

                // collect all field names present in either record
                // Assume ModRecord exposes GetAllFieldNames() -> IEnumerable<string>
                var mineFields = new HashSet<string>(mine.GetAllFieldNames() ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
                var theirFields = new HashSet<string>(theirs.GetAllFieldNames() ?? Enumerable.Empty<string>(), StringComparer.Ordinal);

                var allFields = mineFields.Union(theirFields, StringComparer.Ordinal);

                // If a fieldFilter was provided, restrict to it
                if (fieldFilter != null && fieldFilter.Count > 0)
                {
                    var filterSet = new HashSet<string>(fieldFilter, StringComparer.Ordinal);
                    allFields = allFields.Where(f => filterSet.Contains(f)).ToList();
                }

                foreach (var field in allFields.OrderBy(f => f, StringComparer.Ordinal))
                {
                    bool hasMine = mineFields.Contains(field);
                    bool hasTheirs = theirFields.Contains(field);

                    object? valMine = hasMine ? mine.GetFieldAsObject(field) : null;
                    object? valTheirs = hasTheirs ? theirs.GetFieldAsObject(field) : null;

                    string svalMine = FormatFieldValueForDisplay(valMine);
                    string svalTheirs = FormatFieldValueForDisplay(valTheirs);

                    if (hasMine && hasTheirs)
                    {
                        // both present: show old -> new (other -> this)
                        string text = $"{field}: {svalTheirs}  →  {svalMine}";
                        Color color = AreFieldValuesEqual(valMine, valTheirs) ? Color.Gray : Color.LightGreen;
                        blocks.Add((text, color));
                        if (IsNumericType(valMine!))
                        {
                            double a = Convert.ToDouble(valMine, CultureInfo.InvariantCulture);
                            double b = Convert.ToDouble(valTheirs, CultureInfo.InvariantCulture);
                            double dif = Math.Abs(a - b);
                            if (dif > maxdif)
                                maxdif = dif;
                            if (dif < mindif)
                                mindif = dif;
                            avdif += dif;
                            num++;
                        }
                    }
                    else if (hasTheirs) // only in other (base)
                    {
                        string text = $"{field}: {svalTheirs}  (base only)";
                        blocks.Add((text, Color.LightBlue));
                    }
                    else // only in this (patch)
                    {
                        string text = $"{field}: {svalMine}  (patch only)";
                        blocks.Add((text, Color.LightYellow));
                    }
                }

                // spacer line
                blocks.Add(("", Color.Transparent));
            }
            if (num > 0)
            {
                avdif /= num;
                blocks.Add(($"Numeric field differences summary: max={maxdif}, min={mindif}, avg={avdif:F6} over {num} fields", Color.Purple));
            }

            return blocks;
        }
        private static string FormatFieldValueForDisplay(object? v)
        {
            if (v == null) return "<missing>";
            if (v is float f) return f.ToString(CultureInfo.InvariantCulture);
            if (v is double d) return d.ToString(CultureInfo.InvariantCulture);
            if (v is int i) return i.ToString(CultureInfo.InvariantCulture);
            if (v is long l) return l.ToString(CultureInfo.InvariantCulture);
            if (v is bool b) return b ? "true" : "false";
            if (v is string s) return $"\"{s}\"";
            if (v is IEnumerable<object> list) return $"[{string.Join(", ", list)}]";
            if (v is float[] fa) return $"[{string.Join(", ", fa.Select(x => x.ToString(CultureInfo.InvariantCulture)))}]";
            return v.ToString() ?? "<null>";
        }
        private static bool AreFieldValuesEqual(object? a, object? b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;

            // numeric tolerance for floats/doubles
            if (IsNumericType(a) && IsNumericType(b))
            {
                double da = Convert.ToDouble(a, CultureInfo.InvariantCulture);
                double db = Convert.ToDouble(b, CultureInfo.InvariantCulture);
                return Math.Abs(da - db) <= 1e-6 * Math.Max(1.0, Math.Max(Math.Abs(da), Math.Abs(db)));
            }

            return string.Equals(a.ToString(), b.ToString(), StringComparison.Ordinal);
        }
        public static bool IsNumericType(object o)
        {
            return o is byte || o is sbyte || o is short || o is ushort ||
                   o is int || o is uint || o is long || o is ulong ||
                   o is float || o is double || o is decimal;
        }
        private void WriteMergeEntries(BinaryWriter writer, Dictionary<string, MergeEntry> entries)
        {
            writer.Write((byte)entries.Count);
            foreach (var kv in entries)
            {
                WriteString(writer, kv.Key);
                writer.Write(kv.Value.SaveCount);
                writer.Write(kv.Value.LastMerge);
            }
        }

        private Dictionary<string, DeleteRequest> ReadDeleteRequests(BinaryReader reader)
        {
            byte count = reader.ReadByte();
            var dict = new Dictionary<string, DeleteRequest>(count);
            for (int i = 0; i < count; i++)
            {
                string key = ReadString(reader);
                dict[key] = new DeleteRequest(reader.ReadUInt32(), ReadString(reader));
            }
            return dict;
        }

        private void WriteDeleteRequests(BinaryWriter writer, Dictionary<string, DeleteRequest> requests)
        {
            writer.Write((byte)requests.Count);
            foreach (var kv in requests)
            {
                WriteString(writer, kv.Key);
                writer.Write(kv.Value.saveCount);
                WriteString(writer, kv.Value.Target);
            }
        }
        private ModHeader ParseHeader(BinaryReader reader)
        {
            var header = new ModHeader();
            header.FileType = ReadInt(reader);
            switch (header.FileType)
            {
                case 16:
                    header.ModVersion = ReadInt(reader);
                    header.Author = ReadString(reader);
                    header.Description = ReadString(reader);
                    header.Dependencies = ReadString(reader);
                    header.References = ReadString(reader);
                    header.UnknownInt = ReadInt(reader);
                    header.RecordCount = ReadInt(reader);
                    break;
                case 17:
                    header.DetailsLength = ReadInt(reader);
                    header.ModVersion = ReadInt(reader);
                    header.Details = reader.ReadBytes(header.DetailsLength);
                    header.RecordCount = ReadInt(reader);
                    break;
                default:
                    throw new Exception($"Unexpected filetype: {header.FileType}");
            }
            return header;
        }
        private void WriteHeader(BinaryWriter writer, ModHeader header)
        {
            WriteInt(writer, header.FileType);
            switch (header.FileType)
            {
                case 16:
                    WriteInt(writer, header.ModVersion);
                    WriteString(writer, header.Author!);
                    WriteString(writer, header.Description!);
                    WriteString(writer, header.Dependencies!);
                    WriteString(writer, header.References!);
                    WriteInt(writer, header.UnknownInt);
                    //WriteInt(writer, header.RecordCount);
                    WriteInt(writer, this.modData.Records == null ? 0 : this.modData.Records.Count);
                    break;
                case 17:
                    WriteInt(writer, header.DetailsLength);
                    WriteInt(writer, header.ModVersion);
                    writer.Write(header.Details!);
                    //if (header.RecordCount != this.modData.Records.Count)
                    //    CoreUtils.Print($"Writing record: {header.RecordCount} {this.modData.Records.Count})", 1);
                    //WriteInt(writer, header.RecordCount);
                    WriteInt(writer, this.modData.Records == null ? 0 : this.modData.Records.Count);
                    break;
            }
        }
        private ModRecord ParseRecord(BinaryReader reader)
        {
            var record = new ModRecord();
            record.InstanceCount = ReadInt(reader);
            record.RecordType = ReadInt(reader);//BUILDING,GAMESTATE_FACTION,etc
            record.Id = ReadInt(reader);
            record.Name = ReadString(reader);
            record.StringId = ReadString(reader);
            record.ChangeType = ReadInt(reader);//-2147483646 means new,-2147483647 means changed,-2147483645 changed and name was changed

            record.BoolFields = ReadDictionary(reader, ReadBool); //if removed by a mod just one bool field: "REMOVED": True
            record.FloatFields = ReadDictionary(reader, ReadFloat);
            record.LongFields = ReadDictionary(reader, ReadInt);
            record.Vec3Fields = ReadDictionary(reader, r => new float[] { ReadFloat(r), ReadFloat(r), ReadFloat(r) });
            record.Vec4Fields = ReadDictionary(reader, r => new float[] { ReadFloat(r), ReadFloat(r), ReadFloat(r), ReadFloat(r) });
            record.StringFields = ReadDictionary(reader, ReadString);
            record.FilenameFields = ReadDictionary(reader, ReadString);

            record.ExtraDataFields = new Dictionary<string, Dictionary<string, int[]>>();
            int extraCatCount = ReadInt(reader);
            for (int i = 0; i < extraCatCount; i++)
            {
                string catName = ReadString(reader);
                int itemCount = ReadInt(reader);
                var catValue = new Dictionary<string, int[]>();
                for (int j = 0; j < itemCount; j++)
                {
                    string itemName = ReadString(reader);
                    int[] values = new int[3] { ReadInt(reader), ReadInt(reader), ReadInt(reader) };
                    catValue[itemName] = values;
                }
                record.ExtraDataFields[catName] = catValue;
            }

            // Instance fields
            record.InstanceFields = new List<ModInstance>();
            int instanceCount2 = ReadInt(reader);
            for (int i = 0; i < instanceCount2; i++)
            {
                var inst = new ModInstance();
                inst.Id = ReadString(reader);
                inst.Target = ReadString(reader);
                inst.Tx = ReadFloat(reader);
                inst.Ty = ReadFloat(reader);
                inst.Tz = ReadFloat(reader);
                inst.Rw = ReadFloat(reader);
                inst.Rx = ReadFloat(reader);
                inst.Ry = ReadFloat(reader);
                inst.Rz = ReadFloat(reader);
                inst.StateCount = ReadInt(reader);
                inst.States = new List<string>();
                for (int j = 0; j < inst.StateCount; j++)
                    inst.States.Add(ReadString(reader));
                record.InstanceFields.Add(inst);
            }

            return record;
        }
        private void WriteRecord(BinaryWriter writer, ModRecord record)
        {
            //if (record.InstanceCount!= record.InstanceFields.Count())
            //CoreUtils.Print($"Writing record: {record.Name} ({record.StringId} {record.InstanceCount} {record.InstanceFields.Count()})", 0);
            //WriteInt(writer, record.InstanceCount);
            WriteInt(writer, record.InstanceFields.Count());
            WriteInt(writer, record.RecordType);
            WriteInt(writer, record.Id);
            WriteString(writer, record.Name);
            WriteString(writer, record.StringId);
            WriteInt(writer, record.ChangeType);

            WriteDictionary(writer, record.BoolFields, WriteBool);
            WriteDictionary(writer, record.FloatFields, WriteFloat);
            WriteDictionary(writer, record.LongFields, WriteInt);
            WriteDictionary(writer, record.Vec3Fields, (w, v) => { foreach (var f in v) WriteFloat(w, f); });
            WriteDictionary(writer, record.Vec4Fields, (w, v) => { foreach (var f in v) WriteFloat(w, f); });
            WriteDictionary(writer, record.StringFields, WriteString);
            WriteDictionary(writer, record.FilenameFields, WriteString);

            // Extra data
            WriteInt(writer, record.ExtraDataFields!.Count());
            //if (record.ExtraDataFields!.Count ==0)
            //CoreUtils.Print($"Writing record: {record.Name} ({record.StringId} {record.ExtraDataFields!.Count} {record.InstanceFields.Count})", 1);

            foreach (var kv in record.ExtraDataFields)
            {
                WriteString(writer, kv.Key);
                WriteInt(writer, kv.Value.Count());
                foreach (var kv2 in kv.Value)
                {
                    WriteString(writer, kv2.Key);
                    foreach (var val in kv2.Value)
                        WriteInt(writer, val);
                }
            }

            // Instance fields
            WriteInt(writer, record.InstanceFields!.Count());
            foreach (var inst in record.InstanceFields)
            {
                WriteString(writer, inst.Id!);
                WriteString(writer, inst.Target!);
                WriteFloat(writer, inst.Tx);
                WriteFloat(writer, inst.Ty);
                WriteFloat(writer, inst.Tz);
                WriteFloat(writer, inst.Rw);
                WriteFloat(writer, inst.Rx);
                WriteFloat(writer, inst.Ry);
                WriteFloat(writer, inst.Rz);
                WriteInt(writer, inst.StateCount);
                foreach (var s in inst.States!)
                    WriteString(writer, s);
            }
        }
        public void ApplyToStrings(Func<string, string> func)
        {
            if (modData.Header!.FileType == 16 && modData.Header.Description != null)
                modData.Header.Description = func(modData.Header.Description);

            foreach (var record in modData.Records!)
            {
                if (record.Name != null)
                    record.Name = func(record.Name);

                if (record.StringFields != null)
                {
                    var keys = new List<string>(record.StringFields.Keys);
                    foreach (var key in keys)
                        record.StringFields[key] = func(record.StringFields[key]);
                }
            }
        }
        private bool isAlphabet(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }
        public Tuple<string, string> getModSummary(int maxChars = 2000)//5000)
        {
            StringBuilder sba = new StringBuilder();
            StringBuilder sbs = new StringBuilder();
            foreach (var record in modData.Records!)
            {
                if (!string.IsNullOrEmpty(record.Name))
                    if (sba.Length <= maxChars)
                        sba.Append(",").Append(new String(record.Name.Where(c => (isAlphabet(c) || c == ' ')).ToArray()));
                if (sbs.Length <= maxChars)
                    sbs.Append(",").Append(new String(record.Name.Where(c => !(isAlphabet(c) || c == ' ')).ToArray()));
                if (record.StringFields != null)
                {
                    foreach (var value in record.StringFields.Values)
                    {
                        if (sba.Length <= maxChars)
                            sba.Append(",").Append(new String(value.Where(c => (isAlphabet(c) || c == ' ')).ToArray()));
                        if (sbs.Length <= maxChars)
                            sbs.Append(",").Append(new String(value.Where(c => !(isAlphabet(c) || c == ' ')).ToArray()));
                    }
                }
                if ((sba.Length >= maxChars) && (sbs.Length >= maxChars))
                    break;
            }
            return Tuple.Create(sba.ToString(), sbs.ToString());
        }
        //for debugging purposes.
        public void generateResaves(string start = "C:/AlternativProgramFiles/Steam/steamapps/common/Kenshi/mods/")
        {
            //string start = "C:/AlternativProgramFiles/Steam/steamapps/common/Kenshi/mods/";
            //string start = "C:/AlternativProgramFiles/Steam/steamapps/workshop/content/233860";

            foreach (string folder in Directory.GetDirectories(start))
            {
                System.Diagnostics.Debug.WriteLine(folder);
                foreach (string file in Directory.GetFiles(folder, "*.mod"))
                {
                    System.Diagnostics.Debug.WriteLine(file);
                    this.LoadModFile(file);
                    string resavedPath = Path.Combine(
                        Path.GetDirectoryName(file)!,
                        Path.GetFileNameWithoutExtension(file) + ".resaved"
                    );
                    this.SaveModFile(resavedPath);
                }
            }
        }
        public void testAll()
        {
            List<string> files = new List<string>();
            foreach (string folder in Directory.GetDirectories("C:/AlternativProgramFiles/Steam/steamapps/common/Kenshi/mods/"))
            {
                foreach (string file in Directory.GetFiles(folder, "*.mod"))
                {
                    files.Add(file);
                }
            }
            foreach (string folder in Directory.GetDirectories("C:/AlternativProgramFiles/Steam/steamapps/workshop/content/233860"))
            {
                foreach (string file in Directory.GetFiles(folder, "*.mod"))
                {
                    files.Add(file);
                }
            }
            foreach (string folder in Directory.GetDirectories("C:/AlternativProgramFiles/Steam/steamapps/common/Kenshi/data"))
            {
                foreach (string file in Directory.GetFiles(folder, "*.mod"))
                {
                    files.Add(file);
                }
            }
            foreach (string folder in Directory.GetDirectories("C:/AlternativProgramFiles/Steam/steamapps/common/Kenshi/data"))
            {
                foreach (string file in Directory.GetFiles(folder, "*.base"))
                {
                    files.Add(file);
                }
            }
            foreach (string file in files)
            {
                this.LoadModFile(file);
                List<ModRecord> recs = this.GetRecordsByTypeMUTABLE("SQUAD_TEMPLATE");
                foreach (ModRecord rec in recs)
                {
                    if (rec.isNew() && rec.ExtraDataFields != null)
                        foreach (var cat in rec.ExtraDataFields.Values)
                            foreach (var vals in cat.Values)
                                if (vals.Length != 3)
                                    CoreUtils.Print($"array tiene {vals.Length} elementos " + rec.ToString(), 0);
                }
            }
            CoreUtils.Print("termino", 1);
        }

        public ModRecord EnsureRecordExists(ModRecord target)
        {
            ModRecord? ownedtarget = searchModRecordByStringId(target.StringId);
            if (ownedtarget == null)
            {
                ownedtarget = new ModRecord();
                ownedtarget.Name = target.Name;
                ownedtarget.StringId = target.StringId;
                ownedtarget.RecordType = target.RecordType;
                ownedtarget.ChangeType = 0;//target.ChangeType;
                ownedtarget.SetRecordStatus(this.modData.Header!.FileType, "existing");
                ownedtarget.SetChangeCounter(2);//2 was too low
                this.modData.Records!.Add(ownedtarget);
            }
            return ownedtarget;
        }

        public void SetField(ModRecord target, string fieldname, string value)
        {
            ModRecord? ownedtarget = EnsureRecordExists(target);
            ownedtarget.EnsureFieldExist(target, fieldname);
            ownedtarget.SetField(fieldname, value);
        }
        public void ForceSetField(ModRecord target, string fieldname, string value, string valuetype)
        {
            ModRecord? ownedtarget = EnsureRecordExists(target);
            if (!ownedtarget.EnsureFieldExist(target, fieldname))
                ownedtarget.ForceEnsureFieldExist(fieldname, valuetype);
            ownedtarget.SetField(fieldname, value);
        }
        public void AddExtraData(ModRecord target, ModRecord source, string category, int[]? vars = null, bool force = false)
        {
            ModRecord? ownedtarget = EnsureRecordExists(target);
            if (ownedtarget.ExtraDataFields == null)
                ownedtarget.ExtraDataFields = new Dictionary<string, Dictionary<string, int[]>>();
            ownedtarget.ExtraDataFields!.TryGetValue(category, out var cat);
            target.ExtraDataFields!.TryGetValue(category, out var target_cat);
            if (!force && ExtraDataExists(cat, target_cat, source.StringId))
                return;
            if (cat == null)
            {
                cat = new Dictionary<string, int[]>();
                ownedtarget.ExtraDataFields.Add(category, cat);
            }
            if (vars == null)
                vars = new int[] { 0, 0, 0 };
            cat[source.StringId] = vars;
        }
        private static bool ExtraDataExists(
            Dictionary<string, int[]>? patchCat, Dictionary<string, int[]>? baseCat, string key)
        {
            if (patchCat != null && patchCat.TryGetValue(key, out var p))
                return !IsDeleted(p);

            if (baseCat != null && baseCat.TryGetValue(key, out var b))
                return !IsDeleted(b);

            return false;
        }
        public void deleteRecord(ModRecord record)
        {
            var owned = searchModRecordByStringId(record.StringId);
            if (owned != null)
            {
                modData.Records!.Remove(owned);
                if (owned.isNew())
                    return;
            }
            var todelete = EnsureRecordExists(record);
            todelete.BoolFields ??= new Dictionary<string, bool>();
            todelete.BoolFields["REMOVED"] = true;
        }
        private static bool IsDeleted(int[] vals)
        {
            return (vals[0] == DELETED) && (vals[1] == DELETED) && (vals[2] == DELETED);
        }
        public void EditExtraData(ModRecord target, string category, Func<int, int>[] transformers, Func<int[], bool>? isValid = null)
        {
            bool exist_at_beginning = searchModRecordByStringId(target.StringId) != null;
            ModRecord? ownedtarget = EnsureRecordExists(target);
            if (ownedtarget.ExtraDataFields == null)
                ownedtarget.ExtraDataFields = new Dictionary<string, Dictionary<string, int[]>>();
            ownedtarget.ExtraDataFields!.TryGetValue(category, out var cat);
            target.ExtraDataFields!.TryGetValue(category, out var target_cat);
            if (target_cat == null)
            {
                //
                this.modData.Records!.Remove(ownedtarget);
                return;
            }

            if (cat == null)
            {
                cat = new Dictionary<string, int[]>();
                ownedtarget.ExtraDataFields.Add(category, cat);
            }
            bool changed = false;
            foreach (string d in target_cat.Keys)
            {
                int[] original;
                if (cat.TryGetValue(d, out var patchValue))
                    original = patchValue;
                else
                    original = target_cat[d];
                if (isValid == null || isValid(original))
                {
                    changed = true;
                    ownedtarget.ExtraDataFields[category][d] = new int[] {
                    transformers[0](original[0]),
                    transformers[1](original[1]),
                    transformers[2](original[2]) };
                }
            }
            if (!changed && !exist_at_beginning)
            {
                //CoreUtils.Print($"Removing empty record {ownedtarget.StringId}", 1);
                this.modData.Records!.Remove(ownedtarget);
            }
        }
        public ModRecord? searchModRecordByStringId(string stringId)
        {
            return this.modData.Records!.Find(r => r.StringId == stringId);
        }
        public List<ModRecord> CloneRecord(ModRecord toclone, int n)
        {
            List<ModRecord> clones = new List<ModRecord>();
            for (int i = 0; i < n; i++)
            {
                ModRecord clone = toclone.deepClone();
                clone.StringId = $"{GetNextFreeStringIdNumber()}-{this.modname}";
                //clone.Name = $"{toclone.Name}_{i}";
                clone.ChangeType = this.modData.Header!.FileType == 16 ? NEW_V16 : NEW_V17;
                this.modData.Records!.Add(clone);
                clones.Add(clone);
            }
            return clones;
        }
        public ModRecord CreateNewRecord(int recordType, string name)
        {
            // Find next free number starting at 10
            int nextId = GetNextFreeStringIdNumber();

            // Build unique StringID like "10-test_animation_patcher.mod"
            string stringId = $"{nextId}-{this.modname}";

            // Create new record
            var newRecord = new ModRecord
            {
                Name = name,
                StringId = stringId,
                RecordType = recordType,
                ChangeType = this.modData.Header!.FileType == 16 ? NEW_V16 : NEW_V17,//ModRecord.ModTypeCodes.FirstOrDefault(kv => kv.Value == recordType).Key,
                Id = 0
            };
            this.modData.Records!.Add(newRecord);
            return newRecord;
        }
        public void EnsurePlaceholderExists(int recordType)
        {
            ModRecord.ModTypeCodes.TryGetValue(recordType, out string? recordTypeName);
            if (string.IsNullOrEmpty(recordTypeName))
                throw new FormatException($"record type code not found: {recordType}");
            // If already exists (same Name), do nothing
            if (this.GetRecordsByTypeMUTABLE(recordType)!.Any(r => string.Equals(r.Name, recordTypeName)))
                return;

            ModRecord record = CreateNewRecord(recordType, recordTypeName);
        }
        private int GetNextFreeStringIdNumber()
        {
            var usedNumbers = new HashSet<int>();
            var regex = new Regex(@"^(\d+)-");
            foreach (var record in this.modData.Records!)
            {
                if (string.IsNullOrEmpty(record.StringId)) continue;
                var match = regex.Match(record.StringId);
                if (match.Success && int.TryParse(match.Groups[1].Value, out int num) && (record.GetModName() == this.modname))
                    usedNumbers.Add(num);
            }
            int candidate = 10;
            while (usedNumbers.Contains(candidate))
                candidate++;
            return candidate;
        }
    }
    public class ModData
    {
        public ModHeader? Header { get; set; }
        public List<ModRecord>? Records { get; set; }
        public byte[]? Leftover { get; set; }
    }
    public class MergeEntry
    {
        public uint SaveCount { get; set; }
        public uint LastMerge { get; set; }

        public MergeEntry(uint saveCount, uint lastMerge)
        {
            SaveCount = saveCount;
            LastMerge = lastMerge;
        }
    }
    public class DeleteRequest
    {
        public uint saveCount { get; set; }
        public string Target { get; set; }

        public DeleteRequest(uint savecount, string target)
        {
            saveCount = savecount;
            Target = target;
        }
    }
    public class ModHeader
    {
        public int FileType { get; set; }
        public int ModVersion { get; set; }
        public string? Author { get; set; } = null;
        public string? Description { get; set; } = null;
        public string? Dependencies { get; set; } = null;
        public string? References { get; set; } = null;
        public int UnknownInt { get; set; }
        public int RecordCount { get; set; }


        // Only for v17
        public uint? SaveCount { get; set; }
        public uint? LastMerge { get; set; }
        public Dictionary<string, MergeEntry>? MergeEntries { get; set; } = null;
        public Dictionary<string, DeleteRequest>? DeleteRequests { get; set; } = null;

        public byte[]? Details { get; set; }
        public byte[]? UnparsedDetails { get; set; }
        public int DetailsLength { get; set; }
        public void AddDependency(string modName)
        {
            Dependencies = CoreUtils.AddModToList(Dependencies, modName);
        }
        public void AddReference(string modName)
        {
            References = CoreUtils.AddModToList(References, modName);
        }
    }
}
   
