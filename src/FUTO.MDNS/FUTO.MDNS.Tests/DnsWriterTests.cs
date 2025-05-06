namespace FUTO.MDNS.Tests;

[TestClass]
public class DnsWriterTests
{
    [TestMethod]
    public void BasicOperation()
    {
        byte[] expectedData = [
            0x00, 0x01,
            0x00, 0x00, 0x00, 0x02,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03,
            0x00, 0x01,
            0x00, 0x00, 0x00, 0x02,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03
        ];

        var writer = new DnsWriter();
        writer.Write((ushort)1);
        writer.Write((uint)2);
        writer.Write((ulong)3);
        writer.Write((short)1);
        writer.Write((int)2);
        writer.Write((long)3);
        CollectionAssert.AreEqual(expectedData, writer.ToArray());
    }

    [TestMethod]
    public void DnsQuestionFormat()
    {
        byte[] expectedBytes = [ 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x08, 0x5f, 0x61, 0x69, 0x72, 0x70, 0x6c, 0x61, 0x79, 0x04, 0x5f, 0x74, 0x63, 0x70, 0x05, 0x6c, 0x6f, 0x63, 0x61, 0x6c, 0x00, 0x00, 0x0c, 0x00, 0x01 ];
        var writer = new DnsWriter();
        writer.WritePacket(
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
                ResponseCode = DnsResponseCode.NoError,
            }, 
            questionCount: 1,
            questionWriter: (w, i) => 
            {
                w.Write(new DnsQuestion()
                {
                    Name = "_airplay._tcp.local",
                    Type = QuestionType.PTR,
                    Class = QuestionClass.IN,
                    QueryUnicast = false
                });
            }
        );

        CollectionAssert.AreEqual(expectedBytes, writer.ToArray());
    }
}