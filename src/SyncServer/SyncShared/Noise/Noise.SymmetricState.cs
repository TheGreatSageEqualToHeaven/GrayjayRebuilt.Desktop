// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// Noise.SymmetricState<CipherType,DhType,HashType>

namespace Noise;

internal sealed class SymmetricState<CipherType, DhType, HashType> : IDisposable where CipherType : Cipher, new()
    where DhType : Dh, new()
    where HashType : Hash, new()
{
    private readonly Cipher cipher = new CipherType();

    private readonly byte[] ck;

    private readonly DhType dh = new();

    private readonly byte[] h;

    private readonly Hash hash = new HashType();

    private readonly Hkdf<HashType> hkdf = new();

    private readonly CipherState<CipherType> state = new();

    private bool disposed;

    public SymmetricState(ReadOnlySpan<byte> protocolName)
    {
        var hashLen = hash.HashLen;
        ck = new byte[hashLen];
        h = new byte[hashLen];
        if (protocolName.Length <= hashLen)
        {
            protocolName.CopyTo(h);
        }
        else
        {
            hash.AppendData(protocolName);
            hash.GetHashAndReset(h);
        }

        Array.Copy(h, ck, hashLen);
    }

    public void Dispose()
    {
        if (!disposed)
        {
            hash.Dispose();
            hkdf.Dispose();
            state.Dispose();
            Utilities.ZeroMemory(ck);
            disposed = true;
        }
    }

    public void MixKey(ReadOnlySpan<byte> inputKeyMaterial)
    {
        _ = inputKeyMaterial.Length;
        Span<byte> output = stackalloc byte[2 * hash.HashLen];
        hkdf.ExtractAndExpand2(ck, inputKeyMaterial, output);
        output.Slice(0, hash.HashLen).CopyTo(ck);
        var span = output.Slice(hash.HashLen, 32);
        state.InitializeKey(span);
    }

    public void MixHash(ReadOnlySpan<byte> data)
    {
        hash.AppendData(h);
        hash.AppendData(data);
        hash.GetHashAndReset(h);
    }

    public void MixKeyAndHash(ReadOnlySpan<byte> inputKeyMaterial)
    {
        _ = inputKeyMaterial.Length;
        Span<byte> output = stackalloc byte[3 * hash.HashLen];
        hkdf.ExtractAndExpand3(ck, inputKeyMaterial, output);
        output.Slice(0, hash.HashLen).CopyTo(ck);
        var span = output.Slice(hash.HashLen, hash.HashLen);
        var span2 = output.Slice(2 * hash.HashLen, 32);
        MixHash(span);
        state.InitializeKey(span2);
    }

    public byte[] GetHandshakeHash()
    {
        return h;
    }

    public int EncryptAndHash(ReadOnlySpan<byte> plaintext, Span<byte> ciphertext)
    {
        var num = state.EncryptWithAd(h, plaintext, ciphertext);
        MixHash(ciphertext.Slice(0, num));
        return num;
    }

    public int DecryptAndHash(ReadOnlySpan<byte> ciphertext, Span<byte> plaintext)
    {
        var result = state.DecryptWithAd(h, ciphertext, plaintext);
        MixHash(ciphertext);
        return result;
    }

    public (CipherState<CipherType> c1, CipherState<CipherType> c2) Split()
    {
        Span<byte> output = stackalloc byte[2 * hash.HashLen];
        hkdf.ExtractAndExpand2(ck, null, output);
        var span = output.Slice(0, 32);
        var span2 = output.Slice(hash.HashLen, 32);
        var cipherState = new CipherState<CipherType>();
        var cipherState2 = new CipherState<CipherType>();
        cipherState.InitializeKey(span);
        cipherState2.InitializeKey(span2);
        return (c1: cipherState, c2: cipherState2);
    }

    public bool HasKey()
    {
        return state.HasKey();
    }
}