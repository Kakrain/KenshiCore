using KenshiCore.Mods;
using KenshiCore.UI;
using KenshiCore.Utilities;
using System;
using System.Buffers;
using System.Data.Common;
using System.Globalization;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace KenshiCore.ReverseEngineering
{
    public class ReverseEngineer
    {
        public const int HARD_STRING_LIMIT = 2003;//2000 worked
        public const int DELETED = 2147483647;
        public ModData modData { get; private set; } = new();
        public string modname { get; private set; }
        public void InitializeEmptyMod(int fileType = 17)
        {
            modData.Header = new ModHeader_V17
            {
                FileType = fileType,
                ModVersion = 1,
            };
        }
        public ReverseEngineer(string modn)
        {
            this.modname = modn;
        }
        private const int NEW_V16 = unchecked((int)0x80000002u);
        private const int NEW_V17 = 0x00000020;
        public List<string> GetStringIdsNewRecords()
        {
            return modData.GetRecords().Where(r => r.isNew()).Select(r => r.getStringId()).ToList();
        }
        public List<string> GetStringIdsOldRecords()
        {
            return modData.GetRecords().Where(r => !r.isNew()).Select(r => r.getStringId()).ToList();
        }
        public List<string> getDependenciesAsList()
        {
            return CoreUtils.SplitModList(modData.Header!.GetDependencies());//modData.Header!.Dependencies ?? new() ;//CoreUtils.SplitModList(this.modData.Header?.Dependencies);
        }
        public List<string> getReferencesAsList()
        {
            return CoreUtils.SplitModList(modData.Header!.GetReferences());//CoreUtils.SplitModList(this.modData.Header?.References);
        }
        public void MergeReverseEngineer(ReverseEngineer other)
        {
            addDependencies(other.getDependenciesAsList());
            addReferences(other.getReferencesAsList());
            foreach(ModRecord record in other.modData.GetRecords())
            {
                this.modData.AddRecord(record);
            }
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
        public void WriteDictionary<T>(BinaryWriter writer, Dictionary<string, T> dict, Action<BinaryWriter, T> writeValue)
        {
            writer.Write(dict.Count);
            foreach (var kv in dict)
            {
                WriteString(writer, kv.Key);
                writeValue(writer, kv.Value);
            }
        }
        public bool LoadModFile(string path)
        {
            _nextFreeStringIdNumber = -1;
            ModReader mreader = new ModReader();

            bool result = mreader.Read(path);

            modData = mreader.data ?? new ModData();
            return result;
        }
        public static int readJustFiletype(string path)
        {
            using var fs = File.OpenRead(path);
            using var reader = new BinaryReader(fs, Encoding.UTF8);
            return reader.ReadInt32();
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
            CoreUtils.Print($"⚠ Warning: Unexpected filetype {filetype} in {path}", 0);
            return -1;
        }
        public void SaveModFile(string path)
        {
            ModWriter writer = new ModWriter();
            writer.Write(path, modData);
        }
        public List<(string Text, Color Color)> CompareWith(ReverseEngineer other, string? recordTypeFilter = null, List<string>? fieldFilter = null)
        {
            var blocks = new List<(string, Color)>();
            int typeCompare = ModRecord.getRecordTypeInt(recordTypeFilter ?? "");
            if (this.modData?.Count == 0 || other.modData?.Count == 0)
                return blocks;

            // Build quick lookup by record name (case-insensitive).
            var mineByName = this.modData!.GetRecords()
                .Where(r => typeCompare == -1 || r.RecordType == typeCompare)
                .ToDictionary(r => r.Name ?? "", StringComparer.Ordinal);

            var otherByName = other.modData!.GetRecords()
                .Where(r => typeCompare == -1 || r.RecordType == typeCompare)
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
        public void ApplyToStrings(Func<string, string> func)
        {
            if (modData.Header!.FileType == 16 && modData.Header.Description != null)
                modData.Header.Description = func(modData.Header.Description);

            foreach (var record in modData.GetRecords())
            {
                if (record.Name != null)
                    record.Name = func(record.Name);

                if (record.StringFields != null)
                {
                    foreach (var key in record.StringFields.Keys)
                    {
                        record.StringFields[key] = func(record.StringFields[key]);
                    }
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
            foreach (var record in modData.GetRecords())
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
        public ModRecord EnsureRecordExists(ModRecord target)
        {
            ModRecord? ownedtarget = modData.GetRecordByStringId(target.StringId);
            if (ownedtarget == null)
            {
                ownedtarget = new ModRecord();
                ownedtarget.Name = target.Name;
                ownedtarget.SetStringId(target.StringId);
                //ownedtarget.StringId = target.StringId;
                ownedtarget.RecordType = target.RecordType;
                ownedtarget.ChangeType = 0;//target.ChangeType;
                ownedtarget.SetRecordStatus(this.modData.Header!.FileType, "existing");
                ownedtarget.SetChangeCounter(2);//2 was too low
                this.modData.AddRecord(ownedtarget);
            }
            return ownedtarget;
        }
        public void AddRecordAsExisting(ModRecord target)
        {
            modData.RemoveRecord(target.StringId);
            target.ChangeType = 0;
            target.SetChangeCounter(2);
            target.SetRecordStatus(this.modData.Header!.FileType, "existing");
            modData.AddRecord(target);
        }

        public void SetField(ModRecord target, string fieldname, string value)
        {
            ModRecord? ownedtarget = EnsureRecordExists(target);
            ownedtarget.EnsureFieldExist(target, fieldname);
            ownedtarget.SetField(fieldname, value);
        }
        public void DeleteField(ModRecord target, string fieldname)
        {
            ModRecord? ownedtarget = EnsureRecordExists(target);
            ownedtarget.DeleteField(fieldname);
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
            if (target.ExtraDataFields == null)
                target.ExtraDataFields = new();
            target.ExtraDataFields.TryGetValue(category, out var target_cat);
            if (!force && (ownedtarget.ExtraDataExists(category,source.StringId)|| target.ExtraDataExists(category,source.StringId))) 
                //ExtraDataExists(cat, target_cat, source.StringId))
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
        public void AddExtraDataString(ModRecord target, string idsource, string category, int[]? vars = null, bool force = false)
        {
            ModRecord? ownedtarget = EnsureRecordExists(target);
            if (ownedtarget.ExtraDataFields == null)
                ownedtarget.ExtraDataFields = new Dictionary<string, Dictionary<string, int[]>>();
            ownedtarget.ExtraDataFields!.TryGetValue(category, out var cat);
            target.ExtraDataFields!.TryGetValue(category, out var target_cat);
            if (!force && (ownedtarget.ExtraDataExists(category, idsource) || target.ExtraDataExists(category, idsource)))
                //if (!force && ExtraDataExists(cat, target_cat, idsource))
                return;
            if (cat == null)
            {
                cat = new Dictionary<string, int[]>();
                ownedtarget.ExtraDataFields.Add(category, cat);
            }
            if (vars == null)
                vars = new int[] { 0, 0, 0 };
            cat[idsource] = vars;
        }
        public void RemoveExtraData(ModRecord target, ModRecord source, string category)
        {
            ModRecord ownedtarget = EnsureRecordExists(target);
            if (ownedtarget.ExtraDataExists(category, source.StringId))
            {
                ownedtarget.ExtraDataFields![category].Remove(source.StringId);
            }
            if (target.ExtraDataExists(category, source.StringId))
            {
                if (ownedtarget.ExtraDataFields == null)
                    ownedtarget.ExtraDataFields = new();
                if(!ownedtarget.ExtraDataFields.ContainsKey(category))
                    ownedtarget.ExtraDataFields[category] = new();
                ownedtarget.ExtraDataFields[category][source.StringId]= new int[] { DELETED, DELETED, DELETED };
            }
        }
        public void deleteRecordFromPatch(ModRecord record)
        {
            modData.RemoveRecord(record.StringId);
        }
        public void deleteEmptyRecordFromPatch(ModRecord record)
        {
            ModRecord? owned = modData.GetRecordByStringId(record.StringId);
            if (owned != null && owned.GetRecordCompleteness() == 0)
            {
                modData.RemoveRecord(owned.StringId);
            }
        }

        public void deleteRecord(ModRecord record)
        {
            var owned = modData.GetRecordByStringId(record.StringId);
            if (owned != null)
            {
                modData.RemoveRecord(owned.StringId);
                if (owned.isNew())
                    return;
            }
            var todelete = EnsureRecordExists(record);
            todelete.BoolFields ??= new Dictionary<string, bool>();
            todelete.BoolFields["REMOVED"] = true;
        }
        public void EditExtraData(ModRecord target, string category, Func<int, int>[] transformers, Func<int[], bool>? isValid = null)
        {
            bool exist_at_beginning = modData.GetRecordByStringId(target.StringId) != null;
            if (target.ExtraDataFields == null)
                return;
            target.ExtraDataFields!.TryGetValue(category, out var target_cat);
            if (target_cat == null)
            {
                return;
            }
            ModRecord? ownedtarget = EnsureRecordExists(target);
            if (ownedtarget.ExtraDataFields == null)
                ownedtarget.ExtraDataFields = new Dictionary<string, Dictionary<string, int[]>>();
            ownedtarget.ExtraDataFields.TryGetValue(category, out var cat);

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
                this.modData.RemoveRecord(ownedtarget.StringId);
            }
        }
        public List<ModRecord> CloneRecord(ModRecord toclone, int n)
        {
            List<ModRecord> clones = new List<ModRecord>();
            for (int i = 0; i < n; i++)
            {
                ModRecord clone = toclone.deepClone();
                clone.SetStringId($"{GetNextFreeStringIdNumber()}-{this.modname}");
                clone.ChangeType = this.modData.Header!.FileType == 16 ? NEW_V16 : NEW_V17;
                this.modData.AddRecord(clone);
                clones.Add(clone);
            }
            return clones;
        }
        public ModRecord CreateNewRecord(int recordType, string name)
        {
            // Find next free number starting at 10
            int nextId = GetNextFreeStringIdNumber();

            string stringId = $"{nextId}-{this.modname}";

            // Create new record
            var newRecord = new ModRecord
            {
                Name = name,
                //StringId = stringId,
                RecordType = recordType,
                ChangeType = this.modData.Header!.FileType == 16 ? NEW_V16 : NEW_V17,//ModRecord.ModTypeCodes.FirstOrDefault(kv => kv.Value == recordType).Key,
                Id = 0
            };
            newRecord.SetStringId(stringId);
            this.modData.AddRecord(newRecord);
            return newRecord;
        }
        public void EnsurePlaceholderExists(int recordType)
        {
            ModRecord.ModTypeCodes.TryGetValue(recordType, out string? recordTypeName);
            if (string.IsNullOrEmpty(recordTypeName))
                throw new FormatException($"record type code not found: {recordType}");
            // If already exists (same Name), do nothing
            if (this.modData.GetRecordsByType(recordType)!.Any(r => string.Equals(r.Name, recordTypeName)))
                return;

            ModRecord record = CreateNewRecord(recordType, recordTypeName);
        }


        private int _nextFreeStringIdNumber = -1;
        private int GetNextFreeStringIdNumber()
        {
            if (_nextFreeStringIdNumber >= 0)
                return ++_nextFreeStringIdNumber;
            var regex = new Regex(@"^(\d+)-(.+)$");
            int candidate = 10;

            foreach (string stringId in modData.GetStringIds())
            {
                var match = regex.Match(stringId);

                if (!match.Success || !string.Equals(match.Groups[2].Value, modname, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (int.TryParse(match.Groups[1].Value, out int num) && num >= candidate)
                    candidate = num + 1;
            }

            _nextFreeStringIdNumber = candidate;
            return candidate;
        }
        public void SetText(ModRecord record, Func<string, string> modifier)
        {
            ModRecord ownedtarget = EnsureRecordExists(record);

            for (int i = 0; ; i++)
            {
                string field = "text" + i;
                string? textValue = record.GetFieldAsString(field);
                if (textValue == null || textValue == "")
                    break;
                ownedtarget.EnsureFieldExist(record, field);
                ownedtarget.SetField(field, modifier(textValue));
            }
        }
    }
    public class ModData
    {
        public ModHeader? Header { get; set; }
        private readonly Dictionary<string, ModRecord> _recordsByStringId = new(StringComparer.Ordinal);
        private readonly Dictionary<int, List<ModRecord>> _recordsByType = new();
        public byte[]? Leftover { get; set; }
        public IEnumerable<string> GetStringIds() => _recordsByStringId.Keys;
        public int Count => _recordsByStringId.Count;

        public IEnumerable<ModRecord> GetRecords() => _recordsByStringId.Values;
        public void AddRecord(ModRecord record)
        {
            if (string.IsNullOrEmpty(record.StringId))
                throw new ArgumentException("Record must have a StringId.", nameof(record));
            if (_recordsByStringId.TryGetValue(record.StringId, out var existing))
            {
                if (ReferenceEquals(existing, record))
                    return;
                CoreUtils.Print($"Warning: Replacing existing record with StringId '{record.StringId}'", 1);
                RemoveRecord(existing.StringId);
            }
            _recordsByStringId[record.StringId] = record;
            if (!_recordsByType.TryGetValue(record.getRecordTypeCode(), out var records))
            {
                records = new List<ModRecord>();
                _recordsByType[record.getRecordTypeCode()] = records;
            }
            records.Add(record);

        }
        public bool RemoveRecord(string stringId)
        {
            ModRecord? record = null;
            bool removed = _recordsByStringId.TryGetValue(stringId, out record);
            if (record == null)
                return false;
            _recordsByStringId.Remove(stringId);
            if (_recordsByType.TryGetValue(record.getRecordTypeCode(), out var records))
            {
                records.Remove(record);
                if (records.Count == 0)
                    _recordsByType.Remove(record.getRecordTypeCode());
            }
            return true;
        }
        public ModRecord? GetRecordByStringId(string stringId)
        {
            if (_recordsByStringId.TryGetValue(stringId, out var record))
                return record;
            return null;
        }
        public IReadOnlyList<ModRecord> GetRecordsByType(string type)
        {
            int typeCode = ModRecord.getRecordTypeInt(type);
            if (typeCode == -1)
                throw new FormatException($"Invalid patch definition format: '{type}' is not a valid Record Type");
            return GetRecordsByType(typeCode);
        }
        public IReadOnlyList<ModRecord> GetRecordsByType(int type)
        {
            return _recordsByType.TryGetValue(type, out var records) ? records : Array.Empty<ModRecord>();
        }
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
}

