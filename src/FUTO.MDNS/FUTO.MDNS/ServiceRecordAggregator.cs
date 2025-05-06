using System.Net;
using System.Text;
using static FUTO.MDNS.DnsReader;

namespace FUTO.MDNS;

public class DnsService
{
    public required string Name;
    public required string Target;
    public ushort Port;
    public readonly List<IPAddress> Addresses = new();
    public readonly List<string> Pointers = new();
    public readonly List<string> Texts = new();
}

public class CachedDnsAddressRecord
{
    public DateTime ExpirationTime { get; init; }
    public required IPAddress Address { get; init; }
}

public class CachedDnsTxtRecord
{
    public DateTime ExpirationTime { get; init; }
    public required List<string> Texts { get; init; }
}

public class CachedDnsPtrRecord
{
    public DateTime ExpirationTime { get; init; }
    public required string Target { get; init; }
}

public class CachedDnsSrvRecord
{
    public DateTime ExpirationTime { get; init; }
    public required SRVRecord Service { get; init; }
}

public class ServiceRecordAggregator
{
    private readonly object _lockObject = new object();
    private readonly Dictionary<string, List<CachedDnsAddressRecord>> _cachedAddressRecords = new();
    private readonly Dictionary<string, CachedDnsTxtRecord> _cachedTxtRecords = new();
    private readonly Dictionary<string, List<CachedDnsPtrRecord>> _cachedPtrRecords = new();
    private readonly Dictionary<string, CachedDnsSrvRecord> _cachedSrvRecords = new();
    private readonly List<DnsService> _currentServices = new();
    private CancellationTokenSource? _cts;

    public event Action<List<DnsService>>? OnServicesUpdated;

    public void Start()
    {
        lock (_lockObject)
        {
            if (_cts != null)
                throw new Exception("Already started.");

            _cts = new CancellationTokenSource();
        }

        _ = Task.Run(async () =>
        {
            try
            {
                List<DnsService> currentServices;
                while (_cts != null && !_cts.IsCancellationRequested)
                {
                    var now = DateTime.Now;

                    lock (_currentServices)
                    {
                        foreach (var pair in _cachedAddressRecords)
                            pair.Value.RemoveAll(v => now > v.ExpirationTime);

                        var expiredTxtRecords = new List<string>();
                        foreach (var pair in _cachedTxtRecords)
                        {
                            if (now > pair.Value.ExpirationTime)
                                expiredTxtRecords.Add(pair.Key);
                        }

                        foreach (var expiredRecord in expiredTxtRecords)
                            _cachedTxtRecords.Remove(expiredRecord);

                        var expiredSrvRecords = new List<string>();
                        foreach (var pair in _cachedSrvRecords)
                        {
                            if (now > pair.Value.ExpirationTime)
                                expiredSrvRecords.Add(pair.Key);
                        }

                        foreach (var expiredRecord in expiredSrvRecords)
                            _cachedSrvRecords.Remove(expiredRecord);

                        foreach (var pair in _cachedPtrRecords)
                            pair.Value.RemoveAll(v => now > v.ExpirationTime);

                        currentServices = GetCurrentServices();
                        _currentServices.Clear();
                        _currentServices.AddRange(currentServices);
                    }

                    OnServicesUpdated?.Invoke(currentServices);
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Service record aggregator closed abruptly: " + e.ToString());
            }
        });
    }

    public void Stop()
    {
        lock (_lockObject)
        {
            _cts?.Cancel();
            _cts = null;
        }
    }

    public void Add(DnsPacket packet)
    {
        var dnsResourceRecords = packet.Answers.Concat(packet.Additionals).Concat(packet.Authorities).ToList();
        var txtRecords = dnsResourceRecords.Where(r => r.Type == ResourceRecordType.TXT).Select(r => new { Record = r, Content = r.GetDataReader().ReadTXTRecord() }).ToList();
        var aRecords = dnsResourceRecords.Where(r => r.Type == ResourceRecordType.A).Select(r => new { Record = r, Content = r.GetDataReader().ReadARecord() }).ToList();
        var aaaaRecords = dnsResourceRecords.Where(r => r.Type == ResourceRecordType.AAAA).Select(r => new { Record = r, Content = r.GetDataReader().ReadAAAARecord() }).ToList();
        var srvRecords = dnsResourceRecords.Where(r => r.Type == ResourceRecordType.SRV).Select(r => new { Record = r, Content = r.GetDataReader().ReadSRVRecord() }).ToList();
        var ptrRecords = dnsResourceRecords.Where(r => r.Type == ResourceRecordType.PTR).Select(r => new { Record = r, Content = r.GetDataReader().ReadPTRRecord() }).ToList();

        /*var builder = new StringBuilder();
        builder.AppendLine("Received records:");
        foreach (var srvRecord in srvRecords)
            builder.AppendLine($" {srvRecord.Record.Name} {srvRecord.Record.Type} {srvRecord.Record.Class} TTL {srvRecord.Record.TimeToLive}: (Port: {srvRecord.Content.Port}, Target: {srvRecord.Content.Target}, Priority: {srvRecord.Content.Priority}, Weight: {srvRecord.Content.Weight})");
        foreach (var ptrRecord in ptrRecords)
            builder.AppendLine($" {ptrRecord.Record.Name} {ptrRecord.Record.Type} {ptrRecord.Record.Class} TTL {ptrRecord.Record.TimeToLive}: {ptrRecord.Content.DomainName}");
        foreach (var txtRecord in txtRecords)
            builder.AppendLine($" {txtRecord.Record.Name} {txtRecord.Record.Type} {txtRecord.Record.Class} TTL {txtRecord.Record.TimeToLive}: {string.Join(", ", txtRecord.Content.Texts)}");
        foreach (var aRecord in aRecords)
            builder.AppendLine($" {aRecord.Record.Name} {aRecord.Record.Type} {aRecord.Record.Class} TTL {aRecord.Record.TimeToLive}: {aRecord.Content.Address}");
        foreach (var aaaaRecord in aaaaRecords)
            builder.AppendLine($" {aaaaRecord.Record.Name} {aaaaRecord.Record.Type} {aaaaRecord.Record.Class} TTL {aaaaRecord.Record.TimeToLive}: {aaaaRecord.Content.Address}");
        lock (_lockObject)
        {
            //File.AppendAllText("records.txt", builder.ToString());
        }*/

        List<DnsService> currentServices;
        lock (_currentServices)
        {
            foreach (var record in ptrRecords)
            {
                List<CachedDnsPtrRecord>? cachedPtrRecord;
                if (!_cachedPtrRecords.TryGetValue(record.Record.Name, out cachedPtrRecord) || cachedPtrRecord == null)
                {
                    cachedPtrRecord = new List<CachedDnsPtrRecord>();
                    _cachedPtrRecords[record.Record.Name] = cachedPtrRecord;
                }

                bool foundPtrRecord = false;
                for (var i = 0; i < cachedPtrRecord.Count; i++)
                {
                    if (cachedPtrRecord[i].Target == record.Content.DomainName)
                    {
                        foundPtrRecord = true;
                        cachedPtrRecord[i] = new CachedDnsPtrRecord()
                        {
                            Target = record.Content.DomainName,
                            ExpirationTime = DateTime.Now.AddSeconds(record.Record.TimeToLive)
                        };
                    }
                }

                if (!foundPtrRecord)
                {
                    cachedPtrRecord.Add(new CachedDnsPtrRecord()
                    {
                        Target = record.Content.DomainName,
                        ExpirationTime = DateTime.Now.AddSeconds(record.Record.TimeToLive)
                    });
                }

                foreach (var aRecord in aRecords)
                {
                    List<CachedDnsAddressRecord>? cachedARecord;
                    if (!_cachedAddressRecords.TryGetValue(aRecord.Record.Name, out cachedARecord) || cachedARecord == null)
                    {
                        cachedARecord = new List<CachedDnsAddressRecord>();
                        _cachedAddressRecords[aRecord.Record.Name] = cachedARecord;
                    }

                    var newARecord = new CachedDnsAddressRecord()
                    {
                        Address = aRecord.Content.Address,
                        ExpirationTime = DateTime.Now.AddSeconds(aRecord.Record.TimeToLive)
                    };

                    bool foundARecord = false;
                    for (var i = 0; i < cachedARecord.Count; i++)
                    {
                        if (cachedARecord[i].Address.Equals(newARecord.Address))
                        {
                            foundARecord = true;
                            cachedARecord[i] = newARecord;
                        }
                    }

                    if (!foundARecord)
                        cachedARecord.Add(newARecord);
                }

                foreach (var aaaaRecord in aaaaRecords)
                {
                    List<CachedDnsAddressRecord>? cachedAaaaRecord;
                    if (!_cachedAddressRecords.TryGetValue(aaaaRecord.Record.Name, out cachedAaaaRecord) || cachedAaaaRecord == null)
                    {
                        cachedAaaaRecord = new List<CachedDnsAddressRecord>();
                        _cachedAddressRecords[aaaaRecord.Record.Name] = cachedAaaaRecord;
                    }

                    var newAaaaRecord = new CachedDnsAddressRecord()
                    {
                        Address = aaaaRecord.Content.Address,
                        ExpirationTime = DateTime.Now.AddSeconds(aaaaRecord.Record.TimeToLive)
                    };

                    bool foundAaaaRecord = false;
                    for (var i = 0; i < cachedAaaaRecord.Count; i++)
                    {
                        if (cachedAaaaRecord[i].Address.Equals(newAaaaRecord.Address))
                        {
                            foundAaaaRecord = true;
                            cachedAaaaRecord[i] = newAaaaRecord;
                        }
                    }

                    if (!foundAaaaRecord)
                        cachedAaaaRecord.Add(newAaaaRecord);
                }
            }

            foreach (var txtRecord in txtRecords)
            {
                _cachedTxtRecords[txtRecord.Record.Name] = new CachedDnsTxtRecord()
                {
                    Texts = txtRecord.Content.Texts,
                    ExpirationTime = DateTime.Now.AddSeconds(txtRecord.Record.TimeToLive)
                };
            }

            foreach (var srvRecord in srvRecords)
            {
                _cachedSrvRecords[srvRecord.Record.Name] = new CachedDnsSrvRecord()
                {
                    Service = srvRecord.Content,
                    ExpirationTime = DateTime.Now.AddSeconds(srvRecord.Record.TimeToLive)
                };
            }

            currentServices = GetCurrentServices();
            _currentServices.Clear();
            _currentServices.AddRange(currentServices);
        }

        OnServicesUpdated?.Invoke(currentServices);
    }

    public List<DnsQuestion> GetAllQuestions(string serviceName)
    {
        List<DnsQuestion> questions = new List<DnsQuestion>();
        lock (_currentServices)
        {
            if (!_cachedPtrRecords.TryGetValue(serviceName, out var servicePtrRecords))
                return new List<DnsQuestion>();

            var ptrWithoutSrvRecord = _cachedPtrRecords[serviceName]?.Where(v => !_cachedSrvRecords.ContainsKey(v.Target))?.Select(v => v.Target).ToList() ?? [];
            questions.AddRange(ptrWithoutSrvRecord.SelectMany(s =>
            {
                return new DnsQuestion[]
                {
                    new DnsQuestion()
                    {
                        Name = s,
                        Type = QuestionType.SRV,
                        Class = QuestionClass.IN,
                        QueryUnicast = false
                    }
                };
            }));

            var incompleteCurrentServices = _currentServices.Where(s => s.Addresses.Count == 0 && s.Name.EndsWith(serviceName)).ToList();
            questions.AddRange(incompleteCurrentServices.SelectMany(s =>
            {
                var srvRecord = _cachedSrvRecords[s.Name];
                return new DnsQuestion[]
                {
                    new DnsQuestion()
                    {
                        Name = s.Name,
                        Type = QuestionType.TXT,
                        Class = QuestionClass.IN,
                        QueryUnicast = false
                    },
                    new DnsQuestion()
                    {
                        Name = s.Target,
                        Type = QuestionType.A,
                        Class = QuestionClass.IN,
                        QueryUnicast = false
                    },
                    new DnsQuestion()
                    {
                        Name = s.Target,
                        Type = QuestionType.AAAA,
                        Class = QuestionClass.IN,
                        QueryUnicast = false
                    }
                };
            }).ToList());
        }

        return questions;
    }

    private List<DnsService> GetCurrentServices()
    {
        List<DnsService> currentServices = _cachedSrvRecords.Select(v => new DnsService
        {
            Name = v.Key,
            Target = v.Value.Service.Target,
            Port = v.Value.Service.Port
        }).ToList();

        foreach (var service in currentServices)
        {
            //TODO: Recursively resolve PTRs?
            if (_cachedAddressRecords.TryGetValue(service.Target, out var cachedRecords))
                service.Addresses.AddRange(cachedRecords.Select(v => v.Address).ToList());
        }

        foreach (var service in currentServices)
        {
            //TODO: Recursively resolve PTRs?
            service.Pointers.AddRange(_cachedPtrRecords.Where(w => w.Value.Any(x => x.Target == service.Name)).Select(w => w.Key).ToList());
        }

        foreach (var service in currentServices)
        {
            if (_cachedTxtRecords.TryGetValue(service.Name, out var cachedRecords))
                service.Texts.AddRange(cachedRecords.Texts);
        }

        return currentServices;
    }
}