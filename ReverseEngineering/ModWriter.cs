using KenshiCore.Mods;
using KenshiCore.UI;
using KenshiCore.Utilities;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KenshiCore.ReverseEngineering
{
    public class ModWriter
    {
        public bool Write(string path,ModData data)
        {
            try
            {
                var header = data.Header ?? throw new InvalidOperationException("Mod header is missing.");
                var buffer = new ArrayBufferWriter<byte>();
                var writer = new PrimitiveWriter(buffer);
                header.WriteHeader(ref writer);
                writer.WriteInt(data.Count);
                foreach (var record in data.GetRecords())
                    WriteRecord(ref writer, record);

                if (data.Leftover != null)
                {
                    writer.WriteBytes(data.Leftover);
                }

                File.WriteAllBytes(path, buffer.WrittenSpan.ToArray());
            }
            catch (System.IO.IOException)
            {
                UiService.ShowMessage($"The process cannot access the file {path}");
            }
            return true;

        }
        private void WriteRecord(ref PrimitiveWriter writer, ModRecord record)
        {
            writer.WriteInt(record.UnknownValue);//record.InstanceFields != null ? record.InstanceFields.Count() : 0);
            writer.WriteInt(record.RecordType);
            writer.WriteInt(record.Id);
            writer.WriteString(record.Name);
            writer.WriteString(record.StringId);
            writer.WriteInt(record.ChangeType);

            writer.WriteDictionary<bool>(record.BoolFields, static (ref PrimitiveWriter w, bool v) => w.WriteBool(v));
            writer.WriteDictionary<float>(record.FloatFields, static (ref PrimitiveWriter w, float v) => w.WriteFloat(v));
            writer.WriteDictionary<int>(record.LongFields, static (ref PrimitiveWriter w, int v) => w.WriteInt(v));
            writer.WriteDictionary(record.Vec3Fields, static (ref PrimitiveWriter w, float[] v) => { foreach (var f in v) w.WriteFloat(f); });
            writer.WriteDictionary(record.Vec4Fields, static (ref PrimitiveWriter w, float[] v) => { foreach (var f in v) w.WriteFloat(f); });
            writer.WriteDictionary(record.StringFields, static (ref PrimitiveWriter w, string s) => w.WriteString(s));
            writer.WriteDictionary(record.FilenameFields, static (ref PrimitiveWriter w, string s) => w.WriteString(s));


            // Extra data
            int extradatasize = record.ExtraDataFields != null ? record.ExtraDataFields.Count() : 0;
            writer.WriteInt(extradatasize);
            if (extradatasize > 0)
            {
                foreach (var kv in record.ExtraDataFields!)
                {
                    writer.WriteString(kv.Key);
                    writer.WriteInt(kv.Value.Count());
                    foreach (var kv2 in kv.Value)
                    {
                        writer.WriteString(kv2.Key);
                        foreach (var val in kv2.Value)
                            writer.WriteInt(val);
                    }
                }
            }
            // Instance fields
            int instancesize = record.InstanceFields != null ? record.InstanceFields.Count() : 0;
            writer.WriteInt(instancesize);
            if (instancesize > 0)
            {
                foreach (var inst in record.InstanceFields!)
                {
                    writer.WriteString(inst.Id!);
                    writer.WriteString(inst.Target!);
                    writer.WriteFloat(inst.Tx);
                    writer.WriteFloat(inst.Ty);
                    writer.WriteFloat(inst.Tz);
                    writer.WriteFloat(inst.Rw);
                    writer.WriteFloat(inst.Rx);
                    writer.WriteFloat(inst.Ry);
                    writer.WriteFloat(inst.Rz);
                    writer.WriteInt(inst.StateCount);
                    foreach (var s in inst.States!)
                        writer.WriteString(s);
                }
            }
        }
    }
}
