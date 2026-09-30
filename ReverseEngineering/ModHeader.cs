using KenshiCore.Mods;
using KenshiCore.Utilities;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace KenshiCore.ReverseEngineering
{
    public abstract class ModHeader
    {
        public int FileType { get; set; }
        public int ModVersion { get; set; }
        public string? Author { get; set; } = null;
        public string? Description { get; set; } = null;

        private List<string>? Dependencies = null;
        private List<string>? References = null;
        //public string? Dependencies { get; set; } = null;
        //public string? References { get; set; } = null;
        
        public bool isDependenciesNull()
        {
            return Dependencies == null;
        }
        public bool isReferencesNull()
        {
            return References == null;
        }
        public string GetDependencies()
        {
            return string.Join(",", Dependencies??new());
        }
        public string GetReferences()
        {
            return string.Join(",", References ?? new());
        }
        public void SetDependenciesFromString(string deps)
        {
            Dependencies = CoreUtils.SplitModList(deps);
        }
        public void SetReferencesFromString(string deps)
        {
            References = CoreUtils.SplitModList(deps);
        }

        public int UnknownInt { get; set; }
        public int lastStringIdNr = -1;
        public int RecordCount = 0;

        // Only for v17
        public uint? SaveCount { get; set; }
        public uint? LastMerge { get; set; }
        public Dictionary<string, MergeEntry>? MergeEntries { get; set; } = null;
        public Dictionary<string, DeleteRequest>? DeleteRequests { get; set; } = null;

        public byte[]? UnparsedDetails { get; set; }
        public void AddDependency(string modName)
        {

            if (ModRepository.Instance.BaseGameMods.Contains(modName))
            {
                return;
            }
            if (Dependencies == null)
                Dependencies = new();
            if(!Dependencies.Contains(modName))
                Dependencies.Add(modName);
            //Dependencies = CoreUtils.AddModToList(Dependencies, modName, ReverseEngineer.HARD_STRING_LIMIT);
        }
        public void AddReference(string modName)
        {
            if (ModRepository.Instance.BaseGameMods.Contains(modName))
            {
                return;
            }
            if (References == null)
                References = new();
            if (!References.Contains(modName))
                References.Add(modName);
            //References = CoreUtils.AddModToList(References, modName, ReverseEngineer.HARD_STRING_LIMIT);
        }
        public abstract void WriteHeader(ref PrimitiveWriter writer);//, int count);
        public abstract void ReadHeader(ref PrimitiveReader writer);
    }
    public class ModHeader_V14 : ModHeader//does not work yet
    {

        public override void WriteHeader(ref PrimitiveWriter writer)
        {
            writer.WriteInt(FileType);
            writer.WriteInt(lastStringIdNr);
        }
        public override void ReadHeader(ref PrimitiveReader writer)
        {
        }
    }
    public class ModHeader_V15 : ModHeader
    {
        public override void WriteHeader(ref PrimitiveWriter writer)
        {
            writer.WriteInt(FileType);
            writer.WriteInt(lastStringIdNr);
        }
        public override void ReadHeader(ref PrimitiveReader reader)
        {
            lastStringIdNr = reader.ReadInt();
        }

    }
    public class ModHeader_V16 : ModHeader
    {
        public override void WriteHeader(ref PrimitiveWriter writer)
        {
            writer.WriteInt(FileType);

            writer.WriteInt(ModVersion);
            writer.WriteString(Author!);
            writer.WriteString(Description!);
            writer.WriteString(GetDependencies());//string.Join(",",Dependencies??new()));
            writer.WriteString(GetReferences());// string.Join(",", References ?? new()));
            
            //writer.WriteString(References!);
            writer.WriteInt(UnknownInt);

        }
        public override void ReadHeader(ref PrimitiveReader reader)
        {
            ModVersion = reader.ReadInt();
            Author = reader.ReadString();
            Description = reader.ReadString();
            SetDependenciesFromString(reader.ReadString());
            SetReferencesFromString(reader.ReadString());
            //Dependencies =CoreUtils.SplitModList(reader.ReadString());//reader.ReadString();
            //References = CoreUtils.SplitModList(reader.ReadString());//reader.ReadString();
            UnknownInt = reader.ReadInt();

        }
    }
    public class ModHeader_V17 : ModHeader
    {
        public byte[]? Details { get; set; }
        public int DetailsLength { get; set; }
        public override void WriteHeader(ref PrimitiveWriter writer)
        {
            Details = BuildDetails();
            DetailsLength = Details!.Length;


            writer.WriteInt(FileType);

            writer.WriteInt(DetailsLength);
            writer.WriteInt(ModVersion);
            writer.WriteBytes(Details!);
        }
        public override void ReadHeader(ref PrimitiveReader reader)
        {

            DetailsLength = reader.ReadInt();
            ModVersion = reader.ReadInt();
            Details = reader.ReadBytes(DetailsLength);
        }
        public byte[] BuildDetails()
        {
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new PrimitiveWriter(buffer);

            CoreUtils.Print($"Dependencies {GetDependencies()}");
            CoreUtils.Print($"References {GetReferences()}");

            if (Author != null)
                writer.WriteString(Author);
            if (Description != null)
                writer.WriteString(Description);
            if (!isDependenciesNull())
                writer.WriteString(GetDependencies());//string.Join(",",Dependencies));
            if (!isReferencesNull())
                writer.WriteString(GetReferences());//string.Join(",", References));
            if (SaveCount != null)
                writer.WriteUInt32(SaveCount.Value);
            if (LastMerge != null)
                writer.WriteUInt32(LastMerge.Value);
            if (MergeEntries != null)
                writer.WriteMergeEntries(MergeEntries);
            if (DeleteRequests != null)
                writer.WriteDeleteRequests(DeleteRequests);
            if (UnparsedDetails != null && UnparsedDetails.Length > 0)
            {
                writer.WriteBytes(UnparsedDetails);
            }
            return buffer.WrittenSpan.ToArray();
        }
    }
    
}
