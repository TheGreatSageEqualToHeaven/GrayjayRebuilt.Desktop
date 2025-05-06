// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// SyncShared.SyncStream

namespace SyncShared;

public class SyncStream : IDisposable
{
    public const int MAXIMUM_SIZE = 10000000;

    private readonly byte[] _buffer;

    private readonly int _expectedSize;

    public readonly ContentEncoding ContentEncoding;

    public readonly Opcode Opcode;

    public readonly byte SubOpcode;

    public SyncStream(int expectedSize, Opcode opcode, byte subOpcode, ContentEncoding contentEncoding)
    {
        if (expectedSize > 10000000) throw new Exception($"{expectedSize} exceeded maximum size {10000000}");
        Opcode = opcode;
        SubOpcode = subOpcode;
        ContentEncoding = contentEncoding;
        _expectedSize = expectedSize;
        _buffer = Utilities.RentBytes(expectedSize);
    }

    public int BytesReceived { get; private set; }

    public bool IsComplete { get; private set; }

    public void Dispose()
    {
        Utilities.ReturnBytes(_buffer);
    }

    public void Add(ReadOnlySpan<byte> data)
    {
        var num = _expectedSize - BytesReceived;
        if (data.Length > num) throw new Exception($"More bytes received {data.Length} than expected remaining {num}");
        data.CopyTo(_buffer.AsSpan().Slice(BytesReceived));
        BytesReceived += data.Length;
        IsComplete = BytesReceived == _expectedSize;
    }

    public ArraySegment<byte> GetBytes()
    {
        if (!IsComplete) throw new Exception("Data is not complete yet");
        return new ArraySegment<byte>(_buffer, 0, _expectedSize);
    }
}