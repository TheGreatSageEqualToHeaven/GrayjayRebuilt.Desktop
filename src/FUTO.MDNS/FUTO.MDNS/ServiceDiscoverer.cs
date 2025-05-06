namespace FUTO.MDNS;

public class ServiceDiscoverer : IDisposable
{
    private readonly string[] _names;
    private CancellationTokenSource? _cts;
    private MDNSListener? _listener;
    public event Action<List<DnsService>>? OnServicesUpdated;

    public ServiceDiscoverer(params string[] names)
    {
        if (names.Length < 1)
            throw new ArgumentException("At least one name must be specified.");

        _names = names;
    }

    public async Task BroadcastServiceAsync(string deviceName, string serviceName, ushort port, uint ttl = 120, ushort weight = 0, ushort priority = 0, List<string>? texts = null)
    {
        var listener = _listener;
        if (listener == null)
            return;

        await listener.BroadcastServiceAsync(deviceName, serviceName, port, ttl, weight, priority, texts);
    }

    public void Dispose()
    {
        _listener?.Dispose();
        _listener = null;
        _cts?.Cancel();
        _cts = null;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (_cts != null)
            throw new Exception("Already running.");

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var listener = new MDNSListener();
        _listener = listener;
        listener.OnServicesUpdated += (services) => OnServicesUpdated?.Invoke(services);
        _ = listener.RunAsync(cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            await listener.QueryServicesAsync(_names, cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            await listener.QueryAllQuestionsAsync(_names, cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }
}