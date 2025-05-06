// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// SyncShared.SyncKeyPair

namespace SyncShared;

public class SyncKeyPair
{
    public SyncKeyPair(int version, string publicKey, string privateKey)
    {
        PublicKey = publicKey;
        PrivateKey = privateKey;
        Version = version;
    }

    public string PublicKey { get; set; }

    public string PrivateKey { get; set; }

    public int Version { get; set; }
}