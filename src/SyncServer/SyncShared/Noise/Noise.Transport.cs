// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// Noise.Transport

namespace Noise;

public interface Transport : IDisposable
{
    bool IsOneWay { get; }

    int WriteMessage(ReadOnlySpan<byte> payload, Span<byte> messageBuffer);

    int ReadMessage(ReadOnlySpan<byte> message, Span<byte> payloadBuffer);

    void RekeyInitiatorToResponder();

    void RekeyResponderToInitiator();
}