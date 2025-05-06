// SyncClient, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// SyncClient.ChannelRelayed

using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Noise;
using SyncShared;

namespace SyncClient;

public class ChannelRelayed : IChannel, IDisposable
{
    private readonly KeyPair _localKeyPair;

    private readonly SyncSocketSession _session;
    private readonly object _decryptLock = new();

    private bool _disposed;

    private HandshakeState? _handshakeState;

    private Action<IChannel>? _onClose;

    private Action<SyncSocketSession, IChannel, Opcode, byte, ReadOnlySpan<byte>>? _onData;

    private Transport? _transport;

    public ChannelRelayed(SyncSocketSession session, KeyPair localKeyPair, string publicKey, bool initiator)
    {
        _session = session;
        _localKeyPair = localKeyPair;
        HandshakeState handshakeState;
        if (!initiator)
        {
            var protocol = Constants.Protocol;
            var privateKey = _localKeyPair.PrivateKey;
            handshakeState = protocol.Create(initiator, default, privateKey);
        }
        else
        {
            var protocol2 = Constants.Protocol;
            var privateKey = _localKeyPair.PrivateKey;
            var rs = Convert.FromBase64String(publicKey);
            handshakeState = protocol2.Create(initiator, default, privateKey, rs);
        }

        _handshakeState = handshakeState;
        RemotePublicKey = publicKey;
    }

    private SemaphoreSlim _sendSemaphore { get; } = new(1);

    public bool IsAuthorized => Authorizable?.IsAuthorized ?? false;

    public long ConnectionId { get; set; }

    public IAuthorizable? Authorizable { get; set; }

    public string? RemotePublicKey { get; private set; }

    public int? RemoteVersion { get; private set; }

    public object? SyncSession { get; set; }

    public LinkType LinkType => LinkType.Relayed;

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
        _disposed = true;
        var connectionId = ConnectionId;
        if (connectionId != 0L)
            Task.Run(async delegate
            {
                try
                {
                    await _session.SendRelayError(connectionId, RelayErrorCode.ConnectionClosed);
                }
                catch (Exception ex)
                {
                    Logger.Error<SyncSocketSession>("Exception while sending relay error.", ex);
                }
            });

        _sendSemaphore.Dispose();
        _transport?.Dispose();
        _transport = null;
        _handshakeState?.Dispose();
        _handshakeState = null;
        _onClose?.Invoke(this);
    }

    public async Task SendAsync(Opcode opcode, byte subOpcode, byte[]? data = null, int offset = 0, int count = -1,
        ContentEncoding contentEncoding = ContentEncoding.Raw,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (count == -1) count = data != null ? data.Length : 0;

        if (count != 0 && data == null) throw new Exception("Data must be set if count is not 0");

        var processedData = data;
        if (data != null && contentEncoding == ContentEncoding.Gzip)
        {
            if (opcode == Opcode.DATA)
            {
                using var compressedStream = new MemoryStream();
                using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Compress))
                {
                    await gzipStream.WriteAsync(data.AsMemory(offset, count), cancellationToken);
                }

                processedData = compressedStream.ToArray();
                count = processedData.Length;
                offset = 0;
            }
            else
            {
                Logger.Warning<SyncSocketSession>(
                    $"Gzip requested but not supported on this (opcode = {opcode}, subOpcode = {subOpcode}), falling back.");
                contentEncoding = ContentEncoding.Raw;
            }
        }

        if (count > 65472 && processedData != null)
        {
            var streamId = _session.GenerateStreamId();
            var totalSize = count;
            int bytesToSend;
            for (var sendOffset = 0; sendOffset < totalSize; sendOffset += bytesToSend)
            {
                var num = totalSize - sendOffset;
                bytesToSend = Math.Min(65461, num);
                StreamOpcode streamOpcode;
                byte[] array;
                if (sendOffset == 0)
                {
                    streamOpcode = StreamOpcode.START;
                    array = new byte[11 + bytesToSend];
                    BinaryPrimitives.WriteInt32LittleEndian(array.AsSpan(0, 4), streamId);
                    BinaryPrimitives.WriteInt32LittleEndian(array.AsSpan(4, 4), totalSize);
                    array[8] = (byte)opcode;
                    array[9] = subOpcode;
                    array[10] = (byte)contentEncoding;
                    Array.Copy(processedData, offset + sendOffset, array, 11, bytesToSend);
                }
                else
                {
                    array = new byte[8 + bytesToSend];
                    BinaryPrimitives.WriteInt32LittleEndian(array.AsSpan(0, 4), streamId);
                    BinaryPrimitives.WriteInt32LittleEndian(array.AsSpan(4, 4), sendOffset);
                    Array.Copy(processedData, offset + sendOffset, array, 8, bytesToSend);
                    streamOpcode = bytesToSend < num ? StreamOpcode.DATA : StreamOpcode.END;
                }

                var array2 = new byte[7 + array.Length];
                BinaryPrimitives.WriteInt32LittleEndian(array2.AsSpan(0, 4), array.Length + 3);
                array2[4] = 3;
                array2[5] = (byte)streamOpcode;
                array2[6] = 0;
                Array.Copy(array, 0, array2, 7, array.Length);
                await SendPacketAsync(array2, cancellationToken);
            }
        }
        else
        {
            var array3 = new byte[7 + count];
            BinaryPrimitives.WriteInt32LittleEndian(array3.AsSpan(0, 4), count + 7 - 4);
            array3[4] = (byte)opcode;
            array3[5] = subOpcode;
            array3[6] = (byte)contentEncoding;
            if (count > 0 && processedData != null) Array.Copy(processedData, offset, array3, 7, count);

            await SendPacketAsync(array3, cancellationToken);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException("ChannelRelayed");
    }

    public void InvokeDataHandler(Opcode opcode, byte subOpcode, ReadOnlySpan<byte> data)
    {
        _onData?.Invoke(_session, this, opcode, subOpcode, data);
    }

    private void CompleteHandshake(int remoteVersion, Transport transport)
    {
        ThrowIfDisposed();
        RemoteVersion = remoteVersion;
        RemotePublicKey = Convert.ToBase64String(_handshakeState.RemoteStaticPublicKey);
        _handshakeState.Dispose();
        _handshakeState = null;
        _transport = transport;
        Logger.Info<SyncSocketSession>($"Completed handshake for connectionId {ConnectionId}");
    }

    private async Task SendPacketAsync(byte[] packet,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _sendSemaphore.WaitAsync(cancellationToken);
        try
        {
            var array = new byte[packet.Length + 16];
            var num = _transport.WriteMessage(packet, array);
            var array2 = new byte[8 + num];
            BinaryPrimitives.WriteInt64LittleEndian(array2.AsSpan(0, 8), ConnectionId);
            Array.Copy(array, 0, array2, 8, num);
            await _session.SendAsync(Opcode.RELAY, 0, array2, 0, -1, ContentEncoding.Raw, cancellationToken);
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }

    public async Task SendErrorAsync(RelayErrorCode errorCode,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _sendSemaphore.WaitAsync(cancellationToken);
        try
        {
            Span<byte> span = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(span, (int)errorCode);
            var array = new byte[20];
            var num = _transport.WriteMessage(span, array);
            var array2 = new byte[8 + num];
            BinaryPrimitives.WriteInt64LittleEndian(array2.AsSpan(0, 8), ConnectionId);
            Array.Copy(array, 0, array2, 8, num);
            await _session.SendAsync(Opcode.RELAY, 2, array2, 0, -1, ContentEncoding.Raw, cancellationToken);
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }

    public async Task SendRequestTransportAsync(int requestId, string publicKey, uint appId,
        string? pairingCode = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _sendSemaphore.WaitAsync();
        try
        {
            var array = new byte[1024];
            var item = _handshakeState.WriteMessage(null, array).BytesWritten;
            var array2 = Convert.FromBase64String(publicKey);
            if (array2.Length != 32) throw new ArgumentException("Public key must be 32 bytes.");

            int num;
            byte[] source;
            if (pairingCode != null)
            {
                var protocol = new Protocol(HandshakePattern.N, CipherFunction.ChaChaPoly,
                    HashFunction.Blake2b);
                var rs = array2;
                using var handshakeState =
                    protocol.Create(true, default, null, rs);
                var bytes = Encoding.UTF8.GetBytes(pairingCode);
                if (bytes.Length > 32) throw new ArgumentException("Pairing code must not exceed 32 bytes.");

                var array3 = new byte[1024];
                var item2 = handshakeState.WriteMessage(bytes, array3).BytesWritten;
                num = item2;
                source = array3.AsSpan(0, item2).ToArray();
            }
            else
            {
                num = 0;
                source = Array.Empty<byte>();
            }

            var array4 = new byte[44 + num + 4 + item];
            var num2 = 0;
            BinaryPrimitives.WriteInt32LittleEndian(array4.AsSpan(num2, 4), requestId);
            num2 += 4;
            BinaryPrimitives.WriteUInt32LittleEndian(array4.AsSpan(num2, 4), appId);
            num2 += 4;
            array2.CopyTo(array4.AsSpan(num2, 32));
            num2 += 32;
            BinaryPrimitives.WriteInt32LittleEndian(array4.AsSpan(num2, 4), num);
            num2 += 4;
            if (num > 0)
            {
                source.CopyTo(array4.AsSpan(num2));
                num2 += num;
            }

            BinaryPrimitives.WriteInt32LittleEndian(array4.AsSpan(num2, 4), item);
            num2 += 4;
            array.AsSpan(0, item).CopyTo(array4.AsSpan(num2));
            await _session.SendAsync(Opcode.REQUEST, 1, array4, 0, -1, ContentEncoding.Raw, cancellationToken);
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }

    public async Task SendResponseTransportAsync(int remoteVersion, int requestId, byte[] handshakeMessage)
    {
        ThrowIfDisposed();
        var array = new byte[1024];
        var array2 = new byte[1024];
        _handshakeState.ReadMessage(handshakeMessage, array2);
        var tuple =
            _handshakeState.WriteMessage(null, array);
        var item = tuple.BytesWritten;
        var item2 = tuple.Transport;
        var array3 = new byte[20 + item];
        BinaryPrimitives.WriteInt32LittleEndian(array3.AsSpan(0, 4), 0);
        BinaryPrimitives.WriteInt64LittleEndian(array3.AsSpan(4, 8), ConnectionId);
        BinaryPrimitives.WriteInt32LittleEndian(array3.AsSpan(12, 4), requestId);
        BinaryPrimitives.WriteInt32LittleEndian(array3.AsSpan(16, 4), item);
        array.AsSpan(0, item).CopyTo(array3.AsSpan(20));
        CompleteHandshake(remoteVersion, item2);
        await _session.SendAsync(Opcode.RESPONSE, 1, array3);
    }

    public (byte[] decryptedPayload, int length) Decrypt(ReadOnlySpan<byte> encryptedPayload)
    {
        ThrowIfDisposed();
        lock (_decryptLock)
        {
            var array = new byte[encryptedPayload.Length - 16];
            var num = _transport.ReadMessage(encryptedPayload, array);
            if (num != array.Length) throw new Exception($"Expected decrypted payload length to be {num}");

            return (decryptedPayload: array, length: num);
        }
    }

    public void HandleTransportRelayed(int remoteVersion, long connectionId, byte[] handshakeMessage)
    {
        ThrowIfDisposed();
        lock (_decryptLock)
        {
            ConnectionId = connectionId;
            var array = new byte[1024];
            var item = _handshakeState.ReadMessage(handshakeMessage, array).Transport;
            CompleteHandshake(remoteVersion, item);
        }
    }
}