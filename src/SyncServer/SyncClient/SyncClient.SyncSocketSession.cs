// SyncClient, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// SyncClient.SyncSocketSession

using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using Noise;
using SyncShared;

namespace SyncClient;

public class SyncSocketSession : IDisposable
{
    private const int CURRENT_VERSION = 4;

    public const int MAXIMUM_PACKET_SIZE = 65519;

    public const int MAXIMUM_PACKET_SIZE_ENCRYPTED = 65535;

    public const int HEADER_SIZE = 7;

    private static readonly byte[] VERSION_BYTES = BitConverter.GetBytes(4);

    private readonly byte[] _buffer = new byte[65535];

    private readonly byte[] _bufferDecrypted = new byte[65519];

    private readonly ConcurrentDictionary<long, ChannelRelayed> _channels = new();

    private readonly Func<LinkType, SyncSocketSession, string, string?, uint, bool>? _isHandshakeAllowed;

    private readonly Action<SyncSocketSession, ChannelRelayed, bool>? _onChannelEstablished;

    private readonly Action<SyncSocketSession>? _onClose;

    private readonly Action<SyncSocketSession, Opcode, byte, ReadOnlySpan<byte>>? _onData;

    private readonly Action<SyncSocketSession>? _onHandshakeComplete;

    private readonly Action<SyncSocketSession, ChannelRelayed>? _onNewChannel;

    private readonly ConcurrentDictionary<int, TaskCompletionSource<Dictionary<string, ConnectionInfo>>>
        _pendingBulkConnectionInfoRequests = new();

    private readonly
        ConcurrentDictionary<int, TaskCompletionSource<Dictionary<string, (byte[] Data, DateTime Timestamp)>>>
        _pendingBulkGetRecordRequests = new();

    private readonly ConcurrentDictionary<int, (ChannelRelayed Channel, TaskCompletionSource<ChannelRelayed> Tcs)>
        _pendingChannels = new();

    private readonly ConcurrentDictionary<int, TaskCompletionSource<ConnectionInfo?>> _pendingConnectionInfoRequests =
        new();

    private readonly ConcurrentDictionary<int, TaskCompletionSource<bool>> _pendingDeleteRequests = new();

    private readonly ConcurrentDictionary<int, TaskCompletionSource<(byte[] EncryptedBlob, DateTime Timestamp)?>>
        _pendingGetRecordRequests = new();

    private readonly ConcurrentDictionary<int, TaskCompletionSource<List<(string Key, DateTime Timestamp)>>>
        _pendingListKeysRequests = new();

    private readonly ConcurrentDictionary<int, TaskCompletionSource<bool>> _pendingPublishRequests = new();

    private readonly byte[] _sendBuffer = new byte[65519];

    private readonly byte[] _sendBufferEncrypted = new byte[65539];

    private readonly SemaphoreSlim _sendSemaphore = new(1);
    private readonly Socket _socket;

    private readonly Dictionary<int, SyncStream> _syncStreams = new();

    private readonly KeyPair _localKeyPair;

    private int _requestIdGenerator;

    private bool _started;

    private int _streamIdGenerator;

    private Transport? _transport;

    public SyncSocketSession(string remoteAddress, KeyPair localKeyPair, Socket socket,
        Action<SyncSocketSession>? onClose = null, Action<SyncSocketSession>? onHandshakeComplete = null,
        Action<SyncSocketSession, Opcode, byte, ReadOnlySpan<byte>>? onData = null,
        Action<SyncSocketSession, ChannelRelayed>? onNewChannel = null,
        Func<LinkType, SyncSocketSession, string, string?, uint, bool>? isHandshakeAllowed = null,
        Action<SyncSocketSession, ChannelRelayed, bool>? onChannelEstablished = null)
    {
        _socket = socket;
        _socket.ReceiveBufferSize = 65535;
        _socket.SendBufferSize = 65535;
        _onClose = onClose;
        _onHandshakeComplete = onHandshakeComplete;
        _onChannelEstablished = onChannelEstablished;
        _localKeyPair = localKeyPair;
        _onData = onData;
        _onNewChannel = onNewChannel;
        LocalPublicKey = Convert.ToBase64String(localKeyPair.PublicKey);
        _isHandshakeAllowed = isHandshakeAllowed;
        RemoteAddress = remoteAddress;
    }

    public string? RemotePublicKey { get; private set; }

    public string LocalPublicKey { get; }

    public string RemoteAddress { get; }

    public int RemoteVersion { get; private set; } = -1;

    public IAuthorizable? Authorizable { get; set; }

    public bool IsAuthorized => Authorizable?.IsAuthorized ?? false;

    public void Dispose()
    {
        foreach (KeyValuePair<int, TaskCompletionSource<ConnectionInfo>> pendingConnectionInfoRequest in
                 _pendingConnectionInfoRequests) pendingConnectionInfoRequest.Value.TrySetCanceled();
        _pendingConnectionInfoRequests.Clear();
        foreach (var pendingPublishRequest in _pendingPublishRequests) pendingPublishRequest.Value.TrySetCanceled();
        _pendingPublishRequests.Clear();
        foreach (var pendingDeleteRequest in _pendingDeleteRequests) pendingDeleteRequest.Value.TrySetCanceled();
        _pendingDeleteRequests.Clear();
        foreach (KeyValuePair<int, TaskCompletionSource<List<(string, DateTime)>>> pendingListKeysRequest in
                 _pendingListKeysRequests) pendingListKeysRequest.Value.TrySetCanceled();
        _pendingListKeysRequests.Clear();
        foreach (KeyValuePair<int, TaskCompletionSource<(byte[], DateTime)?>> pendingGetRecordRequest in
                 _pendingGetRecordRequests) pendingGetRecordRequest.Value.TrySetCanceled();
        _pendingGetRecordRequests.Clear();
        foreach (KeyValuePair<int, TaskCompletionSource<Dictionary<string, (byte[], DateTime)>>>
                     pendingBulkGetRecordRequest in _pendingBulkGetRecordRequests)
            pendingBulkGetRecordRequest.Value.TrySetCanceled();
        _pendingBulkGetRecordRequests.Clear();
        foreach (var pendingBulkConnectionInfoRequest in _pendingBulkConnectionInfoRequests)
            pendingBulkConnectionInfoRequest.Value.TrySetCanceled();
        _pendingBulkConnectionInfoRequests.Clear();
        foreach (KeyValuePair<int, (ChannelRelayed, TaskCompletionSource<ChannelRelayed>)> pendingChannel in
                 _pendingChannels)
        {
            pendingChannel.Value.Item2.TrySetCanceled();
            pendingChannel.Value.Item1.Dispose();
        }

        _pendingChannels.Clear();
        lock (_syncStreams)
        {
            foreach (var syncStream in _syncStreams) syncStream.Value.Dispose();
            _syncStreams.Clear();
        }

        foreach (var value in _channels.Values) value.Dispose();
        _channels.Clear();
        _started = false;
        _onClose?.Invoke(this);
        _socket.Close();
        _transport?.Dispose();
        Logger.Info<SyncSocketSession>("Session closed");
    }

    public async Task StartAsInitiatorAsync(string remotePublicKey, uint appId = 0u, string? pairingCode = null,
        CancellationToken cancellationToken = default)
    {
        _started = true;
        try
        {
            await HandshakeAsInitiatorAsync(remotePublicKey, appId, pairingCode, cancellationToken);
            _onHandshakeComplete?.Invoke(this);
            await ReceiveLoopAsync(cancellationToken);
        }
        catch (Exception value)
        {
            Logger.Error<SyncSocketSession>($"Failed to run as initiator: {value}");
        }
        finally
        {
            Dispose();
        }
    }

    public async Task StartAsResponderAsync(CancellationToken cancellationToken = default)
    {
        _started = true;
        try
        {
            if (await HandshakeAsResponderAsync(cancellationToken))
            {
                _onHandshakeComplete?.Invoke(this);
                await ReceiveLoopAsync(cancellationToken);
            }
        }
        catch (Exception value)
        {
            Logger.Error<SyncSocketSession>($"Failed to run as responder: {value}");
        }
        finally
        {
            Dispose();
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken = default)
    {
        var messageSizeBytes = new byte[4];
        while (_started)
            try
            {
                await ReceiveExactAsync(messageSizeBytes, 0, 4, cancellationToken);
                var messageSize = BinaryPrimitives.ReadInt32LittleEndian(messageSizeBytes.AsSpan(0, 4));
                if (messageSize == 0) throw new Exception("Disconnected.");
                if (Logger.WillLog(LogLevel.Debug)) Logger.Debug<SyncSocketSession>($"Read message size {messageSize}");
                if (messageSize > 65535)
                    throw new Exception($"Message size ({messageSize}) exceeds maximum allowed size ({65535})");
                await ReceiveExactAsync(_buffer, 0, messageSize, cancellationToken);
                if (Logger.WillLog(LogLevel.Debug))
                    Logger.Debug<SyncSocketSession>($"Read message bytes {messageSize}");
                var num = Decrypt(_buffer.AsSpan().Slice(0, messageSize), _bufferDecrypted);
                if (Logger.WillLog(LogLevel.Debug)) Logger.Debug<SyncSocketSession>($"Decrypted message bytes {num}");
                HandleData(_bufferDecrypted, num);
            }
            catch (Exception value)
            {
                Logger.Error<SyncSocketSession>($"Exception while receiving data: {value}");
                Dispose();
                break;
            }
    }

    private async ValueTask HandshakeAsInitiatorAsync(string remotePublicKey, uint appId = 0u,
        string? pairingCode = null, CancellationToken cancellationToken = default)
    {
        await PerformVersionCheckAsync();
        var message = new byte[512];
        var plaintext = new byte[512];
        var protocol = Constants.Protocol;
        var privateKey = _localKeyPair.PrivateKey;
        var rs = Convert.FromBase64String(remotePublicKey);
        using var handshakeState = protocol.Create(true, default, privateKey, rs);
        var source = Array.Empty<byte>();
        var pairingMessageLength = 0;
        if (pairingCode != null)
        {
            var protocol2 = new Protocol(HandshakePattern.N, CipherFunction.ChaChaPoly, HashFunction.Blake2b);
            rs = Convert.FromBase64String(remotePublicKey);
            using var handshakeState2 = protocol2.Create(true, default, null, rs);
            var bytes = Encoding.UTF8.GetBytes(pairingCode);
            if (bytes.Length > 32) throw new ArgumentException("Pairing code must not exceed 32 bytes.");
            var array = new byte[512];
            pairingMessageLength = handshakeState2.WriteMessage(bytes, array).BytesWritten;
            source = array.AsSpan(0, pairingMessageLength).ToArray();
        }

        var num = 4;
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(num, 4), appId);
        num += 4;
        BinaryPrimitives.WriteInt32LittleEndian(message.AsSpan(num, 4), pairingMessageLength);
        num += 4;
        if (pairingMessageLength > 0)
        {
            source.CopyTo(message.AsSpan(num, pairingMessageLength));
            num += pairingMessageLength;
        }

        var channelBytesWritten = handshakeState.WriteMessage(null, message.AsSpan(num)).BytesWritten;
        var totalMessageSize = 8 + pairingMessageLength + channelBytesWritten;
        BinaryPrimitives.WriteInt32LittleEndian(message.AsSpan(0, 4), totalMessageSize);
        await SendAsync(message, 0, totalMessageSize + 4, cancellationToken);
        Logger.Info<SyncSocketSession>(
            $"HandshakeAsInitiator: Wrote message size {totalMessageSize} (pairing: {pairingMessageLength}, channel: {channelBytesWritten}, app id: {appId}");
        await ReceiveExactAsync(message, 0, 4);
        var messageSize = BitConverter.ToInt32(message);
        Logger.Info<SyncSocketSession>($"HandshakeAsInitiator: Read message size {messageSize} (app id: {appId})");
        await ReceiveExactAsync(message, 0, messageSize);
        var item = handshakeState.ReadMessage(message.AsSpan().Slice(0, messageSize), plaintext).Transport;
        _transport = item;
        RemotePublicKey = Convert.ToBase64String(handshakeState.RemoteStaticPublicKey);
    }

    private async ValueTask<bool> HandshakeAsResponderAsync(CancellationToken cancellationToken = default)
    {
        await PerformVersionCheckAsync();
        var message = new byte[512];
        var plaintext = new byte[512];
        var protocol = Constants.Protocol;
        var privateKey = _localKeyPair.PrivateKey;
        using var handshakeState = protocol.Create(false, default, privateKey);
        await ReceiveExactAsync(message, 0, 4);
        var messageSize = BinaryPrimitives.ReadInt32LittleEndian(message.AsSpan(0, 4));
        Logger.Info<SyncSocketSession>($"HandshakeAsResponder: Read message size {messageSize}");
        await ReceiveExactAsync(message, 0, messageSize);
        var num = 0;
        var appId = BinaryPrimitives.ReadUInt32LittleEndian(message.AsSpan(num, 4));
        num += 4;
        var num2 = BinaryPrimitives.ReadInt32LittleEndian(message.AsSpan(num, 4));
        if (num2 > 128)
            throw new InvalidDataException(
                $"Received (pairing message length: {num2}, app id: {appId}) exceeds maximum allowed size (128).");
        num += 4;
        string text = null;
        if (num2 > 0)
        {
            var protocol2 = new Protocol(HandshakePattern.N, CipherFunction.ChaChaPoly, HashFunction.Blake2b);
            privateKey = _localKeyPair.PrivateKey;
            using var handshakeState2 = protocol2.Create(false, default, privateKey);
            var span = message.AsSpan(num, num2);
            num += num2;
            var array = new byte[512];
            handshakeState2.ReadMessage(span, array);
            text = Encoding.UTF8.GetString(array, 0, Array.IndexOf(array, (byte)0, 0, Math.Min(32, array.Length)));
            Logger.Info<SyncSocketSession>($"HandshakeAsResponder: Received pairing code '{text}' (app id: {appId})");
        }

        var span2 = message.AsSpan(num, messageSize - num);
        handshakeState.ReadMessage(span2, plaintext);
        var remotePublicKey = Convert.ToBase64String(handshakeState.RemoteStaticPublicKey);
        if (!(remotePublicKey != LocalPublicKey) ||
            !(_isHandshakeAllowed?.Invoke(LinkType.Direct, this, remotePublicKey, text, appId) ?? true))
        {
            Logger.Info<SyncSocketSession>(
                $"HandshakeAsResponder: Handshake is not allowed (app id: {appId}). Closing connection.");
            Dispose();
            return false;
        }

        var (bytesWritten, _, transport) = handshakeState.WriteMessage(null, message.AsSpan(4));
        BinaryPrimitives.WriteInt32LittleEndian(message.AsSpan(0, 4), bytesWritten);
        await SendAsync(message, 0, bytesWritten + 4, cancellationToken);
        Logger.Info<SyncSocketSession>($"HandshakeAsResponder: Wrote message size {bytesWritten} (app id: {appId})");
        _transport = transport;
        RemotePublicKey = remotePublicKey;
        return true;
    }

    private async ValueTask PerformVersionCheckAsync(CancellationToken cancellationToken = default)
    {
        await SendAsync(VERSION_BYTES, 0, 4, cancellationToken);
        var versionBytes = new byte[4];
        await ReceiveExactAsync(versionBytes, 0, 4);
        RemoteVersion = BinaryPrimitives.ReadInt32LittleEndian(versionBytes.AsSpan(0, 4));
        Logger.Info("SyncSocketSession", $"PerformVersionCheck {RemoteVersion}");
        if (RemoteVersion < 4) throw new Exception("Invalid version");
    }

    private int Encrypt(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        var num = _transport.WriteMessage(source, destination);
        if (Logger.WillLog(LogLevel.Debug))
            Logger.Debug<SyncSocketSession>(
                $"Encrypted message bytes (source size: {source.Length}, destination size: {num})");
        return num;
    }

    private int Decrypt(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        var num = _transport.ReadMessage(source, destination);
        if (Logger.WillLog(LogLevel.Debug))
            Logger.Debug<SyncSocketSession>(
                $"Decrypted message bytes (source size: {source.Length}, destination size: {num})");
        return num;
    }

    private async ValueTask ReceiveExactAsync(byte[] buffer, int offset, int size,
        CancellationToken cancellationToken = default)
    {
        Stopwatch sw = null;
        if (Logger.WillLog(LogLevel.Debug)) sw = new Stopwatch();
        int totalBytesReceived;
        int num;
        for (totalBytesReceived = 0; totalBytesReceived < size; totalBytesReceived += num)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sw?.Restart();
            num = await _socket.ReceiveAsync(
                new ArraySegment<byte>(buffer, offset + totalBytesReceived, size - totalBytesReceived),
                cancellationToken);
            if (num == 0) throw new Exception("Connection closed");
            if (Logger.WillLog(LogLevel.Debug))
                Logger.Debug<SyncSocketSession>($"Receive duration ({num}/{size} bytes): {sw?.ElapsedMilliseconds}ms");
        }

        if (Logger.WillLog(LogLevel.Debug)) Logger.Debug<SyncSocketSession>($"Received {totalBytesReceived} bytes.");
    }

    private async ValueTask SendAsync(byte[] data, int offset = 0, int count = -1,
        CancellationToken cancellationToken = default)
    {
        if (count == -1) count = data.Length;
        if (Logger.WillLog(LogLevel.Debug)) Logger.Debug<SyncSocketSession>($"Sending {count} bytes.");
        Stopwatch sw = null;
        if (Logger.WillLog(LogLevel.Debug)) sw = new Stopwatch();
        int num;
        for (var totalBytesSent = 0; totalBytesSent < count; totalBytesSent += num)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sw?.Restart();
            if (Logger.WillLog(LogLevel.Debug))
                Logger.Debug<SyncSocketSession>($"Sending {count - totalBytesSent} bytes.");
            num = await _socket.SendAsync(new ArraySegment<byte>(data, offset + totalBytesSent,
                count - totalBytesSent));
            if (num == 0) throw new Exception("Failed to send.");
            if (Logger.WillLog(LogLevel.Debug))
                Logger.Debug<SyncSocketSession>($"Send duration ({num} bytes): {sw?.ElapsedMilliseconds}ms");
        }
    }

    public int GenerateStreamId()
    {
        return Interlocked.Increment(ref _streamIdGenerator);
    }

    public async Task SendAsync(Opcode opcode, byte subOpcode, byte[] data, int offset = 0, int size = -1,
        ContentEncoding contentEncoding = ContentEncoding.Raw, CancellationToken cancellationToken = default)
    {
        await SendAsync((byte)opcode, subOpcode, data, offset, size, contentEncoding, cancellationToken);
    }

    public async Task SendAsync(byte opcode, byte subOpcode, byte[] data, int offset = 0, int size = -1,
        ContentEncoding contentEncoding = ContentEncoding.Raw, CancellationToken cancellationToken = default)
    {
        if (size == -1) size = data.Length;
        if (Logger.WillLog(LogLevel.Debug))
            Logger.Debug<SyncSocketSession>(
                $"SendAsync (opcode = {opcode}, subOpcode = {subOpcode}, contentEncoding = {contentEncoding}, size = {size})");
        var processedData = data;
        var processedSize = size;
        if (contentEncoding == ContentEncoding.Gzip)
        {
            if (opcode == 4)
            {
                using var compressedStream = new MemoryStream();
                using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Compress))
                {
                    await gzipStream.WriteAsync(data.AsMemory(offset, size), cancellationToken);
                }

                processedData = compressedStream.ToArray();
                processedSize = processedData.Length;
            }
            else
            {
                Logger.Warning<SyncSocketSession>(
                    $"Gzip requested but not supported on this (opcode = {opcode}, subOpcode = {subOpcode}), falling back.");
                contentEncoding = ContentEncoding.Raw;
            }
        }

        Span<byte> span;
        if (processedSize + 7 > 65519)
        {
            var segmentSize = 65512;
            var id = GenerateStreamId();
            var segmentData = Utilities.RentBytes(segmentSize);
            try
            {
                var sendOffset = 0;
                while (sendOffset < processedSize)
                {
                    var num = processedSize - sendOffset;
                    StreamOpcode streamOpcode;
                    int num2;
                    int length;
                    if (sendOffset == 0)
                    {
                        streamOpcode = StreamOpcode.START;
                        num2 = segmentSize - 4 - 7;
                        length = num2 + 4 + 7;
                    }
                    else
                    {
                        num2 = Math.Min(segmentSize - 4 - 4, num);
                        streamOpcode = num2 < num ? StreamOpcode.DATA : StreamOpcode.END;
                        length = num2 + 4 + 4;
                    }

                    if (streamOpcode == StreamOpcode.START)
                    {
                        span = segmentData.AsSpan();
                        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(0, 4), id);
                        span = segmentData.AsSpan();
                        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(4, 4), processedSize);
                        segmentData[8] = opcode;
                        segmentData[9] = subOpcode;
                        segmentData[10] = (byte)contentEncoding;
                        span = processedData.AsSpan(offset, processedSize);
                        span = span.Slice(sendOffset, num2);
                        span.CopyTo(segmentData.AsSpan().Slice(11));
                    }
                    else
                    {
                        span = segmentData.AsSpan();
                        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(0, 4), id);
                        span = segmentData.AsSpan();
                        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(4, 4), sendOffset);
                        span = processedData.AsSpan(offset, processedSize);
                        span = span.Slice(sendOffset, num2);
                        span.CopyTo(segmentData.AsSpan().Slice(8));
                    }

                    sendOffset += num2;
                    var syncSocketSession = this;
                    var subOpcode2 = streamOpcode;
                    span = segmentData.AsSpan();
                    span = span.Slice(0, length);
                    await syncSocketSession.SendAsync(3, (byte)subOpcode2, span.ToArray(), 0, -1, ContentEncoding.Raw,
                        cancellationToken);
                }
            }
            finally
            {
                Utilities.ReturnBytes(segmentData);
            }

            return;
        }

        try
        {
            await _sendSemaphore.WaitAsync();
            BinaryPrimitives.WriteInt32LittleEndian(_sendBuffer.AsSpan(0, 4), processedSize + 7 - 4);
            _sendBuffer[4] = opcode;
            _sendBuffer[5] = subOpcode;
            _sendBuffer[6] = (byte)contentEncoding;
            var source = processedData;
            span = _sendBuffer.AsSpan();
            source.CopyTo(span.Slice(7));
            if (Logger.WillLog(LogLevel.Debug))
                Logger.Debug<SyncSocketSession>($"Encrypted message bytes {processedSize + 7}");
            var syncSocketSession2 = this;
            span = _sendBuffer.AsSpan();
            var id = syncSocketSession2.Encrypt(span.Slice(0, processedSize + 7), _sendBufferEncrypted.AsSpan(4));
            BinaryPrimitives.WriteInt32LittleEndian(_sendBufferEncrypted.AsSpan(0, 4), id);
            await SendAsync(_sendBufferEncrypted, 0, id + 4, cancellationToken);
            if (Logger.WillLog(LogLevel.Debug)) Logger.Debug<SyncSocketSession>($"Wrote message bytes {id}");
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }

    public async Task SendAsync(Opcode opcode, byte subOpcode = 0, CancellationToken cancellationToken = default)
    {
        if (Logger.WillLog(LogLevel.Debug))
            Logger.Debug<SyncSocketSession>($"SendAsync (opcode = {opcode}, subOpcode = {subOpcode}, size = 0)");
        try
        {
            await _sendSemaphore.WaitAsync(cancellationToken);
            Array.Copy(BitConverter.GetBytes(2), 0, _sendBuffer, 0, 4);
            _sendBuffer[4] = (byte)opcode;
            _sendBuffer[5] = subOpcode;
            _sendBuffer[6] = 0;
            if (Logger.WillLog(LogLevel.Debug)) Logger.Debug<SyncSocketSession>($"Encrypted message bytes {7}");
            var len = Encrypt(_sendBuffer.AsSpan().Slice(0, 7), _sendBufferEncrypted);
            await SendAsync(BitConverter.GetBytes(len), 0, 4, cancellationToken);
            if (Logger.WillLog(LogLevel.Debug)) Logger.Debug<SyncSocketSession>($"Wrote message size {len}");
            await SendAsync(_sendBufferEncrypted, 0, len, cancellationToken);
            if (Logger.WillLog(LogLevel.Debug)) Logger.Debug<SyncSocketSession>($"Wrote message bytes {len}");
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }

    private void HandleData(byte[] data, int length, ChannelRelayed? sourceChannel = null)
    {
        if (length < 7) throw new Exception($"Packet must be at least {7} bytes (header size)");
        var num = BitConverter.ToInt32(data, 0);
        if (num != length - 4) throw new Exception("Incomplete packet received");
        var opcode = data[4];
        var subOpcode = data[5];
        var contentEncoding = (ContentEncoding)data[6];
        ReadOnlySpan<byte> data2 = data.AsSpan(7, num - 7 + 4);
        HandlePacket((Opcode)opcode, subOpcode, data2, contentEncoding, sourceChannel);
    }

    private int GenerateRequestId()
    {
        return Interlocked.Increment(ref _requestIdGenerator);
    }

    public Task<ConnectionInfo?> RequestConnectionInfoAsync(string publicKey,
        CancellationToken cancellationToken = default)
    {
        var taskCompletionSource = new TaskCompletionSource<ConnectionInfo>();
        var requestId = GenerateRequestId();
        _pendingConnectionInfoRequests[requestId] = taskCompletionSource;
        cancellationToken.Register(delegate
        {
            if (_pendingConnectionInfoRequests.TryRemove(requestId, out TaskCompletionSource<ConnectionInfo> value2))
                value2.TrySetCanceled();
        });
        try
        {
            var array = Convert.FromBase64String(publicKey);
            if (array.Length != 32) throw new ArgumentException("Public key must be 32 bytes.");
            var array2 = new byte[36];
            BinaryPrimitives.WriteInt32LittleEndian(array2.AsSpan(0, 4), requestId);
            array.CopyTo(array2.AsSpan(4, 32));
            SendAsync(Opcode.REQUEST, 0, array2, 0, -1, ContentEncoding.Raw, cancellationToken).ContinueWith(
                delegate(Task t)
                {
                    if (t.IsFaulted && _pendingConnectionInfoRequests.TryRemove(requestId,
                            out TaskCompletionSource<ConnectionInfo> value2))
                        value2.TrySetException(t.Exception.InnerException);
                });
        }
        catch (Exception exception)
        {
            if (_pendingConnectionInfoRequests.TryRemove(requestId, out TaskCompletionSource<ConnectionInfo> value))
                value.TrySetException(exception);
            throw;
        }

        return taskCompletionSource.Task;
    }

    public async Task<Dictionary<string, ConnectionInfo>> RequestBulkConnectionInfoAsync(IEnumerable<string> publicKeys,
        CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<Dictionary<string, ConnectionInfo>>();
        var requestId = GenerateRequestId();
        _pendingBulkConnectionInfoRequests[requestId] = tcs;
        cancellationToken.Register(delegate
        {
            if (_pendingBulkConnectionInfoRequests.TryRemove(requestId, out var value2)) value2.TrySetCanceled();
        });
        try
        {
            var list = publicKeys.ToList();
            var count = list.Count;
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write(requestId);
            writer.Write((byte)count);
            foreach (var item in list)
            {
                var array = Convert.FromBase64String(item);
                if (array.Length != 32)
                    throw new ArgumentException("Invalid public key length for " + item + "; must be 32 bytes.");
                writer.Write(array);
            }

            var data = ms.ToArray();
            await SendAsync(Opcode.REQUEST, 9, data, 0, -1, ContentEncoding.Raw, cancellationToken);
        }
        catch (Exception exception)
        {
            if (_pendingBulkConnectionInfoRequests.TryRemove(requestId, out var value))
                value.TrySetException(exception);
            throw;
        }

        return await tcs.Task;
    }

    public Task<ChannelRelayed> StartRelayedChannelAsync(string publicKey, uint appId = 0u, string? pairingCode = null,
        CancellationToken cancellationToken = default)
    {
        var taskCompletionSource = new TaskCompletionSource<ChannelRelayed>();
        var requestId = GenerateRequestId();
        var channelRelayed = new ChannelRelayed(this, _localKeyPair, publicKey, true);
        _onNewChannel?.Invoke(this, channelRelayed);
        _pendingChannels[requestId] = (channelRelayed, taskCompletionSource);
        cancellationToken.Register(delegate
        {
            if (_pendingChannels.TryRemove(requestId,
                    out (ChannelRelayed, TaskCompletionSource<ChannelRelayed>) value2))
            {
                value2.Item1.Dispose();
                value2.Item2.TrySetCanceled();
            }
        });
        try
        {
            channelRelayed.SendRequestTransportAsync(requestId, publicKey, appId, pairingCode, cancellationToken)
                .ContinueWith(delegate(Task t)
                {
                    if (t.IsFaulted && _pendingChannels.TryRemove(requestId,
                            out (ChannelRelayed, TaskCompletionSource<ChannelRelayed>) value2))
                    {
                        value2.Item1.Dispose();
                        value2.Item2.TrySetException(t.Exception.InnerException);
                    }
                });
        }
        catch (Exception exception)
        {
            if (_pendingChannels.TryRemove(requestId, out (ChannelRelayed, TaskCompletionSource<ChannelRelayed>) value))
            {
                value.Item1.Dispose();
                value.Item2.TrySetException(exception);
            }

            throw;
        }

        return taskCompletionSource.Task;
    }

    public async Task PublishConnectionInformationAsync(string[] authorizedKeys, int port, bool allowLocalDirect,
        bool allowRemoteDirect, bool allowRemoteHolePunched, bool allowRemoteRelayed,
        CancellationToken cancellationToken = default)
    {
        if (authorizedKeys.Length > 255)
            throw new ArgumentException($"Number of authorized keys exceeds the maximum limit of {255}.");
        var list = new List<IPAddress>(4);
        var list2 = new List<IPAddress>(4);
        var allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
        foreach (var networkInterface in allNetworkInterfaces)
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up) continue;
            foreach (var unicastAddress in networkInterface.GetIPProperties().UnicastAddresses)
            {
                var address = unicastAddress.Address;
                if (!IPAddress.IsLoopback(address))
                {
                    if (address.AddressFamily == AddressFamily.InterNetwork)
                        list.Add(address);
                    else if (address.AddressFamily == AddressFamily.InterNetworkV6) list2.Add(address);
                }
            }
        }

        var limitedUtf8Bytes = Utilities.GetLimitedUtf8Bytes(OSHelper.GetComputerName(), 255);
        var array = new byte[3 + limitedUtf8Bytes.Length + 1 + list.Count * 4 + 1 + list2.Count * 16 + 1 + 1 + 1 + 1];
        using (var output = new MemoryStream(array))
        {
            using var binaryWriter = new BinaryWriter(output);
            binaryWriter.Write((ushort)port);
            binaryWriter.Write((byte)limitedUtf8Bytes.Length);
            binaryWriter.Write(limitedUtf8Bytes);
            binaryWriter.Write((byte)list.Count);
            foreach (var item in list) binaryWriter.Write(item.GetAddressBytes());
            binaryWriter.Write((byte)list2.Count);
            foreach (var item2 in list2) binaryWriter.Write(item2.GetAddressBytes());
            binaryWriter.Write(allowLocalDirect ? (byte)1 : (byte)0);
            binaryWriter.Write(allowRemoteDirect ? (byte)1 : (byte)0);
            binaryWriter.Write(allowRemoteHolePunched ? (byte)1 : (byte)0);
            binaryWriter.Write(allowRemoteRelayed ? (byte)1 : (byte)0);
        }

        var num = 1 + authorizedKeys.Length * (100 + array.Length);
        var array2 = new byte[num];
        using (var output2 = new MemoryStream(array2, 0, num, true, true))
        {
            using var binaryWriter2 = new BinaryWriter(output2);
            binaryWriter2.Write((byte)authorizedKeys.Length);
            for (var i = 0; i < authorizedKeys.Length; i++)
            {
                var array3 = Convert.FromBase64String(authorizedKeys[i]);
                if (array3.Length != 32) throw new InvalidOperationException("Public key must be 32 bytes.");
                binaryWriter2.Write(array3);
                var protocol = new Protocol(HandshakePattern.N, CipherFunction.ChaChaPoly, HashFunction.Blake2b);
                var rs = array3;
                using var handshakeState = protocol.Create(true, default, null, rs);
                var num2 = 48;
                var array4 = new byte[num2];
                var (num3, _, transport) = handshakeState.WriteMessage(null, array4);
                if (num3 != num2) throw new InvalidOperationException($"Handshake message must be {num2} bytes.");
                binaryWriter2.Write(array4, 0, num2);
                var array5 = new byte[array.Length + 16];
                if (transport.WriteMessage(array, array5) != array.Length + 16)
                    throw new InvalidOperationException("Ciphertext size mismatch.");
                binaryWriter2.Write(array.Length + 16);
                binaryWriter2.Write(array5, 0, array.Length + 16);
            }
        }

        await SendAsync(Opcode.NOTIFY, 2, array2, 0, -1, ContentEncoding.Raw, cancellationToken);
    }

    private void HandleNotify(NotifyOpcode opcode, ReadOnlySpan<byte> data, ChannelRelayed? sourceChannel = null)
    {
        if (opcode > NotifyOpcode.UNAUTHORIZED)
            _ = 2;
        else if (sourceChannel != null)
            sourceChannel.InvokeDataHandler(Opcode.NOTIFY, (byte)opcode, data);
        else
            _onData?.Invoke(this, Opcode.NOTIFY, (byte)opcode, data);
    }

    private void HandleResponse(ResponseOpcode opcode, ReadOnlySpan<byte> data, ChannelRelayed? sourceChannel = null)
    {
        if (data.Length < 8)
        {
            Logger.Error<SyncSocketSession>("Response packet too short");
            return;
        }

        var num = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0, 4));
        var num2 = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(4, 4));
        data = data.Slice(8);
        switch (opcode)
        {
            case ResponseOpcode.CONNECTION_INFO:
            {
                if (_pendingConnectionInfoRequests.TryRemove(num, out TaskCompletionSource<ConnectionInfo> value3))
                {
                    if (num2 == 0)
                        try
                        {
                            var result = ParseConnectionInfo(data);
                            value3.SetResult(result);
                            break;
                        }
                        catch (Exception exception2)
                        {
                            value3.SetException(exception2);
                            break;
                        }

                    value3.SetResult(null);
                }
                else
                {
                    Logger.Error<SyncSocketSession>($"No pending request for requestId {num}");
                }

                break;
            }
            case ResponseOpcode.TRANSPORT_RELAYED:
            {
                (ChannelRelayed, TaskCompletionSource<ChannelRelayed>) value10;
                if (num2 == 0)
                {
                    if (data.Length < 16)
                    {
                        Logger.Error<SyncSocketSession>("RESPONSE_RELAYED_TRANSPORT packet too short");
                        break;
                    }

                    var remoteVersion = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0, 4));
                    var num16 = BinaryPrimitives.ReadInt64LittleEndian(data.Slice(4, 8));
                    var num17 = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(12, 4));
                    if (data.Length != 16 + num17)
                    {
                        Logger.Error<SyncSocketSession>(
                            $"Invalid RESPONSE_RELAYED_TRANSPORT packet size. Expected {16 + num17}, got {data.Length}");
                        break;
                    }

                    var handshakeMessage = data.Slice(16, num17).ToArray();
                    if (_pendingChannels.TryRemove(num,
                            out (ChannelRelayed, TaskCompletionSource<ChannelRelayed>) value9))
                    {
                        var (channelRelayed, taskCompletionSource) = value9;
                        channelRelayed.HandleTransportRelayed(remoteVersion, num16, handshakeMessage);
                        _channels[num16] = channelRelayed;
                        taskCompletionSource.SetResult(channelRelayed);
                        _onChannelEstablished?.Invoke(this, channelRelayed, false);
                    }
                    else
                    {
                        Logger.Error<SyncSocketSession>($"No pending channel for requestId {num}");
                    }
                }
                else if (_pendingChannels.TryRemove(num, out value10))
                {
                    var (channelRelayed2, taskCompletionSource2) = value10;
                    channelRelayed2.Dispose();
                    taskCompletionSource2.SetException(
                        new Exception($"Relayed transport request {num} failed with error code {num2}"));
                }

                break;
            }
            case ResponseOpcode.PUBLISH_RECORD:
            {
                Logger.Info<SyncSocketSession>($"Received publishing record response requestId = {num}.");
                if (_pendingPublishRequests.TryRemove(num, out var value12))
                {
                    if (num2 == 0)
                        value12.SetResult(true);
                    else
                        value12.SetResult(false);
                }
                else
                {
                    Logger.Error<SyncSocketSession>($"No pending publish request for requestId {num}");
                }

                break;
            }
            case ResponseOpcode.DELETE_RECORD:
            {
                if (_pendingDeleteRequests.TryRemove(num, out var value7))
                {
                    if (num2 == 0)
                        value7.SetResult(true);
                    else
                        value7.SetResult(false);
                }
                else
                {
                    Logger.Error<SyncSocketSession>($"No pending delete request for requestId {num}");
                }

                break;
            }
            case ResponseOpcode.BULK_DELETE_RECORD:
            {
                if (_pendingDeleteRequests.TryRemove(num, out var value4))
                {
                    if (num2 == 0)
                        value4.SetResult(true);
                    else
                        value4.SetResult(false);
                }
                else
                {
                    Logger.Error<SyncSocketSession>($"No pending bulk delete request for requestId {num}");
                }

                break;
            }
            case ResponseOpcode.LIST_RECORD_KEYS:
            {
                if (_pendingListKeysRequests.TryRemove(num, out TaskCompletionSource<List<(string, DateTime)>> value6))
                {
                    if (num2 == 0)
                        try
                        {
                            var list = new List<(string, DateTime)>();
                            if (data.Length >= 4)
                            {
                                var num6 = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0, 4));
                                data = data.Slice(4);
                                for (var j = 0; j < num6; j++)
                                {
                                    if (data.Length < 1) throw new Exception("Packet too short for key length");
                                    var b2 = data[0];
                                    data = data.Slice(1);
                                    if (data.Length < b2) throw new Exception("Packet too short for key data");
                                    var item = Encoding.UTF8.GetString(data.Slice(0, b2));
                                    data = data.Slice(b2);
                                    if (data.Length < 8) throw new Exception("Packet too short for timestamp");
                                    var dateData = BinaryPrimitives.ReadInt64LittleEndian(data.Slice(0, 8));
                                    data = data.Slice(8);
                                    var item2 = DateTime.FromBinary(dateData);
                                    list.Add((item, item2));
                                }

                                value6.SetResult(list);
                                break;
                            }

                            throw new Exception("Packet too short for key count");
                        }
                        catch (Exception exception3)
                        {
                            value6.SetException(exception3);
                            break;
                        }

                    value6.SetException(new Exception($"Error listing keys: status code {num2}"));
                }
                else
                {
                    Logger.Error<SyncSocketSession>($"No pending list keys request for requestId {num}");
                }

                break;
            }
            case ResponseOpcode.BULK_GET_RECORD:
            {
                if (!_pendingBulkGetRecordRequests.TryRemove(num,
                        out TaskCompletionSource<Dictionary<string, (byte[], DateTime)>> value11)) break;
                if (num2 == 0)
                    try
                    {
                        var num18 = 0;
                        var b3 = data[num18];
                        num18++;
                        var dictionary2 = new Dictionary<string, (byte[], DateTime)>(b3);
                        for (var l = 0; l < b3; l++)
                        {
                            var key2 = Convert.ToBase64String(data.Slice(num18, 32).ToArray());
                            num18 += 32;
                            var num19 = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(num18, 4));
                            num18 += 4;
                            var array3 = data.Slice(num18, num19).ToArray();
                            num18 += num19;
                            var item5 = DateTime.FromBinary(
                                BinaryPrimitives.ReadInt64LittleEndian(data.Slice(num18, 8)));
                            num18 += 8;
                            var protocol2 = new Protocol(HandshakePattern.N, CipherFunction.ChaChaPoly,
                                HashFunction.Blake2b);
                            var privateKey = _localKeyPair.PrivateKey;
                            using var handshakeState2 = protocol2.Create(false, default, privateKey);
                            var array4 = new byte[48];
                            array3.AsSpan(0, 48).CopyTo(array4);
                            var item6 = handshakeState2.ReadMessage(array4, new byte[0]).Transport;
                            var num20 = 48;
                            var num21 = 0;
                            var num22 = 0;
                            while (num20 + 4 <= array3.Length)
                            {
                                var num23 = BinaryPrimitives.ReadInt32LittleEndian(array3.AsSpan(num20, 4));
                                if (num23 <= 16 || num20 + 4 + num23 > array3.Length)
                                    throw new InvalidDataException("Invalid encrypted chunk length");
                                num21 += num23 - 16;
                                num20 += 4 + num23;
                                num22++;
                            }

                            if (num22 == 0) throw new Exception("No valid chunks decrypted");
                            var array5 = new byte[num21];
                            var num24 = 0;
                            num20 = 48;
                            for (var m = 0; m < num22; m++)
                            {
                                var num25 = BinaryPrimitives.ReadInt32LittleEndian(array3.AsSpan(num20, 4));
                                num20 += 4;
                                var span = array3.AsSpan(num20, num25);
                                var payloadBuffer2 = array5.AsSpan(num24, num25 - 16);
                                var num26 = item6.ReadMessage(span, payloadBuffer2);
                                num24 += num26;
                                num20 += num25;
                            }

                            dictionary2[key2] = (array5, item5);
                        }

                        value11.SetResult(dictionary2);
                        break;
                    }
                    catch (Exception ex2)
                    {
                        Logger.Error<SyncSocketSession>("Error processing RESPONSE_BULK_GET_RECORD: {Exception}", ex2);
                        value11.SetException(ex2);
                        break;
                    }

                value11.SetException(new Exception($"Error getting bulk records: statusCode {num2}"));
                break;
            }
            case ResponseOpcode.GET_RECORD:
            {
                if (!_pendingGetRecordRequests.TryRemove(num, out TaskCompletionSource<(byte[], DateTime)?> value8))
                    break;
                switch (num2)
                {
                    case 0:
                        try
                        {
                            var num7 = 0;
                            var num8 = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(num7, 4));
                            num7 += 4;
                            var readOnlySpan = data.Slice(num7, num8);
                            num7 += num8;
                            var item3 = DateTime.FromBinary(
                                BinaryPrimitives.ReadInt64LittleEndian(data.Slice(num7, 8)));
                            var protocol = new Protocol(HandshakePattern.N, CipherFunction.ChaChaPoly,
                                HashFunction.Blake2b);
                            var privateKey = _localKeyPair.PrivateKey;
                            using var handshakeState = protocol.Create(false, default, privateKey);
                            var array = new byte[48];
                            readOnlySpan.Slice(0, 48).CopyTo(array);
                            var item4 = handshakeState.ReadMessage(array, new byte[0]).Transport;
                            var num9 = 48;
                            var num10 = 0;
                            var num11 = 0;
                            while (num9 + 4 <= readOnlySpan.Length)
                            {
                                var num12 = BinaryPrimitives.ReadInt32LittleEndian(readOnlySpan.Slice(num9, 4));
                                if (num12 <= 16 || num9 + 4 + num12 > readOnlySpan.Length)
                                    throw new InvalidDataException("Invalid encrypted chunk length");
                                num10 += num12 - 16;
                                num9 += 4 + num12;
                                num11++;
                            }

                            if (num11 == 0) throw new Exception("No valid chunks decrypted");
                            var array2 = new byte[num10];
                            var num13 = 0;
                            num9 = 48;
                            for (var k = 0; k < num11; k++)
                            {
                                var num14 = BinaryPrimitives.ReadInt32LittleEndian(readOnlySpan.Slice(num9, 4));
                                num9 += 4;
                                var message = readOnlySpan.Slice(num9, num14);
                                var payloadBuffer = array2.AsSpan(num13, num14 - 16);
                                var num15 = item4.ReadMessage(message, payloadBuffer);
                                num13 += num15;
                                num9 += num14;
                            }

                            value8.SetResult((array2, item3));
                            break;
                        }
                        catch (Exception ex)
                        {
                            Logger.Error<SyncSocketSession>("Error processing RESPONSE_GET_RECORD: {Exception}", ex);
                            value8.SetException(ex);
                            break;
                        }
                    case 2:
                        value8.SetResult(null);
                        break;
                    default:
                        value8.SetException(new Exception($"Error getting record: statusCode {num2}"));
                        break;
                }

                break;
            }
            case ResponseOpcode.BULK_PUBLISH_RECORD:
            {
                if (_pendingPublishRequests.TryRemove(num, out var value5))
                {
                    value5.SetResult(num2 == 0);
                    break;
                }

                Logger.Error<SyncSocketSession>($"No pending bulk publish request for requestId {num}");
                break;
            }
            case ResponseOpcode.BULK_CONNECTION_INFO:
            {
                if (_pendingBulkConnectionInfoRequests.TryRemove(num, out var value))
                    try
                    {
                        var dictionary = new Dictionary<string, ConnectionInfo>();
                        var num3 = 0;
                        var b = data[num3];
                        num3++;
                        for (var i = 0; i < b; i++)
                        {
                            if (num3 + 32 + 1 > data.Length)
                                throw new Exception("Invalid RESPONSE_BULK_CONNECTION_INFO packet: insufficient data");
                            var key = Convert.ToBase64String(data.Slice(num3, 32));
                            num3 += 32;
                            var num4 = data[num3];
                            num3++;
                            if (num4 == 0)
                            {
                                if (num3 + 4 > data.Length)
                                    throw new Exception("Invalid RESPONSE_BULK_CONNECTION_INFO packet: missing length");
                                var num5 = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(num3, 4));
                                num3 += 4;
                                if (num3 + num5 > data.Length)
                                    throw new Exception("Invalid RESPONSE_BULK_CONNECTION_INFO packet: data truncated");
                                var value2 = ParseConnectionInfo(data.Slice(num3, num5));
                                dictionary[key] = value2;
                                num3 += num5;
                            }
                        }

                        value.SetResult(dictionary);
                        break;
                    }
                    catch (Exception exception)
                    {
                        value.SetException(exception);
                        break;
                    }

                Logger.Error<SyncSocketSession>($"No pending bulk request for requestId {num}");
                break;
            }
            case ResponseOpcode.TRANSPORT:
                break;
        }
    }

    private void HandleRequest(RequestOpcode opcode, ReadOnlySpan<byte> data, ChannelRelayed? sourceChannel = null)
    {
        if (opcode == RequestOpcode.TRANSPORT_RELAYED)
        {
            Logger.Info<SyncSocketSession>("Received request for a relayed transport");
            HandleRequestTransportRelayed(data);
        }
    }

    private void HandleStream(StreamOpcode opcode, ReadOnlySpan<byte> data, ChannelRelayed? sourceChannel = null)
    {
        switch (opcode)
        {
            case StreamOpcode.START:
            {
                var readOnlySpan3 = data;
                var key3 = BinaryPrimitives.ReadInt32LittleEndian(readOnlySpan3);
                readOnlySpan3 = readOnlySpan3.Slice(4);
                var expectedSize = BinaryPrimitives.ReadInt32LittleEndian(readOnlySpan3);
                readOnlySpan3 = readOnlySpan3.Slice(4);
                var opcode2 = readOnlySpan3[0];
                readOnlySpan3 = readOnlySpan3.Slice(1);
                var subOpcode = readOnlySpan3[0];
                readOnlySpan3 = readOnlySpan3.Slice(1);
                var contentEncoding = (ContentEncoding)readOnlySpan3[0];
                readOnlySpan3 = readOnlySpan3.Slice(1);
                var syncStream = new SyncStream(expectedSize, (Opcode)opcode2, subOpcode, contentEncoding);
                if (readOnlySpan3.Length > 0) syncStream.Add(readOnlySpan3);
                lock (_syncStreams)
                {
                    _syncStreams[key3] = syncStream;
                    break;
                }
            }
            case StreamOpcode.DATA:
            {
                var readOnlySpan2 = data;
                var key2 = BinaryPrimitives.ReadInt32LittleEndian(readOnlySpan2);
                readOnlySpan2 = readOnlySpan2.Slice(4);
                var num2 = BinaryPrimitives.ReadInt32LittleEndian(readOnlySpan2);
                readOnlySpan2 = readOnlySpan2.Slice(4);
                SyncStream value2;
                lock (_syncStreams)
                {
                    if (!_syncStreams.TryGetValue(key2, out value2) || value2 == null)
                        throw new Exception("Received data for sync stream that does not exist");
                }

                if (num2 != value2.BytesReceived)
                    throw new Exception("Expected offset not matching with the amount of received bytes");
                if (readOnlySpan2.Length > 0) value2.Add(readOnlySpan2);
                break;
            }
            case StreamOpcode.END:
            {
                var readOnlySpan = data;
                var key = BinaryPrimitives.ReadInt32LittleEndian(readOnlySpan);
                readOnlySpan = readOnlySpan.Slice(4);
                var num = BinaryPrimitives.ReadInt32LittleEndian(readOnlySpan);
                readOnlySpan = readOnlySpan.Slice(4);
                SyncStream value = null;
                try
                {
                    lock (_syncStreams)
                    {
                        if (!_syncStreams.Remove(key, out value) || value == null)
                            throw new Exception("Received data for sync stream that does not exist");
                    }

                    if (num != value.BytesReceived)
                        throw new Exception("Expected offset not matching with the amount of received bytes");
                    if (readOnlySpan.Length > 0) value.Add(readOnlySpan);
                    if (!value.IsComplete) throw new Exception("After sync stream end, the stream must be complete");
                    HandlePacket(value.Opcode, value.SubOpcode, value.GetBytes(), value.ContentEncoding, sourceChannel);
                    break;
                }
                finally
                {
                    value?.Dispose();
                }
            }
        }
    }

    private void HandleRelay(RelayOpcode opcode, ReadOnlySpan<byte> data, ChannelRelayed? sourceChannel = null)
    {
        switch (opcode)
        {
            case RelayOpcode.RELAYED_DATA:
                HandleRelayedData(data);
                break;
            case RelayOpcode.RELAYED_ERROR:
                HandleRelayedError(data);
                break;
            case RelayOpcode.RELAY_ERROR:
                HandleRelayError(data);
                break;
            case RelayOpcode.ERROR:
                break;
        }
    }

    private void HandlePacket(Opcode opcode, byte subOpcode, ReadOnlySpan<byte> data, ContentEncoding contentEncoding,
        ChannelRelayed? sourceChannel = null)
    {
        if (Logger.WillLog(LogLevel.Debug))
            Logger.Debug<SyncSocketSession>(
                $"HandlePacket (opcode = {opcode}, subOpcode = {subOpcode}, data.length = {data.Length}, contentEncoding = {contentEncoding}, sourceChannel.ConnectionId = {sourceChannel?.ConnectionId})");
        if (contentEncoding == ContentEncoding.Gzip)
        {
            if (opcode != Opcode.DATA)
                throw new Exception(
                    $"Failed to handle packet, gzip is not supported for this opcode (opcode = {opcode}, subOpcode = {subOpcode}, data.length = {data.Length}).");
            using var stream = new MemoryStream(data.ToArray());
            using var memoryStream = new MemoryStream();
            using (var gZipStream = new GZipStream(stream, CompressionMode.Decompress))
            {
                gZipStream.CopyTo(memoryStream);
            }

            data = memoryStream.ToArray();
        }

        switch (opcode)
        {
            case Opcode.PING:
                Task.Run(async delegate
                {
                    _ = 1;
                    try
                    {
                        if (sourceChannel != null)
                            await sourceChannel.SendAsync(Opcode.PONG, 0);
                        else
                            await SendAsync(Opcode.PONG);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error<SyncSocketSession>("Failed to send pong: " + ex, ex);
                    }
                });
                Logger.Debug<SyncSocketSession>("Received PONG");
                return;
            case Opcode.PONG:
                Logger.Debug<SyncSocketSession>("Received PONG");
                return;
            case Opcode.RESPONSE:
                HandleResponse((ResponseOpcode)subOpcode, data, sourceChannel);
                return;
            case Opcode.REQUEST:
                HandleRequest((RequestOpcode)subOpcode, data, sourceChannel);
                return;
            case Opcode.NOTIFY:
                HandleNotify((NotifyOpcode)subOpcode, data, sourceChannel);
                return;
            case Opcode.RELAY:
                HandleRelay((RelayOpcode)subOpcode, data, sourceChannel);
                return;
        }

        if (!(sourceChannel != null ? sourceChannel.IsAuthorized : IsAuthorized))
        {
            Logger.Warning<SyncSocketSession>(
                $"Ignored message due to lack of authorization (opcode: {opcode}, subOpcode: {subOpcode}) because ");
            return;
        }

        switch (opcode)
        {
            case Opcode.STREAM:
                HandleStream((StreamOpcode)subOpcode, data, sourceChannel);
                break;
            case Opcode.DATA:
                if (sourceChannel != null)
                    sourceChannel.InvokeDataHandler(opcode, subOpcode, data);
                else
                    _onData?.Invoke(this, opcode, subOpcode, data);
                break;
            default:
                Logger.Warning<SyncSocketSession>(
                    $"Unknown opcode received (opcode = {opcode}, subOpcode = {subOpcode})");
                break;
        }
    }

    private void HandleRelayedData(ReadOnlySpan<byte> data)
    {
        if (data.Length < 8)
        {
            Logger.Error<SyncSocketSession>("RELAYED_DATA packet too short.");
            return;
        }

        var num = BinaryPrimitives.ReadInt64LittleEndian(data.Slice(0, 8));
        if (!_channels.TryGetValue(num, out var channel))
        {
            Logger.Error<SyncSocketSession>($"No channel found for connectionId {num}.");
            return;
        }

        var encryptedPayload = data.Slice(8);
        var (data2, length) = channel.Decrypt(encryptedPayload);
        try
        {
            HandleData(data2, length, channel);
        }
        catch (Exception ex)
        {
            Logger.Error<SyncSocketSession>("Exception while handling relayed data.", ex);
            if (channel != null)
                Task.Run(async delegate
                {
                    try
                    {
                        await channel.SendErrorAsync(RelayErrorCode.ConnectionClosed);
                    }
                    catch (Exception ex2)
                    {
                        Logger.Error<SyncSocketSession>("Exception while sending relayed error.", ex2);
                    }
                    finally
                    {
                        channel.Dispose();
                    }
                });
            _channels.TryRemove(num, out var _);
        }
    }

    private void HandleRelayedError(ReadOnlySpan<byte> data)
    {
        if (data.Length < 8)
        {
            Logger.Error<SyncSocketSession>("RELAYED_ERROR packet too short.");
            return;
        }

        var connectionId = BinaryPrimitives.ReadInt64LittleEndian(data.Slice(0, 8));
        if (!_channels.TryGetValue(connectionId, out var value) || value == null)
        {
            Logger.Error<SyncSocketSession>($"No channel found for connectionId {connectionId}.");
            Task.Run(async delegate
            {
                try
                {
                    await SendRelayError(connectionId, RelayErrorCode.NotFound);
                }
                catch (Exception ex2)
                {
                    Logger.Error<SyncSocketSession>("Exception while sending relay error.", ex2);
                }
            });
            return;
        }

        var encryptedPayload = data.Slice(8);
        try
        {
            var tuple = value.Decrypt(encryptedPayload);
            var item = tuple.decryptedPayload;
            var item2 = tuple.length;
            var value2 = (RelayErrorCode)BinaryPrimitives.ReadInt32LittleEndian(item.AsSpan(0, item2));
            Logger.Error<SyncSocketSession>(
                $"Received relayed error (errorCode = {value2}) on connection id {connectionId}, closing connection.");
        }
        catch (Exception ex)
        {
            Logger.Error<SyncSocketSession>("Exception while handling relayed error.", ex);
        }
        finally
        {
            value.Dispose();
            _channels.TryRemove(connectionId, out var _);
        }
    }

    public async Task SendRelayError(long connectionId, RelayErrorCode errorCode,
        CancellationToken cancellationToken = default)
    {
        Span<byte> span = stackalloc byte[12];
        BinaryPrimitives.WriteInt64LittleEndian(span.Slice(0, 8), connectionId);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(8, 4), (int)errorCode);
        await SendAsync(Opcode.RELAY, 4, span.ToArray(), 0, -1, ContentEncoding.Raw, cancellationToken);
    }

    private void HandleRelayError(ReadOnlySpan<byte> data)
    {
        if (data.Length < 12)
        {
            Logger.Error<SyncSocketSession>("RELAY_ERROR packet too short.");
            return;
        }

        var num = BinaryPrimitives.ReadInt64LittleEndian(data.Slice(0, 8));
        var value = (RelayErrorCode)BinaryPrimitives.ReadInt32LittleEndian(data.Slice(8, 4));
        if (!_channels.TryGetValue(num, out var value2) || value2 == null)
        {
            Logger.Error<SyncSocketSession>(
                $"Received error code {value} for non existant channel with connectionId {num}.");
            return;
        }

        Logger.Info<SyncSocketSession>(
            $"Received relay error (errorCode = {value}) on connection id {num}, closing connection.");
        value2.Dispose();
        _channels.TryRemove(num, out var _);
        var key = -1;
        foreach (KeyValuePair<int, (ChannelRelayed, TaskCompletionSource<ChannelRelayed>)> pendingChannel in
                 _pendingChannels)
            if (pendingChannel.Value.Item1 == value2)
            {
                key = pendingChannel.Key;
                break;
            }

        if (_pendingChannels.TryRemove(key, out (ChannelRelayed, TaskCompletionSource<ChannelRelayed>) value4))
            value4.Item2.TrySetCanceled();
    }

    private void HandleRequestTransportRelayed(ReadOnlySpan<byte> data)
    {
        if (data.Length < 52)
        {
            Logger.Error<SyncSocketSession>("HandleRequestRelayedTransport: Packet too short");
            return;
        }

        var num = 0;
        var remoteVersion = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(num, 4));
        num += 4;
        var num2 = BinaryPrimitives.ReadInt64LittleEndian(data.Slice(num, 8));
        num += 8;
        var requestId = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(num, 4));
        num += 4;
        var num3 = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(num, 4));
        num += 4;
        var inArray = data.Slice(num, 32).ToArray();
        num += 32;
        var num4 = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(num, 4));
        num += 4;
        if (num4 > 128)
            throw new InvalidDataException(
                $"Received (pairing message length: {num4}, app id: {num3}) exceeds maximum allowed size (128).");
        var array = Array.Empty<byte>();
        if (num4 > 0)
        {
            if (data.Length < num + num4 + 4)
            {
                Logger.Error<SyncSocketSession>(
                    $"HandleRequestRelayedTransport: Packet too short for pairing message (app id: {num3})");
                return;
            }

            array = data.Slice(num, num4).ToArray();
            num += num4;
        }

        var num5 = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(num, 4));
        if (data.Length != num + 4 + num5)
        {
            Logger.Error<SyncSocketSession>(
                $"HandleRequestRelayedTransport: Invalid packet size. Expected {num + 4 + num5}, got {data.Length}");
            return;
        }

        var channelHandshakeMessage = data.Slice(num + 4, num5).ToArray();
        var text = Convert.ToBase64String(inArray);
        string arg = null;
        if (num4 > 0)
        {
            var protocol = new Protocol(HandshakePattern.N, CipherFunction.ChaChaPoly, HashFunction.Blake2b);
            var privateKey = _localKeyPair.PrivateKey;
            using var handshakeState = protocol.Create(false, default, privateKey);
            var array2 = new byte[1024];
            handshakeState.ReadMessage(array, array2);
            arg = Encoding.UTF8.GetString(array2, 0, Array.IndexOf(array2, (byte)0, 0, Math.Min(32, array2.Length)));
        }

        if (text != LocalPublicKey)
        {
            var isHandshakeAllowed = _isHandshakeAllowed;
            if (isHandshakeAllowed == null || isHandshakeAllowed(LinkType.Relayed, this, text, arg, num3))
            {
                var channel = new ChannelRelayed(this, _localKeyPair, text, false);
                channel.ConnectionId = num2;
                _onNewChannel?.Invoke(this, channel);
                _channels[num2] = channel;
                Task.Run(async delegate
                {
                    try
                    {
                        await channel.SendResponseTransportAsync(remoteVersion, requestId, channelHandshakeMessage);
                        _onChannelEstablished?.Invoke(this, channel, true);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error<SyncSocketSession>("Failed to send relayed transport response", ex);
                    }
                });
                return;
            }
        }

        var rp = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(rp.AsSpan(0, 4), 2);
        BinaryPrimitives.WriteInt64LittleEndian(rp.AsSpan(4, 8), num2);
        BinaryPrimitives.WriteInt32LittleEndian(rp.AsSpan(12, 4), requestId);
        Task.Run(async delegate
        {
            try
            {
                await SendAsync(Opcode.RESPONSE, 1, rp);
            }
            catch (Exception ex)
            {
                Logger.Error<SyncSocketSession>("Failed to send relayed transport response", ex);
            }
        });
    }

    private ConnectionInfo ParseConnectionInfo(ReadOnlySpan<byte> data)
    {
        var b = data[0];
        var remoteIp = new IPAddress(data.Slice(1, b));
        var num = 1 + b;
        var array = data.Slice(num, 48).ToArray();
        var start = num + 48;
        var array2 = data.Slice(start).ToArray();
        var protocol = new Protocol(HandshakePattern.N, CipherFunction.ChaChaPoly, HashFunction.Blake2b);
        var privateKey = _localKeyPair.PrivateKey;
        using var handshakeState = protocol.Create(false, default, privateKey);
        var array3 = new byte[0];
        var item = handshakeState.ReadMessage(array, array3).Transport;
        var array4 = new byte[array2.Length - 16];
        if (item.ReadMessage(array2, array4) != array4.Length)
            throw new Exception("Decryption failed: incomplete data");
        ReadOnlySpan<byte> source = array4;
        var port = BinaryPrimitives.ReadUInt16LittleEndian(source);
        source = source.Slice(2);
        var b2 = source[0];
        source = source.Slice(1);
        var bytes = source.Slice(0, b2);
        var name = Encoding.UTF8.GetString(bytes);
        source = source.Slice(b2);
        var b3 = source[0];
        source = source.Slice(1);
        var list = new List<IPAddress>();
        for (var i = 0; i < b3; i++)
        {
            var address = source.Slice(0, 4);
            list.Add(new IPAddress(address));
            source = source.Slice(4);
        }

        var b4 = source[0];
        source = source.Slice(1);
        var list2 = new List<IPAddress>();
        for (var j = 0; j < b4; j++)
        {
            var address2 = source.Slice(0, 16);
            list2.Add(new IPAddress(address2));
            source = source.Slice(16);
        }

        var allowLocalDirect = source[0] != 0;
        source = source.Slice(1);
        var allowRemoteDirect = source[0] != 0;
        source = source.Slice(1);
        var allowRemoteHolePunched = source[0] != 0;
        source = source.Slice(1);
        var allowRemoteRelayed = source[0] != 0;
        return new ConnectionInfo(port, name, remoteIp, list, list2, allowLocalDirect, allowRemoteDirect,
            allowRemoteHolePunched, allowRemoteRelayed);
    }

    public async Task<bool> PublishRecordsAsync(IEnumerable<string> consumerPublicKeys, string key, byte[] data,
        ContentEncoding contentEncoding = ContentEncoding.Raw, CancellationToken cancellationToken = default)
    {
        var bytes = Encoding.UTF8.GetBytes(key);
        if (string.IsNullOrEmpty(key) || bytes.Length > 32)
            throw new ArgumentException("Key must be non-empty and at most 32 bytes.", "key");
        var list = consumerPublicKeys.ToList();
        if (list.Count == 0) throw new ArgumentException("At least one consumer is required.");
        var requestId = GenerateRequestId();
        var tcs = new TaskCompletionSource<bool>();
        _pendingPublishRequests[requestId] = tcs;
        cancellationToken.Register(delegate
        {
            if (_pendingPublishRequests.TryRemove(requestId, out var value2)) value2.TrySetCanceled();
        });
        try
        {
            var num = (data.Length + 65535 - 1) / 65535;
            var num2 = 48;
            for (var num3 = 0; num3 < num; num3++)
            {
                var num4 = Math.Min(65535, data.Length - num3 * 65535);
                num2 += 4 + num4 + 16;
            }

            var array = new byte[5 + bytes.Length + 1 + list.Count * (36 + num2)];
            var num5 = 0;
            BinaryPrimitives.WriteInt32LittleEndian(array.AsSpan(num5, 4), requestId);
            num5 += 4;
            array[num5] = (byte)bytes.Length;
            num5++;
            bytes.CopyTo(array.AsSpan(num5, bytes.Length));
            num5 += bytes.Length;
            array[num5] = (byte)list.Count;
            num5++;
            var array2 = ArrayPool<byte>.Shared.Rent(65551);
            foreach (var item2 in list)
            {
                var array3 = Convert.FromBase64String(item2);
                if (array3.Length != 32) throw new ArgumentException("Consumer public key must be 32 bytes: " + item2);
                var protocol = new Protocol(HandshakePattern.N, CipherFunction.ChaChaPoly, HashFunction.Blake2b);
                var rs = array3;
                using var handshakeState = protocol.Create(true, default, null, rs);
                var array4 = new byte[48];
                var item = handshakeState.WriteMessage(null, array4).Transport;
                array3.CopyTo(array.AsSpan(num5, 32));
                num5 += 32;
                BinaryPrimitives.WriteInt32LittleEndian(array.AsSpan(num5, 4), num2);
                num5 += 4;
                array4.CopyTo(array.AsSpan(num5, 48));
                num5 += 48;
                var num6 = 0;
                for (var num7 = 0; num7 < num; num7++)
                {
                    var num8 = Math.Min(65535, data.Length - num6);
                    var span = data.AsSpan(num6, num8);
                    item.WriteMessage(span, array2.AsSpan(0, num8 + 16));
                    BinaryPrimitives.WriteInt32LittleEndian(array.AsSpan(num5, 4), num8 + 16);
                    num5 += 4;
                    array2.AsSpan(0, num8 + 16).CopyTo(array.AsSpan(num5));
                    num5 += num8 + 16;
                    num6 += num8;
                }
            }

            ArrayPool<byte>.Shared.Return(array2);
            await SendAsync(Opcode.REQUEST, 7, array, 0, -1, contentEncoding, cancellationToken);
        }
        catch (Exception exception)
        {
            if (_pendingPublishRequests.TryRemove(requestId, out var value)) value.TrySetException(exception);
            throw;
        }

        return await tcs.Task;
    }

    public async Task<bool> PublishRecordAsync(string consumerPublicKey, string key, byte[] data,
        ContentEncoding contentEncoding = ContentEncoding.Raw, CancellationToken cancellationToken = default)
    {
        var bytes = Encoding.UTF8.GetBytes(key);
        if (string.IsNullOrEmpty(key) || bytes.Length > 32)
            throw new ArgumentException("Key must be non-empty and at most 32 bytes.", "key");
        var requestId = GenerateRequestId();
        var tcs = new TaskCompletionSource<bool>();
        _pendingPublishRequests[requestId] = tcs;
        cancellationToken.Register(delegate
        {
            if (_pendingPublishRequests.TryRemove(requestId, out var value2)) value2.TrySetCanceled();
        });
        try
        {
            var array = Convert.FromBase64String(consumerPublicKey);
            if (array.Length != 32)
                throw new ArgumentException("Consumer public key must be 32 bytes.", "consumerPublicKey");
            var num = (data.Length + 65519 - 1) / 65519;
            var num2 = 48;
            for (var num3 = 0; num3 < num; num3++)
            {
                var num4 = Math.Min(65519, data.Length - num3 * 65519);
                num2 += 4 + num4 + 16;
            }

            var array2 = new byte[37 + bytes.Length + 4 + num2];
            var num5 = 0;
            BinaryPrimitives.WriteInt32LittleEndian(array2.AsSpan(num5, 4), requestId);
            num5 += 4;
            array.CopyTo(array2.AsSpan(num5, 32));
            num5 += 32;
            array2[num5] = (byte)bytes.Length;
            num5++;
            bytes.CopyTo(array2.AsSpan(num5, bytes.Length));
            num5 += bytes.Length;
            BinaryPrimitives.WriteInt32LittleEndian(array2.AsSpan(num5, 4), num2);
            num5 += 4;
            var protocol = new Protocol(HandshakePattern.N, CipherFunction.ChaChaPoly, HashFunction.Blake2b);
            var rs = array;
            using var handshakeState = protocol.Create(true, default, null, rs);
            var array3 = new byte[48];
            var item = handshakeState.WriteMessage(null, array3).Transport;
            array3.CopyTo(array2.AsSpan(num5, 48));
            num5 += 48;
            var array4 = ArrayPool<byte>.Shared.Rent(65535);
            var num6 = 0;
            for (var num7 = 0; num7 < num; num7++)
            {
                var num8 = Math.Min(65519, data.Length - num6);
                var span = data.AsSpan(num6, num8);
                item.WriteMessage(span, array4.AsSpan(0, num8 + 16));
                BinaryPrimitives.WriteInt32LittleEndian(array2.AsSpan(num5, 4), num8 + 16);
                num5 += 4;
                array4.AsSpan(0, num8 + 16).CopyTo(array2.AsSpan(num5));
                num5 += num8 + 16;
                num6 += num8;
            }

            ArrayPool<byte>.Shared.Return(array4);
            Logger.Verbose<SyncSocketSession>($"Sent publish request with requestId {requestId}");
            await SendAsync(Opcode.REQUEST, 3, array2, 0, -1, contentEncoding, cancellationToken);
        }
        catch (Exception exception)
        {
            if (_pendingPublishRequests.TryRemove(requestId, out var value)) value.TrySetException(exception);
            throw;
        }

        return await tcs.Task;
    }

    public async Task<(byte[] Data, DateTime Timestamp)?> GetRecordAsync(string publisherPublicKey, string key,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key) || key.Length > 32)
            throw new ArgumentException("Key must be non-empty and at most 32 bytes.", "key");
        TaskCompletionSource<(byte[] Data, DateTime Timestamp)?> tcs = new();
        var requestId = GenerateRequestId();
        _pendingGetRecordRequests[requestId] = tcs;
        cancellationToken.Register(delegate
        {
            if (_pendingGetRecordRequests.TryRemove(requestId, out TaskCompletionSource<(byte[], DateTime)?> value2))
                value2.TrySetCanceled();
        });
        try
        {
            var array = Convert.FromBase64String(publisherPublicKey);
            if (array.Length != 32)
                throw new ArgumentException("Publisher public key must be 32 bytes.", "publisherPublicKey");
            var bytes = Encoding.UTF8.GetBytes(key);
            var array2 = new byte[37 + bytes.Length];
            var num = 0;
            BinaryPrimitives.WriteInt32LittleEndian(array2.AsSpan(num, 4), requestId);
            num += 4;
            array.CopyTo(array2.AsSpan(num, 32));
            num += 32;
            array2[num] = (byte)bytes.Length;
            num++;
            bytes.CopyTo(array2.AsSpan(num, bytes.Length));
            await SendAsync(Opcode.REQUEST, 6, array2, 0, -1, ContentEncoding.Raw, cancellationToken);
        }
        catch (Exception exception)
        {
            if (_pendingGetRecordRequests.TryRemove(requestId, out TaskCompletionSource<(byte[], DateTime)?> value))
                value.TrySetException(exception);
            throw;
        }

        return await tcs.Task;
    }

    public async Task<Dictionary<string, (byte[] Data, DateTime Timestamp)>> GetRecordsAsync(
        IEnumerable<string> publisherPublicKeys, string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key) || key.Length > 32)
            throw new ArgumentException("Key must be non-empty and at most 32 bytes.", "key");
        var list = publisherPublicKeys.ToList();
        if (list.Count == 0) return new Dictionary<string, (byte[], DateTime)>();
        var requestId = GenerateRequestId();
        var tcs = new TaskCompletionSource<Dictionary<string, (byte[], DateTime)>>();
        _pendingBulkGetRecordRequests[requestId] = tcs;
        cancellationToken.Register(delegate
        {
            if (_pendingBulkGetRecordRequests.TryRemove(requestId,
                    out TaskCompletionSource<Dictionary<string, (byte[], DateTime)>> value2)) value2.TrySetCanceled();
        });
        try
        {
            var bytes = Encoding.UTF8.GetBytes(key);
            var array = new byte[5 + bytes.Length + 1 + list.Count * 32];
            var num = 0;
            BinaryPrimitives.WriteInt32LittleEndian(array.AsSpan(num, 4), requestId);
            num += 4;
            array[num] = (byte)bytes.Length;
            num++;
            bytes.CopyTo(array.AsSpan(num, bytes.Length));
            num += bytes.Length;
            array[num] = (byte)list.Count;
            num++;
            foreach (var item in list)
            {
                var array2 = Convert.FromBase64String(item);
                if (array2.Length != 32) throw new ArgumentException("Publisher public key must be 32 bytes: " + item);
                array2.CopyTo(array.AsSpan(num, 32));
                num += 32;
            }

            await SendAsync(Opcode.REQUEST, 8, array, 0, -1, ContentEncoding.Raw, cancellationToken);
        }
        catch (Exception exception)
        {
            if (_pendingBulkGetRecordRequests.TryRemove(requestId,
                    out TaskCompletionSource<Dictionary<string, (byte[], DateTime)>> value))
                value.TrySetException(exception);
            throw;
        }

        return await tcs.Task;
    }

    public async Task<bool> DeleteRecordsAsync(string publisherPublicKey, string consumerPublicKey,
        IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        var list = keys.ToList();
        if (list.Any(k => Encoding.UTF8.GetByteCount(k) > 32))
            throw new ArgumentException("Keys must be at most 32 bytes.", "keys");
        var tcs = new TaskCompletionSource<bool>();
        var requestId = GenerateRequestId();
        _pendingDeleteRequests[requestId] = tcs;
        cancellationToken.Register(delegate
        {
            if (_pendingDeleteRequests.TryRemove(requestId, out var value2)) value2.TrySetCanceled();
        });
        try
        {
            var array = Convert.FromBase64String(publisherPublicKey);
            if (array.Length != 32)
                throw new ArgumentException("Publisher public key must be 32 bytes.", "publisherPublicKey");
            var array2 = Convert.FromBase64String(consumerPublicKey);
            if (array2.Length != 32)
                throw new ArgumentException("Consumer public key must be 32 bytes.", "consumerPublicKey");
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write(requestId);
            writer.Write(array);
            writer.Write(array2);
            writer.Write((byte)list.Count);
            foreach (var item in list)
            {
                var bytes = Encoding.UTF8.GetBytes(item);
                writer.Write((byte)bytes.Length);
                writer.Write(bytes);
            }

            var data = ms.ToArray();
            await SendAsync(Opcode.REQUEST, 10, data, 0, -1, ContentEncoding.Raw, cancellationToken);
        }
        catch (Exception exception)
        {
            if (_pendingDeleteRequests.TryRemove(requestId, out var value)) value.TrySetException(exception);
            throw;
        }

        return await tcs.Task;
    }

    public Task<bool> DeleteRecordAsync(string publisherPublicKey, string consumerPublicKey, string key,
        CancellationToken cancellationToken = default)
    {
        if (key.Length > 32) throw new ArgumentException("Key must be at most 32 bytes.", "key");
        var taskCompletionSource = new TaskCompletionSource<bool>();
        var requestId = GenerateRequestId();
        _pendingDeleteRequests[requestId] = taskCompletionSource;
        cancellationToken.Register(delegate
        {
            if (_pendingDeleteRequests.TryRemove(requestId, out var value2)) value2.TrySetCanceled();
        });
        try
        {
            var array = Convert.FromBase64String(publisherPublicKey);
            if (array.Length != 32)
                throw new ArgumentException("Publisher public key must be 32 bytes.", "publisherPublicKey");
            var array2 = Convert.FromBase64String(consumerPublicKey);
            if (array2.Length != 32)
                throw new ArgumentException("Consumer public key must be 32 bytes.", "consumerPublicKey");
            var bytes = Encoding.UTF8.GetBytes(key);
            if (bytes.Length > 32) throw new ArgumentException("Key must be at most 32 bytes.", "key");
            var array3 = new byte[69 + bytes.Length];
            var num = 0;
            BinaryPrimitives.WriteInt32LittleEndian(array3.AsSpan(num, 4), requestId);
            num += 4;
            array.CopyTo(array3.AsSpan(num, 32));
            num += 32;
            array2.CopyTo(array3.AsSpan(num, 32));
            num += 32;
            array3[num] = (byte)bytes.Length;
            num++;
            bytes.CopyTo(array3.AsSpan(num, bytes.Length));
            SendAsync(Opcode.REQUEST, 4, array3, 0, -1, ContentEncoding.Raw, cancellationToken).ContinueWith(
                delegate(Task t)
                {
                    if (t.IsFaulted && _pendingDeleteRequests.TryRemove(requestId, out var value2))
                        value2.TrySetException(t.Exception.InnerException);
                });
        }
        catch (Exception exception)
        {
            if (_pendingDeleteRequests.TryRemove(requestId, out var value)) value.TrySetException(exception);
            throw;
        }

        return taskCompletionSource.Task;
    }

    public Task<List<(string Key, DateTime Timestamp)>> ListRecordKeysAsync(string publisherPublicKey,
        string consumerPublicKey, CancellationToken cancellationToken = default)
    {
        var taskCompletionSource = new TaskCompletionSource<List<(string, DateTime)>>();
        var requestId = GenerateRequestId();
        _pendingListKeysRequests[requestId] = taskCompletionSource;
        cancellationToken.Register(delegate
        {
            if (_pendingListKeysRequests.TryRemove(requestId,
                    out TaskCompletionSource<List<(string, DateTime)>> value2)) value2.TrySetCanceled();
        });
        try
        {
            var array = Convert.FromBase64String(publisherPublicKey);
            if (array.Length != 32)
                throw new ArgumentException("Publisher public key must be 32 bytes.", "publisherPublicKey");
            var array2 = Convert.FromBase64String(consumerPublicKey);
            if (array2.Length != 32)
                throw new ArgumentException("Consumer public key must be 32 bytes.", "consumerPublicKey");
            var array3 = new byte[68];
            BinaryPrimitives.WriteInt32LittleEndian(array3.AsSpan(0, 4), requestId);
            array.CopyTo(array3.AsSpan(4, 32));
            array2.CopyTo(array3.AsSpan(36, 32));
            SendAsync(Opcode.REQUEST, 5, array3, 0, -1, ContentEncoding.Raw, cancellationToken).ContinueWith(
                delegate(Task t)
                {
                    if (t.IsFaulted && _pendingListKeysRequests.TryRemove(requestId,
                            out TaskCompletionSource<List<(string, DateTime)>> value2))
                        value2.TrySetException(t.Exception.InnerException);
                });
        }
        catch (Exception exception)
        {
            if (_pendingListKeysRequests.TryRemove(requestId, out TaskCompletionSource<List<(string, DateTime)>> value))
                value.TrySetException(exception);
            throw;
        }

        return taskCompletionSource.Task;
    }
}