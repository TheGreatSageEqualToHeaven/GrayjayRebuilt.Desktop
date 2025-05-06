// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// Noise.CipherFunction

namespace Noise;

public sealed class CipherFunction
{
    public static readonly CipherFunction AesGcm = new("AESGCM");

    public static readonly CipherFunction ChaChaPoly = new("ChaChaPoly");

    private readonly string name;

    private CipherFunction(string name)
    {
        this.name = name;
    }

    public override string ToString()
    {
        return name;
    }

    internal static CipherFunction Parse(ReadOnlySpan<char> s)
    {
        if (s.SequenceEqual(AesGcm.name.AsSpan())) return AesGcm;
        if (s.SequenceEqual(ChaChaPoly.name.AsSpan())) return ChaChaPoly;
        throw new ArgumentException("Unknown cipher function.", "s");
    }
}