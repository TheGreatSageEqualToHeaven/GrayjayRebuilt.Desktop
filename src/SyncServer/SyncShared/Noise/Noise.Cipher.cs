// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// Noise.Cipher

internal interface Cipher
{
    int Encrypt(ReadOnlySpan<byte> k, ulong n, ReadOnlySpan<byte> ad, ReadOnlySpan<byte> plaintext,
        Span<byte> ciphertext);

    int Decrypt(ReadOnlySpan<byte> k, ulong n, ReadOnlySpan<byte> ad, ReadOnlySpan<byte> ciphertext,
        Span<byte> plaintext);
}