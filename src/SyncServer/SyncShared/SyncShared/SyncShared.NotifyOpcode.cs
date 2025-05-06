// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// SyncShared.NotifyOpcode

namespace SyncShared;

public enum NotifyOpcode : byte
{
    AUTHORIZED,
    UNAUTHORIZED,
    CONNECTION_INFO
}