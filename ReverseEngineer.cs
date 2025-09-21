using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;

namespace KenshiUtilities.Core
{
    public class ReverseEngineer
    {
        public ModData modData;
        public ReverseEngineer()
        {
            modData = new ModData();
        }
        public int ReadInt(BinaryReader reader) => reader.ReadInt32();
        public float ReadFloat(BinaryReader reader) => reader.ReadSingle();
        public bool ReadBool(BinaryReader reader) => reader.ReadBoolean();
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
            modData = new ModData();
            using var fs = File.OpenRead(path);
            using var reader = new BinaryReader(fs, Encoding.UTF8);

            modData.Header = ParseHeader(reader);
            int recordCount = modData.Header.RecordCount;
            modData.Records = new List<ModRecord>();
            for (int i = 0; i < recordCount; i++)
            {
                modData.Records.Add(ParseRecord(reader));
            }

            long leftover = fs.Length - fs.Position;
            if (leftover > 0)
            {
                modData.Leftover = reader.ReadBytes((int)leftover);
                Console.WriteLine($"⚠ Warning: {leftover} leftover bytes detected.");
            }
        }
        public void SaveModFile(string path)
        {
            using var fs = File.OpenWrite(path);
            using var writer = new BinaryWriter(fs, Encoding.UTF8);

            WriteHeader(writer, modData.Header!);
            foreach (var record in modData.Records!)
                WriteRecord(writer, record);

            if (modData.Leftover != null)
                writer.Write(modData.Leftover);
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
                    WriteString(writer, header.Author);
                    WriteString(writer, header.Description);
                    WriteString(writer, header.Dependencies);
                    WriteString(writer, header.References);
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
            record.TypeCode = ReadInt(reader);
            record.Id = ReadInt(reader);
            record.Name = ReadString(reader);
            record.StringId = ReadString(reader);
            record.ModDataType = ReadInt(reader);

            record.BoolFields = ReadDictionary(reader, ReadBool);
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
            WriteInt(writer, record.TypeCode);
            WriteInt(writer, record.Id);
            WriteString(writer, record.Name);
            WriteString(writer, record.StringId);
            WriteInt(writer, record.ModDataType);

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
        

    }
    public class ModData
    {
        public ModHeader? Header { get; set; }
        public List<ModRecord>? Records { get; set; }
        public byte[]? Leftover { get; set; }
    }
    public class ModHeader
    {
        public int FileType { get; set; }
        public int ModVersion { get; set; }
        public string Author { get; set; } = "";
        public string Description { get; set; } = "";
        public string Dependencies { get; set; } = "";
        public string References { get; set; } = "";
        public int UnknownInt { get; set; }
        public int RecordCount { get; set; }

        public int DetailsLength { get; set; }
        public byte[]? Details { get; set; }
    }
    public class ModRecord
    {
        public int InstanceCount { get; set; }
        public int TypeCode { get; set; }
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string StringId { get; set; } = "";
        public int ModDataType { get; set; }

        public Dictionary<string, bool> BoolFields { get; set; } = new();
        public Dictionary<string, float> FloatFields { get; set; } = new();
        public Dictionary<string, int> LongFields { get; set; } = new();
        public Dictionary<string, float[]> Vec3Fields { get; set; } = new();
        public Dictionary<string, float[]> Vec4Fields { get; set; } = new();
        public Dictionary<string, string> StringFields { get; set; } = new();
        public Dictionary<string, string> FilenameFields { get; set; } = new();
        public Dictionary<string, Dictionary<string, int[]>>? ExtraDataFields { get; set; }
        public List<ModInstance>? InstanceFields { get; set; }
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
