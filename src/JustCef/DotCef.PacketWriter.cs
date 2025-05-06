// DotCef, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// DotCef.PacketWriter

using System.Text;

namespace DotCef;

public class PacketWriter
{
    private byte[] _data;

    private readonly int _maxSize;

    public PacketWriter(int maxSize = 10485760)
    {
        _maxSize = maxSize;
        _data = new byte[Math.Min(maxSize, 512)];
        Size = 0;
    }

    public byte[] Data => _data;

    public int Size { get; private set; }

    public unsafe PacketWriter Write<T>(T value) where T : unmanaged
    {
        var num = sizeof(T);
        EnsureCapacity(Size + num);
        fixed (byte* ptr = &_data[Size])
        {
            *(T*)ptr = value;
        }

        Size += num;
        return this;
    }

    public PacketWriter WriteSizePrefixedString(string? str)
    {
        if (str == null)
        {
            Write(-1);
        }
        else
        {
            var bytes = Encoding.UTF8.GetBytes(str);
            Write(bytes.Length);
            WriteBytes(bytes);
        }

        return this;
    }

    public PacketWriter WriteString(string str)
    {
        var bytes = Encoding.UTF8.GetBytes(str);
        WriteBytes(bytes);
        return this;
    }

    public PacketWriter WriteBytes(byte[] data)
    {
        EnsureCapacity(Size + data.Length);
        Buffer.BlockCopy(data, 0, _data, Size, data.Length);
        Size += data.Length;
        return this;
    }

    public PacketWriter WriteBytes(byte[] data, int offset, int size)
    {
        EnsureCapacity(Size + size);
        Buffer.BlockCopy(data, offset, _data, Size, size);
        Size += size;
        return this;
    }

    private void EnsureCapacity(int requiredCapacity)
    {
        if (requiredCapacity > _data.Length)
        {
            var num = Math.Max(2 * _data.Length, requiredCapacity);
            if (num > _maxSize) throw new InvalidOperationException("Exceeding max buffer size.");
            Array.Resize(ref _data, num);
        }
    }
}