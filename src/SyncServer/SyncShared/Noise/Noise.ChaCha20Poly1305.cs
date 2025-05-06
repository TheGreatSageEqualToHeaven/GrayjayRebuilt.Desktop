// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// Noise.ChaCha20Poly1305

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Noise;

internal sealed class ChaCha20Poly1305 : Cipher
{
    public int Encrypt(ReadOnlySpan<byte> k, ulong n, ReadOnlySpan<byte> ad, ReadOnlySpan<byte> plaintext,
        Span<byte> ciphertext)
    {
        Span<byte> span = stackalloc byte[12];
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(4), n);
        if (Libsodium.crypto_aead_chacha20poly1305_ietf_encrypt(ref MemoryMarshal.GetReference(ciphertext),
                out var clen_p, ref MemoryMarshal.GetReference(plaintext), plaintext.Length,
                ref MemoryMarshal.GetReference(ad), ad.Length, IntPtr.Zero, ref MemoryMarshal.GetReference(span),
                ref MemoryMarshal.GetReference(k)) != 0) throw new CryptographicException("Encryption failed.");
        return (int)clen_p;
    }

    public int Decrypt(ReadOnlySpan<byte> k, ulong n, ReadOnlySpan<byte> ad, ReadOnlySpan<byte> ciphertext,
        Span<byte> plaintext)
    {
        Span<byte> span = stackalloc byte[12];
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(4), n);
        if (Libsodium.crypto_aead_chacha20poly1305_ietf_decrypt(ref MemoryMarshal.GetReference(plaintext),
                out var mlen_p, IntPtr.Zero, ref MemoryMarshal.GetReference(ciphertext), ciphertext.Length,
                ref MemoryMarshal.GetReference(ad), ad.Length, ref MemoryMarshal.GetReference(span),
                ref MemoryMarshal.GetReference(k)) != 0) throw new CryptographicException("Decryption failed.");
        return (int)mlen_p;
    }
}