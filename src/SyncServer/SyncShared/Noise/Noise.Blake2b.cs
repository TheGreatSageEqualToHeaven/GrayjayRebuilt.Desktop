// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// Noise.Blake2b

using System.Runtime.InteropServices;

namespace Noise;

internal sealed class Blake2b : Hash, IDisposable
{
    private readonly nint aligned;
    private readonly nint raw;

    private bool disposed;

    public Blake2b()
    {
        var num = 361;
        var num2 = 64;
        raw = Marshal.AllocHGlobal(num + num2 - 1);
        aligned = Utilities.Align(raw, num2);
        Reset();
    }

    public int HashLen => 64;

    public int BlockLen => 128;

    public void AppendData(ReadOnlySpan<byte> data)
    {
        if (!data.IsEmpty)
            Libsodium.crypto_generichash_blake2b_update(aligned, ref MemoryMarshal.GetReference(data),
                (ulong)data.Length);
    }

    public void GetHashAndReset(Span<byte> hash)
    {
        Libsodium.crypto_generichash_blake2b_final(aligned, ref MemoryMarshal.GetReference(hash), (nuint)hash.Length);
        Reset();
    }

    public void Dispose()
    {
        if (!disposed)
        {
            Marshal.FreeHGlobal(raw);
            disposed = true;
        }
    }

    private void Reset()
    {
        Libsodium.crypto_generichash_blake2b_init(aligned, null, UIntPtr.Zero, (nuint)HashLen);
    }
}