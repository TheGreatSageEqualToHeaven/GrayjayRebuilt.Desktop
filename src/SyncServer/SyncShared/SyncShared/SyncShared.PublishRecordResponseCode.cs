// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// SyncShared.PublishRecordResponseCode

namespace SyncShared;

public enum PublishRecordResponseCode
{
    Success,
    GeneralError,
    InvalidRequest,
    RateLimitExceeded,
    StorageLimitExceeded,
    ConsumerPublicKeyDataLengthMismatch,
    BlobPublicKeyDataLengthMismatch
}