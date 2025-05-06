// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// Noise.Curve25519

using System.Runtime.InteropServices;

namespace Noise;

internal sealed class Curve25519 : Dh
{
    public int DhLen => 32;

    public KeyPair GenerateKeyPair()
    {
        var randomBytes = Utilities.GetRandomBytes(DhLen);
        var array = new byte[DhLen];
        Libsodium.crypto_scalarmult_curve25519_base(array, randomBytes);
        return new KeyPair(randomBytes, array);
    }

    public KeyPair GenerateKeyPair(ReadOnlySpan<byte> privateKey)
    {
        var array = privateKey.ToArray();
        var array2 = new byte[DhLen];
        Libsodium.crypto_scalarmult_curve25519_base(array2, array);
        return new KeyPair(array, array2);
    }

    public void Dh(KeyPair keyPair, ReadOnlySpan<byte> publicKey, Span<byte> sharedKey)
    {
        Libsodium.crypto_scalarmult_curve25519(ref MemoryMarshal.GetReference(sharedKey),
            ref MemoryMarshal.GetReference(keyPair.PrivateKey.AsSpan()), ref MemoryMarshal.GetReference(publicKey));
    }
}