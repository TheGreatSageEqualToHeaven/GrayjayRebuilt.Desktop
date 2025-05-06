// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// SyncShared.ResponseOpcode

namespace SyncShared;

public enum ResponseOpcode : byte
{
    CONNECTION_INFO,
    TRANSPORT,
    TRANSPORT_RELAYED,
    PUBLISH_RECORD,
    DELETE_RECORD,
    LIST_RECORD_KEYS,
    GET_RECORD,
    BULK_PUBLISH_RECORD,
    BULK_GET_RECORD,
    BULK_CONNECTION_INFO,
    BULK_DELETE_RECORD
}