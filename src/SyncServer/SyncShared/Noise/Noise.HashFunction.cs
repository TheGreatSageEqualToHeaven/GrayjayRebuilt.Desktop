// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// Noise.HashFunction

namespace Noise;

public sealed class HashFunction
{
    public static readonly HashFunction Sha256 = new("SHA256");

    public static readonly HashFunction Sha512 = new("SHA512");

    public static readonly HashFunction Blake2s = new("BLAKE2s");

    public static readonly HashFunction Blake2b = new("BLAKE2b");

    private readonly string name;

    private HashFunction(string name)
    {
        this.name = name;
    }

    public override string ToString()
    {
        return name;
    }

    internal static HashFunction Parse(ReadOnlySpan<char> s)
    {
        if (s.SequenceEqual(Sha256.name.AsSpan())) return Sha256;
        if (s.SequenceEqual(Sha512.name.AsSpan())) return Sha512;
        if (s.SequenceEqual(Blake2s.name.AsSpan())) return Blake2s;
        if (s.SequenceEqual(Blake2b.name.AsSpan())) return Blake2b;
        throw new ArgumentException("Unknown hash function.", "s");
    }
}