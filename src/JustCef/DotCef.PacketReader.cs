// DotCef, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// DotCef.PacketReader

using System.Text;

namespace DotCef;

public class PacketReader
{
    private readonly byte[] _data;

    private int _position;

    public PacketReader(byte[] data)
        : this(data, data.Length)
    {
    }

    public PacketReader(byte[] data, int size)
    {
        if (size > data.Length) throw new ArgumentException("Size must be less than data size.");
        _data = data;
        _position = 0;
    }

    public int RemainingSize => _data.Length - _position;

    public unsafe T Read<T>() where T : unmanaged
    {
        var num = sizeof(T);
        if (_position + num > _data.Length)
            throw new InvalidOperationException("Reading past the end of the data buffer.");
        T result;
        fixed (byte* ptr = &_data[_position])
        {
            result = *(T*)ptr;
        }

        _position += num;
        return result;
    }

    public string ReadString(int size)
    {
        if (_position + size > _data.Length)
            throw new InvalidOperationException("Reading past the end of the data buffer.");
        var result = Encoding.UTF8.GetString(_data, _position, size);
        _position += size;
        return result;
    }

    public byte[] ReadBytes(int size)
    {
        if (_position + size > _data.Length)
            throw new InvalidOperationException("Reading past the end of the data buffer.");
        var span = _data.AsSpan();
        span = span.Slice(_position, size);
        var result = span.ToArray();
        _position += size;
        return result;
    }

    public string? ReadSizePrefixedString()
    {
        var num = Read<int>();
        if (num == -1) return null;
        return ReadString(num);
    }

    public void Skip(int size)
    {
        if (_position + size > _data.Length)
            throw new InvalidOperationException("Skipping past the end of the data buffer.");
        _position += size;
    }

    public bool HasAvailable(int size)
    {
        return _position + size <= _data.Length;
    }
}