// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// Noise.Protocol

using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace Noise;

public sealed class Protocol
{
    public const int MaxMessageLength = 65535;

    private const int MaxProtocolNameLength = 255;

    private static readonly int MinProtocolNameLength = "Noise_N_448_AESGCM_SHA256".Length;

    private static readonly Dictionary<string, HandshakePattern> patterns = typeof(HandshakePattern).GetTypeInfo()
        .DeclaredFields.Where(field => field.IsPublic && field.IsStatic && field.FieldType == typeof(HandshakePattern))
        .ToDictionary(field => field.Name, field => (HandshakePattern)field.GetValue(null));

    public Protocol(HandshakePattern handshakePattern, PatternModifiers modifiers = PatternModifiers.None)
        : this(handshakePattern, CipherFunction.ChaChaPoly, HashFunction.Sha256, modifiers)
    {
    }

    public Protocol(HandshakePattern handshakePattern, CipherFunction cipher, HashFunction hash,
        PatternModifiers modifiers = PatternModifiers.None)
    {
        Exceptions.ThrowIfNull(handshakePattern, "handshakePattern");
        Exceptions.ThrowIfNull(cipher, "cipher");
        Exceptions.ThrowIfNull(hash, "hash");
        HandshakePattern = handshakePattern;
        Cipher = cipher;
        Dh = DhFunction.Curve25519;
        Hash = hash;
        Modifiers = modifiers;
        Name = GetName();
    }

    public HandshakePattern HandshakePattern { get; }

    public CipherFunction Cipher { get; }

    public DhFunction Dh { get; }

    public HashFunction Hash { get; }

    public PatternModifiers Modifiers { get; }

    internal byte[] Name { get; }

    public HandshakeState Create(bool initiator, ReadOnlySpan<byte> prologue = default, byte[]? s = null,
        byte[]? rs = null, IEnumerable<byte[]>? psks = null)
    {
        if (psks == null) psks = Enumerable.Empty<byte[]>();
        if (Cipher == CipherFunction.AesGcm && Hash == HashFunction.Sha256)
            return new HandshakeState<Aes256Gcm, Curve25519, Sha256>(this, initiator, prologue, s, rs, psks);
        if (Cipher == CipherFunction.AesGcm && Hash == HashFunction.Sha512)
            return new HandshakeState<Aes256Gcm, Curve25519, Sha512>(this, initiator, prologue, s, rs, psks);
        if (Cipher == CipherFunction.AesGcm && Hash == HashFunction.Blake2s)
            return new HandshakeState<Aes256Gcm, Curve25519, Blake2s>(this, initiator, prologue, s, rs, psks);
        if (Cipher == CipherFunction.AesGcm && Hash == HashFunction.Blake2b)
            return new HandshakeState<Aes256Gcm, Curve25519, Blake2b>(this, initiator, prologue, s, rs, psks);
        if (Cipher == CipherFunction.ChaChaPoly && Hash == HashFunction.Sha256)
            return new HandshakeState<ChaCha20Poly1305, Curve25519, Sha256>(this, initiator, prologue, s, rs, psks);
        if (Cipher == CipherFunction.ChaChaPoly && Hash == HashFunction.Sha512)
            return new HandshakeState<ChaCha20Poly1305, Curve25519, Sha512>(this, initiator, prologue, s, rs, psks);
        if (Cipher == CipherFunction.ChaChaPoly && Hash == HashFunction.Blake2s)
            return new HandshakeState<ChaCha20Poly1305, Curve25519, Blake2s>(this, initiator, prologue, s, rs, psks);
        if (Cipher == CipherFunction.ChaChaPoly && Hash == HashFunction.Blake2b)
            return new HandshakeState<ChaCha20Poly1305, Curve25519, Blake2b>(this, initiator, prologue, s, rs, psks);
        throw new InvalidOperationException();
    }

    public HandshakeState Create(ProtocolConfig config)
    {
        Exceptions.ThrowIfNull(config, "config");
        return Create(config.Initiator, config.Prologue, config.LocalStatic, config.RemoteStatic, config.PreSharedKeys);
    }

    public static Protocol Parse(ReadOnlySpan<char> s)
    {
        if (s.Length < MinProtocolNameLength || s.Length > 255)
            throw new ArgumentException("Invalid Noise protocol name.", "s");
        var stringSplitter = new StringSplitter(s, '_');
        if (!stringSplitter.Next().SequenceEqual("Noise".AsSpan()))
            throw new ArgumentException("Invalid Noise protocol name.", "s");
        var readOnlySpan = stringSplitter.Next();
        var s2 = readOnlySpan.Length > 1 && char.IsUpper(readOnlySpan[1])
            ? readOnlySpan.Slice(0, 2)
            : readOnlySpan.Slice(0, 1);
        var handshakePattern = ParseHandshakePattern(s2);
        var modifiers = ParseModifiers(readOnlySpan.Slice(s2.Length));
        DhFunction.Parse(stringSplitter.Next());
        var cipher = CipherFunction.Parse(stringSplitter.Next());
        var hash = HashFunction.Parse(stringSplitter.Next());
        if (!stringSplitter.Next().IsEmpty) throw new ArgumentException("Invalid Noise protocol name.", "s");
        return new Protocol(handshakePattern, cipher, hash, modifiers);
    }

    [Conditional("DEBUG")]
    private static void ValidateProtocolName(ReadOnlySpan<char> s, Protocol protocol)
    {
        Encoding.ASCII.GetString(protocol.Name);
        new string(s.ToArray());
    }

    private static HandshakePattern ParseHandshakePattern(ReadOnlySpan<char> s)
    {
        foreach (var pattern in patterns)
            if (pattern.Key.AsSpan().SequenceEqual(s))
                return pattern.Value;
        throw new ArgumentException("Invalid Noise handshake pattern name.", "s");
    }

    private static PatternModifiers ParseModifiers(ReadOnlySpan<char> s)
    {
        var stringSplitter = new StringSplitter(s, '+');
        var patternModifiers = PatternModifiers.None;
        var s2 = stringSplitter.Next();
        while (!s2.IsEmpty)
        {
            var patternModifiers2 = ParseModifier(s2);
            if (patternModifiers2 <= patternModifiers)
                throw new ArgumentException("PSK pattern modifiers are required to be sorted alphabetically.");
            patternModifiers |= patternModifiers2;
            s2 = stringSplitter.Next();
        }

        return patternModifiers;
    }

    private static PatternModifiers ParseModifier(ReadOnlySpan<char> s)
    {
        return s switch
        {
            "psk0" => PatternModifiers.Psk0,
            "psk1" => PatternModifiers.Psk1,
            "psk2" => PatternModifiers.Psk2,
            "psk3" => PatternModifiers.Psk3,
            "fallback" => PatternModifiers.Fallback,
            _ => throw new ArgumentException("Unknown pattern modifier.", "s")
        };
    }

    private byte[] GetName()
    {
        var stringBuilder = new StringBuilder("Noise");
        stringBuilder.Append('_');
        stringBuilder.Append(HandshakePattern.Name);
        if (Modifiers != PatternModifiers.None)
        {
            var value = string.Empty;
            foreach (PatternModifiers value2 in Enum.GetValues(typeof(PatternModifiers)))
                if ((Modifiers & value2) != PatternModifiers.None)
                {
                    stringBuilder.Append(value);
                    stringBuilder.Append(value2.ToString().ToLowerInvariant());
                    value = "+";
                }
        }

        stringBuilder.Append('_');
        stringBuilder.Append(Dh);
        stringBuilder.Append('_');
        stringBuilder.Append(Cipher);
        stringBuilder.Append('_');
        stringBuilder.Append(Hash);
        return Encoding.ASCII.GetBytes(stringBuilder.ToString());
    }

    private ref struct StringSplitter
    {
        private ReadOnlySpan<char> s;

        private readonly char separator;

        public StringSplitter(ReadOnlySpan<char> s, char separator)
        {
            this.s = s;
            this.separator = separator;
        }

        public ReadOnlySpan<char> Next()
        {
            var num = s.IndexOf(separator);
            if (num > 0)
            {
                var result = s.Slice(0, num);
                s = s.Slice(num + 1);
                return result;
            }

            var result2 = s;
            s = ReadOnlySpan<char>.Empty;
            return result2;
        }
    }
}