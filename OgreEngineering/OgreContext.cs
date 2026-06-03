using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KenshiCore.OgreEngineering
{
    public class OgreContext
    {
        private bool FlipEndian { get; set; }
        private BinaryReader reader = null!;
        public OgreContext(BinaryReader reader)
        {
            this.reader = reader;
        }
        public void loadFlipEndian()
        {
            FlipEndian = getShouldFlipEndian();
        }
        public ushort ReadUInt16()
        {
            ushort v = reader.ReadUInt16();
            return FlipEndian ? BinaryPrimitives.ReverseEndianness(v) : v;
        }
        public ushort[] ReadUInts16(int count)
        {
            ushort[] values = new ushort[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = ReadUInt16();
            }
            return values;
        }
        public uint ReadUInt32()
        {
            uint v = reader.ReadUInt32();
            return FlipEndian ? BinaryPrimitives.ReverseEndianness(v) : v;
        }
        public uint[] ReadUInts32(int count)
        {
            uint[] values = new uint[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = ReadUInt32();
            }
            return values;
        }
        public float ReadFloat()
        {
            uint v = reader.ReadUInt32();
            if (FlipEndian)
                v = BinaryPrimitives.ReverseEndianness(v);

            return BitConverter.Int32BitsToSingle((int)v);
        }
        public bool ReadBool()
        {
            return reader.ReadByte() != 0;
        }
        public float[] ReadFloats(int count)
        {
            float[] values = new float[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = ReadFloat();
            }
            return values;
        }
        public string ReadString()
        {
            List<byte> bytes = new();
            while (true)
            {
                byte b = reader.ReadByte();

                if (b == '\n')
                    break;

                bytes.Add(b);
            }
            string str = Encoding.UTF8.GetString(bytes.ToArray());
            if (str.EndsWith("\r"))
                str = str[..^1];
            return str;
        }
        private bool getShouldFlipEndian()
        {
            ushort dest = reader.ReadUInt16();
            return dest == ((uint)endianType.OTHER);
        }
        public bool IsEndOfStream(int offset = 0)
        {
            return reader.BaseStream.Position + offset >= reader.BaseStream.Length;
        }
        public void offsetStream(int offset)
        {
            reader.BaseStream.Position += offset;
        }
        public (ushort, uint) ReadChunkHeader()
        {
            ushort id = reader.ReadUInt16();
            uint length = reader.ReadUInt32();
            return (id, length);
        }
        public byte[] ReadBytes(int count)
        {
            int available = (int)(reader.BaseStream.Length - reader.BaseStream.Position);
            int toRead = Math.Min(available, count);

            byte[] buffer = new byte[toRead];
            int read = reader.BaseStream.Read(buffer, 0, toRead);

            return buffer;
        }

    }
}
