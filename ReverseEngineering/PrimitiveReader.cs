using KenshiCore.Utilities;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KenshiCore.ReverseEngineering
{
    public ref struct PrimitiveReader
    {
        private ReadOnlySpan<byte> _data;
        private int _position;
        public int getPosition()=>_position;

        public PrimitiveReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _position = 0;
        }

        public int ReadInt()
        {
            int value = BinaryPrimitives.ReadInt32LittleEndian(_data.Slice(_position, 4));
            _position += 4;
            return value;
        }
        public ushort ReadUInt16()
        {
            ushort value = BinaryPrimitives.ReadUInt16LittleEndian(
                _data.Slice(_position, 2));

            _position += 2;
            return value;
        }
        public uint ReadUInt32()
        {
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(
                _data.Slice(_position, 4));

            _position += 4;
            return value;
        }
        public float ReadFloat()
        {
            float value = BinaryPrimitives.ReadSingleLittleEndian(_data.Slice(_position, 4));
            _position += 4;
            return value;
        }
        public byte ReadByte()
        {
            return _data[_position++];
        }
        public bool ReadBool()
        {
            return ReadByte() != 0;//_data[_position++] != 0;
        }
        public byte[] ReadRemainingBytes()
        {
            var result = _data.Slice(_position);
            _position = _data.Length;
            return result.ToArray();
        }

        public byte[] ReadBytes(int length)
        {
            var result = _data.Slice(_position, length);
            _position += length;
            return result.ToArray();
        }
        public string ReadString()
        {
            int length = ReadInt();

            if (length < 0 || _position + length > _data.Length)
                throw new InvalidDataException();

            string value = Encoding.UTF8.GetString(_data.Slice(_position, length));

            _position += length;

            return value;
        }
        public int Remaining => _data.Length - _position;
        public delegate T ReadValue<T>(ref PrimitiveReader reader);
        public Dictionary<string, T>? ReadDictionary<T>(ReadValue<T> readValue)
        {
            int count = ReadInt();

            if (count == 0)
                return null;
                
            var dict = new Dictionary<string, T>(count);

            for (int i = 0; i < count; i++)
            {
                string key = ReadString();

                dict.Add(key, readValue(ref this));
            }

            return dict;
        }
        public Dictionary<string, MergeEntry>? ReadMergeEntries()
        {
            byte count = ReadByte();

            var dict = new Dictionary<string, MergeEntry>(count);

            for (int i = 0; i < count; i++)
            {
                string key = ReadString();

                dict.Add( key, new MergeEntry(ReadUInt32(),ReadUInt32()));
            }

            return dict;
        }
        public Dictionary<string, DeleteRequest>? ReadDeleteRequests()
        {
            byte count = ReadByte();
            var dict = new Dictionary<string, DeleteRequest>(count);
            for (int i = 0; i < count; i++)
            {
                string key = ReadString();
                dict[key] = new DeleteRequest(ReadUInt32(), ReadString());
            }
            return dict;
        }
        public T? TryRead<T>(ReadValue<T> readValue,out bool success)
        {
            
            int startPos = _position;

            try
            {
                T value = readValue(ref this);

                success = true;

                return value;
            }
            catch
            {
                _position = startPos;
                success = false;
                return default;
            }
        }
    }
}
