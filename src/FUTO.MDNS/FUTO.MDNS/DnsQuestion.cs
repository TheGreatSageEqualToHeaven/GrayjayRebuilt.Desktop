namespace FUTO.MDNS;

using System.Buffers.Binary;

public enum QuestionType : ushort
{
    A = 1,
    /// <summary>
    /// An authorative name server
    /// </summary>
    NS = 2,
    /// <summary>
    /// A mail destination
    /// </summary>
    MD = 3,
    /// <summary>
    /// A mail forwarder
    /// </summary>
    MF = 4,
    /// <summary>
    /// The canonical name for an alias
    /// </summary>
    CNAME = 5,
    /// <summary>
    /// Marks the start of a zone of authority
    /// </summary>
    SOA = 6,
    /// <summary>
    /// A mailbox domain name
    /// </summary>
    MB = 7,
    /// <summary>
    /// A mail group member
    /// </summary>
    MG = 8,
    /// <summary>
    /// A mail rename domain name
    /// </summary>
    MR = 9,
    /// <summary>
    /// A null resource record
    /// </summary>
    NULL = 10,
    /// <summary>
    /// A well known service description
    /// </summary>
    WKS = 11,
    /// <summary>
    /// A domain name pointer
    /// </summary>
    PTR = 12,
    /// <summary>
    /// Host information
    /// </summary>
    HINFO = 13,
    /// <summary>
    /// Mailbox of mail list information
    /// </summary>
    MINFO = 14,
    /// <summary>
    /// Mail exchange
    /// </summary>
    MX = 15,
    /// <summary>
    /// Text strings
    /// </summary>
    TXT = 16,
    /// <summary>
    /// Information about the responsible person(s) for the domain. Usually an email address with the @ replaced by a .
    /// </summary>
    RP = 17,
    /// <summary>
    /// Location of database servers of an AFS cell. This record is commonly used by AFS clients to contact AFS cells outside their local domain. A subtype of this record is used by the obsolete DCE/DFS file system.
    /// </summary>
    AFSDB = 18,
    /// <summary>
    /// Signature record used in SIG(0) (RFC 2931) and TKEY (RFC 2930).[7] RFC 3755 designated RRSIG as the replacement for SIG for use within DNSSEC.[7]
    /// </summary>
    SIG = 24,
    /// <summary>
    /// Used only for SIG(0) (RFC 2931) and TKEY (RFC 2930).[5] RFC 3445 eliminated their use for application keys and limited their use to DNSSEC.[6] RFC 3755 designates DNSKEY as the replacement within DNSSEC.[7] RFC 4025 designates IPSECKEY as the replacement for use with IPsec.[8]
    /// </summary>
    KEY = 25,
    /// <summary>
    /// Returns a 128-bit IPv6 address, most commonly used to map hostnames to an IP address of the host.
    /// </summary>
    AAAA = 28,
    /// <summary>
    /// Specifies a geographical location associated with a domain name
    /// </summary>
    LOC = 29,
    /// <summary>
    /// Generalized service location record, used for newer protocols instead of creating protocol-specific records such as MX.
    /// </summary>
    SRV = 33,
    /// <summary>
    /// Allows regular-expression-based rewriting of domain names which can then be used as URIs, further domain names to lookups, etc.
    /// </summary>
    NAPTR = 35,
    /// <summary>
    /// Used with some cryptographic systems (not including DNSSEC) to identify a key management agent for the associated domain-name. Note that this has nothing to do with DNS Security. It is Informational status, rather than being on the IETF standards-track. It has always had limited deployment, but is still in use.
    /// </summary>
    KX = 36,
    /// <summary>
    /// Stores PKIX, SPKI, PGP, etc.
    /// </summary>
    CERT = 37,
    /// <summary>
    /// Alias for a name and all its subnames, unlike CNAME, which is an alias for only the exact name. Like a CNAME record, the DNS lookup will continue by retrying the lookup with the new name.
    /// </summary>
    DNAME = 39,
    /// <summary>
    /// Specify lists of address ranges, e.g. in CIDR format, for various address families. Experimental.
    /// </summary>
    APL = 42,
    /// <summary>
    /// The record used to identify the DNSSEC signing key of a delegated zone
    /// </summary>
    DS = 43,
    /// <summary>
    /// Resource record for publishing SSH public host key fingerprints in the DNS, in order to aid in verifying the authenticity of the host. RFC 6594 defines ECC SSH keys and SHA-256 hashes. See the IANA SSHFP RR parameters registry for details.
    /// </summary>
    SSHFP = 44,
    /// <summary>
    /// Key record that can be used with IPsec
    /// </summary>
    IPSECKEY = 45,
    /// <summary>
    /// Signature for a DNSSEC-secured record set. Uses the same format as the SIG record.
    /// </summary>
    RRSIG = 46,
    /// <summary>
    /// Part of DNSSEC—used to prove a name does not exist. Uses the same format as the (obsolete) NXT record.
    /// </summary>
    NSEC = 47,
    /// <summary>
    /// The key record used in DNSSEC. Uses the same format as the KEY record.
    /// </summary>
    DNSKEY = 48,
    /// <summary>
    /// Used in conjunction with the FQDN option to DHCP
    /// </summary>
    DHCID = 49,
    /// <summary>
    /// An extension to DNSSEC that allows proof of nonexistence for a name without permitting zonewalking
    /// </summary>
    NSEC3 = 50,
    /// <summary>
    /// Parameter record for use with NSEC3
    /// </summary>
    NSEC3PARAM = 51,
    /// <summary>
    /// A record for DANE. RFC 6698 defines "The TLSA DNS resource record is used to associate a TLS server certificate or public key with the domain name where the record is found, thus forming a 'TLSA certificate association'".
    /// </summary>
    TSLA = 52,
    /// <summary>
    /// Associates an S/MIME certificate with a domain name for sender authentication.
    /// </summary>
    SMIMEA = 53,
    /// <summary>
    /// Method of separating the end-point identifier and locator roles of IP addresses.
    /// </summary>
    HIP = 55,
    /// <summary>
    /// Child copy of DS record, for transfer to parent
    /// </summary>
    CDS = 59,
    /// <summary>
    /// Child copy of DNSKEY record, for transfer to parent
    /// </summary>
    CDNSKEY = 60,
    /// <summary>
    /// A DNS-based Authentication of Named Entities (DANE) method for publishing and locating OpenPGP public keys in DNS for a specific email address using an OPENPGPKEY DNS resource record.
    /// </summary>
    OPENPGPKEY = 61,
    /// <summary>
    /// Specify a synchronization mechanism between a child and a parent DNS zone. Typical example is declaring the same NS records in the parent and the child zone
    /// </summary>
    CSYNC = 62,
    /// <summary>
    /// Provides a cryptographic message digest over DNS zone data at rest.
    /// </summary>
    ZONEMD = 63,
    /// <summary>
    /// RR that improves performance for clients that need to resolve many resources to access a domain.
    /// </summary>
    SVCB = 64,
    /// <summary>
    /// RR that improves performance for clients that need to resolve many resources to access a domain.
    /// </summary>
    HTTPS = 65,
    /// <summary>
    /// A 48-bit IEEE Extended Unique Identifier.
    /// </summary>
    EUI48 = 108,
    /// <summary>
    /// A 64-bit IEEE Extended Unique Identifier.
    /// </summary>
    EUI64 = 109,
    /// <summary>
    /// A method of providing keying material to be used with TSIG that is encrypted under the public key in an accompanying KEY RR.[12]
    /// </summary>
    TKEY = 249,
    /// <summary>
    /// Can be used to authenticate dynamic updates as coming from an approved client, or to authenticate responses as coming from an approved recursive name server[13] similar to DNSSEC.
    /// </summary>
    TSIG = 250,
    /// <summary>
    /// Can be used for publishing mappings from hostnames to URIs.
    /// </summary>
    URI = 256,
    /// <summary>
    /// DNS Certification Authority Authorization, constraining acceptable CAs for a host/domain
    /// </summary>
    CAA = 257,
    /// <summary>
    /// Part of a deployment proposal for DNSSEC without a signed DNS root. See the IANA database and Weiler Spec for details. Uses the same format as the DS record.
    /// </summary>
    TA = 32768,
    /// <summary>
    /// For publishing DNSSEC trust anchors outside of the DNS delegation chain. Uses the same format as the DS record. RFC 5074 describes a way of using these records.
    /// </summary>
    DLV = 32769,
    /// <summary>
    /// Transfer entire zone file from the primary name server to secondary name servers.
    /// </summary>
    AXFR = 252,
    /// <summary>
    /// Requests a zone transfer of the given zone but only differences from a previous serial number. This request may be ignored and a full (AXFR) sent in response if the authoritative server is unable to fulfill the request due to configuration or lack of required deltas.
    /// </summary>
    IXFR = 251,
    /// <summary>
    /// This is a pseudo-record type needed to support EDNS.
    /// </summary>
    OPT = 41,
    /// <summary>
    /// A request for mailbox-related records (MB, MG or MR)
    /// </summary>
    MAILB = 253,
    /// <summary>
    /// A request for mail agent RRs (Obsolete - see MX)
    /// </summary>
    MALA = 254,
    /// <summary>
    /// A request for all records
    /// </summary>
    All = 252
}

public enum QuestionClass : ushort
{
    /// <summary>
    /// The internet
    /// </summary>
    IN = 1,
    /// <summary>
    /// The CSNET class
    /// </summary>
    CS = 2,
    /// <summary>
    /// The CHAOS class
    /// </summary>
    CH = 3,
    /// <summary>
    /// Hesiod [Dryer 87]
    /// </summary>
    HS = 4,
    /// <summary>
    /// A request for all records
    /// </summary>
    All = 255
}

public class DnsQuestion : DnsResourceRecordBase<QuestionType, QuestionClass> 
{ 
    /// <summary>
    /// Specifies if the query response should be sent in unicast.
    /// </summary>
    public bool QueryUnicast { get; init; }

    public static DnsQuestion Parse(byte[] data, ref int position)
    {
        var span = data.AsSpan();
        var qname = data.ReadDomainName(ref position);
        var qtype = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(position, 2));
        position += 2;
        var qclass = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(position, 2));
        position += 2;

        return new DnsQuestion
        {
            Name = qname,
            Type = (QuestionType)qtype,
            QueryUnicast = ((qclass >> 15) & 0b1) != 0,
            Class = (QuestionClass)(qclass & 0b111111111111111)
        };
    }
}
public class DnsResourceRecordBase<TType, TClass>
{
    /// <summary>
    /// A domain name to which this resource record pertains.
    /// </summary>
    public required string Name { get; init; }
    /// <summary>
    /// Specificies the type.
    /// </summary>
    public required TType Type { get; init; }
    /// <summary>
    /// Specifies the class.
    /// </summary>
    public required TClass Class { get; init; }
}