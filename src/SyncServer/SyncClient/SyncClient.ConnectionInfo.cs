// SyncClient, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// SyncClient.ConnectionInfo

using System.Net;

namespace SyncClient;

public record ConnectionInfo(
    ushort Port,
    string Name,
    IPAddress RemoteIp,
    List<IPAddress> Ipv4Addresses,
    List<IPAddress> Ipv6Addresses,
    bool AllowLocalDirect,
    bool AllowRemoteDirect,
    bool AllowRemoteHolePunched,
    bool AllowRemoteRelayed);