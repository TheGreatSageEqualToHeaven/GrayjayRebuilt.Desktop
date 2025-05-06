// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// Noise.Hkdf<HashType>

namespace Noise;

internal sealed class Hkdf<HashType> : IDisposable where HashType : Hash, new()
{
    private static readonly byte[] one = new byte[1] { 1 };

    private static readonly byte[] two = new byte[1] { 2 };

    private static readonly byte[] three = new byte[1] { 3 };

    private readonly HashType inner = new();

    private readonly HashType outer = new();

    private bool disposed;

    public void Dispose()
    {
        if (!disposed)
        {
            inner.Dispose();
            outer.Dispose();
            disposed = true;
        }
    }

    public void ExtractAndExpand2(ReadOnlySpan<byte> chainingKey, ReadOnlySpan<byte> inputKeyMaterial,
        Span<byte> output)
    {
        var hashLen = inner.HashLen;
        Span<byte> span = stackalloc byte[hashLen];
        HmacHash(chainingKey, span, inputKeyMaterial);
        var span2 = output.Slice(0, hashLen);
        HmacHash(span, span2, one);
        var hmac = output.Slice(hashLen, hashLen);
        HmacHash(span, hmac, span2, two);
    }

    public void ExtractAndExpand3(ReadOnlySpan<byte> chainingKey, ReadOnlySpan<byte> inputKeyMaterial,
        Span<byte> output)
    {
        var hashLen = inner.HashLen;
        Span<byte> span = stackalloc byte[hashLen];
        HmacHash(chainingKey, span, inputKeyMaterial);
        var span2 = output.Slice(0, hashLen);
        HmacHash(span, span2, one);
        var span3 = output.Slice(hashLen, hashLen);
        HmacHash(span, span3, span2, two);
        var hmac = output.Slice(2 * hashLen, hashLen);
        HmacHash(span, hmac, span3, three);
    }

    private void HmacHash(ReadOnlySpan<byte> key, Span<byte> hmac, ReadOnlySpan<byte> data1 = default,
        ReadOnlySpan<byte> data2 = default)
    {
        var blockLen = inner.BlockLen;
        Span<byte> span = stackalloc byte[blockLen];
        Span<byte> span2 = stackalloc byte[blockLen];
        key.CopyTo(span);
        key.CopyTo(span2);
        for (var i = 0; i < blockLen; i++)
        {
            span[i] ^= 54;
            span2[i] ^= 92;
        }

        inner.AppendData(span);
        inner.AppendData(data1);
        inner.AppendData(data2);
        inner.GetHashAndReset(hmac);
        outer.AppendData(span2);
        outer.AppendData(hmac);
        outer.GetHashAndReset(hmac);
    }
}