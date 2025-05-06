// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// Noise.ProtocolConfig

namespace Noise;

public sealed class ProtocolConfig
{
    public ProtocolConfig(bool initiator = false, byte[]? prologue = null, byte[]? s = null, byte[]? rs = null,
        IEnumerable<byte[]>? psks = null)
    {
        Initiator = initiator;
        Prologue = prologue;
        LocalStatic = s;
        RemoteStatic = rs;
        PreSharedKeys = psks;
    }

    public bool Initiator { get; set; }

    public byte[]? Prologue { get; set; }

    public byte[]? LocalStatic { get; set; }

    public byte[]? RemoteStatic { get; set; }

    public IEnumerable<byte[]>? PreSharedKeys { get; set; }
}