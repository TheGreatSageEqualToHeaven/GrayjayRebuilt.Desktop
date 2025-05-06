// SyncClient, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// SyncClient.ChannelSocket

using SyncShared;

namespace SyncClient;

public class ChannelSocket : IChannel, IDisposable
{
    private readonly SyncSocketSession _session;

    private Action<IChannel>? _onClose;

    private Action<SyncSocketSession, IChannel, Opcode, byte, ReadOnlySpan<byte>>? _onData;

    public ChannelSocket(SyncSocketSession session)
    {
        _session = session;
    }

    public string? RemotePublicKey => _session.RemotePublicKey;

    public int? RemoteVersion => _session.RemoteVersion;

    public LinkType LinkType => LinkType.Direct;

    public IAuthorizable? Authorizable
    {
        get => _session.Authorizable;
        set => _session.Authorizable = value;
    }

    public object? SyncSession { get; set; }

    public void SetDataHandler(Action<SyncSocketSession, IChannel, Opcode, byte, ReadOnlySpan<byte>>? onData)
    {
        _onData = onData;
    }

    public void SetCloseHandler(Action<IChannel>? onClose)
    {
        _onClose = onClose;
    }

    public void Dispose()
    {
        _session.Dispose();
        _onClose?.Invoke(this);
    }

    public async Task SendAsync(Opcode opcode, byte subOpcode, byte[]? data = null, int offset = 0, int count = -1,
        ContentEncoding contentEncoding = ContentEncoding.Raw, CancellationToken cancellationToken = default)
    {
        if (data != null)
            await _session.SendAsync(opcode, subOpcode, data, offset, count, contentEncoding, cancellationToken);
        else
            await _session.SendAsync(opcode, subOpcode, cancellationToken);
    }

    public void InvokeDataHandler(Opcode opcode, byte subOpcode, ReadOnlySpan<byte> data)
    {
        _onData?.Invoke(_session, this, opcode, subOpcode, data);
    }
}