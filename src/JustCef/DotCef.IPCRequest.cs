// DotCef, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// DotCef.IPCRequest

namespace DotCef;

public class IPCRequest
{
    public required string Method { get; set; }

    public required string Url { get; set; }

    public required Dictionary<string, List<string>> Headers { get; set; }

    public required List<IPCProxyBodyElement> Elements { get; set; }
}