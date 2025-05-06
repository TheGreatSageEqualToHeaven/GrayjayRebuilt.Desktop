// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// Noise.Exceptions

namespace Noise;

internal static class Exceptions
{
    public static void ThrowIfNull(object value, string name)
    {
        if (value == null) throw new ArgumentNullException(name);
    }

    public static void ThrowIfDisposed(bool disposed, string name)
    {
        if (disposed) throw new ObjectDisposedException(name);
    }
}