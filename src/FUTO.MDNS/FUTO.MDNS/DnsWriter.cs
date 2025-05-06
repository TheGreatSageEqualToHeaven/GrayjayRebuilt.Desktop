using System.Buffers.Binary;
using System.Text;
using static FUTO.MDNS.DnsReader;

namespace FUTO.MDNS;

public class DnsWriter
{
    private readonly List<byte> _data = new();
    private readonly Dictionary<string, int> _namePositions = new();

    public byte[] ToArray() => _data.ToArray();

    public void WritePacket(DnsPacketHeader header, 
        int? questionCount = null, Action<DnsWriter, int>? questionWriter = null, 
        int? answerCount = null, Action<DnsWriter, int>? answerWriter = null, 
        int? authorityCount = null, Action<DnsWriter, int>? authorityWriter = null, 
        int? additionalsCount = null, Action<DnsWriter, int>? additionalWriter = null)
    {
        if (questionCount != null && questionWriter == null || questionCount == null && questionWriter != null)
            throw new Exception("When question count is given, question writer should also be given.");
        if (answerCount != null && answerWriter == null || answerCount == null && answerWriter != null)
            throw new Exception("When answer count is given, answer writer should also be given.");
        if (authorityCount != null && authorityWriter == null || authorityCount == null && authorityWriter != null)
            throw new Exception("When authority count is given, authority writer should also be given.");
        if (additionalsCount != null && additionalWriter == null || additionalsCount == null && additionalWriter != null)
            throw new Exception("When additionals count is given, additional writer should also be given.");

        WriteHeader(header, questionCount ?? 0, answerCount ?? 0, authorityCount ?? 0, additionalsCount ?? 0);

        for (int i = 0; i < questionCount; i++)
            questionWriter?.Invoke(this, i);

        for (int i = 0; i < answerCount; i++)
            answerWriter?.Invoke(this, i);

        for (int i = 0; i < authorityCount; i++)
            authorityWriter?.Invoke(this, i);

        for (int i = 0; i < additionalsCount; i++)
            additionalWriter?.Invoke(this, i);
    }

    public void WriteHeader(DnsPacketHeader header, int questionCount, int answerCount, int authorityCount, int additionalsCount)
    {
        Write(header.Identifier);

        ushort flags = 0;
        flags |= (ushort)((ushort)header.QueryResponse << 15);
        flags |= (ushort)((ushort)header.Opcode << 11);
        flags |= (ushort)(header.AuthorativeAnswer ? 1 << 10 : 0);
        flags |= (ushort)(header.Truncated ? 1 << 9 : 0);
        flags |= (ushort)(header.RecursionDesired ? 1 << 8 : 0);
        flags |= (ushort)(header.RecursionAvailable ? 1 << 7 : 0);
        flags |= (ushort)(header.AnswerAuthenticated ? 1 << 5 : 0);
        flags |= (ushort)(header.NonAuthenticatedData ? 1 << 4 : 0);
        flags |= (ushort)header.ResponseCode;
        Write(flags);

        Write((ushort)questionCount);
        Write((ushort)answerCount);
        Write((ushort)authorityCount);
        Write((ushort)additionalsCount);
    }

    public void WriteDomainName(string name)
    {
        lock (_namePositions)
        {
            var labels = name.Split('.');
            foreach (var label in labels)
            {
                string nameAtOffset = name.Substring(name.IndexOf(label));
                if (_namePositions.TryGetValue(nameAtOffset, out var position))
                {
                    ushort pointer = (ushort)(0b11000000_00000000 | position);
                    Write(pointer);
                    return;
                }

                if (!string.IsNullOrEmpty(label))
                {
                    var labelBytes = Encoding.UTF8.GetBytes(label);
                    int nameStartPos = _data.Count;
                    _data.Add((byte)labelBytes.Length);
                    _data.AddRange(labelBytes);
                    _namePositions[nameAtOffset] = nameStartPos;
                }
            }

            _data.Add(0);
        }
    }

    public void Write(DnsResourceRecord value, Action<DnsWriter> dataWriter)
    {
        WriteDomainName(value.Name);
        Write((ushort)value.Type);
        ushort cls = (ushort)(((value.CacheFlush ? 1u : 0u) << 15) | (ushort)value.Class);
        Write(cls);
        Write(value.TimeToLive);

        int lengthOffset = _data.Count;
        Write((ushort)0);
        dataWriter(this);
        int rdLength = _data.Count - lengthOffset - 2;
        Span<byte> rdLengthBytes = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16BigEndian(rdLengthBytes, (ushort)rdLength);
        _data[lengthOffset] = rdLengthBytes[0];
        _data[lengthOffset + 1] = rdLengthBytes[1];
    }

    public void Write(DnsQuestion value)
    {
        WriteDomainName(value.Name);
        Write((ushort)value.Type);
        Write((ushort)((ushort)((value.QueryUnicast ? 1u : 0u) << 15) | (ushort)value.Class));
    }

    public void Write(double value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(double)];
        BinaryPrimitives.WriteDoubleBigEndian(bytes, value);
        Write(bytes);
    }

    public void Write(short value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(short)];
        BinaryPrimitives.WriteInt16BigEndian(bytes, value);
        Write(bytes);
    }

    public void Write(int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        Write(bytes);
    }

    public void Write(long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        Write(bytes);
    }

    public void Write(float value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(float)];
        BinaryPrimitives.WriteSingleBigEndian(bytes, value);
        Write(bytes);
    }

    public void Write(byte value)
    {
        _data.Add(value);
    }

    public void Write(byte[] value)
    {
        _data.AddRange(value);
    }

    public void Write(byte[] value, int offset, int length)
    {
        _data.AddRange(value.AsSpan().Slice(offset, length));
    }

    public void Write(ReadOnlySpan<byte> value)
    {
        _data.AddRange(value);
    }

    public void Write(ushort value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        Write(bytes);
    }

    public void Write(uint value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        Write(bytes);
    }

    public void Write(ulong value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        Write(bytes);
    }

    public void Write(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Write((byte)bytes.Length);
        Write(bytes);
    }

    public void Write(PTRRecord value)
    {
        WriteDomainName(value.DomainName);
    }

    public void Write(ARecord value)
    {
        var bytes = value.Address.GetAddressBytes();
        if (bytes.Length != 4)
            throw new Exception("Unexpected amount of address bytes.");
        Write(bytes);
    }

    public void Write(AAAARecord value)
    {
        var bytes = value.Address.GetAddressBytes();
        if (bytes.Length != 16)
            throw new Exception("Unexpected amount of address bytes.");
        Write(bytes);
    }

    public void Write(TXTRecord value)
    {
        foreach (var t in value.Texts)
        {
            var bytes = Encoding.UTF8.GetBytes(t);
            Write((byte)bytes.Length);
            Write(bytes);
        }
    }

    public void Write(SRVRecord value)
    {
        Write(value.Priority);
        Write(value.Weight);
        Write(value.Port);
        WriteDomainName(value.Target);
    }

    public void Write(NSECRecord value)
    {
        WriteDomainName(value.OwnerName);

        foreach (var typeBitMap in value.TypeBitMaps)
        {
            Write(typeBitMap.WindowBlock);
            Write((byte)typeBitMap.Bitmap.Length);
            Write(typeBitMap.Bitmap);
        }
    }

    public void Write(OPTRecord value)
    {
        foreach (var option in value.Options)
        {
            Write(option.Code);
            Write((ushort)option.Data.Length);
            Write(option.Data);
        }
    }
}