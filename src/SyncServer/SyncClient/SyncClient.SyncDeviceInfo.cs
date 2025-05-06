// SyncClient, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// SyncClient.SyncDeviceInfo

using System.Text.Json.Serialization;

namespace SyncClient;

public class SyncDeviceInfo
{
    public SyncDeviceInfo(string publicKey, string[] addresses, int port, string? pairingCode)
    {
        PublicKey = publicKey;
        Addresses = addresses;
        Port = port;
        PairingCode = pairingCode;
    }

    [JsonPropertyName("publicKey")] public string PublicKey { get; set; }

    [JsonPropertyName("addresses")] public string[] Addresses { get; set; }

    [JsonPropertyName("port")] public int Port { get; set; }

    [JsonPropertyName("pairingCode")] public string? PairingCode { get; set; }
}