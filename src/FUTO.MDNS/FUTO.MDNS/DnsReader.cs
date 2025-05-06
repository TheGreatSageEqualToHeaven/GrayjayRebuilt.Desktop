using System.Buffers.Binary;
using System.Text;

namespace FUTO.MDNS;

public class DnsReader
{
    private readonly byte[] _data;
    private int _position;
    private readonly int _length;
    private readonly int _endPosition;

    public DnsReader(byte[] data)
    {
        _data = data;
        _position = 0;
        _length = data.Length;
        _endPosition = data.Length;
    }

    public DnsReader(byte[] data, int position, int length)
    {
        _data = data;
        _position = position;
        _length = length;
        _endPosition = _position + _length;
    }

    public string ReadDomainName()
    {
        return _data.ReadDomainName(ref _position);
    }

    public double ReadDouble()
    {
        if (_position + sizeof(double) > _endPosition)
            throw new IndexOutOfRangeException();

        var result = BinaryPrimitives.ReadDoubleBigEndian(_data.AsSpan().Slice(_position, sizeof(double)));
        _position += sizeof(double);
        return result;
    }

    public short ReadInt16()
    {
        if (_position + sizeof(short) > _endPosition)
            throw new IndexOutOfRangeException();

        var result = BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan().Slice(_position, sizeof(short)));
        _position += sizeof(short);
        return result;
    }

    public int ReadInt32()
    {
        if (_position + sizeof(int) > _endPosition)
            throw new IndexOutOfRangeException();

        var result = BinaryPrimitives.ReadInt32BigEndian(_data.AsSpan().Slice(_position, sizeof(int)));
        _position += sizeof(int);
        return result;
    }

    public long ReadInt64()
    {
        if (_position + sizeof(long) > _endPosition)
            throw new IndexOutOfRangeException();

        var result = BinaryPrimitives.ReadInt64BigEndian(_data.AsSpan().Slice(_position, sizeof(long)));
        _position += sizeof(long);
        return result;
    }

    public float ReadSingle()
    {
        if (_position + sizeof(float) > _endPosition)
            throw new IndexOutOfRangeException();

        var result = BinaryPrimitives.ReadSingleBigEndian(_data.AsSpan().Slice(_position, sizeof(float)));
        _position += sizeof(float);
        return result;
    }

    public byte ReadByte()
    {
        if (_position + sizeof(byte) > _endPosition)
            throw new IndexOutOfRangeException();

        var result = _data[_position];
        _position += sizeof(byte);
        return result;
    }

    public byte[] ReadBytes(int length)
    {
        if (_position + length > _endPosition)
            throw new IndexOutOfRangeException();

        var data = new byte[length];
        Array.Copy(_data, _position, data, 0, length);
        _position += length;
        return data;
    }

    public ushort ReadUInt16()
    {
        if (_position + sizeof(ushort) > _endPosition)
            throw new IndexOutOfRangeException();

        var result = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan().Slice(_position, sizeof(ushort)));
        _position += sizeof(ushort);
        return result;
    }

    public uint ReadUInt32()
    {
        if (_position + sizeof(uint) > _endPosition)
            throw new IndexOutOfRangeException();

        var result = BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan().Slice(_position, sizeof(uint)));
        _position += sizeof(uint);
        return result;
    }

    public ulong ReadUInt64()
    {
        if (_position + sizeof(ulong) > _endPosition)
            throw new IndexOutOfRangeException();

        var result = BinaryPrimitives.ReadUInt64BigEndian(_data.AsSpan().Slice(_position, sizeof(ulong)));
        _position += sizeof(ulong);
        return result;
    }

    public string ReadString()
    {
        var length = _data[_position++];
        if (_position + length > _endPosition)
            throw new IndexOutOfRangeException();

        var result = Encoding.UTF8.GetString(_data, _position, length);
        _position += length;
        return result;
    }

    public class PTRRecord
    {
        public required string DomainName { get; init; }
    }

    public PTRRecord ReadPTRRecord()
    {
        return new PTRRecord
        {
            DomainName = ReadDomainName()
        };
    }

    public class ARecord
    {
        public required System.Net.IPAddress Address { get; init; }
    }

    public ARecord ReadARecord()
    {
        if (_position + 4 > _endPosition)
            throw new IndexOutOfRangeException();

        var address = new byte[4];
        Buffer.BlockCopy(_data, _position, address, 0, 4);
        _position += 4;

        return new ARecord
        {
            Address = new System.Net.IPAddress(address)
        };
    }


    public class AAAARecord
    {
        public required System.Net.IPAddress Address { get; init; }
    }

    public AAAARecord ReadAAAARecord()
    {
        if (_position + 16 > _endPosition)
            throw new IndexOutOfRangeException();

        var address = new byte[16];
        Buffer.BlockCopy(_data, _position, address, 0, 16);
        _position += 16;

        return new AAAARecord
        {
            Address = new System.Net.IPAddress(address)
        };
    }

    public class MXRecord
    {
        public ushort Preference { get; init; }
        public required string Exchange { get; init; }
    }

    public MXRecord ReadMXRecord()
    {
        var preference = ReadUInt16();
        var exchange = ReadDomainName();

        return new MXRecord
        {
            Preference = preference,
            Exchange = exchange
        };
    }

    public class CNAMERecord
    {
        public required string CName { get; init; }
    }

    public CNAMERecord ReadCNAMERecord()
    {
        var cname = ReadDomainName();

        return new CNAMERecord
        {
            CName = cname
        };
    }

    public class TXTRecord
    {
        public required List<string> Texts { get; init; }
    }

    public TXTRecord ReadTXTRecord()
    {
        var texts = new List<string>();
        while (_position < _endPosition)
        {
            var textLength = _data[_position++];
            if (_position + textLength > _endPosition)
                throw new IndexOutOfRangeException();

            var text = Encoding.UTF8.GetString(_data, _position, textLength);
            texts.Add(text);
            _position += textLength;
        }

        return new TXTRecord
        {
            Texts = texts
        };
    }

    public class SOARecord
    {
        public required string PrimaryNameServer { get; init; }
        public required string ResponsibleAuthorityMailbox { get; init; }
        public required long SerialNumber { get; init; }
        public required int RefreshInterval { get; init; }
        public required int RetryInterval { get; init; }
        public required int ExpiryLimit { get; init; }
        public required int MinimumTTL { get; init; }
    }

    public SOARecord ReadSOARecord()
    {
        var primaryNameServer = ReadDomainName();
        var responsibleAuthorityMailbox = ReadDomainName();
        var serialNumber = ReadInt32();
        var refreshInterval = ReadInt32();
        var retryInterval = ReadInt32();
        var expiryLimit = ReadInt32();
        var minimumTTL = ReadInt32();

        return new SOARecord
        {
            PrimaryNameServer = primaryNameServer,
            ResponsibleAuthorityMailbox = responsibleAuthorityMailbox,
            SerialNumber = serialNumber,
            RefreshInterval = refreshInterval,
            RetryInterval = retryInterval,
            ExpiryLimit = expiryLimit,
            MinimumTTL = minimumTTL
        };
    }

    public class SRVRecord
    {
        public ushort Priority { get; init; }
        public ushort Weight { get; init; }
        public ushort Port { get; init; }
        public required string Target { get; init; }
    }

    public SRVRecord ReadSRVRecord()
    {
        var priority = ReadUInt16();
        var weight = ReadUInt16();
        var port = ReadUInt16();
        var target = ReadDomainName();

        return new SRVRecord
        {
            Priority = priority,
            Weight = weight,
            Port = port,
            Target = target
        };
    }

    public class NSRecord
    {
        public required string NameServer { get; init; }
    }

    public NSRecord ReadNSRecord()
    {
        var nameServer = ReadDomainName();

        return new NSRecord
        {
            NameServer = nameServer
        };
    }

    public class CAARecord
    {
        public byte Flags { get; init; }
        public required string Tag { get; init; }
        public required string Value { get; init; }
    }

    public CAARecord ReadCAARecord()
    {
        var length = ReadUInt16();
        if (_position + length > _endPosition)
            throw new IndexOutOfRangeException();

        var flags = _data[_position++];
        var tagLength = _data[_position++];
        var tag = System.Text.Encoding.ASCII.GetString(_data, _position, tagLength);
        _position += tagLength;
        var valueLength = length - 1 - 1 - tagLength; // Total length - flags - tag length byte - tag
        var value = System.Text.Encoding.ASCII.GetString(_data, _position, valueLength);
        _position += valueLength;

        return new CAARecord
        {
            Flags = flags,
            Tag = tag,
            Value = value
        };
    }

    public class HINFORecord
    {
        public required string CPU { get; init; }
        public required string OS { get; init; }
    }

    public HINFORecord ReadHINFORecord()
    {
        var cpuLength = _data[_position++];
        var cpu = System.Text.Encoding.ASCII.GetString(_data, _position, cpuLength);
        _position += cpuLength;

        var osLength = _data[_position++];
        var os = System.Text.Encoding.ASCII.GetString(_data, _position, osLength);
        _position += osLength;

        return new HINFORecord
        {
            CPU = cpu,
            OS = os
        };
    }

    public class RPRecord
    {
        public required string Mailbox { get; init; }
        public required string TxtDomainName { get; init; }
    }

    public RPRecord ReadRPRecord()
    {
        var mailbox = ReadDomainName();
        var txtDomainName = ReadDomainName();

        return new RPRecord
        {
            Mailbox = mailbox,
            TxtDomainName = txtDomainName
        };
    }

    public class AFSDBRecord
    {
        public ushort Subtype { get; init; }
        public required string Hostname { get; init; }
    }

    public AFSDBRecord ReadAFSDBRecord()
    {
        var subtype = ReadUInt16();
        var hostname = ReadDomainName();

        return new AFSDBRecord
        {
            Subtype = subtype,
            Hostname = hostname
        };
    }

    public class LOCRecord
    {
        public byte Version { get; init; }
        public double Size { get; init; }
        public double HorizontalPrecision { get; init; }
        public double VerticalPrecision { get; init; }
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public double Altitude { get; init; }

        public static double DecodeSizeOrPrecision(byte coded)
        {
            int baseValue = (coded >> 4) & 0x0F;
            int exponent = coded & 0x0F;
            return baseValue * Math.Pow(10, exponent);
        }

        public static double DecodeLatitudeOrLongitude(int coded)
        {
            // Latitude and Longitude are stored as 32-bit integers where 2^31 represents the equator for latitude or the prime meridian for longitude.
            double arcSeconds = coded / 1E3; // Convert thousandths of a second of arc to seconds of arc
            return arcSeconds / 3600; // Convert seconds of arc to degrees
        }

        public static double DecodeAltitude(int coded)
        {
            // Altitude is stored as a 32-bit integer, in centimeters, from a base of 100,000 meters below the WGS 84 reference spheroid.
            return (coded / 100.0) - 100000.0; // Convert centimeters to meters and adjust for base
        }
    }

    public LOCRecord ReadLOCRecord()
    {
        var version = _data[_position++];
        var size = LOCRecord.DecodeSizeOrPrecision(_data[_position++]);
        var horizPre = LOCRecord.DecodeSizeOrPrecision(_data[_position++]);
        var vertPre = LOCRecord.DecodeSizeOrPrecision(_data[_position++]);
        var latitudeCoded = ReadInt32();
        var longitudeCoded = ReadInt32();
        var altitudeCoded = ReadInt32();

        var latitude = LOCRecord.DecodeLatitudeOrLongitude(latitudeCoded);
        var longitude = LOCRecord.DecodeLatitudeOrLongitude(longitudeCoded);
        var altitude = LOCRecord.DecodeAltitude(altitudeCoded);

        return new LOCRecord
        {
            Version = version,
            Size = size,
            HorizontalPrecision = horizPre,
            VerticalPrecision = vertPre,
            Latitude = latitude,
            Longitude = longitude,
            Altitude = altitude
        };
    }

    public class NAPTRRecord
    {
        public ushort Order { get; init; }
        public ushort Preference { get; init; }
        public required string Flags { get; init; }
        public required string Services { get; init; }
        public required string Regexp { get; init; }
        public required string Replacement { get; init; }
    }

    public NAPTRRecord ReadNAPTRRecord()
    {
        var order = ReadUInt16();
        var preference = ReadUInt16();
        var flags = ReadString();
        var services = ReadString();
        var regexp = ReadString();
        var replacement = ReadDomainName();

        return new NAPTRRecord
        {
            Order = order,
            Preference = preference,
            Flags = flags,
            Services = services,
            Regexp = regexp,
            Replacement = replacement
        };
    }

    public class KXRecord
    {
        public ushort Preference { get; init; }
        public required string Exchanger { get; init; }
    }

    public KXRecord ReadKXRecord()
    {
        var preference = ReadUInt16();
        var exchanger = ReadDomainName();

        return new KXRecord
        {
            Preference = preference,
            Exchanger = exchanger
        };
    }

    public class CERTRecord
    {
        public ushort Type { get; init; }
        public ushort KeyTag { get; init; }
        public byte Algorithm { get; init; }
        public byte[] Certificate { get; init; }

        public CERTRecord(ushort type, ushort keyTag, byte algorithm, byte[] certificate)
        {
            Type = type;
            KeyTag = keyTag;
            Algorithm = algorithm;
            Certificate = certificate;
        }
    }

    public CERTRecord ReadCERTRecord()
    {
        var type = ReadUInt16();
        var keyTag = ReadUInt16();
        var algorithm = _data[_position++];
        var certificateLength = ReadUInt16() - 5; // Type(2) + KeyTag(2) + Algorithm(1)
        var certificate = new byte[certificateLength];
        Buffer.BlockCopy(_data, _position, certificate, 0, certificateLength);
        _position += certificateLength;

        return new CERTRecord(type, keyTag, algorithm, certificate);
    }

    public class DNAMERecord
    {
        public required string Target { get; init; }
    }

    public DNAMERecord ReadDNAMERecord()
    {
        var target = ReadDomainName();
        return new DNAMERecord
        {
            Target = target
        };
    }

    public class DSRecord
    {
        public ushort KeyTag { get; init; }
        public byte Algorithm { get; init; }
        public byte DigestType { get; init; }
        public byte[] Digest { get; init; }

        public DSRecord(ushort keyTag, byte algorithm, byte digestType, byte[] digest)
        {
            KeyTag = keyTag;
            Algorithm = algorithm;
            DigestType = digestType;
            Digest = digest;
        }
    }

    public DSRecord ReadDSRecord()
    {
        var keyTag = ReadUInt16();
        var algorithm = _data[_position++];
        var digestType = _data[_position++];
        var digestLength = ReadUInt16() - 4; // KeyTag(2) + Algorithm(1) + DigestType(1)
        var digest = new byte[digestLength];
        Buffer.BlockCopy(_data, _position, digest, 0, digestLength);
        _position += digestLength;

        return new DSRecord(keyTag, algorithm, digestType, digest);
    }

    public class SSHFPRecord
    {
        public byte Algorithm { get; init; }
        public byte FingerprintType { get; init; }
        public byte[] Fingerprint { get; init; }

        public SSHFPRecord(byte algorithm, byte fingerprintType, byte[] fingerprint)
        {
            Algorithm = algorithm;
            FingerprintType = fingerprintType;
            Fingerprint = fingerprint;
        }
    }

    public SSHFPRecord ReadSSHFPRecord()
    {
        var algorithm = _data[_position++];
        var fingerprintType = _data[_position++];
        var fingerprintLength = ReadUInt16() - 2; // Algorithm(1) + FingerprintType(1)
        var fingerprint = new byte[fingerprintLength];
        Buffer.BlockCopy(_data, _position, fingerprint, 0, fingerprintLength);
        _position += fingerprintLength;

        return new SSHFPRecord(algorithm, fingerprintType, fingerprint);
    }

    public class TLSARecord
    {
        public byte Usage { get; init; }
        public byte Selector { get; init; }
        public byte MatchingType { get; init; }
        public byte[] CertificateAssociationData { get; init; }

        public TLSARecord(byte usage, byte selector, byte matchingType, byte[] certificateAssociationData)
        {
            Usage = usage;
            Selector = selector;
            MatchingType = matchingType;
            CertificateAssociationData = certificateAssociationData;
        }
    }

    public TLSARecord ReadTLSARecord()
    {
        var usage = _data[_position++];
        var selector = _data[_position++];
        var matchingType = _data[_position++];
        var dataLength = ReadUInt16() - 3; // Usage(1) + Selector(1) + MatchingType(1)
        var certificateAssociationData = new byte[dataLength];
        Buffer.BlockCopy(_data, _position, certificateAssociationData, 0, dataLength);
        _position += dataLength;

        return new TLSARecord(usage, selector, matchingType, certificateAssociationData);
    }

    public class SMIMEARecord
    {
        public byte Usage { get; init; }
        public byte Selector { get; init; }
        public byte MatchingType { get; init; }
        public byte[] CertificateAssociationData { get; init; }

        public SMIMEARecord(byte usage, byte selector, byte matchingType, byte[] certificateAssociationData)
        {
            Usage = usage;
            Selector = selector;
            MatchingType = matchingType;
            CertificateAssociationData = certificateAssociationData;
        }
    }

    public SMIMEARecord ReadSMIMEARecord()
    {
        var usage = _data[_position++];
        var selector = _data[_position++];
        var matchingType = _data[_position++];
        var dataLength = ReadUInt16() - 3; // Usage(1) + Selector(1) + MatchingType(1)
        var certificateAssociationData = new byte[dataLength];
        Buffer.BlockCopy(_data, _position, certificateAssociationData, 0, dataLength);
        _position += dataLength;

        return new SMIMEARecord(usage, selector, matchingType, certificateAssociationData);
    }

    public class URIRecord
    {
        public ushort Priority { get; init; }
        public ushort Weight { get; init; }
        public string Target { get; init; }

        public URIRecord(ushort priority, ushort weight, string target)
        {
            Priority = priority;
            Weight = weight;
            Target = target;
        }
    }

    public URIRecord ReadURIRecord()
    {
        var priority = ReadUInt16();
        var weight = ReadUInt16();
        var length = ReadUInt16();
        if (length > _length)
            throw new IndexOutOfRangeException();
        var target = System.Text.Encoding.ASCII.GetString(_data, _position, length);
        _position += length;

        return new URIRecord(priority, weight, target);
    }

    public class RRSIGRecord
    {
        public ushort TypeCovered { get; init; }
        public byte Algorithm { get; init; }
        public byte Labels { get; init; }
        public uint OriginalTTL { get; init; }
        public uint SignatureExpiration { get; init; }
        public uint SignatureInception { get; init; }
        public ushort KeyTag { get; init; }
        public string SignersName { get; init; }
        public byte[] Signature { get; init; }

        public RRSIGRecord(ushort typeCovered, byte algorithm, byte labels, uint originalTTL, uint signatureExpiration, uint signatureInception, ushort keyTag, string signersName, byte[] signature)
        {
            TypeCovered = typeCovered;
            Algorithm = algorithm;
            Labels = labels;
            OriginalTTL = originalTTL;
            SignatureExpiration = signatureExpiration;
            SignatureInception = signatureInception;
            KeyTag = keyTag;
            SignersName = signersName;
            Signature = signature;
        }
    }

    public RRSIGRecord ReadRRSIGRecord()
    {
        var typeCovered = ReadUInt16();
        var algorithm = _data[_position++];
        var labels = _data[_position++];
        var originalTTL = ReadUInt32();
        var signatureExpiration = ReadUInt32();
        var signatureInception = ReadUInt32();
        var keyTag = ReadUInt16();
        var signersName = ReadDomainName();
        var signatureLength = ReadUInt16(); // Adjust based on actual length calculation
        var signature = new byte[signatureLength];
        Buffer.BlockCopy(_data, _position, signature, 0, signatureLength);
        _position += signatureLength;

        return new RRSIGRecord(typeCovered, algorithm, labels, originalTTL, signatureExpiration, signatureInception, keyTag, signersName, signature);
    }

    public class NSECRecord
    {
        public required string OwnerName { get; init; }
        public required List<(byte WindowBlock, byte[] Bitmap)> TypeBitMaps { get; init; }
    }

    public NSECRecord ReadNSECRecord()
    {
        var ownerName = ReadDomainName();
        var typeBitMaps = new List<(byte windowBlock, byte[] bitmap)>();

        while (_position < _endPosition)
        {
            if (_position + 2 > _endPosition)
                throw new IndexOutOfRangeException("Not enough data for window number and bitmap length.");

            var windowBlock = ReadByte();
            var bitmapLength = ReadByte();
            var bitmap = ReadBytes(bitmapLength);
            typeBitMaps.Add((windowBlock, bitmap));
        }

        return new NSECRecord
        {
            OwnerName = ownerName,
            TypeBitMaps = typeBitMaps
        };
    }

    public class NSEC3Record
    {
        public byte HashAlgorithm { get; init; }
        public byte Flags { get; init; }
        public ushort Iterations { get; init; }
        public byte[] Salt { get; init; }
        public byte[] NextHashedOwnerName { get; init; }
        public List<ushort> TypeBitMaps { get; init; }

        public NSEC3Record(byte hashAlgorithm, byte flags, ushort iterations, byte[] salt, byte[] nextHashedOwnerName, List<ushort> typeBitMaps)
        {
            HashAlgorithm = hashAlgorithm;
            Flags = flags;
            Iterations = iterations;
            Salt = salt;
            NextHashedOwnerName = nextHashedOwnerName;
            TypeBitMaps = typeBitMaps;
        }
    }

    public NSEC3Record ReadNSEC3Record()
    {
        var hashAlgorithm = _data[_position++];
        var flags = _data[_position++];
        var iterations = ReadUInt16();
        var saltLength = _data[_position++];
        var salt = new byte[saltLength];
        Buffer.BlockCopy(_data, _position, salt, 0, saltLength);
        _position += saltLength;

        var hashLength = _data[_position++];
        var nextHashedOwnerName = new byte[hashLength];
        Buffer.BlockCopy(_data, _position, nextHashedOwnerName, 0, hashLength);
        _position += hashLength;

        var bitMapLength = ReadUInt16();
        if (_position + bitMapLength > _length)
            throw new IndexOutOfRangeException();

        var typeBitMaps = new List<ushort>();
        int endPosition = _position + bitMapLength;
        while (_position < endPosition)
        {
            var type = ReadUInt16();
            typeBitMaps.Add(type);
        }

        return new NSEC3Record(hashAlgorithm, flags, iterations, salt, nextHashedOwnerName, typeBitMaps);
    }

    public class NSEC3PARAMRecord
    {
        public byte HashAlgorithm { get; init; }
        public byte Flags { get; init; }
        public ushort Iterations { get; init; }
        public byte[] Salt { get; init; }

        public NSEC3PARAMRecord(byte hashAlgorithm, byte flags, ushort iterations, byte[] salt)
        {
            HashAlgorithm = hashAlgorithm;
            Flags = flags;
            Iterations = iterations;
            Salt = salt;
        }
    }

    public NSEC3PARAMRecord ReadNSEC3PARAMRecord()
    {
        var hashAlgorithm = _data[_position++];
        var flags = _data[_position++];
        var iterations = ReadUInt16();
        var saltLength = _data[_position++];
        var salt = new byte[saltLength];
        Buffer.BlockCopy(_data, _position, salt, 0, saltLength);
        _position += saltLength;

        return new NSEC3PARAMRecord(hashAlgorithm, flags, iterations, salt);
    }

    public class SPFRecord
    {
        public required List<string> Texts { get; init; }
    }

    public SPFRecord ReadSPFRecord()
    {
        var length = ReadUInt16();
        if (_position + length > _length)
            throw new IndexOutOfRangeException();

        var texts = new List<string>();
        int endPosition = _position + length;
        while (_position < endPosition)
        {
            var textLength = _data[_position++];
            var text = System.Text.Encoding.ASCII.GetString(_data, _position, textLength);
            texts.Add(text);
            _position += textLength;
        }

        return new SPFRecord
        {
            Texts = texts
        };
    }

    public class TKEYRecord
    {
        public required string Algorithm { get; init; }
        public uint Inception { get; init; }
        public uint Expiration { get; init; }
        public ushort Mode { get; init; }
        public ushort Error { get; init; }
        public required byte[] KeyData { get; init; }
        public required byte[] OtherData { get; init; }
    }

    public TKEYRecord ReadTKEYRecord()
    {
        var algorithm = ReadDomainName();
        var inception = ReadUInt32();
        var expiration = ReadUInt32();
        var mode = ReadUInt16();
        var error = ReadUInt16();
        var keySize = ReadUInt16();
        var keyData = new byte[keySize];
        Buffer.BlockCopy(_data, _position, keyData, 0, keySize);
        _position += keySize;

        var otherSize = ReadUInt16();
        var otherData = new byte[otherSize];
        Buffer.BlockCopy(_data, _position, otherData, 0, otherSize);
        _position += otherSize;

        return new TKEYRecord
        {
            Algorithm = algorithm,
            Inception = inception,
            Expiration = expiration,
            Mode = mode,
            Error = error,
            KeyData = keyData,
            OtherData = otherData
        };
    }

    public class TSIGRecord
    {
        public required string AlgorithmName { get; init; }
        public uint TimeSigned { get; init; }
        public ushort Fudge { get; init; }
        public required byte[] MAC { get; init; }
        public ushort OriginalID { get; init; }
        public ushort Error { get; init; }
        public required byte[] OtherData { get; init; }
    }

    public TSIGRecord ReadTSIGRecord()
    {
        var algorithmName = ReadDomainName();
        var timeSigned = ReadUInt32();
        var fudge = ReadUInt16();
        var macSize = ReadUInt16();
        var mac = new byte[macSize];
        Buffer.BlockCopy(_data, _position, mac, 0, macSize);
        _position += macSize;

        var originalID = ReadUInt16();
        var error = ReadUInt16();
        var otherSize = ReadUInt16();
        var otherData = new byte[otherSize];
        Buffer.BlockCopy(_data, _position, otherData, 0, otherSize);
        _position += otherSize;

        return new TSIGRecord
        {
            AlgorithmName = algorithmName,
            TimeSigned = timeSigned,
            Fudge = fudge,
            MAC = mac,
            OriginalID = originalID,
            Error = error,
            OtherData = otherData
        };
    }

    public class OPTRecordOption
    {
        public ushort Code { get; init; }
        public required byte[] Data { get; init; }
    }

    public class OPTRecord
    {
        public required List<OPTRecordOption> Options { get; init; }
    }

    public OPTRecord ReadOPTRecord()
    {
        var options = new List<OPTRecordOption>();
        while (_position < _endPosition)
        {
            var optionCode = ReadUInt16();
            var optionLength = ReadUInt16();
            var optionData = ReadBytes(optionLength);
            options.Add(new OPTRecordOption()
            {
                Code = optionCode,
                Data = optionData
            });
        }

        return new OPTRecord
        {
            Options = options
        };
    }

    //TODO: Implement for all other types
}