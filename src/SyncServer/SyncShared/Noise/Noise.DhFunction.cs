// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// Noise.DhFunction

namespace Noise;

public sealed class DhFunction
{
    public static readonly DhFunction Curve25519 = new("25519");

    private readonly string name;

    private DhFunction(string name)
    {
        this.name = name;
    }

    public override string ToString()
    {
        return name;
    }

    internal static DhFunction Parse(ReadOnlySpan<char> s)
    {
        if (s.SequenceEqual(Curve25519.name.AsSpan())) return Curve25519;
        throw new ArgumentException("Unknown DH function.", "s");
    }
}