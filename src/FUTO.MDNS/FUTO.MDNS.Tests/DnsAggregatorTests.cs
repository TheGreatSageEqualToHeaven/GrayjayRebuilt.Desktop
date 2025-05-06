using System.Net;
using System.Text.Json;

namespace FUTO.MDNS.Tests;

[TestClass]
public class DnsAggregatorTests
{
    [TestMethod]
    public void Test()
    {
        if (File.Exists("test.txt"))
            File.Delete("test.txt");

        var lines = File.ReadAllLines("data/stream.txt");
        var packets = lines.Select(v => 
        {
            var tokens = v.Split(';');
            return (IPEndPoint.Parse(tokens[0]), Convert.FromHexString(tokens[1]));
        });

        var aggregator = new ServiceRecordAggregator();
        foreach (var packet in packets)
        {
            var parser = DnsPacket.Parse(packet.Item2);
            aggregator.Add(parser);
        }

        int counter = 0;
        aggregator.OnServicesUpdated += (services) =>
        {
            File.WriteAllText($"services-{counter++}.txt", JsonSerializer.Serialize(services.Select(v => new
            {
                v.Port,
                v.Name,
                Addresses = v.Addresses.Select(w => w.ToString()),
                v.Texts,
                v.Pointers
            }), new JsonSerializerOptions()
            {
                    WriteIndented = true
            }));
        };
    }
}