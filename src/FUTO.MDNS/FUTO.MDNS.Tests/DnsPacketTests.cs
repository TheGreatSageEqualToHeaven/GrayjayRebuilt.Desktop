using System.Formats.Asn1;
using System.Net;

namespace FUTO.MDNS.Tests;

[TestClass]
public class DnsPacketTests
{
    [TestMethod]
    public void ParseDnsPrinter()
    {
        byte[] data = 
        [
            0x00, 0x00,
            0x84, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x06, 0x04, 0x5f, 0x69, 0x70, 0x70, 0x04,
            0x5f, 0x74, 0x63, 0x70, 0x05, 0x6c, 0x6f, 0x63, 0x61, 0x6c, 0x00, 0x00, 0x0c, 0x00, 0x01, 0x00,
            0x00, 0x11, 0x94, 0x00, 0x1e, 0x1b, 0x42, 0x72, 0x6f, 0x74, 0x68, 0x65, 0x72, 0x20, 0x44, 0x43,
            0x50, 0x2d, 0x4c, 0x33, 0x35, 0x35, 0x30, 0x43, 0x44, 0x57, 0x20, 0x73, 0x65, 0x72, 0x69, 0x65,
            0x73, 0xc0, 0x0c, 0xc0, 0x27, 0x00, 0x10, 0x80, 0x01, 0x00, 0x00, 0x11, 0x94, 0x02, 0x53, 0x09,
            0x74, 0x78, 0x74, 0x76, 0x65, 0x72, 0x73, 0x3d, 0x31, 0x08, 0x71, 0x74, 0x6f, 0x74, 0x61, 0x6c,
            0x3d, 0x31, 0x42, 0x70, 0x64, 0x6c, 0x3d, 0x61, 0x70, 0x70, 0x6c, 0x69, 0x63, 0x61, 0x74, 0x69,
            0x6f, 0x6e, 0x2f, 0x6f, 0x63, 0x74, 0x65, 0x74, 0x2d, 0x73, 0x74, 0x72, 0x65, 0x61, 0x6d, 0x2c,
            0x69, 0x6d, 0x61, 0x67, 0x65, 0x2f, 0x75, 0x72, 0x66, 0x2c, 0x69, 0x6d, 0x61, 0x67, 0x65, 0x2f,
            0x6a, 0x70, 0x65, 0x67, 0x2c, 0x69, 0x6d, 0x61, 0x67, 0x65, 0x2f, 0x70, 0x77, 0x67, 0x2d, 0x72,
            0x61, 0x73, 0x74, 0x65, 0x72, 0x0c, 0x72, 0x70, 0x3d, 0x69, 0x70, 0x70, 0x2f, 0x70, 0x72, 0x69,
            0x6e, 0x74, 0x05, 0x6e, 0x6f, 0x74, 0x65, 0x3d, 0x1e, 0x74, 0x79, 0x3d, 0x42, 0x72, 0x6f, 0x74,
            0x68, 0x65, 0x72, 0x20, 0x44, 0x43, 0x50, 0x2d, 0x4c, 0x33, 0x35, 0x35, 0x30, 0x43, 0x44, 0x57,
            0x20, 0x73, 0x65, 0x72, 0x69, 0x65, 0x73, 0x25, 0x70, 0x72, 0x6f, 0x64, 0x75, 0x63, 0x74, 0x3d,
            0x28, 0x42, 0x72, 0x6f, 0x74, 0x68, 0x65, 0x72, 0x20, 0x44, 0x43, 0x50, 0x2d, 0x4c, 0x33, 0x35,
            0x35, 0x30, 0x43, 0x44, 0x57, 0x20, 0x73, 0x65, 0x72, 0x69, 0x65, 0x73, 0x29, 0x3c, 0x61, 0x64,
            0x6d, 0x69, 0x6e, 0x75, 0x72, 0x6c, 0x3d, 0x68, 0x74, 0x74, 0x70, 0x3a, 0x2f, 0x2f, 0x42, 0x52,
            0x57, 0x31, 0x30, 0x35, 0x42, 0x41, 0x44, 0x34, 0x41, 0x31, 0x35, 0x37, 0x30, 0x2e, 0x6c, 0x6f,
            0x63, 0x61, 0x6c, 0x2e, 0x2f, 0x6e, 0x65, 0x74, 0x2f, 0x6e, 0x65, 0x74, 0x2f, 0x61, 0x69, 0x72,
            0x70, 0x72, 0x69, 0x6e, 0x74, 0x2e, 0x68, 0x74, 0x6d, 0x6c, 0x0b, 0x70, 0x72, 0x69, 0x6f, 0x72,
            0x69, 0x74, 0x79, 0x3d, 0x32, 0x35, 0x0f, 0x75, 0x73, 0x62, 0x5f, 0x4d, 0x46, 0x47, 0x3d, 0x42,
            0x72, 0x6f, 0x74, 0x68, 0x65, 0x72, 0x1b, 0x75, 0x73, 0x62, 0x5f, 0x4d, 0x44, 0x4c, 0x3d, 0x44,
            0x43, 0x50, 0x2d, 0x4c, 0x33, 0x35, 0x35, 0x30, 0x43, 0x44, 0x57, 0x20, 0x73, 0x65, 0x72, 0x69,
            0x65, 0x73, 0x19, 0x75, 0x73, 0x62, 0x5f, 0x43, 0x4d, 0x44, 0x3d, 0x50, 0x4a, 0x4c, 0x2c, 0x50,
            0x43, 0x4c, 0x2c, 0x50, 0x43, 0x4c, 0x58, 0x4c, 0x2c, 0x55, 0x52, 0x46, 0x07, 0x43, 0x6f, 0x6c,
            0x6f, 0x72, 0x3d, 0x54, 0x08, 0x43, 0x6f, 0x70, 0x69, 0x65, 0x73, 0x3d, 0x54, 0x08, 0x44, 0x75,
            0x70, 0x6c, 0x65, 0x78, 0x3d, 0x54, 0x05, 0x46, 0x61, 0x78, 0x3d, 0x46, 0x06, 0x53, 0x63, 0x61,
            0x6e, 0x3d, 0x54, 0x0d, 0x50, 0x61, 0x70, 0x65, 0x72, 0x43, 0x75, 0x73, 0x74, 0x6f, 0x6d, 0x3d,
            0x54, 0x08, 0x42, 0x69, 0x6e, 0x61, 0x72, 0x79, 0x3d, 0x54, 0x0d, 0x54, 0x72, 0x61, 0x6e, 0x73,
            0x70, 0x61, 0x72, 0x65, 0x6e, 0x74, 0x3d, 0x54, 0x06, 0x54, 0x42, 0x43, 0x50, 0x3d, 0x46, 0x3e,
            0x55, 0x52, 0x46, 0x3d, 0x53, 0x52, 0x47, 0x42, 0x32, 0x34, 0x2c, 0x57, 0x38, 0x2c, 0x43, 0x50,
            0x31, 0x2c, 0x49, 0x53, 0x34, 0x2d, 0x31, 0x2c, 0x4d, 0x54, 0x31, 0x2d, 0x33, 0x2d, 0x34, 0x2d,
            0x35, 0x2d, 0x38, 0x2d, 0x31, 0x31, 0x2c, 0x4f, 0x42, 0x31, 0x30, 0x2c, 0x50, 0x51, 0x34, 0x2c,
            0x52, 0x53, 0x36, 0x30, 0x30, 0x2c, 0x56, 0x31, 0x2e, 0x34, 0x2c, 0x44, 0x4d, 0x31, 0x25, 0x6b,
            0x69, 0x6e, 0x64, 0x3d, 0x64, 0x6f, 0x63, 0x75, 0x6d, 0x65, 0x6e, 0x74, 0x2c, 0x65, 0x6e, 0x76,
            0x65, 0x6c, 0x6f, 0x70, 0x65, 0x2c, 0x6c, 0x61, 0x62, 0x65, 0x6c, 0x2c, 0x70, 0x6f, 0x73, 0x74,
            0x63, 0x61, 0x72, 0x64, 0x11, 0x50, 0x61, 0x70, 0x65, 0x72, 0x4d, 0x61, 0x78, 0x3d, 0x6c, 0x65,
            0x67, 0x61, 0x6c, 0x2d, 0x41, 0x34, 0x29, 0x55, 0x55, 0x49, 0x44, 0x3d, 0x65, 0x33, 0x32, 0x34,
            0x38, 0x30, 0x30, 0x30, 0x2d, 0x38, 0x30, 0x63, 0x65, 0x2d, 0x31, 0x31, 0x64, 0x62, 0x2d, 0x38,
            0x30, 0x30, 0x30, 0x2d, 0x33, 0x63, 0x32, 0x61, 0x66, 0x34, 0x61, 0x61, 0x63, 0x30, 0x61, 0x34,
            0x0c, 0x70, 0x72, 0x69, 0x6e, 0x74, 0x5f, 0x77, 0x66, 0x64, 0x73, 0x3d, 0x54, 0x14, 0x6d, 0x6f,
            0x70, 0x72, 0x69, 0x61, 0x2d, 0x63, 0x65, 0x72, 0x74, 0x69, 0x66, 0x69, 0x65, 0x64, 0x3d, 0x31,
            0x2e, 0x33, 0x0f, 0x42, 0x52, 0x57, 0x31, 0x30, 0x35, 0x42, 0x41, 0x44, 0x34, 0x41, 0x31, 0x35,
            0x37, 0x30, 0xc0, 0x16, 0x00, 0x01, 0x80, 0x01, 0x00, 0x00, 0x00, 0x78, 0x00, 0x04, 0xc0, 0xa8,
            0x01, 0xc5, 0xc2, 0xa4, 0x00, 0x1c, 0x80, 0x01, 0x00, 0x00, 0x00, 0x78, 0x00, 0x10, 0xfe, 0x80,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x12, 0x5b, 0xad, 0xff, 0xfe, 0x4a, 0x15, 0x70, 0xc0, 0x27,
            0x00, 0x21, 0x80, 0x01, 0x00, 0x00, 0x00, 0x78, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x02, 0x77,
            0xc2, 0xa4, 0xc0, 0x27, 0x00, 0x2f, 0x80, 0x01, 0x00, 0x00, 0x11, 0x94, 0x00, 0x09, 0xc0, 0x27,
            0x00, 0x05, 0x00, 0x00, 0x80, 0x00, 0x40, 0xc2, 0xa4, 0x00, 0x2f, 0x80, 0x01, 0x00, 0x00, 0x00,
            0x78, 0x00, 0x08, 0xc2, 0xa4, 0x00, 0x04, 0x40, 0x00, 0x00, 0x08
        ];

        var p = DnsPacket.Parse(data);
        Assert.AreEqual(QueryResponse.Response, p.Header.QueryResponse);
        Assert.AreEqual(DnsOpcode.StandardQuery, p.Header.Opcode);
        Assert.AreEqual(true, p.Header.AuthorativeAnswer);
        Assert.AreEqual(false, p.Header.Truncated);
        Assert.AreEqual(false, p.Header.RecursionDesired);
        Assert.AreEqual(false, p.Header.RecursionAvailable);
        Assert.AreEqual(false, p.Header.AnswerAuthenticated);
        Assert.AreEqual(false, p.Header.NonAuthenticatedData);
        Assert.AreEqual(DnsResponseCode.NoError, p.Header.ResponseCode);
        Assert.AreEqual(0, p.Questions.Count);
        Assert.AreEqual(1, p.Answers.Count);
        Assert.AreEqual(0, p.Authorities.Count);
        Assert.AreEqual(6, p.Additionals.Count);

        Assert.AreEqual("_ipp._tcp.local", p.Answers[0].Name);
        Assert.AreEqual(ResourceRecordType.PTR, p.Answers[0].Type);
        Assert.AreEqual(ResourceRecordClass.IN, p.Answers[0].Class);
        Assert.AreEqual(false, p.Answers[0].CacheFlush);
        Assert.AreEqual(4500u, p.Answers[0].TimeToLive);
        Assert.AreEqual(30, p.Answers[0].DataLength);
        Assert.AreEqual("Brother DCP-L3550CDW series._ipp._tcp.local", p.Answers[0].GetDataReader().ReadPTRRecord().DomainName);

        Assert.AreEqual("Brother DCP-L3550CDW series._ipp._tcp.local", p.Additionals[0].Name);
        Assert.AreEqual(ResourceRecordType.TXT, p.Additionals[0].Type);
        Assert.AreEqual(ResourceRecordClass.IN, p.Additionals[0].Class);
        Assert.AreEqual(true, p.Additionals[0].CacheFlush);
        Assert.AreEqual(4500u, p.Additionals[0].TimeToLive);
        Assert.AreEqual(595, p.Additionals[0].DataLength);

        {
            var txtRecord = p.Additionals[0].GetDataReader().ReadTXTRecord();
            CollectionAssert.AreEqual(new string[] {
                "txtvers=1",
                "qtotal=1",
                "pdl=application/octet-stream,image/urf,image/jpeg,image/pwg-raster",
                "rp=ipp/print",
                "note=",
                "ty=Brother DCP-L3550CDW series",
                "product=(Brother DCP-L3550CDW series)",
                "adminurl=http://BRW105BAD4A1570.local./net/net/airprint.html",
                "priority=25",
                "usb_MFG=Brother",
                "usb_MDL=DCP-L3550CDW series",
                "usb_CMD=PJL,PCL,PCLXL,URF",
                "Color=T",
                "Copies=T",
                "Duplex=T",
                "Fax=F",
                "Scan=T",
                "PaperCustom=T",
                "Binary=T",
                "Transparent=T",
                "TBCP=F",
                "URF=SRGB24,W8,CP1,IS4-1,MT1-3-4-5-8-11,OB10,PQ4,RS600,V1.4,DM1",
                "kind=document,envelope,label,postcard",
                "PaperMax=legal-A4",
                "UUID=e3248000-80ce-11db-8000-3c2af4aac0a4",
                "print_wfds=T",
                "mopria-certified=1.3"
            }, txtRecord.Texts);
        }

        Assert.AreEqual("BRW105BAD4A1570.local", p.Additionals[1].Name);
        Assert.AreEqual(ResourceRecordType.A, p.Additionals[1].Type);
        Assert.AreEqual(ResourceRecordClass.IN, p.Additionals[1].Class);
        Assert.AreEqual(true, p.Additionals[1].CacheFlush);
        Assert.AreEqual(120u, p.Additionals[1].TimeToLive);
        Assert.AreEqual(4, p.Additionals[1].DataLength);

        {
            var aRecord = p.Additionals[1].GetDataReader().ReadARecord();
            Assert.AreEqual(IPAddress.Parse("192.168.1.197"), aRecord.Address);
        }

        Assert.AreEqual("BRW105BAD4A1570.local", p.Additionals[2].Name);
        Assert.AreEqual(ResourceRecordType.AAAA, p.Additionals[2].Type);
        Assert.AreEqual(ResourceRecordClass.IN, p.Additionals[2].Class);
        Assert.AreEqual(true, p.Additionals[2].CacheFlush);
        Assert.AreEqual(120u, p.Additionals[2].TimeToLive);
        Assert.AreEqual(16, p.Additionals[2].DataLength);

        {
            var aaaaRecord = p.Additionals[2].GetDataReader().ReadAAAARecord();
            Assert.AreEqual(IPAddress.Parse("fe80::125b:adff:fe4a:1570"), aaaaRecord.Address);
        }

        Assert.AreEqual("Brother DCP-L3550CDW series._ipp._tcp.local", p.Additionals[3].Name);
        Assert.AreEqual(ResourceRecordType.SRV, p.Additionals[3].Type);
        Assert.AreEqual(ResourceRecordClass.IN, p.Additionals[3].Class);
        Assert.AreEqual(true, p.Additionals[3].CacheFlush);
        Assert.AreEqual(120u, p.Additionals[3].TimeToLive);
        Assert.AreEqual(8, p.Additionals[3].DataLength);

        {
            var srvRecord = p.Additionals[3].GetDataReader().ReadSRVRecord();
            Assert.AreEqual("BRW105BAD4A1570.local", srvRecord.Target);
            Assert.AreEqual(0, srvRecord.Weight);
            Assert.AreEqual(0, srvRecord.Priority);
            Assert.AreEqual(631, srvRecord.Port);
        }

        Assert.AreEqual("Brother DCP-L3550CDW series._ipp._tcp.local", p.Additionals[4].Name);
        Assert.AreEqual(ResourceRecordType.NSEC, p.Additionals[4].Type);
        Assert.AreEqual(ResourceRecordClass.IN, p.Additionals[4].Class);
        Assert.AreEqual(true, p.Additionals[4].CacheFlush);
        Assert.AreEqual(4500u, p.Additionals[4].TimeToLive);
        Assert.AreEqual(9, p.Additionals[4].DataLength);

        {
            var nSECRecord = p.Additionals[4].GetDataReader().ReadNSECRecord();
            Assert.AreEqual("Brother DCP-L3550CDW series._ipp._tcp.local", nSECRecord.OwnerName);
            Assert.AreEqual(1, nSECRecord.TypeBitMaps.Count);
            Assert.AreEqual(0, nSECRecord.TypeBitMaps[0].WindowBlock);
            CollectionAssert.AreEqual(new byte[] { 0, 0, 128, 0, 64 }, nSECRecord.TypeBitMaps[0].Bitmap);
        }
    }

    [TestMethod]
    public void ParseSamsungTV()
    {
        byte[] data = File.ReadAllBytes("data/samsung-airplay.hex");

        var p = DnsPacket.Parse(data);
        Assert.AreEqual(QueryResponse.Response, p.Header.QueryResponse);
        Assert.AreEqual(DnsOpcode.StandardQuery, p.Header.Opcode);
        Assert.AreEqual(true, p.Header.AuthorativeAnswer);
        Assert.AreEqual(false, p.Header.Truncated);
        Assert.AreEqual(false, p.Header.RecursionDesired);
        Assert.AreEqual(false, p.Header.RecursionAvailable);
        Assert.AreEqual(false, p.Header.AnswerAuthenticated);
        Assert.AreEqual(false, p.Header.NonAuthenticatedData);
        Assert.AreEqual(DnsResponseCode.NoError, p.Header.ResponseCode);
        Assert.AreEqual(0, p.Questions.Count);
        Assert.AreEqual(6, p.Answers.Count);
        Assert.AreEqual(0, p.Authorities.Count);
        Assert.AreEqual(4, p.Additionals.Count);

        Assert.AreEqual("9.1.168.192.in-addr.arpa", p.Answers[0].Name);
        Assert.AreEqual(ResourceRecordType.PTR, p.Answers[0].Type);
        Assert.AreEqual(ResourceRecordClass.IN, p.Answers[0].Class);
        Assert.AreEqual(true, p.Answers[0].CacheFlush);
        Assert.AreEqual(120u, p.Answers[0].TimeToLive);
        Assert.AreEqual(15, p.Answers[0].DataLength);
        Assert.AreEqual("Samsung.local", p.Answers[0].GetDataReader().ReadPTRRecord().DomainName);

        {
            Assert.AreEqual("Samsung 8 Series (49)._airplay._tcp.local", p.Answers[1].Name);
            Assert.AreEqual(ResourceRecordType.TXT, p.Answers[1].Type);
            Assert.AreEqual(ResourceRecordClass.IN, p.Answers[1].Class);
            Assert.AreEqual(true, p.Answers[1].CacheFlush);
            Assert.AreEqual(4500u, p.Answers[1].TimeToLive);
            Assert.AreEqual(368, p.Answers[1].DataLength);

            var txtRecord = p.Answers[1].GetDataReader().ReadTXTRecord();
            Console.WriteLine(string.Join("\n", txtRecord.Texts));
            CollectionAssert.AreEqual(new string[] 
            {
                "acl=0",
                "deviceid=D4:9D:C0:2F:52:16",
                "features=0x7F8AD0,0x38BCB46",
                "rsf=0x3",
                "fv=p20.0.1",
                "flags=0x244",
                "model=URU8000",
                "manufacturer=Samsung",
                "serialNumber=0EQC3HDM900064X",
                "protovers=1.1",
                "srcvers=377.17.24.6",
                "pi=ED:0C:A5:ED:10:08",
                "psi=00000000-0000-0000-0000-ED0CA5ED1008",
                "gid=00000000-0000-0000-0000-ED0CA5ED1008",
                "gcgl=0",
                "pk=d25488cbff1334756165cd7229a235475ef591f2595f38ed251d46b8a4d2345d"
            }, txtRecord.Texts);
        }

        Assert.AreEqual("_services._dns-sd._udp.local", p.Answers[2].Name);
        Assert.AreEqual(ResourceRecordType.PTR, p.Answers[2].Type);
        Assert.AreEqual(ResourceRecordClass.IN, p.Answers[2].Class);
        Assert.AreEqual(false, p.Answers[2].CacheFlush);
        Assert.AreEqual(4500u, p.Answers[2].TimeToLive);
        Assert.AreEqual(2, p.Answers[2].DataLength);
        Assert.AreEqual("_airplay._tcp.local", p.Answers[2].GetDataReader().ReadPTRRecord().DomainName);

        Assert.AreEqual("_airplay._tcp.local", p.Answers[3].Name);
        Assert.AreEqual(ResourceRecordType.PTR, p.Answers[3].Type);
        Assert.AreEqual(ResourceRecordClass.IN, p.Answers[3].Class);
        Assert.AreEqual(false, p.Answers[3].CacheFlush);
        Assert.AreEqual(4500u, p.Answers[3].TimeToLive);
        Assert.AreEqual(2, p.Answers[3].DataLength);
        Assert.AreEqual("Samsung 8 Series (49)._airplay._tcp.local", p.Answers[3].GetDataReader().ReadPTRRecord().DomainName);

        Assert.AreEqual("Samsung 8 Series (49)._airplay._tcp.local", p.Answers[4].Name);
        Assert.AreEqual(ResourceRecordType.SRV, p.Answers[4].Type);
        Assert.AreEqual(ResourceRecordClass.IN, p.Answers[4].Class);
        Assert.AreEqual(true, p.Answers[4].CacheFlush);
        Assert.AreEqual(120u, p.Answers[4].TimeToLive);
        Assert.AreEqual(8, p.Answers[4].DataLength);
        
        {
            var srvRecord = p.Answers[4].GetDataReader().ReadSRVRecord();
            Assert.AreEqual(33482, srvRecord.Port);
            Assert.AreEqual(0, srvRecord.Priority);
            Assert.AreEqual(0, srvRecord.Weight);
            Assert.AreEqual("Samsung.local", srvRecord.Target);
        }

        Assert.AreEqual("Samsung.local", p.Answers[5].Name);
        Assert.AreEqual(ResourceRecordType.A, p.Answers[5].Type);
        Assert.AreEqual(ResourceRecordClass.IN, p.Answers[5].Class);
        Assert.AreEqual(true, p.Answers[5].CacheFlush);
        Assert.AreEqual(120u, p.Answers[5].TimeToLive);
        Assert.AreEqual(4, p.Answers[5].DataLength);
        Assert.AreEqual(IPAddress.Parse("192.168.1.9"), p.Answers[5].GetDataReader().ReadARecord().Address);

        Assert.AreEqual("9.1.168.192.in-addr.arpa", p.Additionals[0].Name);
        Assert.AreEqual(ResourceRecordType.NSEC, p.Additionals[0].Type);
        Assert.AreEqual(ResourceRecordClass.IN, p.Additionals[0].Class);
        Assert.AreEqual(true, p.Additionals[0].CacheFlush);
        Assert.AreEqual(120u, p.Additionals[0].TimeToLive);
        Assert.AreEqual(6, p.Additionals[0].DataLength);

        {
            var nSECRecord = p.Additionals[0].GetDataReader().ReadNSECRecord();
            Assert.AreEqual("9.1.168.192.in-addr.arpa", nSECRecord.OwnerName);
            Assert.AreEqual(1, nSECRecord.TypeBitMaps.Count);
            Assert.AreEqual(0, nSECRecord.TypeBitMaps[0].WindowBlock);
            CollectionAssert.AreEqual(new byte[] { 0, 8 }, nSECRecord.TypeBitMaps[0].Bitmap);
        }

        Assert.AreEqual("Samsung 8 Series (49)._airplay._tcp.local", p.Additionals[1].Name);
        Assert.AreEqual(ResourceRecordType.NSEC, p.Additionals[1].Type);
        Assert.AreEqual(ResourceRecordClass.IN, p.Additionals[1].Class);
        Assert.AreEqual(true, p.Additionals[1].CacheFlush);
        Assert.AreEqual(4500u, p.Additionals[1].TimeToLive);
        Assert.AreEqual(9, p.Additionals[1].DataLength);

        {
            var nSECRecord = p.Additionals[1].GetDataReader().ReadNSECRecord();
            Assert.AreEqual("Samsung 8 Series (49)._airplay._tcp.local", nSECRecord.OwnerName);
            Assert.AreEqual(1, nSECRecord.TypeBitMaps.Count);
            Assert.AreEqual(0, nSECRecord.TypeBitMaps[0].WindowBlock);
            CollectionAssert.AreEqual(new byte[] { 0, 0, 128, 0, 64 }, nSECRecord.TypeBitMaps[0].Bitmap);
        }

        Assert.AreEqual("Samsung.local", p.Additionals[2].Name);
        Assert.AreEqual(ResourceRecordType.NSEC, p.Additionals[2].Type);
        Assert.AreEqual(ResourceRecordClass.IN, p.Additionals[2].Class);
        Assert.AreEqual(true, p.Additionals[2].CacheFlush);
        Assert.AreEqual(120u, p.Additionals[2].TimeToLive);
        Assert.AreEqual(5, p.Additionals[2].DataLength);

        {
            var nSECRecord = p.Additionals[2].GetDataReader().ReadNSECRecord();
            Assert.AreEqual("Samsung.local", nSECRecord.OwnerName);
            Assert.AreEqual(1, nSECRecord.TypeBitMaps.Count);
            Assert.AreEqual(0, nSECRecord.TypeBitMaps[0].WindowBlock);
            CollectionAssert.AreEqual(new byte[] { 64 }, nSECRecord.TypeBitMaps[0].Bitmap);
        }

        Assert.AreEqual("", p.Additionals[3].Name);
        Assert.AreEqual(ResourceRecordType.OPT, p.Additionals[3].Type);
        Assert.AreEqual((ResourceRecordClass)160, p.Additionals[3].Class);
        Assert.AreEqual(false, p.Additionals[3].CacheFlush);
        Assert.AreEqual(4500u, p.Additionals[3].TimeToLive);
        Assert.AreEqual(9, p.Additionals[3].DataLength);

        {
            var optRecord = p.Additionals[3].GetDataReader().ReadOPTRecord();
            Assert.AreEqual(1, optRecord.Options.Count);
            Assert.AreEqual(65001, optRecord.Options[0].Code);
            Assert.AreEqual(5, optRecord.Options[0].Data.Length);
            CollectionAssert.AreEqual(new byte[] { 0, 0, 116, 206, 97 }, optRecord.Options[0].Data);
        }
    }

    [TestMethod]
    public void UnicodeTest()
    {
        byte[] data = [
            0x00, 0x00, 0x84, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x15, 0x41, 0x69, 0x64,
            0x61, 0x6E, 0xE2, 0x80, 0x99, 0x73, 0x20, 0x4D, 0x61, 0x63, 0x42, 0x6F, 0x6F, 0x6B, 0x20, 0x50,
            0x72, 0x6F, 0x0F, 0x5F, 0x63, 0x6F, 0x6D, 0x70, 0x61, 0x6E, 0x69, 0x6F, 0x6E, 0x2D, 0x6C, 0x69,
            0x6E, 0x6B, 0x04, 0x5F, 0x74, 0x63, 0x70, 0x05, 0x6C, 0x6F, 0x63, 0x61, 0x6C, 0x00, 0x00, 0x10,
            0x80, 0x01, 0x00, 0x00, 0x11, 0x94, 0x00, 0x5B, 0x16, 0x72, 0x70, 0x42, 0x41, 0x3D, 0x30, 0x33,
            0x3A, 0x43, 0x32, 0x3A, 0x33, 0x33, 0x3A, 0x38, 0x36, 0x3A, 0x33, 0x43, 0x3A, 0x45, 0x45, 0x11,
            0x72, 0x70, 0x41, 0x44, 0x3D, 0x66, 0x33, 0x33, 0x37, 0x61, 0x38, 0x61, 0x32, 0x38, 0x64, 0x35,
            0x31, 0x0C, 0x72, 0x70, 0x46, 0x6C, 0x3D, 0x30, 0x78, 0x32, 0x30, 0x30, 0x30, 0x30, 0x11, 0x72,
            0x70, 0x48, 0x4E, 0x3D, 0x31, 0x66, 0x66, 0x64, 0x64, 0x64, 0x66, 0x33, 0x63, 0x39, 0x65, 0x33,
            0x07, 0x72, 0x70, 0x4D, 0x61, 0x63, 0x3D, 0x30, 0x0A, 0x72, 0x70, 0x56, 0x72, 0x3D, 0x33, 0x36,
            0x30, 0x2E, 0x34, 0xC0, 0x0C, 0x00, 0x2F, 0x80, 0x01, 0x00, 0x00, 0x11, 0x94, 0x00, 0x09, 0xC0,
            0x0C, 0x00, 0x05, 0x00, 0x00, 0x80, 0x00, 0x40
        ];

        var packet = DnsPacket.Parse(data);
        Assert.AreEqual("Aidan’s MacBook Pro._companion-link._tcp.local", packet.Additionals[0].Name);
    }

    [TestMethod]
    public void ReserializeDnsPrinter()
    {
        byte[] data = File.ReadAllBytes("data/samsung-airplay.hex");
        var p = DnsPacket.Parse(data);
        var writer = new DnsWriter();
        writer.WritePacket(p.Header,
            questionCount: p.Questions.Count,
            questionWriter: (w, i) => { },
            answerCount: p.Answers.Count,
            answerWriter: (w, i) => 
            {
                w.Write(p.Answers[i], (v) =>
                {
                    var reader = p.Answers[i].GetDataReader();
                    switch (i)
                    {
                        case 0:
                        case 2:
                        case 3:
                            v.Write(reader.ReadPTRRecord());
                            break;
                        case 1:
                            v.Write(reader.ReadTXTRecord());
                            break;
                        case 4:
                            v.Write(reader.ReadSRVRecord());
                            break;
                        case 5:
                            v.Write(reader.ReadARecord());
                            break;
                    }
                });
            },
            authorityCount: p.Authorities.Count,
            authorityWriter: (w, i) => { },
            additionalsCount: p.Additionals.Count,
            additionalWriter: (w, i) => 
            {
                w.Write(p.Additionals[i], (v) =>
                {
                    var reader = p.Additionals[i].GetDataReader();
                    switch (i)
                    {
                        case 0:
                        case 1:
                        case 2:
                            v.Write(reader.ReadNSECRecord());
                            break;
                        case 3:
                            v.Write(reader.ReadOPTRecord());
                            break;
                    }
                });
            }
        );

        CollectionAssert.AreEqual(data, writer.ToArray());
    }

    /*[TestMethod]
    public void TestReadDomainName()
    {
        byte[] data = [
            0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0B, 0x5F, 0x67, 0x6F,
            0x6F, 0x67, 0x6C, 0x65, 0x63, 0x61, 0x73, 0x74, 0x04, 0x5F, 0x74, 0x63, 0x70, 0x05, 0x6C, 0x6F,
            0x63, 0x61, 0x6C, 0xC0, 0x0C, 0x00, 0x0C, 0x00, 0x01, 0x08, 0x5F, 0x61, 0x69, 0x72, 0x70, 0x6C,
            0x61, 0x79, 0xC0, 0x18, 0x00, 0x0C, 0x00, 0x01, 0x09, 0x5F, 0x66, 0x61, 0x73, 0x74, 0x63, 0x61,
            0x73, 0x74, 0xC0, 0x18, 0x00, 0x0C, 0x00, 0x01, 0x06, 0x5F, 0x66, 0x63, 0x61, 0x73, 0x74, 0xC0,
            0x18, 0x00, 0x0C, 0x00, 0x01
        ];

        var packet = DnsPacket.Parse(data);
        Console.WriteLine();
    }*/
}