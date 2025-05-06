// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// SyncShared.Opcode

namespace SyncShared;

public enum Opcode : byte
{
    PING,
    PONG,
    NOTIFY,
    STREAM,
    DATA,
    REQUEST,
    RESPONSE,
    RELAY
}