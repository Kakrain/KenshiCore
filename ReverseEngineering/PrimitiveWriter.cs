using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace KenshiCore.ReverseEngineering
{
    public ref struct PrimitiveWriter
    {
        private ArrayBufferWriter<byte> _buffer;
        public PrimitiveWriter(ArrayBufferWriter<byte> buffer)
        {
            _buffer = buffer;
        }

        public int Position => _buffer.WrittenCount;

        public void WriteInt(int value)
        {
            Span<byte> span = _buffer.GetSpan(4);
            BinaryPrimitives.WriteInt32LittleEndian(span, value);
            _buffer.Advance(4);
        }
        public void WriteLong(long value)
        {
            Span<byte> span = _buffer.GetSpan(8);
            BinaryPrimitives.WriteInt64LittleEndian(span, value);
            _buffer.Advance(8);
        }
        public void WriteUInt32(uint value)
        {
            Span<byte> span = _buffer.GetSpan(4);
            BinaryPrimitives.WriteUInt32LittleEndian(span, value);
            _buffer.Advance(4);
        }
        public void WriteUInt16(ushort value)
        {
            Span<byte> span = _buffer.GetSpan(2);
            BinaryPrimitives.WriteUInt16LittleEndian(span, value);
            _buffer.Advance(2);
        }
        public void WriteFloat(float value)
        {
            Span<byte> span = _buffer.GetSpan(4);
            BinaryPrimitives.WriteSingleLittleEndian(span, value);
            _buffer.Advance(4);
        }

        public void WriteByte(byte value)
        {
            Span<byte> span = _buffer.GetSpan(1);
            span[0] = value;
            _buffer.Advance(1);
        }

        public void WriteBool(bool value)
        {
            WriteByte(value ? (byte)1 : (byte)0);
        }

        public void WriteBytes(ReadOnlySpan<byte> value)
        {
            Span<byte> span = _buffer.GetSpan(value.Length);
            value.CopyTo(span);
            _buffer.Advance(value.Length);
        }

        public void WriteString(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);

            WriteInt(bytes.Length);
            WriteBytes(bytes);
        }
        public delegate void WriteValue<T>(ref PrimitiveWriter writer,T value);
        public void WriteDictionary<T>( Dictionary<string, T>? dictionary, WriteValue<T> writeValue)
        {
            if (dictionary == null)
            {
                WriteInt(0);
                return;
            }

            WriteInt(dictionary.Count);

            foreach (var pair in dictionary)
            {
                WriteString(pair.Key);
                writeValue(ref this, pair.Value);
            }
        }
        public void WriteMergeEntries(Dictionary<string, MergeEntry>? entries)
        {
            if (entries!.Count > byte.MaxValue)
                throw new InvalidOperationException("Too many merge entries; the format stores the count as a byte.");

            WriteByte((byte)entries.Count);

            foreach (var kv in entries)
            {
                WriteString(kv.Key);
                WriteUInt32(kv.Value.SaveCount);
                WriteUInt32(kv.Value.LastMerge);
            }
        }

        public void WriteDeleteRequests(Dictionary<string, DeleteRequest>? requests)
        {
            if (requests!.Count > byte.MaxValue)
                throw new InvalidOperationException("Too many delete requests; the format stores the count as a byte.");

            WriteByte((byte)requests.Count);

            foreach (var kv in requests)
            {
                WriteString(kv.Key);
                WriteUInt32(kv.Value.saveCount);
                WriteString(kv.Value.Target);
            }
        }
    }
}
