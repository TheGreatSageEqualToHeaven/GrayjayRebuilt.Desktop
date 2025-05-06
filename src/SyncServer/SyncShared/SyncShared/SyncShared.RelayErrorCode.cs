// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// SyncShared.RelayErrorCode

namespace SyncShared;

public enum RelayErrorCode
{
    Success,
    GeneralError,
    NotFound,
    Unauthorized,
    RateLimitExceeded,
    StorageLimitExceeded,
    InvalidData,
    ConnectionClosed
}