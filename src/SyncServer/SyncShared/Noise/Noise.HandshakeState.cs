// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// Noise.HandshakeState

namespace Noise;

public interface HandshakeState : IDisposable
{
    ReadOnlySpan<byte> RemoteStaticPublicKey { get; }

    void Fallback(Protocol protocol, ProtocolConfig config);

    (int BytesWritten, byte[]? HandshakeHash, Transport? Transport) WriteMessage(ReadOnlySpan<byte> payload,
        Span<byte> messageBuffer);

    (int BytesRead, byte[]? HandshakeHash, Transport? Transport) ReadMessage(ReadOnlySpan<byte> message,
        Span<byte> payloadBuffer);
}