namespace FUTO.MDNS;

using System.Buffers.Binary;
using System.Net;

public enum QueryResponse : byte
{
    Query = 0,
    Response = 1
}

public enum DnsOpcode : byte
{
    StandardQuery = 0,
    InverseQuery = 1,
    ServerStatusRequest = 2
}

public enum DnsResponseCode : byte
{
    NoError = 0,
    FormatError = 1,
    ServerFailure = 2,
    NameError = 3,
    NotImplemented = 4,
    Refused = 5
}

public class DnsPacketHeader
{
    public ushort Identifier { get; init; }
    /// <summary>
    /// A one bit field that specifies whether this message is a query (0), or a response (1).
    /// </summary>
    public QueryResponse QueryResponse { get; init; }
    /// <summary>
    /// A four bit field that specifies kind of query in this message.  This value is set by the originator of a query and copied into the response.
    /// </summary>
    public DnsOpcode Opcode { get; init; }
    /// <summary>
    /// Authoritative Answer - this bit is valid in responses, and specifies that the responding name server is an authority for the domain name in question section. Note that the contents of the answer section may have multiple owner names because of aliases. The AA bit corresponds to the name which matches the query name, or the first owner name in the answer section.
    /// </summary>
    public bool AuthorativeAnswer { get; init; }
    /// <summary>
    /// Specifies that this message was truncated due to length greater than that permitted on the transmission channel. 
    /// </summary>
    public bool Truncated { get; init; }
    /// <summary>
    /// This bit may be set in a query and is copied into the response.  If RD is set, it directs the name server to pursue the query recursively. Recursive query support is optional.
    /// </summary>
    public bool RecursionDesired { get; init; }
    /// <summary>
    /// This be is set or cleared in a response, and denotes whether recursive query support is available in the name server.
    /// </summary>
    public bool RecursionAvailable { get; init; }
    public bool AnswerAuthenticated { get; init; }
    public bool NonAuthenticatedData { get; init; }
    /// <summary>
    /// This 4 bit field is set as part of responses.
    /// </summary>
    public DnsResponseCode ResponseCode { get; init; }
}

public class DnsPacket
{
    public required DnsPacketHeader Header { get; init; }
    public required List<DnsQuestion> Questions { get; init; }
    public required List<DnsResourceRecord> Answers { get; init; }
    public required List<DnsResourceRecord> Authorities { get; init; }
    public required List<DnsResourceRecord> Additionals { get; init; }

    public static DnsPacket Parse(byte[] data)
    {
        var span = new ReadOnlySpan<byte>(data);
        var flags = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(2, 2));
        var questionCount = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(4, 2));
        var answerCount = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(6, 2));
        var authorityCount = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(8, 2));
        var additionalCount = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(10, 2));

        var packet = new DnsPacket()
        {
            Header = new DnsPacketHeader()
            {
                Identifier = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(0, 2)),
                QueryResponse = (QueryResponse)((flags >> 15) & 0b1),
                Opcode = (DnsOpcode)((flags >> 11) & 0b1111),
                AuthorativeAnswer = ((flags >> 10) & 0b1) != 0,
                Truncated = ((flags >> 9) & 0b1) != 0,
                RecursionDesired = ((flags >> 8) & 0b1) != 0,
                RecursionAvailable = ((flags >> 7) & 0b1) != 0,
                AnswerAuthenticated = ((flags >> 5) & 0b1) != 0,
                NonAuthenticatedData = ((flags >> 4) & 0b1) != 0,
                ResponseCode = (DnsResponseCode)(flags & 0b1111)
            },
            Questions = new List<DnsQuestion>(questionCount),
            Answers = new List<DnsResourceRecord>(answerCount),
            Authorities = new List<DnsResourceRecord>(authorityCount),
            Additionals = new List<DnsResourceRecord>(additionalCount)
        };

        int position = 12;

        for (int i = 0; i < questionCount; i++)
        {
            var question = DnsQuestion.Parse(data, ref position);
            packet.Questions.Add(question);
        }

        for (int i = 0; i < answerCount; i++)
        {
            var answer = DnsResourceRecord.Parse(data, ref position);
            packet.Answers.Add(answer);
        }

        for (int i = 0; i < authorityCount; i++)
        {
            var authority = DnsResourceRecord.Parse(data, ref position);
            packet.Authorities.Add(authority);
        }

        for (int i = 0; i < additionalCount; i++)
        {
            var additional = DnsResourceRecord.Parse(data, ref position);
            packet.Additionals.Add(additional);
        }

        return packet;
    }
}