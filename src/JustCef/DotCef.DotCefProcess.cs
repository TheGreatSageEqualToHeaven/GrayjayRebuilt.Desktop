// DotCef, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// DotCef.DotCefProcess

using System.Buffers;
using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace DotCef;

public class DotCefProcess : IDisposable
{
    public enum OpcodeClient : byte
    {
        Ping,
        Print,
        Echo,
        WindowProxyRequest,
        WindowModifyRequest,
        StreamClose
    }

    public enum OpcodeClientNotification : byte
    {
        Ready = 0,
        Exit = 1,
        WindowOpened = 2,
        WindowClosed = 3,
        WindowFocused = 5,
        WindowUnfocused = 6,
        WindowFullscreenChanged = 12,
        WindowLoadStart = 13,
        WindowLoadEnd = 14,
        WindowLoadError = 15,
        WindowDevToolsEvent = 16
    }

    public enum OpcodeController : byte
    {
        Ping = 0,
        Print = 1,
        Echo = 2,
        WindowCreate = 3,
        WindowSetDevelopmentToolsEnabled = 5,
        WindowLoadUrl = 6,
        WindowGetPosition = 14,
        WindowSetPosition = 15,
        WindowMaximize = 17,
        WindowMinimize = 18,
        WindowRestore = 19,
        WindowShow = 20,
        WindowHide = 21,
        WindowClose = 22,
        WindowRequestFocus = 25,
        WindowActivate = 28,
        WindowBringToTop = 29,
        WindowSetAlwaysOnTop = 30,
        WindowSetFullscreen = 31,
        WindowCenterSelf = 32,
        WindowSetProxyRequests = 33,
        WindowSetModifyRequests = 34,
        StreamOpen = 35,
        StreamClose = 36,
        StreamData = 37,
        PickFile = 38,
        PickDirectory = 39,
        SaveFile = 40,
        WindowExecuteDevToolsMethod = 41,
        WindowSetDevelopmentToolsVisible = 42,
        WindowSetTitle = 43,
        WindowSetIcon = 44,
        WindowAddUrlToProxy = 45,
        WindowRemoveUrlToProxy = 46,
        WindowAddUrlToModify = 47,
        WindowRemoveUrlToModify = 48,
        WindowGetSize = 49,
        WindowSetSize = 50,
        WindowAddDevToolsEventMethod = 51,
        WindowRemoveDevToolsEventMethod = 52,
        WindowAddDomainToProxy = 53,
        WindowRemoveDomainToProxy = 54
    }

    public enum OpcodeControllerNotification : byte
    {
        Exit
    }

    public enum PacketType : byte
    {
        Request,
        Response,
        Notification
    }

    private const int MaxIPCSize = 10485760;

    private const int HeaderSize = 10;

    private static readonly ArrayPool<byte> BufferPool = ArrayPool<byte>.Create();

    private readonly Dictionary<uint, PendingRequest> _pendingRequests = new();

    private readonly AnonymousPipeServerStream _reader;

    private readonly TaskCompletionSource _readyTaskCompletionSource = new();

    private readonly List<DotCefWindow> _windows = new();

    private readonly AnonymousPipeServerStream _writer;

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private Process? _childProcess;

    private uint _requestIdCounter;

    private bool _started;

    private readonly Dictionary<uint, CancellationTokenSource> _streamCancellationTokens = new();

    private uint _streamIdentifierGenerator;

    private readonly SemaphoreSlim _writeSemaphore = new(1);

    public DotCefProcess()
    {
        _writer = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
        _reader = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        AppDomain.CurrentDomain.ProcessExit += delegate
        {
            _writer.Dispose();
            _reader.Dispose();
        };
    }

    public List<DotCefWindow> Windows
    {
        get
        {
            lock (_windows)
            {
                return _windows.ToList();
            }
        }
    }

    public bool HasExited
    {
        get
        {
            try
            {
                return _childProcess?.HasExited ?? true;
            }
            catch
            {
                return true;
            }
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _writer.Dispose();
        _reader.Dispose();
        _childProcess?.Close();
        _readyTaskCompletionSource.TrySetCanceled();
        lock (_pendingRequests)
        {
            foreach (var pendingRequest in _pendingRequests)
                pendingRequest.Value.ResponseBodyTaskCompletionSource.TrySetCanceled();
            _pendingRequests.Clear();
        }

        lock (_windows)
        {
            var array = _windows.ToArray();
            for (var i = 0; i < array.Length; i++) array[i].InvokeOnClose();
            _windows.Clear();
        }

        lock (_streamCancellationTokens)
        {
            foreach (var streamCancellationToken in _streamCancellationTokens) streamCancellationToken.Value.Cancel();
            _streamCancellationTokens.Clear();
        }

        _writeSemaphore.Dispose();
    }

    public DotCefWindow? GetWindow(int identifier)
    {
        lock (_windows)
        {
            return _windows.FirstOrDefault(v => v.Identifier == identifier);
        }
    }

    public void Start(string? args = null)
    {
        if (_started) throw new Exception("Already started.");
        _started = true;
        string text = null;
        var array = GenerateSearchPaths();
        Logger.Info<DotCefProcess>("Searching for dotcefnative, search paths:");
        var array2 = array;
        foreach (var text2 in array2) Logger.Info<DotCefProcess>(" - " + text2);
        array2 = array;
        foreach (var text3 in array2)
            if (File.Exists(text3))
                text = text3;
        if (text == null) throw new Exception("Failed to find dotcefnative");
        var directory = GetDirectory(text);
        Logger.Info<DotCefProcess>("Working directory '" + directory + "'.");
        Logger.Info<DotCefProcess>("CEF exe path '" + text + "'.");
        if (!File.Exists(text))
        {
            Logger.Error<DotCefProcess>("File not found at native path '" + text + "'.");
            throw new Exception("Native executable not found.");
        }

        var processStartInfo = new ProcessStartInfo();
        processStartInfo.FileName = text;
        processStartInfo.WorkingDirectory = directory;
        processStartInfo.Arguments = "--change-stack-guard-on-fork=disable --parent-to-child " +
                                     _writer.GetClientHandleAsString() + " --child-to-parent " +
                                     _reader.GetClientHandleAsString() + (string.IsNullOrEmpty(args) ? "" : " " + args);
        processStartInfo.UseShellExecute = false;
        processStartInfo.RedirectStandardError = true;
        processStartInfo.RedirectStandardOutput = true;
        var processStartInfo2 = processStartInfo;
        Logger.Info<DotCefProcess>(processStartInfo2.Arguments);
        var process = new Process();
        process.StartInfo = processStartInfo2;
        process.ErrorDataReceived += delegate(object _, DataReceivedEventArgs e)
        {
            var text4 = e?.Data;
            if (text4 != null) Logger.Info<DotCefProcess>(text4);
        };
        process.OutputDataReceived += delegate(object _, DataReceivedEventArgs e)
        {
            var text4 = e?.Data;
            if (text4 != null) Logger.Info<DotCefProcess>(text4);
        };
        if (!process.Start()) throw new Exception("Failed to start process.");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        _childProcess = process;
        AppDomain.CurrentDomain.ProcessExit += delegate { _childProcess?.Dispose(); };
        _writer.DisposeLocalCopyOfClientHandle();
        _reader.DisposeLocalCopyOfClientHandle();
        Task.Run(async delegate
        {
            _ = 1;
            try
            {
                Logger.Info<DotCefProcess>("Receive loop started.");
                var headerBuffer = new byte[10];
                while (!_cancellationTokenSource.IsCancellationRequested && !HasExited)
                {
                    await _reader.ReadExactlyAsync(headerBuffer, 0, 10, _cancellationTokenSource.Token);
                    var num = BitConverter.ToUInt32(headerBuffer, 0);
                    var requestId = BitConverter.ToUInt32(headerBuffer, 4);
                    var packetType = (PacketType)headerBuffer[8];
                    var opcode = headerBuffer[9];
                    var num2 = (int)(num + 4 - 10);
                    if (num2 > 10485760)
                    {
                        Logger.Error<DotCefProcess>("Invalid packet size. Shutting down.");
                        Dispose();
                        break;
                    }

                    RentedBuffer<byte>? rentedBodyBuffer = null;
                    if (num2 > 0)
                    {
                        var rb = new RentedBuffer<byte>(BufferPool, num2);
                        await _reader.ReadExactlyAsync(rb.Buffer, 0, num2, _cancellationTokenSource.Token);
                        rentedBodyBuffer = rb;
                    }

                    Task.Run(async delegate
                    {
                        _ = 2;
                        try
                        {
                            if (packetType == PacketType.Response)
                            {
                                bool flag;
                                PendingRequest value;
                                lock (_pendingRequests)
                                {
                                    flag = _pendingRequests.TryGetValue(requestId, out value);
                                }

                                if (flag && value != null)
                                {
                                    var responseBodyTaskCompletionSource = value.ResponseBodyTaskCompletionSource;
                                    byte[] result;
                                    if (!rentedBodyBuffer.HasValue)
                                    {
                                        result = Array.Empty<byte>();
                                    }
                                    else
                                    {
                                        var span = rentedBodyBuffer.Value.Buffer.AsSpan();
                                        span = span.Slice(0, rentedBodyBuffer.Value.Length);
                                        result = span.ToArray();
                                    }

                                    responseBodyTaskCompletionSource.SetResult(result);
                                }
                                else
                                {
                                    Logger.Error<DotCefProcess>(
                                        $"Received a packet response for a request that no longer has an awaiter (request id = {requestId}).");
                                }
                            }
                            else if (packetType == PacketType.Request)
                            {
                                var reader =
                                    new PacketReader(
                                        rentedBodyBuffer.HasValue ? rentedBodyBuffer.Value.Buffer : Array.Empty<byte>(),
                                        rentedBodyBuffer.HasValue ? rentedBodyBuffer.Value.Length : 0);
                                var packetWriter = new PacketWriter();
                                await HandleRequestAsync((OpcodeClient)opcode, reader, packetWriter);
                                var packetSize = 10 + packetWriter.Size;
                                var rentedBuffer = new RentedBuffer<byte>(BufferPool, packetSize);
                                try
                                {
                                    using (var output = new MemoryStream(rentedBuffer.Buffer, 0, packetSize))
                                    {
                                        using var binaryWriter = new BinaryWriter(output);
                                        binaryWriter.Write((uint)(packetSize - 4));
                                        binaryWriter.Write(requestId);
                                        binaryWriter.Write((byte)1);
                                        binaryWriter.Write(opcode);
                                        if (packetWriter.Size > 0)
                                            binaryWriter.Write(packetWriter.Data, 0, packetWriter.Size);
                                    }

                                    try
                                    {
                                        await _writeSemaphore.WaitAsync(_cancellationTokenSource.Token);
                                        await _writer.WriteAsync(rentedBuffer.Buffer, 0, packetSize,
                                            _cancellationTokenSource.Token);
                                    }
                                    finally
                                    {
                                        _writeSemaphore.Release();
                                    }
                                }
                                finally
                                {
                                    rentedBuffer.Dispose();
                                }
                            }
                            else if (packetType == PacketType.Notification)
                            {
                                var reader2 =
                                    new PacketReader(
                                        rentedBodyBuffer.HasValue ? rentedBodyBuffer.Value.Buffer : Array.Empty<byte>(),
                                        rentedBodyBuffer.HasValue ? rentedBodyBuffer.Value.Length : 0);
                                HandleNotification((OpcodeClientNotification)opcode, reader2);
                            }
                        }
                        catch (Exception ex2)
                        {
                            Logger.Error<DotCefProcess>("An exception occurred in the IPC while handling a packet",
                                ex2);
                        }
                        finally
                        {
                            rentedBodyBuffer?.Dispose();
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Error<DotCefProcess>("An exception occurred in the IPC", ex);
            }
            finally
            {
                Logger.Info<DotCefProcess>("Receive loop stopped.");
                Dispose();
            }
        });
    }

    private async Task HandleRequestAsync(OpcodeClient opcode, PacketReader reader, PacketWriter writer)
    {
        try
        {
            switch (opcode)
            {
                case OpcodeClient.Print:
                    Logger.Info<DotCefProcess>(reader.ReadString(reader.RemainingSize));
                    break;
                case OpcodeClient.Echo:
                    writer.WriteBytes(reader.ReadBytes(reader.RemainingSize));
                    break;
                case OpcodeClient.WindowProxyRequest:
                    await HandleWindowProxyRequestAsync(reader, writer);
                    break;
                case OpcodeClient.WindowModifyRequest:
                    HandleWindowModifyRequest(reader, writer);
                    break;
                case OpcodeClient.StreamClose:
                {
                    var key = reader.Read<uint>();
                    lock (_streamCancellationTokens)
                    {
                        if (_streamCancellationTokens.TryGetValue(key, out var value))
                        {
                            value.Cancel();
                            _streamCancellationTokens.Remove(key);
                        }

                        break;
                    }
                }
                default:
                    Logger.Warning<DotCefProcess>($"Received unhandled opcode {opcode}.");
                    break;
                case OpcodeClient.Ping:
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error<DotCefProcess>("Exception occurred while processing call", ex);
            Debugger.Break();
        }
    }

    private async Task HandleWindowProxyRequestAsync(PacketReader reader, PacketWriter writer)
    {
        var identifier = reader.Read<int>();
        var window = GetWindow(identifier);
        if (window == null) return;
        var method = reader.ReadSizePrefixedString();
        var url = reader.ReadSizePrefixedString();
        var num = reader.Read<int>();
        var dictionary = new Dictionary<string, List<string>>(StringComparer.InvariantCultureIgnoreCase);
        for (var i = 0; i < num; i++)
        {
            var key = reader.ReadSizePrefixedString();
            var item = reader.ReadSizePrefixedString();
            if (dictionary.TryGetValue(key, out var value))
                value.Add(item);
            else
                dictionary[key] = new List<string>([item]);
        }

        var num2 = reader.Read<uint>();
        var list = new List<IPCProxyBodyElement>();
        for (var num3 = 0u; num3 < num2; num3++)
        {
            var iPCProxyBodyElementType = (IPCProxyBodyElementType)reader.Read<byte>();
            switch (iPCProxyBodyElementType)
            {
                case IPCProxyBodyElementType.Bytes:
                {
                    var size = reader.Read<uint>();
                    var data = reader.ReadBytes((int)size);
                    list.Add(new IPCProxyBodyElementBytes
                    {
                        Type = iPCProxyBodyElementType,
                        Data = data
                    });
                    break;
                }
                case IPCProxyBodyElementType.File:
                {
                    var fileName = reader.ReadSizePrefixedString();
                    list.Add(new IPCProxyBodyElementFile
                    {
                        Type = iPCProxyBodyElementType,
                        FileName = fileName
                    });
                    break;
                }
            }
        }

        var iPCResponse = await window.ProxyRequestAsync(new IPCRequest
        {
            Method = method,
            Url = url,
            Headers = dictionary,
            Elements = list
        });
        if (iPCResponse == null) return;
        var dictionary2 = new Dictionary<string, List<string>>(
            iPCResponse.Headers.Where(header =>
                !string.Equals(header.Key, "transfer-encoding", StringComparison.InvariantCultureIgnoreCase) ||
                !header.Value.Any(v => string.Equals(v, "chunked", StringComparison.InvariantCultureIgnoreCase))
                    ? true
                    : false), StringComparer.OrdinalIgnoreCase);
        writer.Write((uint)iPCResponse.StatusCode);
        writer.WriteSizePrefixedString(iPCResponse.StatusText);
        writer.Write(dictionary2.Count());
        foreach (var item2 in dictionary2)
            if (!string.Equals(item2.Key, "transfer-encoding", StringComparison.InvariantCultureIgnoreCase) ||
                !item2.Value.Any(v => string.Equals(v, "chunked", StringComparison.InvariantCultureIgnoreCase)))
            {
                writer.WriteSizePrefixedString(item2.Key);
                writer.WriteSizePrefixedString(string.Join(", ", item2.Value));
            }

        dictionary2.ContainsKey("content-type");
        string.Compare(method, "head", true);
        List<string> value2;
        int? contentLength = iPCResponse.Headers.TryGetValue("content-length", out value2) && value2.Count > 0
            ? int.Parse(value2[0])
            : null;
        if (iPCResponse.BodyStream != null)
        {
            if (contentLength.HasValue)
            {
                if (contentLength < 10485760 - writer.Size)
                {
                    writer.Write((byte)1);
                    writer.Write((uint)contentLength.Value);
                    var buffer = ArrayPool<byte>.Shared.Rent(contentLength.Value);
                    try
                    {
                        await iPCResponse.BodyStream.ReadExactlyAsync(buffer, 0, contentLength.Value);
                        writer.WriteBytes(buffer, 0, contentLength.Value);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
                else
                {
                    writer.Write((byte)2);
                    await HandleLargeOrChunkedContentAsync(iPCResponse.BodyStream, writer, contentLength);
                }
            }
            else
            {
                writer.Write((byte)2);
                await HandleLargeOrChunkedContentAsync(iPCResponse.BodyStream, writer);
            }
        }
        else
        {
            writer.Write((byte)0);
        }
    }

    private async Task HandleLargeOrChunkedContentAsync(Stream stream, PacketWriter writer, long? contentLength = null)
    {
        var streamIdentifier = Interlocked.Increment(ref _streamIdentifierGenerator);
        var cancellationTokenSource = new CancellationTokenSource();
        lock (_streamCancellationTokens)
        {
            _streamCancellationTokens[streamIdentifier] = cancellationTokenSource;
        }

        await StreamOpenAsync(streamIdentifier, cancellationTokenSource.Token);
        writer.Write(streamIdentifier);
        Task.Run(async delegate
        {
            try
            {
                _ = 1;
                try
                {
                    var buffer = new byte[65536];
                    var totalBytesRead = 0L;
                    do
                    {
                        int num;
                        var bytesRead = num = await stream.ReadAsync(buffer, 0,
                            (int)(contentLength.HasValue
                                ? Math.Min(buffer.Length, contentLength.Value - totalBytesRead)
                                : buffer.Length), cancellationTokenSource.Token);
                        if (num <= 0) break;
                        cancellationTokenSource.Token.ThrowIfCancellationRequested();
                        if (!await StreamDataAsync(streamIdentifier, buffer, 0, bytesRead,
                                cancellationTokenSource.Token)) throw new Exception("Stream closed.");
                        totalBytesRead += bytesRead;
                    } while (!contentLength.HasValue || totalBytesRead < contentLength.Value);
                }
                catch (Exception ex)
                {
                    Logger.Error<DotCefProcess>("Failed to stream body", ex);
                }
            }
            finally
            {
                stream.Close();
                stream.Dispose();
                await StreamCloseAsync(streamIdentifier);
                lock (_streamCancellationTokens)
                {
                    _streamCancellationTokens.Remove(streamIdentifier);
                }
            }
        });
    }

    private void HandleWindowModifyRequest(PacketReader reader, PacketWriter writer)
    {
        var identifier = reader.Read<int>();
        var window = GetWindow(identifier);
        if (window == null) return;
        var method = reader.ReadSizePrefixedString();
        var url = reader.ReadSizePrefixedString();
        var num = reader.Read<int>();
        var dictionary = new Dictionary<string, List<string>>(StringComparer.InvariantCultureIgnoreCase);
        for (var i = 0; i < num; i++)
        {
            var key = reader.ReadSizePrefixedString();
            var item = reader.ReadSizePrefixedString();
            if (dictionary.TryGetValue(key, out var value))
                value.Add(item);
            else
                dictionary[key] = new List<string>([item]);
        }

        var num2 = reader.Read<uint>();
        var list = new List<IPCProxyBodyElement>();
        for (var num3 = 0u; num3 < num2; num3++)
        {
            var iPCProxyBodyElementType = (IPCProxyBodyElementType)reader.Read<byte>();
            switch (iPCProxyBodyElementType)
            {
                case IPCProxyBodyElementType.Bytes:
                {
                    var size = reader.Read<uint>();
                    var data = reader.ReadBytes((int)size);
                    list.Add(new IPCProxyBodyElementBytes
                    {
                        Type = iPCProxyBodyElementType,
                        Data = data
                    });
                    break;
                }
                case IPCProxyBodyElementType.File:
                {
                    var fileName = reader.ReadSizePrefixedString();
                    list.Add(new IPCProxyBodyElementFile
                    {
                        Type = iPCProxyBodyElementType,
                        FileName = fileName
                    });
                    break;
                }
            }
        }

        var iPCRequest = window.ModifyRequest(new IPCRequest
        {
            Method = method,
            Url = url,
            Headers = dictionary,
            Elements = list
        });
        if (iPCRequest == null) return;
        writer.WriteSizePrefixedString(iPCRequest.Method);
        writer.WriteSizePrefixedString(iPCRequest.Url);
        writer.Write(iPCRequest.Headers.Count);
        foreach (var header in iPCRequest.Headers)
        foreach (var item2 in header.Value)
        {
            writer.WriteSizePrefixedString(header.Key);
            writer.WriteSizePrefixedString(item2);
        }

        writer.Write((uint)iPCRequest.Elements.Count);
        foreach (var element in iPCRequest.Elements)
        {
            writer.Write((byte)element.Type);
            if (element is IPCProxyBodyElementBytes iPCProxyBodyElementBytes)
            {
                writer.Write((uint)iPCProxyBodyElementBytes.Data.Length);
                writer.WriteBytes(iPCProxyBodyElementBytes.Data);
            }
            else if (element is IPCProxyBodyElementFile iPCProxyBodyElementFile)
            {
                writer.WriteSizePrefixedString(iPCProxyBodyElementFile.FileName);
            }
        }
    }

    private void HandleNotification(OpcodeClientNotification opcode, PacketReader reader)
    {
        Logger.Info<DotCefProcess>($"Received notification {opcode}");
        switch (opcode)
        {
            case OpcodeClientNotification.Exit:
                Logger.Info<DotCefProcess>("CEF process is exiting.");
                Dispose();
                break;
            case OpcodeClientNotification.Ready:
                Logger.Info<DotCefProcess>("Client is ready.");
                _readyTaskCompletionSource.SetResult();
                break;
            case OpcodeClientNotification.WindowOpened:
                Logger.Info<DotCefProcess>($"Window opened: {reader.Read<int>()}");
                break;
            case OpcodeClientNotification.WindowClosed:
            {
                DotCefWindow dotCefWindow;
                lock (_windows)
                {
                    var identifier5 = reader.Read<int>();
                    dotCefWindow = _windows.FirstOrDefault(v => v.Identifier == identifier5);
                    if (dotCefWindow != null) _windows.Remove(dotCefWindow);
                }

                Logger.Info<DotCefProcess>($"Window closed: {dotCefWindow}");
                dotCefWindow?.InvokeOnClose();
                break;
            }
            case OpcodeClientNotification.WindowFocused:
                GetWindow(reader.Read<int>())?.InvokeOnFocused();
                break;
            case OpcodeClientNotification.WindowUnfocused:
                GetWindow(reader.Read<int>())?.InvokeOnUnfocused();
                break;
            case OpcodeClientNotification.WindowFullscreenChanged:
            {
                var identifier6 = reader.Read<int>();
                var fullscreen = reader.Read<bool>();
                GetWindow(identifier6)?.InvokeOnFullscreenChanged(fullscreen);
                break;
            }
            case OpcodeClientNotification.WindowLoadStart:
            {
                var identifier4 = reader.Read<int>();
                var url2 = reader.ReadSizePrefixedString();
                GetWindow(identifier4)?.InvokeOnLoadStart(url2);
                break;
            }
            case OpcodeClientNotification.WindowLoadEnd:
            {
                var identifier3 = reader.Read<int>();
                var url = reader.ReadSizePrefixedString();
                GetWindow(identifier3)?.InvokeOnLoadEnd(url);
                break;
            }
            case OpcodeClientNotification.WindowLoadError:
            {
                var identifier2 = reader.Read<int>();
                var errorCode = reader.Read<int>();
                var errorText = reader.ReadSizePrefixedString();
                var failedUrl = reader.ReadSizePrefixedString();
                GetWindow(identifier2)?.InvokeOnLoadError(errorCode, errorText, failedUrl);
                break;
            }
            case OpcodeClientNotification.WindowDevToolsEvent:
            {
                var identifier = reader.Read<int>();
                var method = reader.ReadSizePrefixedString();
                var size = reader.Read<int>();
                var parameters = reader.ReadBytes(size);
                GetWindow(identifier)?.InvokeOnDevToolsEvent(method, parameters);
                break;
            }
            default:
                Logger.Info<DotCefProcess>($"Received unhandled notification opcode {opcode}.");
                break;
        }
    }

    public static RentedBuffer<byte> RentedBytesFromStruct<TStruct>(TStruct s) where TStruct : struct
    {
        var readOnlySpan = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in s, 1));
        var result = new RentedBuffer<byte>(BufferPool, readOnlySpan.Length);
        readOnlySpan.CopyTo(result.Buffer);
        return result;
    }

    public static TStruct BytesToStruct<TStruct>(byte[] bytes) where TStruct : struct
    {
        return MemoryMarshal.Read<TStruct>(new ReadOnlySpan<byte>(bytes));
    }

    public async Task<PacketReader> CallAsync(OpcodeController opcode, PacketWriter writer,
        CancellationToken cancellationToken = default)
    {
        return await CallAsync(opcode, writer.Data, 0, writer.Size, cancellationToken);
    }

    public async Task<PacketReader> CallAsync<TRequest>(OpcodeController opcode, TRequest request,
        CancellationToken cancellationToken = default) where TRequest : struct
    {
        var requestBody = RentedBytesFromStruct(request);
        try
        {
            return await CallAsync(opcode, requestBody.Buffer, 0, requestBody.Length, cancellationToken);
        }
        finally
        {
            requestBody.Dispose();
        }
    }

    public async Task<TResult> CallAsync<TRequest, TResult>(OpcodeController opcode, TRequest request,
        CancellationToken cancellationToken = default) where TRequest : unmanaged where TResult : unmanaged
    {
        var requestBody = RentedBytesFromStruct(request);
        PacketReader packetReader;
        try
        {
            packetReader = await CallAsync(opcode, requestBody.Buffer, 0, requestBody.Length, cancellationToken);
            if (packetReader.RemainingSize < Unsafe.SizeOf<TResult>())
                throw new InvalidOperationException("Response does not contain enough data to fill TResult.");
        }
        finally
        {
            requestBody.Dispose();
        }

        return packetReader.Read<TResult>();
    }

    private async Task<PacketReader> CallAsync(OpcodeController opcode, CancellationToken cancellationToken = default)
    {
        EnsureStarted();
        var linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, cancellationToken);
        var requestId = Interlocked.Increment(ref _requestIdCounter);
        var pendingRequest = new PendingRequest(opcode, requestId);
        lock (_pendingRequests)
        {
            _pendingRequests[requestId] = pendingRequest;
        }

        var packetLength = 10;
        var rentedBuffer = new RentedBuffer<byte>(BufferPool, packetLength);
        try
        {
            using (var output = new MemoryStream(rentedBuffer.Buffer, 0, packetLength))
            {
                using var binaryWriter = new BinaryWriter(output);
                binaryWriter.Write((uint)(packetLength - 4));
                binaryWriter.Write(requestId);
                binaryWriter.Write((byte)0);
                binaryWriter.Write((byte)opcode);
            }

            try
            {
                await _writeSemaphore.WaitAsync(linkedCts.Token);
                await _writer.WriteAsync(rentedBuffer.Buffer, 0, packetLength, linkedCts.Token);
            }
            finally
            {
                _writeSemaphore.Release();
            }

            byte[] data;
            using (linkedCts.Token.Register(delegate
                   {
                       pendingRequest.ResponseBodyTaskCompletionSource.TrySetCanceled();
                   }))
            {
                _ = 2;
                try
                {
                    data = await pendingRequest.ResponseBodyTaskCompletionSource.Task;
                }
                finally
                {
                    lock (_pendingRequests)
                    {
                        _pendingRequests.Remove(requestId);
                    }
                }
            }

            return new PacketReader(data);
        }
        finally
        {
            rentedBuffer.Dispose();
        }
    }

    private async Task<PacketReader> CallAsync(OpcodeController opcode, byte[] body,
        CancellationToken cancellationToken = default)
    {
        return await CallAsync(opcode, body, 0, body.Length, cancellationToken);
    }

    private async Task<PacketReader> CallAsync(OpcodeController opcode, byte[] body, int offset, int size,
        CancellationToken cancellationToken = default)
    {
        EnsureStarted();
        var linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, cancellationToken);
        var requestId = Interlocked.Increment(ref _requestIdCounter);
        var pendingRequest = new PendingRequest(opcode, requestId);
        lock (_pendingRequests)
        {
            _pendingRequests[requestId] = pendingRequest;
        }

        var packetLength = 10 + size;
        var rentedBuffer = new RentedBuffer<byte>(BufferPool, packetLength);
        try
        {
            using (var output = new MemoryStream(rentedBuffer.Buffer, 0, packetLength))
            {
                using var binaryWriter = new BinaryWriter(output);
                binaryWriter.Write((uint)(packetLength - 4));
                binaryWriter.Write(requestId);
                binaryWriter.Write((byte)0);
                binaryWriter.Write((byte)opcode);
                if (size > 0) binaryWriter.Write(body, offset, size);
            }

            try
            {
                await _writeSemaphore.WaitAsync(linkedCts.Token);
                await _writer.WriteAsync(rentedBuffer.Buffer, 0, packetLength, linkedCts.Token);
            }
            finally
            {
                _writeSemaphore.Release();
            }

            byte[] data;
            using (linkedCts.Token.Register(delegate
                   {
                       pendingRequest.ResponseBodyTaskCompletionSource.TrySetCanceled();
                   }))
            {
                _ = 2;
                try
                {
                    data = await pendingRequest.ResponseBodyTaskCompletionSource.Task;
                }
                finally
                {
                    lock (_pendingRequests)
                    {
                        _pendingRequests.Remove(requestId);
                    }
                }
            }

            return new PacketReader(data);
        }
        finally
        {
            rentedBuffer.Dispose();
        }
    }

    private async Task NotifyAsync(OpcodeControllerNotification opcode, byte[] body, int offset, int size,
        CancellationToken cancellationToken = default)
    {
        EnsureStarted();
        var linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, cancellationToken);
        var packetLength = 10 + size;
        var rentedBuffer = new RentedBuffer<byte>(BufferPool, packetLength);
        try
        {
            using (var output = new MemoryStream(rentedBuffer.Buffer, 0, packetLength))
            {
                using var binaryWriter = new BinaryWriter(output);
                binaryWriter.Write((uint)(packetLength - 4));
                binaryWriter.Write(0u);
                binaryWriter.Write((byte)2);
                binaryWriter.Write((byte)opcode);
                if (size > 0) binaryWriter.Write(body, offset, size);
            }

            try
            {
                await _writeSemaphore.WaitAsync(linkedCts.Token);
                await _writer.WriteAsync(rentedBuffer.Buffer, 0, packetLength, linkedCts.Token);
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }
        finally
        {
            rentedBuffer.Dispose();
        }
    }

    private async Task NotifyAsync(OpcodeControllerNotification opcode, CancellationToken cancellationToken = default)
    {
        EnsureStarted();
        var linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, cancellationToken);
        var packetLength = 10;
        var rentedBuffer = new RentedBuffer<byte>(BufferPool, packetLength);
        try
        {
            using (var output = new MemoryStream(rentedBuffer.Buffer, 0, packetLength))
            {
                using var binaryWriter = new BinaryWriter(output);
                binaryWriter.Write((uint)(packetLength - 4));
                binaryWriter.Write(0u);
                binaryWriter.Write((byte)2);
                binaryWriter.Write((byte)opcode);
            }

            try
            {
                await _writeSemaphore.WaitAsync(linkedCts.Token);
                await _writer.WriteAsync(rentedBuffer.Buffer, 0, packetLength, linkedCts.Token);
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }
        finally
        {
            rentedBuffer.Dispose();
        }
    }

    public async Task EchoAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.Echo, data, cancellationToken);
    }

    public async Task PingAsync(CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.Ping, cancellationToken);
    }

    public async Task PrintAsync(string message, CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.Print, Encoding.UTF8.GetBytes(message), cancellationToken);
    }

    private void EnsureStarted()
    {
        if (!_started) throw new Exception("Process should be started.");
    }

    public async Task<DotCefWindow> CreateWindowAsync(string url, int minimumWidth, int minimumHeight,
        int preferredWidth = 0, int preferredHeight = 0, bool fullscreen = false, bool contextMenuEnable = false,
        bool shown = true, bool developerToolsEnabled = false, bool resizable = true, bool frameless = false,
        bool centered = true, bool proxyRequests = false, bool logConsole = false,
        Func<DotCefWindow, IPCRequest, Task<IPCResponse?>>? requestProxy = null, bool modifyRequests = false,
        Func<DotCefWindow, IPCRequest, IPCRequest?>? requestModifier = null, bool modifyRequestBody = false,
        string? title = null, string? iconPath = null, string? appId = null,
        CancellationToken cancellationToken = default)
    {
        EnsureStarted();
        var packetWriter = new PacketWriter();
        packetWriter.Write(resizable);
        packetWriter.Write(frameless);
        packetWriter.Write(fullscreen);
        packetWriter.Write(centered);
        packetWriter.Write(shown);
        packetWriter.Write(contextMenuEnable);
        packetWriter.Write(developerToolsEnabled);
        packetWriter.Write(modifyRequests);
        packetWriter.Write(modifyRequestBody);
        if (proxyRequests && requestProxy == null)
            throw new ArgumentException("When proxyRequests is true, requestProxy must be non null.");
        packetWriter.Write(proxyRequests);
        packetWriter.Write(logConsole);
        packetWriter.Write(minimumWidth);
        packetWriter.Write(minimumHeight);
        packetWriter.Write(preferredWidth);
        packetWriter.Write(preferredHeight);
        packetWriter.WriteSizePrefixedString(url);
        packetWriter.WriteSizePrefixedString(title);
        packetWriter.WriteSizePrefixedString(iconPath);
        packetWriter.WriteSizePrefixedString(appId);
        var dotCefWindow = new DotCefWindow(this,
            (await CallAsync(OpcodeController.WindowCreate, packetWriter.Data, 0, packetWriter.Size, cancellationToken))
            .Read<int>(), requestModifier, requestProxy);
        lock (_windows)
        {
            _windows.Add(dotCefWindow);
            return dotCefWindow;
        }
    }

    public async Task NotifyExitAsync(CancellationToken cancellationToken = default)
    {
        await NotifyAsync(OpcodeControllerNotification.Exit, cancellationToken);
    }

    public async Task StreamOpenAsync(uint identifier, CancellationToken cancellationToken = default)
    {
        var num = 4;
        var packet = BufferPool.Rent(num);
        try
        {
            using (var output = new MemoryStream(packet, 0, num))
            {
                using var binaryWriter = new BinaryWriter(output);
                binaryWriter.Write(identifier);
            }

            await CallAsync(OpcodeController.StreamOpen, packet, 0, num, cancellationToken);
        }
        finally
        {
            BufferPool.Return(packet);
        }
    }

    public async Task<bool> StreamDataAsync(uint identifier, byte[] data, int offset, int size,
        CancellationToken cancellationToken = default)
    {
        var num = size + 4;
        var packet = BufferPool.Rent(num);
        try
        {
            using (var output = new MemoryStream(packet, 0, num))
            {
                using var binaryWriter = new BinaryWriter(output);
                binaryWriter.Write(identifier);
                binaryWriter.Write(data, offset, size);
            }

            return (await CallAsync(OpcodeController.StreamData, packet, 0, num, cancellationToken)).Read<bool>();
        }
        finally
        {
            BufferPool.Return(packet);
        }
    }

    public async Task StreamCloseAsync(uint identifier, CancellationToken cancellationToken = default)
    {
        var num = 4;
        var packet = BufferPool.Rent(num);
        try
        {
            using (var output = new MemoryStream(packet, 0, num))
            {
                using var binaryWriter = new BinaryWriter(output);
                binaryWriter.Write(identifier);
            }

            await CallAsync(OpcodeController.StreamClose, packet, 0, num, cancellationToken);
        }
        finally
        {
            BufferPool.Return(packet);
        }
    }

    public void WaitForExit()
    {
        EnsureStarted();
        _childProcess?.WaitForExit();
    }

    public async Task WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        EnsureStarted();
        if (_childProcess != null)
            try
            {
                await _childProcess.WaitForExitAsync(cancellationToken);
            }
            catch
            {
            }
    }

    public void WaitForReady()
    {
        EnsureStarted();
        _readyTaskCompletionSource.Task.Wait();
    }

    public async Task WaitForReadyAsync(CancellationToken cancellationToken = default)
    {
        EnsureStarted();
        await Task.WhenAny(_readyTaskCompletionSource.Task, Task.Delay(-1, cancellationToken));
    }

    public async Task WindowMaximizeAsync(int identifier, CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.WindowMaximize, new PacketWriter().Write(identifier), cancellationToken);
    }

    public async Task WindowMinimizeAsync(int identifier, CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.WindowMinimize, new PacketWriter().Write(identifier), cancellationToken);
    }

    public async Task WindowRestoreAsync(int identifier, CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.WindowRestore, new PacketWriter().Write(identifier), cancellationToken);
    }

    public async Task WindowShowAsync(int identifier, CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.WindowShow, new PacketWriter().Write(identifier), cancellationToken);
    }

    public async Task WindowHideAsync(int identifier, CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.WindowHide, new PacketWriter().Write(identifier), cancellationToken);
    }

    public async Task WindowActivateAsync(int identifier, CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.WindowActivate, new PacketWriter().Write(identifier), cancellationToken);
    }

    public async Task WindowBringToTopAsync(int identifier, CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.WindowBringToTop, new PacketWriter().Write(identifier), cancellationToken);
    }

    public async Task WindowSetAlwaysOnTopAsync(int identifier, bool alwaysOnTop,
        CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.WindowSetAlwaysOnTop, new PacketWriter().Write(identifier).Write(alwaysOnTop),
            cancellationToken);
    }

    public async Task WindowSetFullscreenAsync(int identifier, bool fullscreen,
        CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.WindowSetFullscreen, new PacketWriter().Write(identifier).Write(fullscreen),
            cancellationToken);
    }

    public async Task WindowCenterSelfAsync(int identifier, CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.WindowCenterSelf, new PacketWriter().Write(identifier), cancellationToken);
    }

    public async Task WindowSetProxyRequestsAsync(int identifier, bool enableProxyRequests,
        CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.WindowSetProxyRequests,
            new PacketWriter().Write(identifier).Write(enableProxyRequests), cancellationToken);
    }

    public async Task WindowSetModifyRequestsAsync(int identifier, bool enableModifyRequests, bool enableModifyBody,
        CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.WindowSetModifyRequests,
            new PacketWriter().Write(identifier)
                .Write((byte)(((enableModifyBody ? 1u : 0u) << 1) | (enableModifyRequests ? 1u : 0u))),
            cancellationToken);
    }

    public async Task RequestFocusAsync(int identifier, CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.WindowRequestFocus, new PacketWriter().Write(identifier), cancellationToken);
    }

    public async Task WindowLoadUrlAsync(int identifier, string url, CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.WindowLoadUrl,
            new PacketWriter().Write(identifier).WriteSizePrefixedString(url), cancellationToken);
    }

    public async Task WindowSetPositionAsync(int identifier, int x, int y,
        CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.WindowSetPosition, new PacketWriter().Write(identifier).Write(x).Write(y),
            cancellationToken);
    }

    public async Task<(int X, int Y)> WindowGetPositionAsync(int identifier,
        CancellationToken cancellationToken = default)
    {
        var obj = await CallAsync(OpcodeController.WindowGetPosition, new PacketWriter().Write(identifier),
            cancellationToken);
        var item = obj.Read<int>();
        var item2 = obj.Read<int>();
        return (X: item, Y: item2);
    }

    public async Task WindowSetSizeAsync(int identifier, int width, int height,
        CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.WindowSetSize, new PacketWriter().Write(identifier).Write(width).Write(height),
            cancellationToken);
    }

    public async Task<(int Width, int Height)> WindowGetSizeAsync(int identifier,
        CancellationToken cancellationToken = default)
    {
        var obj = await CallAsync(OpcodeController.WindowGetSize, new PacketWriter().Write(identifier),
            cancellationToken);
        var item = obj.Read<int>();
        var item2 = obj.Read<int>();
        return (Width: item, Height: item2);
    }

    public async Task WindowSetDevelopmentToolsEnabledAsync(int identifier, bool developmentToolsEnabled,
        CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.WindowSetDevelopmentToolsEnabled,
            new PacketWriter().Write(identifier).Write(developmentToolsEnabled), cancellationToken);
    }

    public async Task WindowSetDevelopmentToolsVisibleAsync(int identifier, bool developmentToolsVisible,
        CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.WindowSetDevelopmentToolsVisible,
            new PacketWriter().Write(identifier).Write(developmentToolsVisible), cancellationToken);
    }

    public async Task WindowCloseAsync(int identifier, bool forceClose = false,
        CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.WindowClose, new PacketWriter().Write(identifier).Write(forceClose),
            cancellationToken);
    }

    public async Task<string[]> PickFileAsync(bool multiple, (string Name, string Pattern)[] filters,
        CancellationToken cancellationToken = default)
    {
        if (OperatingSystem.IsWindows()) return DialogWindows.PickFiles(multiple, filters);
        var packetWriter = new PacketWriter();
        packetWriter.Write(multiple ? (byte)1 : (byte)0);
        packetWriter.Write((uint)filters.Length);
        for (var i = 0; i < filters.Length; i++)
        {
            packetWriter.WriteSizePrefixedString(filters[i].Name);
            packetWriter.WriteSizePrefixedString(filters[i].Pattern);
        }

        var packetReader = await CallAsync(OpcodeController.PickFile, packetWriter, cancellationToken);
        var num = packetReader.Read<uint>();
        var array = new string[num];
        for (var j = 0; j < num; j++) array[j] = packetReader.ReadSizePrefixedString();
        return array;
    }

    public async Task<string> PickDirectoryAsync(CancellationToken cancellationToken = default)
    {
        return (await CallAsync(OpcodeController.PickDirectory, cancellationToken)).ReadSizePrefixedString();
    }

    public async Task<string> SaveFileAsync(string defaultName, (string Name, string Pattern)[] filters,
        CancellationToken cancellationToken = default)
    {
        var packetWriter = new PacketWriter();
        packetWriter.WriteSizePrefixedString(defaultName);
        packetWriter.Write((uint)filters.Length);
        for (var i = 0; i < filters.Length; i++)
        {
            packetWriter.WriteSizePrefixedString(filters[i].Name);
            packetWriter.WriteSizePrefixedString(filters[i].Pattern);
        }

        return (await CallAsync(OpcodeController.SaveFile, packetWriter, cancellationToken)).ReadSizePrefixedString();
    }

    public async Task<(bool Succes, byte[] Data)> WindowExecuteDevToolsMethodAsync(int identifier, string methodName,
        string? json = null, CancellationToken cancellationToken = default)
    {
        var packetWriter = new PacketWriter();
        packetWriter.Write(identifier);
        packetWriter.WriteSizePrefixedString(methodName);
        if (json != null) packetWriter.WriteSizePrefixedString(json);
        var obj = await CallAsync(OpcodeController.WindowExecuteDevToolsMethod, packetWriter, cancellationToken);
        var item = obj.Read<bool>();
        var size = obj.Read<uint>();
        var item2 = obj.ReadBytes((int)size);
        return (Succes: item, Data: item2);
    }

    public async Task WindowSetTitleAsync(int identifier, string title, CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.WindowSetTitle,
            new PacketWriter().Write(identifier).WriteSizePrefixedString(title), cancellationToken);
    }

    public async Task WindowSetIconAsync(int identifier, string iconPath, CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.WindowSetIcon,
            new PacketWriter().Write(identifier).WriteSizePrefixedString(iconPath), cancellationToken);
    }

    public async Task WindowAddUrlToProxyAsync(int identifier, string url,
        CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.WindowAddUrlToProxy,
            new PacketWriter().Write(identifier).WriteSizePrefixedString(url), cancellationToken);
    }

    public async Task WindowRemoveUrlToProxyAsync(int identifier, string url,
        CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.WindowRemoveUrlToProxy,
            new PacketWriter().Write(identifier).WriteSizePrefixedString(url), cancellationToken);
    }

    public async Task WindowAddDomainToProxyAsync(int identifier, string url,
        CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.WindowAddDomainToProxy,
            new PacketWriter().Write(identifier).WriteSizePrefixedString(url), cancellationToken);
    }

    public async Task WindowRemoveDomainToProxyAsync(int identifier, string url,
        CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.WindowRemoveDomainToProxy,
            new PacketWriter().Write(identifier).WriteSizePrefixedString(url), cancellationToken);
    }

    public async Task WindowAddUrlToModifyAsync(int identifier, string url,
        CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.WindowAddUrlToModify,
            new PacketWriter().Write(identifier).WriteSizePrefixedString(url), cancellationToken);
    }

    public async Task WindowRemoveUrlToModifyAsync(int identifier, string url,
        CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.WindowRemoveUrlToModify,
            new PacketWriter().Write(identifier).WriteSizePrefixedString(url), cancellationToken);
    }

    public async Task WindowAddDevToolsEventMethod(int identifier, string method,
        CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.WindowAddDevToolsEventMethod,
            new PacketWriter().Write(identifier).WriteSizePrefixedString(method), cancellationToken);
    }

    public async Task WindowRemoveDevToolsEventMethod(int identifier, string method,
        CancellationToken cancellationToken = default)
    {
        await CallAsync(OpcodeController.WindowRemoveDevToolsEventMethod,
            new PacketWriter().Write(identifier).WriteSizePrefixedString(method), cancellationToken);
    }

    private static string GetNativeFileName()
    {
        if (OperatingSystem.IsWindows()) return "dotcefnative.exe";
        if (OperatingSystem.IsMacOS()) return "dotcefnative";
        if (OperatingSystem.IsLinux()) return "dotcefnative";
        throw new PlatformNotSupportedException("Unsupported platform.");
    }

    private static string? GetDirectory(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        return Path.GetDirectoryName(path);
    }

    private static string[] GenerateSearchPaths()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var nativeFileName = GetNativeFileName();
        var directory = GetDirectory(Assembly.GetEntryAssembly()?.Location);
        var directory2 = GetDirectory(Process.GetCurrentProcess().MainModule?.FileName);
        var currentDirectory = Environment.CurrentDirectory;
        var list = new List<string>();
        if (OperatingSystem.IsMacOS())
        {
            list.Add(Path.Combine(baseDirectory, "dotcefnative.app/Contents/MacOS/" + nativeFileName));
            list.Add(Path.Combine(baseDirectory, "JustCef.app/Contents/MacOS/" + nativeFileName));
        }

        if (OperatingSystem.IsMacOS())
        {
            list.Add(Path.Combine(baseDirectory, "../Frameworks/dotcefnative.app/Contents/MacOS/" + nativeFileName));
            list.Add(Path.Combine(baseDirectory, "../Frameworks/JustCef.app/Contents/MacOS/" + nativeFileName));
        }

        list.Add(Path.Combine(baseDirectory, "cef", nativeFileName));
        list.Add(Path.Combine(baseDirectory, nativeFileName));
        if (directory != null)
        {
            if (OperatingSystem.IsMacOS())
            {
                list.Add(Path.Combine(directory, "dotcefnative.app/Contents/MacOS/" + nativeFileName));
                list.Add(Path.Combine(directory, "JustCef.app/Contents/MacOS/" + nativeFileName));
            }

            if (OperatingSystem.IsMacOS())
            {
                list.Add(Path.Combine(directory, "../Frameworks/dotcefnative.app/Contents/MacOS/" + nativeFileName));
                list.Add(Path.Combine(directory, "../Frameworks/JustCef.app/Contents/MacOS/" + nativeFileName));
            }

            list.Add(Path.Combine(directory, "cef", nativeFileName));
            list.Add(Path.Combine(directory, nativeFileName));
        }

        if (directory2 != null)
        {
            if (OperatingSystem.IsMacOS())
            {
                list.Add(Path.Combine(directory2, "dotcefnative.app/Contents/MacOS/" + nativeFileName));
                list.Add(Path.Combine(directory2, "JustCef.app/Contents/MacOS/" + nativeFileName));
            }

            if (OperatingSystem.IsMacOS())
            {
                list.Add(Path.Combine(directory2, "../Frameworks/dotcefnative.app/Contents/MacOS/" + nativeFileName));
                list.Add(Path.Combine(directory2, "../Frameworks/JustCef.app/Contents/MacOS/" + nativeFileName));
            }

            list.Add(Path.Combine(directory2, "cef", nativeFileName));
            list.Add(Path.Combine(directory2, nativeFileName));
        }

        if (OperatingSystem.IsMacOS())
        {
            list.Add(Path.Combine(currentDirectory, "dotcefnative.app/Contents/MacOS/" + nativeFileName));
            list.Add(Path.Combine(currentDirectory, "JustCef.app/Contents/MacOS/" + nativeFileName));
        }

        if (OperatingSystem.IsMacOS())
        {
            list.Add(Path.Combine(currentDirectory, "../Frameworks/dotcefnative.app/Contents/MacOS/" + nativeFileName));
            list.Add(Path.Combine(currentDirectory, "../Frameworks/JustCef.app/Contents/MacOS/" + nativeFileName));
        }

        list.Add(Path.Combine(currentDirectory, nativeFileName));
        list.Add(Path.Combine(currentDirectory, "cef", nativeFileName));
        return list.Distinct().ToArray();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct IPCPacketHeader
    {
        public uint Size;

        public uint RequestId;

        public PacketType PacketType;

        public byte Opcode;
    }

    private class PendingRequest
    {
        public readonly TaskCompletionSource<byte[]> ResponseBodyTaskCompletionSource = new();
        public OpcodeController Opcode;

        public uint RequestId;

        public PendingRequest(OpcodeController opcode, uint requestId)
        {
            Opcode = opcode;
            RequestId = requestId;
        }
    }
}