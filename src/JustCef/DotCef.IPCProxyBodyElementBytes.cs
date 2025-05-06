// DotCef, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// DotCef.IPCProxyBodyElementBytes

namespace DotCef;

public class IPCProxyBodyElementBytes : IPCProxyBodyElement
{
    public required byte[] Data { get; init; }
}