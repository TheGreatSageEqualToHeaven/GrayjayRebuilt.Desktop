// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// SyncShared.RelayOpcode

namespace SyncShared;

public enum RelayOpcode : byte
{
    DATA,
    RELAYED_DATA,
    ERROR,
    RELAYED_ERROR,
    RELAY_ERROR
}