// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// Noise.CipherState<CipherType>

namespace Noise;

internal sealed class CipherState<CipherType> : IDisposable where CipherType : Cipher, new()
{
    private const ulong MaxNonce = ulong.MaxValue;

    private static readonly byte[] zeroLen = new byte[0];

    private static readonly byte[] zeros = new byte[32];

    private readonly CipherType cipher = new();

    private bool disposed;

    private byte[]? k;

    private ulong n;

    public void Dispose()
    {
        if (!disposed)
        {
            Utilities.ZeroMemory(k);
            disposed = true;
        }
    }

    public void InitializeKey(ReadOnlySpan<byte> key)
    {
        k = k ?? new byte[32];
        key.CopyTo(k);
        n = 0uL;
    }

    public bool HasKey()
    {
        return k != null;
    }

    public void SetNonce(ulong nonce)
    {
        n = nonce;
    }

    public int EncryptWithAd(ReadOnlySpan<byte> ad, ReadOnlySpan<byte> plaintext, Span<byte> ciphertext)
    {
        if (n == ulong.MaxValue) throw new OverflowException("Nonce has reached its maximum value.");
        if (k == null)
        {
            plaintext.CopyTo(ciphertext);
            return plaintext.Length;
        }

        return cipher.Encrypt(k, n++, ad, plaintext, ciphertext);
    }

    public int DecryptWithAd(ReadOnlySpan<byte> ad, ReadOnlySpan<byte> ciphertext, Span<byte> plaintext)
    {
        if (n == ulong.MaxValue) throw new OverflowException("Nonce has reached its maximum value.");
        if (k == null)
        {
            ciphertext.CopyTo(plaintext);
            return ciphertext.Length;
        }

        var result = cipher.Decrypt(k, n, ad, ciphertext, plaintext);
        n++;
        return result;
    }

    public void Rekey()
    {
        Span<byte> ciphertext = stackalloc byte[48];
        cipher.Encrypt(k, ulong.MaxValue, zeroLen, zeros, ciphertext);
        k = k ?? new byte[32];
        ciphertext.Slice(32).CopyTo(k);
    }
}