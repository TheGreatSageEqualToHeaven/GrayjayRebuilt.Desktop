// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// SyncShared.Utilities

using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SyncShared;

public static class Utilities
{
    public static long TotalRented;

    public static long TotalReturned;

    public static string HexDump(this ReadOnlySpan<byte> data)
    {
        var num = (int)Math.Ceiling(data.Length / 16.0);
        var stringBuilder = new StringBuilder(num * 68);
        for (var i = 0; i < num; i++)
        {
            var num2 = i * 16;
            var num3 = Math.Min(data.Length, (i + 1) * 16);
            for (var j = num2; j < num3; j++) stringBuilder.AppendFormat("{0:X2} ", data[j]);
            var num4 = 16 - (num3 - num2);
            for (var k = 0; k < num4; k++) stringBuilder.Append("   ");
            stringBuilder.AppendFormat("; ", default(ReadOnlySpan<object>));
            for (var l = num2; l < num3; l++)
            {
                var b = data[l];
                if (b >= 32 && b <= 126)
                {
                    var aSCII = Encoding.ASCII;
                    var reference = b;
                    stringBuilder.Append(aSCII.GetString(new ReadOnlySpan<byte>(in reference)));
                }
                else
                {
                    stringBuilder.Append(".");
                }
            }

            if (i < num - 1) stringBuilder.AppendLine();
        }

        return stringBuilder.ToString();
    }

    public static byte[] GetLimitedUtf8Bytes(string? str, int maxByteLength)
    {
        if (str == null) return Array.Empty<byte>();
        if (str == null) throw new ArgumentNullException("str");
        if (maxByteLength < 0) throw new ArgumentOutOfRangeException("maxByteLength");
        var bytes = Encoding.UTF8.GetBytes(str);
        if (bytes.Length <= maxByteLength) return bytes;
        var num = maxByteLength;
        while (num > 0 && (bytes[num] & 0xC0) == 128) num--;
        var array = new byte[num];
        Array.Copy(bytes, array, num);
        return array;
    }

    public static byte[] RentBytes(int minimumSize)
    {
        var array = ArrayPool<byte>.Shared.Rent(minimumSize);
        Interlocked.Add(ref TotalRented, array.Length);
        if (Logger.WillLog(LogLevel.Debug))
            Logger.Debug("Utilities",
                $"Rented {array.Length} bytes (requested: {minimumSize}, total rented: {TotalRented}, total returned: {TotalReturned}, delta: {TotalRented - TotalReturned})");
        return array;
    }

    public static void ReturnBytes(byte[] rentedBytes, bool clearArray = false)
    {
        Interlocked.Add(ref TotalReturned, rentedBytes.Length);
        ArrayPool<byte>.Shared.Return(rentedBytes, clearArray);
        if (Logger.WillLog(LogLevel.Debug))
            Logger.Debug("Utilities",
                $"Returned {rentedBytes.Length} bytes (total rented: {TotalRented}, total returned: {TotalReturned}, delta: {TotalRented - TotalReturned})");
    }

    public static Socket OpenTcpSocket(string host, int port)
    {
        var array = Dns.GetHostEntry(host).AddressList
            .OrderBy(a => a.AddressFamily != AddressFamily.InterNetwork ? 1 : 0).ToArray();
        foreach (var iPAddress in array)
            try
            {
                var socket = new Socket(iPAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(new IPEndPoint(iPAddress, port));
                Console.WriteLine($"Connected to {host}:{port} using {iPAddress.AddressFamily}");
                return socket;
            }
            catch
            {
            }

        throw new Exception($"Could not connect to {host}:{port}");
    }
}