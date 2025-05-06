// SyncClient, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// SyncClient.AlwaysAuthorized

namespace SyncClient;

public class AlwaysAuthorized : IAuthorizable
{
    public static readonly AlwaysAuthorized Instance = new();

    public bool IsAuthorized => true;
}