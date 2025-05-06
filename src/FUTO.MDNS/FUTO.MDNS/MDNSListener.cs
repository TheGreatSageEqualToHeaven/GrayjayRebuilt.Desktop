namespace FUTO.MDNS;

using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using static FUTO.MDNS.DnsReader;

//TODO: Implement support for unicast queryies
public class MDNSListener : IDisposable
{
    public static readonly int MulticastPort = 5353;
    private static readonly IPAddress MulticastAddressIPv4 = IPAddress.Parse("224.0.0.251");
    private static readonly IPAddress MulticastAddressIPv6 = IPAddress.Parse("FF02::FB");
    private static readonly IPEndPoint MdnsEndpointIPv6 = new IPEndPoint(MulticastAddressIPv6, MulticastPort);
    private static readonly IPEndPoint MdnsEndpointIPv4 = new IPEndPoint(MulticastAddressIPv4, MulticastPort);

    private readonly object _lockObject = new object();
    private UdpClient? _receiver4;
    private UdpClient? _receiver6;
    private readonly List<UdpClient> _senders = new List<UdpClient>();
    private readonly NICMonitor _nicMonitor = new NICMonitor();
    private CancellationTokenSource? _cts;
    private readonly ServiceRecordAggregator _serviceRecordAggregator = new ServiceRecordAggregator();

    public event Action<DnsPacket>? OnPacket;
    public event Action<List<DnsService>>? OnServicesUpdated;

    private readonly object _recordLockObject = new object();
    private readonly List<(DnsResourceRecord Record, ARecord Content)> _recordsA = new();
    private readonly List<(DnsResourceRecord Record, AAAARecord Content)> _recordsAAAA = new();
    private readonly List<(DnsResourceRecord Record, PTRRecord Content)> _recordsPTR = new();
    private readonly List<(DnsResourceRecord Record, TXTRecord Content)> _recordsTXT = new();
    private readonly List<(DnsResourceRecord Record, SRVRecord Content)> _recordsSRV = new();
    private readonly List<BroadcastService> _services = new();

    

    public MDNSListener()
    {
        _nicMonitor.Added += OnNicsAdded;
        _nicMonitor.Removed += OnNicsRemoved;
        _serviceRecordAggregator.OnServicesUpdated += (services) => OnServicesUpdated?.Invoke(services);
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        Logger.Info<MDNSListener>("Starting");

        lock (_lockObject)
        {
            if (_cts != null)
                throw new Exception("Already started.");

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var receiver4 = new UdpClient(AddressFamily.InterNetwork);
            receiver4.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            receiver4.Client.Bind(new IPEndPoint(IPAddress.Any, MulticastPort));
            _receiver4 = receiver4;

            var receiver6 = new UdpClient(AddressFamily.InterNetworkV6);
            receiver6.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            receiver6.Client.Bind(new IPEndPoint(IPAddress.IPv6Any, MulticastPort));
            _receiver6 = receiver6;

            var ct = _cts.Token;
            _nicMonitor.Start();
            _serviceRecordAggregator.Start();
            OnNicsAdded(_nicMonitor.Current);
        }

        await Task.WhenAll(
            ReceiveLoopAsync(_receiver4, _cts.Token),
            ReceiveLoopAsync(_receiver6, _cts.Token)
        );
    }

    public async Task QueryServicesAsync(string[] names, CancellationToken cancellationToken = default)
    {
        if (names.Length < 1)
            throw new ArgumentException("At least one name must be specified.");

        var writer = new DnsWriter();
        writer.WritePacket
        (
            header: new DnsPacketHeader()
            {
                Identifier = 0,
                QueryResponse = QueryResponse.Query,
                Opcode = DnsOpcode.StandardQuery,
                Truncated = false,
                NonAuthenticatedData = false,
                RecursionDesired = false,
                AnswerAuthenticated = false,
                AuthorativeAnswer = false,
                RecursionAvailable = false,
                ResponseCode = DnsResponseCode.NoError
            },
            questionCount: names.Length,
            questionWriter: (w, i) =>
            {
                w.Write(new DnsQuestion()
                {
                    Name = names[i],
                    Type = QuestionType.PTR,
                    Class = QuestionClass.IN,
                    QueryUnicast = false
                });
            }
        );

        await SendAsync(writer.ToArray(), cancellationToken);
    }

    private async Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        Task[] tasks;
        lock (_lockObject)
        {
            tasks = _senders.Select(async s =>
            {
                try
                {
                    var endPoint = s.Client.AddressFamily == AddressFamily.InterNetwork ? MdnsEndpointIPv4 : MdnsEndpointIPv6;
                    await s.SendAsync(new ReadOnlyMemory<byte>(data, 0, data.Length), endPoint, cancellationToken);
                }
                catch (Exception e)
                {
                    Logger.Error<MDNSListener>($"Failed to send on {s.Client.LocalEndPoint}: {e.Message}.");
                }
            }).ToArray();
        }

        await Task.WhenAll(tasks);
    }

    public async Task QueryAllQuestionsAsync(string[] names, CancellationToken cancellationToken = default)
    {
        if (names.Length < 1)
            throw new ArgumentException("At least one name must be specified.");

        var questions = names.SelectMany(n => _serviceRecordAggregator.GetAllQuestions(n)).ToList();
        foreach (var questionsPerHost in questions.GroupBy(q => q.Name))
        {
            var questionsForHost = questionsPerHost.ToList();

            var writer = new DnsWriter();
            writer.WritePacket
            (
                header: new DnsPacketHeader()
                {
                    Identifier = 0,
                    QueryResponse = QueryResponse.Query,
                    Opcode = DnsOpcode.StandardQuery,
                    Truncated = false,
                    NonAuthenticatedData = false,
                    RecursionDesired = false,
                    AnswerAuthenticated = false,
                    AuthorativeAnswer = false,
                    RecursionAvailable = false,
                    ResponseCode = DnsResponseCode.NoError
                },
                questionCount: questionsForHost.Count,
                questionWriter: (w, i) => w.Write(questionsForHost[i])
            );

            await SendAsync(writer.ToArray(), cancellationToken);
        }
    }

    private void OnNicsAdded(List<NetworkInterface> nics)
    {
        lock (_lockObject)
        {
            if (_cts?.IsCancellationRequested == true)
                return;

            var addresses = Utilities.GetIPs(nics);
            foreach (var address in addresses)
            {
                Logger.Info<MDNSListener>($"New address discovered {address}");

                var localEndpoint = new IPEndPoint(address, MulticastPort);
                var sender = new UdpClient(address.AddressFamily);

                try
                {
                    switch (address.AddressFamily)
                    {
                        case AddressFamily.InterNetwork:
                            _receiver4?.Client?.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(MulticastAddressIPv4, address));
                            sender.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                            sender.Client.Bind(localEndpoint);
                            sender.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(MulticastAddressIPv4));
                            sender.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);
                            break;
                        case AddressFamily.InterNetworkV6:
                            _receiver6?.Client?.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership, new IPv6MulticastOption(MulticastAddressIPv6, address.ScopeId));
                            sender.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                            sender.Client.Bind(localEndpoint);
                            sender.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership, new IPv6MulticastOption(MulticastAddressIPv6));
                            sender.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.MulticastLoopback, true);
                            break;
                        default:
                            throw new NotSupportedException($"Address family {address.AddressFamily}.");
                    }

                    _senders.Add(sender);
                }
                catch (Exception e)
                {
                    Logger.Error<MDNSListener>($"Exception occurred when processing added address {address}: {e.Message}, {e.StackTrace}");
                    sender.Dispose();
                }
            }
        }

        if (nics.Count > 0)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    UpdateBroadcastRecords();
                    await BroadcastRecordsAsync();
                }
                catch (Exception e)
                {
                    Logger.Error<MDNSListener>($"Exception occurred when broadcasting records: {e.Message}, {e.StackTrace}");
                }
            });
        }
    }

    private void OnNicsRemoved(List<NetworkInterface> nics)
    {
        lock (_lockObject)
        {
            if (_cts?.IsCancellationRequested == true)
                return;

            //TODO: Cleanup?
        }

        if (nics.Count > 0)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    UpdateBroadcastRecords();
                    await BroadcastRecordsAsync();
                }
                catch (Exception e)
                {
                    Logger.Error<MDNSListener>($"Exception occurred when broadcasting records: {e.Message}, {e.StackTrace}");
                }
            });
        }
    }

    private async Task ReceiveLoopAsync(UdpClient client, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await client.ReceiveAsync(cancellationToken);
                HandleResult(result);
            }
            catch (Exception e)
            {
                Logger.Error<MDNSListener>($"An exception occurred while handling UDP result: '{e.Message}': {e.StackTrace}");
            }
        }
    }

    public async Task BroadcastServiceAsync(string deviceName, string serviceName, ushort port, uint ttl = 120, ushort weight = 0, ushort priority = 0, List<string>? texts = null)
    {
        lock (_recordLockObject)
        {
            _services.Add(new BroadcastService()
            {
                DeviceName = deviceName,
                Port = port,
                Priority = priority,
                ServiceName = serviceName,
                Texts = texts,
                TTL = ttl,
                Weight = weight
            });
        }

        UpdateBroadcastRecords();
        await BroadcastRecordsAsync();
    }

    private void UpdateBroadcastRecords()
    {
        lock (_recordLockObject)
        {
            _recordsSRV.Clear();
            _recordsPTR.Clear();
            _recordsA.Clear();
            _recordsAAAA.Clear();
            _recordsTXT.Clear();

            foreach (var service in _services)
            {
                var id = Guid.NewGuid().ToString();
                var deviceDomainName = $"{service.DeviceName}.{service.ServiceName}";
                var addressName = $"{id}.local";

                _recordsSRV.Add((new DnsResourceRecord()
                {
                    Class = ResourceRecordClass.IN,
                    Type = ResourceRecordType.SRV,
                    TimeToLive = service.TTL,
                    Name = deviceDomainName,
                    CacheFlush = false
                }, new SRVRecord()
                {
                    Target = addressName,
                    Port = service.Port,
                    Priority = service.Priority,
                    Weight = service.Weight
                }));

                _recordsPTR.Add((new DnsResourceRecord()
                {
                    Class = ResourceRecordClass.IN,
                    Type = ResourceRecordType.PTR,
                    TimeToLive = service.TTL,
                    Name = service.ServiceName,
                    CacheFlush = false
                }, new PTRRecord()
                {
                    DomainName = deviceDomainName
                }));

                var addresses = Utilities.GetIPs(_nicMonitor.Current);
                foreach (var address in addresses)
                {
                    if (address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        _recordsA.Add((new DnsResourceRecord()
                        {
                            Class = ResourceRecordClass.IN,
                            Type = ResourceRecordType.A,
                            TimeToLive = service.TTL,
                            Name = addressName,
                            CacheFlush = false
                        }, new ARecord()
                        {
                            Address = address
                        }));
                    }
                    else if (address.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        _recordsAAAA.Add((new DnsResourceRecord()
                        {
                            Class = ResourceRecordClass.IN,
                            Type = ResourceRecordType.AAAA,
                            TimeToLive = service.TTL,
                            Name = addressName,
                            CacheFlush = false
                        }, new AAAARecord()
                        {
                            Address = address
                        }));
                    }
                    else
                        Logger.Warning<MDNSListener>($"Invalid address type: {address}.");
                }

                if (service.Texts != null)
                {
                    _recordsTXT.Add((new DnsResourceRecord()
                    {
                        Class = ResourceRecordClass.IN,
                        Type = ResourceRecordType.TXT,
                        TimeToLive = service.TTL,
                        Name = deviceDomainName,
                        CacheFlush = false
                    }, new TXTRecord()
                    {
                        Texts = service.Texts
                    }));
                }
            }
        }
    }

    private async Task BroadcastRecordsAsync(List<DnsQuestion>? questions = null)
    {
        var writer = new DnsWriter();
        lock (_recordLockObject)
        {
            List<(DnsResourceRecord Record, ARecord Content)> recordsA;
            List<(DnsResourceRecord Record, AAAARecord Content)> recordsAAAA;
            List<(DnsResourceRecord Record, PTRRecord Content)> recordsPTR;
            List<(DnsResourceRecord Record, TXTRecord Content)> recordsTXT;
            List<(DnsResourceRecord Record, SRVRecord Content)> recordsSRV;

            if (questions != null)
            {
                recordsA = _recordsA.Where(r => questions.Any(q => q.Name == r.Record.Name && (int)q.Class == (int)r.Record.Class && (int)q.Type == (int)r.Record.Type)).ToList();
                recordsAAAA = _recordsAAAA.Where(r => questions.Any(q => q.Name == r.Record.Name && (int)q.Class == (int)r.Record.Class && (int)q.Type == (int)r.Record.Type)).ToList();
                recordsPTR = _recordsPTR.Where(r => questions.Any(q => q.Name == r.Record.Name && (int)q.Class == (int)r.Record.Class && (int)q.Type == (int)r.Record.Type)).ToList();
                recordsSRV = _recordsSRV.Where(r => questions.Any(q => q.Name == r.Record.Name && (int)q.Class == (int)r.Record.Class && (int)q.Type == (int)r.Record.Type)).ToList();
                recordsTXT = _recordsTXT.Where(r => questions.Any(q => q.Name == r.Record.Name && (int)q.Class == (int)r.Record.Class && (int)q.Type == (int)r.Record.Type)).ToList();
            }
            else
            {
                recordsA = _recordsA;
                recordsAAAA = _recordsAAAA;
                recordsPTR = _recordsPTR;
                recordsSRV = _recordsSRV;
                recordsTXT = _recordsTXT;
            }

            var answerCount = recordsA.Count + recordsAAAA.Count + recordsPTR.Count + recordsSRV.Count + recordsTXT.Count;
            if (answerCount < 1)
                return;

            var txtOffset = recordsA.Count + recordsAAAA.Count + recordsPTR.Count + recordsSRV.Count;
            var srvOffset = recordsA.Count + recordsAAAA.Count + recordsPTR.Count;
            var ptrOffset = recordsA.Count + recordsAAAA.Count;
            var aaaaOffset = recordsA.Count;

            writer.WritePacket(
                header: new DnsPacketHeader()
                {
                    Identifier = 0,
                    QueryResponse = QueryResponse.Response,
                    Opcode = DnsOpcode.StandardQuery,
                    Truncated = false,
                    NonAuthenticatedData = false,
                    RecursionDesired = false,
                    AnswerAuthenticated = false,
                    AuthorativeAnswer = true,
                    RecursionAvailable = false,
                    ResponseCode = DnsResponseCode.NoError
                },
                answerCount: answerCount,
                answerWriter: (w, i) =>
                {
                    if (i >= txtOffset)
                    {
                        var record = recordsTXT[i - txtOffset];
                        w.Write(record.Record, (v) => v.Write(record.Content));
                    }
                    else if (i >= srvOffset)
                    {
                        var record = recordsSRV[i - srvOffset];
                        w.Write(record.Record, (v) => v.Write(record.Content));
                    }
                    else if (i >= ptrOffset)
                    {
                        var record = recordsPTR[i - ptrOffset];
                        w.Write(record.Record, (v) => v.Write(record.Content));
                    }
                    else if (i >= aaaaOffset)
                    {
                        var record = recordsAAAA[i - aaaaOffset];
                        w.Write(record.Record, (v) => v.Write(record.Content));
                    }
                    else
                    {
                        var record = recordsA[i];
                        w.Write(record.Record, (v) => v.Write(record.Content));
                    }
                }
            );
        }

        await SendAsync(writer.ToArray());
    }

    private async void HandleResult(UdpReceiveResult result)
    {
        //Console.WriteLine($"Received packet ({result.Buffer.Length} bytes) from {result.RemoteEndPoint}:\n{result.Buffer.ToByteDump()}");
        //File.AppendAllLines("log.txt", [$"Received packet ({result.Buffer.Length} bytes) from {result.RemoteEndPoint}:\n{result.Buffer.ToByteDump()}"]);

        try
        {
            var packet = DnsPacket.Parse(result.Buffer);
            if (packet.Questions.Count > 0)
                await BroadcastRecordsAsync(packet.Questions);
            _serviceRecordAggregator.Add(packet);
            OnPacket?.Invoke(packet);
        }
        catch (Exception e)
        {
            Logger.Info<MDNSListener>("Failed to handle packet: " + e.Message + "\n" + e.StackTrace);
        }
    }

    public void Stop()
    {
        lock (_lockObject)
        {
            _cts?.Cancel();
            _cts = null;

            _nicMonitor.Stop();
            _serviceRecordAggregator.Stop();

            _receiver4?.Dispose();
            _receiver4 = null;

            _receiver6?.Dispose();
            _receiver6 = null;

            foreach (var sender in _senders)
                sender.Dispose();

            _senders.Clear();
        }
    }

    public void Dispose()
    {
        Stop();
    }
}