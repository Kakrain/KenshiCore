using System;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Windows.Forms;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace KenshiCore
{
    public class ReverseEngineer
    {
        public ModData modData;
        public ReverseEngineer()
        {
            modData = new ModData();
        }
        private string modname="";
        private readonly Dictionary<int, List<ModRecord>> _recordsByType=new();
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
        public void WriteInt(BinaryWriter writer, int v) => writer.Write(v);
        public void WriteFloat(BinaryWriter writer, float v) => writer.Write(v);
        public void WriteBool(BinaryWriter writer, bool v) => writer.Write(v);

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
            writer.Write(dict.Count);
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
            if(filetype==16)
                return reader.ReadInt32();
            if (filetype == 17) {
                reader.ReadInt32();
                return reader.ReadInt32();
            }
            throw new Exception($"Unexpected filetype: {filetype}");
        }
        public void enforceSanity()
        {
            this.modData.Header!.RecordCount = this.modData.Records!.Count;
        }
        public void SaveModFile(string path)
        {
            enforceSanity();
            using var fs = File.Create(path);// OpenWrite
            using var writer = new BinaryWriter(fs, Encoding.UTF8);
            modData.Header!.Details=BuildDetails(modData.Header!);
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
            if(header.Author!=null)
                WriteString(writer, header.Author);
            if (header.Description != null)
                WriteString(writer, header.Description);
            if (header.Dependencies != null)
                WriteString(writer, header.Dependencies);
            if (header.References != null)
                WriteString(writer, header.References);
            if(header.SaveCount != null)
                writer.Write(header.SaveCount.Value);
            if (header.LastMerge != null)
                writer.Write(header.LastMerge.Value);
            if(header.MergeEntries!=null)
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
                        if(!rec.ValidateChangeTypeAssumptions(filetype))
                            blocks.Add(($"--- RECORD: {rec.Name} {rec.StringId} ({rec.getModType()}) ({rec.getChangeType()})---", Color.Red));
                        if(!rec.ValidateDataTypeAssumptions())
                            blocks.Add(($"--- RECORD: {rec.Name} {rec.StringId} ({rec.RecordType}) ({rec.getModType()})---", Color.Red));
                    }
                }
            }
            return blocks;
        }
        public List<(string Text, Color Color)> GetAllDataAsBlocks()
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

            // Records
            if (modData.Records != null)
            {
                foreach (var rec in modData.Records)
                {
                    blocks.AddRange(rec.getDataAsBlock());
                }
            }
            return blocks;
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
                    WriteInt(writer, header.RecordCount);
                    break;
                case 17:
                    WriteInt(writer, header.DetailsLength);
                    WriteInt(writer, header.ModVersion);
                    writer.Write(header.Details!);
                    WriteInt(writer, header.RecordCount);
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
            WriteInt(writer, record.InstanceCount);
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
            WriteInt(writer, record.ExtraDataFields!.Count);
            foreach (var kv in record.ExtraDataFields)
            {
                WriteString(writer, kv.Key);
                WriteInt(writer, kv.Value.Count);
                foreach (var kv2 in kv.Value)
                {
                    WriteString(writer, kv2.Key);
                    foreach (var val in kv2.Value)
                        WriteInt(writer, val);
                }
            }

            // Instance fields
            WriteInt(writer, record.InstanceFields!.Count);
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
        public Tuple<string,string> getModSummary(int maxChars = 2000)//5000)
        {
            StringBuilder sba = new StringBuilder();
            StringBuilder sbs = new StringBuilder();
            foreach (var record in modData.Records!)
            {
                if (!string.IsNullOrEmpty(record.Name))
                    if(sba.Length <= maxChars)
                        sba.Append(",").Append(new String(record.Name.Where(c => (isAlphabet(c) ||c == ' ')).ToArray()));
                    if (sbs.Length <= maxChars)
                        sbs.Append(",").Append(new String(record.Name.Where(c => !(isAlphabet(c) || c == ' ')).ToArray()));       
                if (record.StringFields != null)
                {
                    foreach (var value in record.StringFields.Values) {
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
        public void generateResaves(string start= "C:/AlternativProgramFiles/Steam/steamapps/common/Kenshi/mods/")
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
        public void AddExtraData(ModRecord target,ModRecord source, string category)
        {
            ModRecord? ownedtarget = searchModRecordByStringId(target.StringId);
            if(ownedtarget == null)
            {
                ownedtarget = new ModRecord();
                ownedtarget.Name = target.Name;
                ownedtarget.StringId = target.StringId;
                ownedtarget.RecordType = target.RecordType;
                ownedtarget.ChangeType = target.ChangeType;
                ownedtarget.SetRecordStatus(this.modData.Header!.FileType, "existing");
                ownedtarget.SetChangeCounter(2);
                this.modData.Records!.Add(ownedtarget);
            }
            if (ownedtarget.ExtraDataFields == null)
                ownedtarget.ExtraDataFields = new Dictionary<string, Dictionary<string, int[]>>();
            this.modData.Header!.AddDependency(target.GetModName());
            this.modData.Header!.AddReference(source.GetModName());

            ownedtarget.ExtraDataFields!.TryGetValue(category, out var cat);
            if(cat == null)
            {
                cat = new Dictionary<string, int[]>();
                ownedtarget.ExtraDataFields.Add(category, cat);
            }
            //EnsurePlaceholderExists(source.TypeCode);
            cat.Add(source.StringId, new int[] { 0, 0, 0 });
        }
        public ModRecord? searchModRecordByStringId(string stringId)
        {
            return this.modData.Records!.Find(r => r.StringId == stringId);
        }
        public ModRecord CreateNewRecord(int recordType,string name)
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
                RecordType= recordType,
                ChangeType = this.modData.Header!.FileType==16?NEW_V16:NEW_V17,//ModRecord.ModTypeCodes.FirstOrDefault(kv => kv.Value == recordType).Key,
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
                if (match.Success && int.TryParse(match.Groups[1].Value, out int num) && (record.GetModName()==this.modname))
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
            if (string.IsNullOrWhiteSpace(modName))
                return;
            var deps = (Dependencies ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(d => d.Trim()).ToList();
            if (!deps.Any(d => d.Equals(modName, StringComparison.OrdinalIgnoreCase)))
            {
                deps.Add(modName);
                Dependencies = string.Join(",", deps);
            }
        }
        public void AddReference(string modName)
        {
            if (string.IsNullOrWhiteSpace(modName))
                return;
            var refs = (References ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(r => r.Trim()).ToList();
            if (!refs.Any(r => r.Equals(modName, StringComparison.OrdinalIgnoreCase)))
            {
                refs.Add(modName);
                References = string.Join(",", refs);
            }
        }
    }
    public class ModRecord
    {
        public int InstanceCount { get; set; }
        public int RecordType { get; set; }
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string StringId { get; set; } = "";
        public int ChangeType { get; set; }

        private HashSet<string>? changed = null;
        private string sep = ":";

        public Dictionary<string, bool> BoolFields { get; set; } = new();
        public Dictionary<string, float> FloatFields { get; set; } = new();
        public Dictionary<string, int> LongFields { get; set; } = new();
        public Dictionary<string, float[]> Vec3Fields { get; set; } = new();
        public Dictionary<string, float[]> Vec4Fields { get; set; } = new();
        public Dictionary<string, string> StringFields { get; set; } = new();
        public Dictionary<string, string> FilenameFields { get; set; } = new();
        public Dictionary<string, Dictionary<string, int[]>> ExtraDataFields { get; set; } = new();
        public List<ModInstance> InstanceFields { get; set; } = new();


        private void getChangedSpecificFields<TValue>(Dictionary<string, TValue>? fields,string name)
        {
            if (fields == null)
                return;
            foreach (var f in fields)
            {
                changed!.Add(name + sep + f.Key);
            }
        }
        

        public HashSet<string> getChangedFields()
        {
            if (changed != null)
            {
                return changed;
            }
            changed = new HashSet<string>();


            getChangedSpecificFields(this.BoolFields, "bool");
            getChangedSpecificFields(this.FloatFields, "float");
            getChangedSpecificFields(this.LongFields, "long");
            getChangedSpecificFields(this.Vec3Fields, "vec3");
            getChangedSpecificFields(this.Vec4Fields, "vec4");
            getChangedSpecificFields(this.StringFields, "string");
            getChangedSpecificFields(this.FilenameFields, "filename");
            getChangedSpecificFields(this.ExtraDataFields, "extradata");

            return changed;
        }
        public static readonly Dictionary<int, string> ModTypeCodes = new Dictionary<int, string>
        {
            { 0, "BUILDING" },{ 1, "CHARACTER" },{ 2, "WEAPON" },{ 3, "ARMOUR" },{ 4, "ITEM" },
            { 5, "ANIMAL_ANIMATION" },{ 6, "ATTACHMENT" },{ 7, "RACE" },{ 9, "NATURE" },{ 10, "FACTION" },{ 12, "ZONE_MAP" },
            { 13, "TOWN" },{ 16, "LOCATIONAL_DAMAGE" },{ 17, "COMBAT_TECHNIQUE" },{ 18, "DIALOGUE" },{ 19, "DIALOGUE_LINE" },
            { 21, "RESEARCH" },{ 22, "AI_TASK" },{ 24, "ANIMATION" },{ 25, "STATS" },{ 26, "PERSONALITY" },
            { 27, "CONSTANTS" },{ 28, "BIOMES" },{ 29, "BUILDING_PART" },{ 30, "INSTANCE_COLLECTION" },{ 31, "DIALOG_ACTION" },
            { 34, "PLATOON" },{ 36, "GAMESTATE_CHARACTER" },{ 37, "GAMESTATE_FACTION" },{ 38, "GAMESTATE_TOWN_INSTANCE_LIST" },{ 41, "INVENTORY_STATE" },
            { 42, "INVENTORY_ITEM_STATE" },{ 43, "REPEATABLE_BUILDING_PART_SLOT" },{ 44, "MATERIAL_SPEC" },{ 45, "MATERIAL_SPECS_COLLECTION" },{ 46, "CONTAINER" },
            { 47, "MATERIAL_SPECS_CLOTHING" },{ 49, "VENDOR_LIST" },{ 50, "MATERIAL_SPECS_WEAPON" },{ 51, "WEAPON_MANUFACTURER" },{ 52, "SQUAD_TEMPLATE" },
            { 53, "ROAD" },{ 55, "COLOR_DATA" },{ 56, "CAMERA" },{ 57, "MEDICAL_STATE" },{ 59, "FOLIAGE_LAYER" },
            { 60, "FOLIAGE_MESH" },{ 61, "GRASS" },{ 62, "BUILDING_FUNCTIONALITY" },{ 63, "DAY_SCHEDULE" },{ 64, "NEW_GAME_STARTOFF" },
            { 66, "CHARACTER_APPEARANCE" },{ 67, "GAMESTATE_AI" },{ 68, "WILDLIFE_BIRDS" },{ 69, "MAP_FEATURES" },{ 70, "DIPLOMATIC_ASSAULTS" },
            { 71, "SINGLE_DIPLOMATIC_ASSAULT" },{ 72, "AI_PACKAGE" },{ 73, "DIALOGUE_PACKAGE" },{ 74, "GUN_DATA" },{ 76, "ANIMAL_CHARACTER" },
            { 77, "UNIQUE_SQUAD_TEMPLATE" },{ 78, "FACTION_TEMPLATE" },{ 80, "WEATHER" },{ 81, "SEASON" },{ 82, "EFFECT" },
            { 83, "ITEM_PLACEMENT_GROUP" },{ 84, "WORD_SWAPS" },{ 86, "NEST_ITEM" },{ 87, "CHARACTER_PHYSICS_ATTACHMENT" },{ 88, "LIGHT" },
            { 89, "HEAD" },{ 92, "FOLIAGE_BUILDING" },{ 93, "FACTION_CAMPAIGN" },{ 94, "GAMESTATE_TOWN" },{ 95, "BIOME_GROUP" },
            { 96, "EFFECT_FOG_VOLUME" },{ 97, "FARM_DATA" },{ 98, "FARM_PART" },{ 99, "ENVIRONMENT_RESOURCES" },{ 100, "RACE_GROUP" },
            { 101, "ARTIFACTS" },{ 102, "MAP_ITEM" },{ 103, "BUILDINGS_SWAP" },{ 104, "ITEMS_CULTURE" },{ 105, "ANIMATION_EVENT" },
            { 107, "CROSSBOW" },{ 109, "AMBIENT_SOUND" },{ 110, "WORLD_EVENT_STATE" },{ 111, "LIMB_REPLACEMENT" },{112,"ANIMATION_FILE"}
        };
        public static readonly Dictionary<string, int> ModTypeNames=ModTypeCodes.ToDictionary(kv => kv.Value, kv => kv.Key);
        public string getModType()
        {
            return ModTypeCodes.GetValueOrDefault(this.RecordType, $"UNKNOWN:{this.RecordType.ToString()}");
        }
        public List<(string, Color)> getDataAsBlock()
        {
            var blocks = new List<(string, Color)>();
            // Record header
            blocks.Add(($"--- RECORD: {this.Name} ({this.getModType()}) ---", Color.Orange));
            blocks.Add(($"ID: {this.Id}, StringID: {this.StringId}, ChangeType: {this.getChangeType()}", Color.Gray));

            // Basic fields
            foreach (var kv in this.BoolFields) blocks.Add(($"Bool: {kv.Key} = {kv.Value}", Color.LightCyan));
            foreach (var kv in this.FloatFields) blocks.Add(($"Float: {kv.Key} = {kv.Value}", Color.LightCyan));
            foreach (var kv in this.LongFields) blocks.Add(($"Long: {kv.Key} = {kv.Value}", Color.LightCyan));
            foreach (var kv in this.StringFields) blocks.Add(($"String: {kv.Key} = {kv.Value}", Color.LightYellow));
            foreach (var kv in this.FilenameFields) blocks.Add(($"Filename: {kv.Key} = {kv.Value}", Color.LightPink));

            // ExtraData
            if (this.ExtraDataFields != null)
            {
                foreach (var cat in this.ExtraDataFields)
                {
                    blocks.Add(($"ExtraData Category: {cat.Key}", Color.LightSalmon));
                    foreach (var item in cat.Value)
                        blocks.Add(($"  {item.Key} = [{string.Join(",", item.Value)}]", Color.LightSalmon));
                }
            }

            // Instances
            if (this.InstanceFields != null)
            {
                foreach (var inst in this.InstanceFields)
                {
                    blocks.Add(($"Instance: Id={inst.Id}, Target={inst.Target}, Pos=({inst.Tx},{inst.Ty},{inst.Tz}), Rot=({inst.Rx},{inst.Ry},{inst.Rz},{inst.Rw})", Color.LightGray));
                    if (inst.States != null && inst.States.Count > 0)
                        blocks.Add(($"  States: {string.Join(",", inst.States)}", Color.LightGray));
                }
            }
            return blocks;
        }
        public string getChangeType()
        {
            // Convert ModDataType to 32-bit binary string
            string binary = Convert.ToString(ChangeType, 2).PadLeft(32, '0');

            // First 4 groups (first 20 bits) in groups of 4
            string first3Groups = string.Join(" | ", Enumerable.Range(0, 3)
                .Select(i => binary.Substring(i * 4, 4)));

            // Groups 6 + 7 (bits 20–27) = ChangeCounter
            string changeCounterBits = binary.Substring(12, 16);
            int changeCounter = Convert.ToInt32(changeCounterBits, 2);

            // Group 8 (bits 28–31) = NewRecordFlag (keep as bits)
            string newRecordFlagBits = binary.Substring(28, 4);
            bool isExistingRecord = newRecordFlagBits[3] == '1'; // last bit = 1 if existing

            // Format result
            //string result = $"{first3Groups} | Change Counter: {changeCounter} | {newRecordFlagBits} ({(isExistingRecord ? "Existing" : "New")})";
            string result = $"Change Counter: {changeCounter} | {newRecordFlagBits} ({(isExistingRecord ? "Existing" : "New")})";

            if (newRecordFlagBits == "0011")
                result += " (Name Changed)";

            // Append REMOVED if applicable
            if (this.BoolFields.TryGetValue("REMOVED", out var value) && value)
                result += " REMOVED";

            return result;
        }
        public void SetChangeCounter(int newValue)
        {
            // Clamp between 0–65535 (16 bits)
            newValue = Math.Clamp(newValue, 0, 65535);

            // Convert ModDataType to binary string
            char[] binary = Convert.ToString(ChangeType, 2).PadLeft(32, '0').ToCharArray();

            // Replace bits 12–27 (the 16-bit change counter)
            string newBits = Convert.ToString(newValue, 2).PadLeft(16, '0');
            for (int i = 0; i < 16; i++)
                binary[12 + i] = newBits[i];

            // Convert back to int
            ChangeType = Convert.ToInt32(new string(binary), 2);
        }
        public void AddToChangeCounter(int delta)
        {
            // Extract current counter
            string binary = Convert.ToString(ChangeType, 2).PadLeft(32, '0');
            string changeCounterBits = binary.Substring(12, 16);
            int current = Convert.ToInt32(changeCounterBits, 2);

            // Add and clamp to 0–65535
            int newValue = Math.Clamp(current + delta, 0, 65535);

            // Reuse the SetChangeCounter logic
            SetChangeCounter(newValue);
        }
        public void SetRecordStatus(int fileType, string status)
        {
            // Convert to 32-bit binary string
            char[] binary = Convert.ToString(ChangeType, 2).PadLeft(32, '0').ToCharArray();

            // Extract the last 4 bits (bits 28–31)
            string lastGroup = new string(binary[28..32]);

            string newLastGroup = lastGroup; // default keep existing

            switch (status.ToLower())
            {
                case "existing":
                    newLastGroup = "0001";
                    break;

                case "new":
                    newLastGroup = fileType == 16 ? "0010" : "0000";
                    break;

                case "namechanged":
                    // Can only apply if NOT new
                    bool isCurrentlyNew =
                        (fileType == 16 && lastGroup == "0010") ||
                        (fileType == 17 && lastGroup == "0000");

                    if (!isCurrentlyNew)
                        newLastGroup = "0011";
                    break;

                default:
                    throw new ArgumentException($"Unknown record status: {status}");
            }

            // Replace last 4 bits
            for (int i = 0; i < 4; i++)
                binary[28 + i] = newLastGroup[i];

            // Convert back to int
            ChangeType = Convert.ToInt32(new string(binary), 2);
        }
        public string GetModName()
        {
            if (string.IsNullOrEmpty(StringId))
                return string.Empty;

            // Expected format: "number-modname.mod"
            int dashIndex = StringId.IndexOf('-');
            if (dashIndex == -1 || dashIndex >= StringId.Length - 1)
                return string.Empty;

            string modPart = StringId.Substring(dashIndex + 1);

            // Ensure it ends with ".mod"
            if (modPart.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
                return modPart;

            return string.Empty;
        }
        public bool ValidateDataTypeAssumptions()
        {
            return ModTypeCodes.ContainsKey(this.RecordType);
            
        }
        public bool ValidateChangeTypeAssumptions(int fileType)
        {
            string binary = Convert.ToString(ChangeType, 2).PadLeft(32, '0');

            if (fileType == 16)
            {
                // Version 16 assumptions:
                // first group (bits 0-3) = 1000
                // last group (bits 28-31) = 0001 or 0010 or 0011
                string firstGroup = binary.Substring(0, 4);
                string lastGroup = binary.Substring(28, 4);

                bool firstOk = firstGroup == "1000";
                bool lastOk = lastGroup == "0001" || lastGroup == "0010" || lastGroup == "0011";

                return firstOk && lastOk;
            }
            else if (fileType == 17)
            {
                // Version 17 assumptions:
                // first 4 groups (bits 0-15) = 0
                // groups 5,6,7 (bits 16-27) = change counter (any number)
                // last group (bits 28-31) = 0000, 0001, or 0011
                string first3Groups = binary.Substring(0, 12);
                string lastGroup = binary.Substring(28, 4);

                bool firstOk = first3Groups.All(c => c == '0');
                bool lastOk = lastGroup == "0000" || lastGroup == "0001" || lastGroup == "0011";

                return firstOk && lastOk;
            }
            return false;
        }
    }
    public class ModInstance
    {
        public string? Id { get; set; }
        public string? Target { get; set; }
        public float Tx { get; set; }
        public float Ty { get; set; }
        public float Tz { get; set; }
        public float Rw { get; set; }
        public float Rx { get; set; }
        public float Ry { get; set; }
        public float Rz { get; set; }
        public int StateCount { get; set; }
        public List<string>? States { get; set; }
    }
}
