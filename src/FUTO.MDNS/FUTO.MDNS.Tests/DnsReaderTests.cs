namespace FUTO.MDNS.Tests;

[TestClass]
public class DnsReaderTests
{
    [TestMethod]
    public void BeyondTests()
    {
        byte[] data = [
            0x00, 0x01,
            0x00, 0x00, 0x00, 0x02,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03,
            0x00, 0x01,
            0x00, 0x00, 0x00, 0x02,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03
        ];

        var reader = new DnsReader(data);
        Assert.AreEqual(1, reader.ReadInt16());
        Assert.AreEqual(2, reader.ReadInt32());
        Assert.AreEqual(3, reader.ReadInt64());
        Assert.AreEqual(1u, reader.ReadUInt16());
        Assert.AreEqual(2u, reader.ReadUInt32());
        Assert.AreEqual(3u, reader.ReadUInt64());
    }
}