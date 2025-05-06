// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// Noise.Transport<CipherType>

namespace Noise;

internal sealed class Transport<CipherType> : Transport, IDisposable where CipherType : Cipher, new()
{
    private readonly CipherState<CipherType> c1;

    private readonly CipherState<CipherType>? c2;
    private readonly bool initiator;

    private bool disposed;

    public Transport(bool initiator, CipherState<CipherType> c1, CipherState<CipherType>? c2)
    {
        Exceptions.ThrowIfNull(c1, "c1");
        this.initiator = initiator;
        this.c1 = c1;
        this.c2 = c2;
    }

    public bool IsOneWay
    {
        get
        {
            Exceptions.ThrowIfDisposed(disposed, "Transport");
            return c2 == null;
        }
    }

    public int WriteMessage(ReadOnlySpan<byte> payload, Span<byte> messageBuffer)
    {
        Exceptions.ThrowIfDisposed(disposed, "Transport");
        if (!initiator && IsOneWay)
            throw new InvalidOperationException("Responder cannot write messages to a one-way stream.");
        if (payload.Length + 16 > 65535)
            throw new ArgumentException($"Noise message must be less than or equal to {65535} bytes in length.");
        if (payload.Length + 16 > messageBuffer.Length)
            throw new ArgumentException("Message buffer does not have enough space to hold the ciphertext.");
        return (initiator ? c1 : c2).EncryptWithAd(null, payload, messageBuffer);
    }

    public int ReadMessage(ReadOnlySpan<byte> message, Span<byte> payloadBuffer)
    {
        Exceptions.ThrowIfDisposed(disposed, "Transport");
        if (initiator && IsOneWay)
            throw new InvalidOperationException("Initiator cannot read messages from a one-way stream.");
        if (message.Length > 65535)
            throw new ArgumentException($"Noise message must be less than or equal to {65535} bytes in length.");
        if (message.Length < 16)
            throw new ArgumentException($"Noise message must be greater than or equal to {16} bytes in length.");
        if (message.Length - 16 > payloadBuffer.Length)
            throw new ArgumentException("Payload buffer does not have enough space to hold the plaintext.");
        return (initiator ? c2 : c1).DecryptWithAd(null, message, payloadBuffer);
    }

    public void RekeyInitiatorToResponder()
    {
        Exceptions.ThrowIfDisposed(disposed, "Transport");
        c1.Rekey();
    }

    public void RekeyResponderToInitiator()
    {
        Exceptions.ThrowIfDisposed(disposed, "Transport");
        if (IsOneWay) throw new InvalidOperationException("Cannot rekey responder to initiator in a one-way stream.");
        c2.Rekey();
    }

    public void Dispose()
    {
        if (!disposed)
        {
            c1.Dispose();
            c2?.Dispose();
            disposed = true;
        }
    }
}