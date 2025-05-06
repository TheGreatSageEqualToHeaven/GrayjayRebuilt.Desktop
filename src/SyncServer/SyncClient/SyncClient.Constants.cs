// SyncClient, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// SyncClient.Constants

using Noise;

namespace SyncClient;

public static class Constants
{
    public static readonly Protocol
        Protocol = new(HandshakePattern.IK, CipherFunction.ChaChaPoly, HashFunction.Blake2b);
}