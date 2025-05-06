// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// Noise.Sha512

using System.Runtime.InteropServices;

namespace Noise;

internal sealed class Sha512 : Hash, IDisposable
{
    private readonly nint state = Marshal.AllocHGlobal(208);

    private bool disposed;

    public Sha512()
    {
        Reset();
    }

    public int HashLen => 64;

    public int BlockLen => 128;

    public void AppendData(ReadOnlySpan<byte> data)
    {
        if (!data.IsEmpty)
            Libsodium.crypto_hash_sha512_update(state, ref MemoryMarshal.GetReference(data), (ulong)data.Length);
    }

    public void GetHashAndReset(Span<byte> hash)
    {
        Libsodium.crypto_hash_sha512_final(state, ref MemoryMarshal.GetReference(hash));
        Reset();
    }

    public void Dispose()
    {
        if (!disposed)
        {
            Marshal.FreeHGlobal(state);
            disposed = true;
        }
    }

    private void Reset()
    {
        Libsodium.crypto_hash_sha512_init(state);
    }
}