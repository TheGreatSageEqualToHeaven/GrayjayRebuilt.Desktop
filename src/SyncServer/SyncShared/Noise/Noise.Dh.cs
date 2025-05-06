// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// Noise.Dh

namespace Noise;

internal interface Dh
{
    int DhLen { get; }

    KeyPair GenerateKeyPair();

    KeyPair GenerateKeyPair(ReadOnlySpan<byte> privateKey);

    void Dh(KeyPair keyPair, ReadOnlySpan<byte> publicKey, Span<byte> sharedKey);
}