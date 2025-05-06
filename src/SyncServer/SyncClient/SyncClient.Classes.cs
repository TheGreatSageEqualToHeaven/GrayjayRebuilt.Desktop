using SyncShared;

namespace SyncClient;

public interface IAuthorizable
{
    bool IsAuthorized { get; }
}

public enum LinkType
{
    None,
    Direct,
    Relayed
}

public interface IChannel : IDisposable
{
    string? RemotePublicKey { get; }

    int? RemoteVersion { get; }

    IAuthorizable? Authorizable { get; set; }

    object? SyncSession { get; set; }

    LinkType LinkType { get; }

    void SetDataHandler(Action<SyncSocketSession, IChannel, Opcode, byte, ReadOnlySpan<byte>>? onData);

    Task SendAsync(Opcode opcode, byte subOpcode, byte[]? data = null, int offset = 0, int count = -1,
        ContentEncoding contentEncoding = ContentEncoding.Raw, CancellationToken cancellationToken = default);

    void SetCloseHandler(Action<IChannel>? onClose);
}