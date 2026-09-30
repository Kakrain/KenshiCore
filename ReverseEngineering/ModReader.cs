using KenshiCore.Mods;
using KenshiCore.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace KenshiCore.ReverseEngineering
{
    public class ModReader
    {
        public string modname="";
        public ModData? data=null;
        public bool Read(string path)
        {
            data = new ModData();
            try
            {
                var reader = new PrimitiveReader(File.ReadAllBytes(path));

                string fileName = Path.GetFileName(path);
                string extension = Path.GetExtension(fileName).ToLowerInvariant();

                if (extension != ".mod" && extension != ".base")
                {
                    fileName = Path.GetFileNameWithoutExtension(fileName) + ".mod";
                }
                this.modname = fileName;
                data.Header = ParseHeader(ref reader);
                int recordCount = reader.ReadInt();
                for (int i = 0; i < recordCount; i++)//recordCount
                {
                    try
                    {
                        data.AddRecord(ParseRecord(ref reader));
                    }
                    catch (EndOfStreamException)
                    {
                        CoreUtils.Print($"⚠ Warning: EndOfStreamException found prematurely.");
                        return false;
                    }
                }
                TryParseDetails(data.Header);
                long leftover = reader.Remaining;
                if (leftover > 0)
                {
                    data.Leftover = reader.ReadBytes((int)leftover);
                    CoreUtils.Print($"⚠ Warning: {leftover} leftover bytes detected.\n"+(data.Header.FileType==15?"it is normal since this version is 15":$"not expected, version is {data.Header.FileType}"));
                    return true;
                }
                return true;
            }

            catch (FileNotFoundException)
            {
                CoreUtils.Print($"⚠ Warning: File not found: {path}", 0);
                return false;
            }
            catch (EndOfStreamException)
            {
                CoreUtils.Print($"⚠ Warning: Unexpected end of file: {path}", 0);
                return false;
            }
            catch (UnsupportedModFileException)
            {
                CoreUtils.Print($"⚠ Warning: Unsupported File type: {path}", 0);
                return false;
            }
            catch (Exception ex)
            {
                CoreUtils.Print($"⚠ Failed to load mod '{path}': {ex.Message}", 0);
                return false;
            }

        }
        private ModHeader ParseHeader(ref PrimitiveReader reader)
        {
            ModHeader header;
            int fileType = reader.ReadInt();
            
            switch (fileType)
            {
                case 15://Naginata
                    header = new ModHeader_V15();
                    break;
                case 16://erji
                    header = new ModHeader_V16();
                    break;
                case 17://2B
                    header = new ModHeader_V17();
                    break;
                default:
                    throw new UnsupportedModFileException(fileType);
            }

            header.ReadHeader(ref reader);
            header.FileType = fileType;
            return header;
        }
        private ModRecord ParseRecord(ref PrimitiveReader reader)
        {
            int start = reader.getPosition();
            var record = new ModRecord();
            record.UnknownValue = reader.ReadInt();

            record.RecordType = reader.ReadInt();//BUILDING,GAMESTATE_FACTION,etc
            record.Id = reader.ReadInt();

            record.Name = reader.ReadString();

            record.SetStringId(reader.ReadString());
            //record.StringId = reader.ReadString();

            record.ChangeType = reader.ReadInt();//-2147483646 means new,-2147483647 means changed,-2147483645 changed and name was changed

            record.BoolFields = reader.ReadDictionary<bool>(static (ref PrimitiveReader r) => r.ReadBool());//if removed by a mod just one bool field: "REMOVED": True
            record.FloatFields = reader.ReadDictionary<float>(static (ref PrimitiveReader r) => r.ReadFloat());
            record.LongFields = reader.ReadDictionary<int>(static (ref PrimitiveReader r) => r.ReadInt());
            record.Vec3Fields = reader.ReadDictionary<float[]>(static (ref PrimitiveReader r) => new float[] { r.ReadFloat(), r.ReadFloat(), r.ReadFloat() });
            record.Vec4Fields = reader.ReadDictionary<float[]>(static (ref PrimitiveReader r) => new float[] { r.ReadFloat(), r.ReadFloat(), r.ReadFloat(), r.ReadFloat() });
            record.StringFields = reader.ReadDictionary<string>(static (ref PrimitiveReader r) => r.ReadString());
            record.FilenameFields = reader.ReadDictionary<string>(static (ref PrimitiveReader r) => r.ReadString());


            int extraCatCount = reader.ReadInt();
            if (extraCatCount > 0)
                record.ExtraDataFields = new Dictionary<string, Dictionary<string, int[]>>(extraCatCount);

            for (int i = 0; i < extraCatCount; i++)
            {
                string catName = reader.ReadString();
                int itemCount = reader.ReadInt();
                var catValue = new Dictionary<string, int[]>(itemCount);
                for (int j = 0; j < itemCount; j++)
                {
                    string itemName = reader.ReadString();
                    int[] values = new int[3] { reader.ReadInt(), reader.ReadInt(), reader.ReadInt() };
                    catValue[itemName] = values;
                }
                record.ExtraDataFields![catName] = catValue;
            }
            // Instance fields
            int instanceCount2 = reader.ReadInt();
            if (instanceCount2 > 0)
                record.InstanceFields = new List<ModInstance>(instanceCount2);
            for (int i = 0; i < instanceCount2; i++)
            {
                var inst = new ModInstance();
                inst.Id = reader.ReadString();
                inst.Target = reader.ReadString();
                inst.Tx = reader.ReadFloat();
                inst.Ty = reader.ReadFloat();
                inst.Tz = reader.ReadFloat();
                inst.Rw = reader.ReadFloat();
                inst.Rx = reader.ReadFloat();
                inst.Ry = reader.ReadFloat();
                inst.Rz = reader.ReadFloat();
                inst.StateCount = reader.ReadInt();
                inst.States = new List<string>();
                for (int j = 0; j < inst.StateCount; j++)
                    inst.States.Add(reader.ReadString());
                record.InstanceFields!.Add(inst);
            }
            return record;
        }
        private void TryParseDetails(ModHeader header_param)
            
        {
            if (header_param is not ModHeader_V17 header)
                return;
           
            var reader = new PrimitiveReader(header.Details);

            bool ok;

            if (reader.Remaining > 0)
                header.Author = reader.TryRead(static (ref PrimitiveReader r) => r.ReadString(),out ok);

            if (reader.Remaining > 0)
                header.Description = reader.TryRead(static (ref PrimitiveReader r) => r.ReadString(),out ok);

            if (reader.Remaining > 0)
                header.SetDependenciesFromString(reader.TryRead(static (ref PrimitiveReader r) => r.ReadString(), out ok)??"");
                //header.Dependencies = CoreUtils.SplitModList(reader.TryRead(static (ref PrimitiveReader r) => r.ReadString(), out ok));
            //reader.TryRead(static (ref PrimitiveReader r) => r.ReadString(),out ok);

            if (reader.Remaining > 0)
                header.SetReferencesFromString(reader.TryRead(static (ref PrimitiveReader r) => r.ReadString(), out ok) ?? "");
            //header.References = CoreUtils.SplitModList(reader.TryRead(static (ref PrimitiveReader r) => r.ReadString(), out ok));
            //header.References = reader.TryRead( static (ref PrimitiveReader r) => r.ReadString(),out ok);

            if (reader.Remaining > 0)
                header.SaveCount = reader.TryRead(static (ref PrimitiveReader r) => r.ReadUInt32(),out ok);

            if (reader.Remaining > 0)
                header.LastMerge = reader.TryRead( static (ref PrimitiveReader r) => r.ReadUInt32(), out ok);

            if (reader.Remaining > 0)
                header.MergeEntries = reader.TryRead(static (ref PrimitiveReader r) => r.ReadMergeEntries(),out ok);

            if (reader.Remaining > 0)
                header.DeleteRequests = reader.TryRead(  static (ref PrimitiveReader r) => r.ReadDeleteRequests(),out ok);

            if (reader.Remaining > 0)
                header.UnparsedDetails = reader.ReadRemainingBytes();
        }
    }
}
