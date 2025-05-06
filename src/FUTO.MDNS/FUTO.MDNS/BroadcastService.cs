namespace FUTO.MDNS;

public class BroadcastService
{
    public required string DeviceName { get; init; }
    public required string ServiceName { get; init; }
    public required ushort Port { get; init; }
    public required uint TTL { get; init; }
    public required ushort Weight { get; init; }
    public required ushort Priority { get; init; }
    public required List<string>? Texts { get; init; }
}