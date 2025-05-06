// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// SyncShared.TransportResponseCode

namespace SyncShared;

public enum TransportResponseCode
{
    Success,
    GeneralError,
    RateLimitExceeded,
    PairingCodeDataMismatch,
    ChannelMessageDataLengthMismatch,
    Blacklisted,
    DuplicateConnection
}