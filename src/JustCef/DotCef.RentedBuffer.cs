// DotCef, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// DotCef.RentedBuffer<T>

using System.Buffers;

namespace DotCef;

public struct RentedBuffer<T> : IDisposable
{
    private readonly ArrayPool<T> _pool;

    public readonly T[] Buffer;

    public readonly int Length;

    public RentedBuffer(ArrayPool<T> pool, int length)
    {
        _pool = pool;
        Buffer = pool.Rent(length);
        Length = length;
    }

    public void Dispose()
    {
        _pool.Return(Buffer);
    }
}