// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// Noise.KeyPair

namespace Noise;

public sealed class KeyPair : IDisposable
{
    private static readonly Curve25519 dh = new();

    private readonly byte[] privateKey;

    private readonly byte[] publicKey;

    private bool disposed;

    public KeyPair(byte[] privateKey, byte[] publicKey)
    {
        Exceptions.ThrowIfNull(privateKey, "privateKey");
        Exceptions.ThrowIfNull(publicKey, "publicKey");
        if (privateKey.Length != 32)
            throw new ArgumentException("Private key must have length of 32 bytes.", "privateKey");
        if (publicKey.Length != 32)
            throw new ArgumentException("Public key must have length of 32 bytes.", "publicKey");
        this.privateKey = privateKey;
        this.publicKey = publicKey;
    }

    public byte[] PrivateKey
    {
        get
        {
            Exceptions.ThrowIfDisposed(disposed, "KeyPair");
            return privateKey;
        }
    }

    public byte[] PublicKey
    {
        get
        {
            Exceptions.ThrowIfDisposed(disposed, "KeyPair");
            return publicKey;
        }
    }

    public void Dispose()
    {
        if (!disposed)
        {
            Utilities.ZeroMemory(privateKey);
            disposed = true;
        }
    }

    public static KeyPair Generate()
    {
        return dh.GenerateKeyPair();
    }
}