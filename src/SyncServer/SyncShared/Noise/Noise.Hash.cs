// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// Noise.Hash

namespace Noise;

internal interface Hash : IDisposable
{
    int HashLen { get; }

    int BlockLen { get; }

    void AppendData(ReadOnlySpan<byte> data);

    void GetHashAndReset(Span<byte> hash);
}