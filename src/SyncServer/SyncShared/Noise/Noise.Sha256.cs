// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// Noise.Sha256

using System.Runtime.InteropServices;

namespace Noise;

internal sealed class Sha256 : Hash, IDisposable
{
    private readonly nint state = Marshal.AllocHGlobal(104);

    private bool disposed;

    public Sha256()
    {
        Reset();
    }

    public int HashLen => 32;

    public int BlockLen => 64;

    public void AppendData(ReadOnlySpan<byte> data)
    {
        if (!data.IsEmpty)
            Libsodium.crypto_hash_sha256_update(state, ref MemoryMarshal.GetReference(data), (ulong)data.Length);
    }

    public void GetHashAndReset(Span<byte> hash)
    {
        Libsodium.crypto_hash_sha256_final(state, ref MemoryMarshal.GetReference(hash));
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
        Libsodium.crypto_hash_sha256_init(state);
    }
}