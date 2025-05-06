// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// Noise.HandshakeState<CipherType,DhType,HashType>

namespace Noise;

internal sealed class HandshakeState<CipherType, DhType, HashType> : HandshakeState, IDisposable
    where CipherType : Cipher, new() where DhType : Dh, new() where HashType : Hash, new()
{
    private readonly Queue<MessagePattern> messagePatterns = new();

    private readonly Queue<byte[]> psks = new();

    private readonly Role role;

    private Dh dh = new DhType();

    private bool disposed;

    private KeyPair? e;

    private Role initiator;

    private bool isOneWay;

    private bool isPsk;

    private Protocol? protocol;

    private byte[]? re;

    private byte[]? rs;

    private KeyPair? s;

    private SymmetricState<CipherType, DhType, HashType> state;

    private bool turnToWrite;

    public HandshakeState(Protocol protocol, bool initiator, ReadOnlySpan<byte> prologue, ReadOnlySpan<byte> s,
        ReadOnlySpan<byte> rs, IEnumerable<byte[]> psks)
    {
        if (!s.IsEmpty && s.Length != dh.DhLen) throw new ArgumentException("Invalid local static private key.", "s");
        if (!rs.IsEmpty && rs.Length != dh.DhLen)
            throw new ArgumentException("Invalid remote static public key.", "rs");
        if (s.IsEmpty && protocol.HandshakePattern.LocalStaticRequired(initiator))
            throw new ArgumentException("Local static private key required, but not provided.", "s");
        if (!s.IsEmpty && !protocol.HandshakePattern.LocalStaticRequired(initiator))
            throw new ArgumentException("Local static private key provided, but not required.", "s");
        if (rs.IsEmpty && protocol.HandshakePattern.RemoteStaticRequired(initiator))
            throw new ArgumentException("Remote static public key required, but not provided.", "rs");
        if (!rs.IsEmpty && !protocol.HandshakePattern.RemoteStaticRequired(initiator))
            throw new ArgumentException("Remote static public key provided, but not required.", "rs");
        if ((protocol.Modifiers & PatternModifiers.Fallback) != PatternModifiers.None)
            throw new ArgumentException("Fallback modifier can only be applied by calling the Fallback method.");
        state = new SymmetricState<CipherType, DhType, HashType>(protocol.Name);
        state.MixHash(prologue);
        this.protocol = protocol;
        role = !initiator ? Role.Bob : Role.Alice;
        this.initiator = Role.Alice;
        turnToWrite = initiator;
        this.s = s.IsEmpty ? null : dh.GenerateKeyPair(s);
        this.rs = rs.IsEmpty ? null : rs.ToArray();
        ProcessPreMessages(protocol.HandshakePattern);
        ProcessPreSharedKeys(protocol, psks);
        var patternModifiers = PatternModifiers.Psk0 | PatternModifiers.Psk1 | PatternModifiers.Psk2 |
                               PatternModifiers.Psk3;
        isPsk = (protocol.Modifiers & patternModifiers) != 0;
        isOneWay = messagePatterns.Count == 1;
    }

    public ReadOnlySpan<byte> RemoteStaticPublicKey
    {
        get
        {
            ThrowIfDisposed();
            return rs;
        }
    }

    public void Fallback(Protocol protocol, ProtocolConfig config)
    {
        ThrowIfDisposed();
        Exceptions.ThrowIfNull(protocol, "protocol");
        Exceptions.ThrowIfNull(config, "config");
        if (protocol.HandshakePattern != HandshakePattern.XX || protocol.Modifiers != PatternModifiers.Fallback)
            throw new ArgumentException("The only fallback pattern currently supported is XXfallback.");
        if (config.LocalStatic == null)
            throw new ArgumentException("Local static private key is required for the XXfallback pattern.");
        if (initiator == Role.Bob)
            throw new InvalidOperationException("Fallback cannot be applied to a Bob-initiated pattern.");
        if (messagePatterns.Count + 1 != this.protocol.HandshakePattern.Patterns.Count())
            throw new InvalidOperationException("Fallback can only be applied after the first handshake message.");
        this.protocol = null;
        initiator = Role.Bob;
        turnToWrite = role == Role.Bob;
        s = dh.GenerateKeyPair(config.LocalStatic);
        rs = null;
        isPsk = false;
        isOneWay = false;
        while (psks.Count > 0) Utilities.ZeroMemory(psks.Dequeue());
        state.Dispose();
        state = new SymmetricState<CipherType, DhType, HashType>(protocol.Name);
        state.MixHash(config.Prologue);
        if (role == Role.Alice)
            state.MixHash(e.PublicKey);
        else
            state.MixHash(re);
        messagePatterns.Clear();
        foreach (var item in protocol.HandshakePattern.Patterns.Skip(1)) messagePatterns.Enqueue(item);
    }

    public (int, byte[]?, Transport?) WriteMessage(ReadOnlySpan<byte> payload, Span<byte> messageBuffer)
    {
        ThrowIfDisposed();
        if (messagePatterns.Count == 0)
            throw new InvalidOperationException(
                "Cannot call WriteMessage after the handshake has already been completed.");
        var num = messagePatterns.Peek().Overhead(dh.DhLen, state.HasKey(), isPsk);
        var num2 = payload.Length + num;
        if (num2 > 65535)
            throw new ArgumentException($"Noise message must be less than or equal to {65535} bytes in length.");
        if (num2 > messageBuffer.Length)
            throw new ArgumentException("Message buffer does not have enough space to hold the ciphertext.");
        if (!turnToWrite)
            throw new InvalidOperationException("Unexpected call to WriteMessage (should be ReadMessage).");
        var messagePattern = messagePatterns.Dequeue();
        var length = messageBuffer.Length;
        using (var enumerator = messagePattern.Tokens.GetEnumerator())
        {
            while (enumerator.MoveNext())
                switch (enumerator.Current)
                {
                    case Token.E:
                        messageBuffer = WriteE(messageBuffer);
                        break;
                    case Token.S:
                        messageBuffer = WriteS(messageBuffer);
                        break;
                    case Token.EE:
                        DhAndMixKey(e, re);
                        break;
                    case Token.ES:
                        ProcessES();
                        break;
                    case Token.SE:
                        ProcessSE();
                        break;
                    case Token.SS:
                        DhAndMixKey(s, rs);
                        break;
                    case Token.PSK:
                        ProcessPSK();
                        break;
                }
        }

        var num3 = state.EncryptAndHash(payload, messageBuffer);
        _ = messageBuffer.Length;
        byte[] item = null;
        Transport item2 = null;
        if (messagePatterns.Count == 0) (item, item2) = Split();
        turnToWrite = false;
        return (num2, item, item2);
    }

    public (int, byte[]?, Transport?) ReadMessage(ReadOnlySpan<byte> message, Span<byte> payloadBuffer)
    {
        ThrowIfDisposed();
        if (messagePatterns.Count == 0)
            throw new InvalidOperationException(
                "Cannot call WriteMessage after the handshake has already been completed.");
        var num = messagePatterns.Peek().Overhead(dh.DhLen, state.HasKey(), isPsk);
        var num2 = message.Length - num;
        if (message.Length > 65535)
            throw new ArgumentException($"Noise message must be less than or equal to {65535} bytes in length.");
        if (message.Length < num)
            throw new ArgumentException($"Noise message must be greater than or equal to {num} bytes in length.");
        if (num2 > payloadBuffer.Length)
            throw new ArgumentException("Payload buffer does not have enough space to hold the plaintext.");
        if (turnToWrite)
            throw new InvalidOperationException("Unexpected call to ReadMessage (should be WriteMessage).");
        var messagePattern = messagePatterns.Dequeue();
        _ = message.Length;
        using (var enumerator = messagePattern.Tokens.GetEnumerator())
        {
            while (enumerator.MoveNext())
                switch (enumerator.Current)
                {
                    case Token.E:
                        message = ReadE(message);
                        break;
                    case Token.S:
                        message = ReadS(message);
                        break;
                    case Token.EE:
                        DhAndMixKey(e, re);
                        break;
                    case Token.ES:
                        ProcessES();
                        break;
                    case Token.SE:
                        ProcessSE();
                        break;
                    case Token.SS:
                        DhAndMixKey(s, rs);
                        break;
                    case Token.PSK:
                        ProcessPSK();
                        break;
                }
        }

        state.DecryptAndHash(message, payloadBuffer);
        byte[] item = null;
        Transport item2 = null;
        if (messagePatterns.Count == 0) (item, item2) = Split();
        turnToWrite = true;
        return (num2, item, item2);
    }

    public void Dispose()
    {
        if (!disposed)
        {
            Clear();
            disposed = true;
        }
    }

    private void ProcessPreMessages(HandshakePattern handshakePattern)
    {
        foreach (var token in handshakePattern.Initiator.Tokens)
            if (token == Token.S)
                state.MixHash(role == Role.Alice ? s.PublicKey : rs);
        foreach (var token2 in handshakePattern.Responder.Tokens)
            if (token2 == Token.S)
                state.MixHash(role == Role.Alice ? rs : s.PublicKey);
    }

    private void ProcessPreSharedKeys(Protocol protocol, IEnumerable<byte[]> psks)
    {
        var patterns = protocol.HandshakePattern.Patterns;
        var modifiers = protocol.Modifiers;
        var num = 0;
        using var enumerator = psks.GetEnumerator();
        foreach (var item in patterns)
        {
            var messagePattern = item;
            if (num == 0 && modifiers.HasFlag(PatternModifiers.Psk0))
            {
                messagePattern = messagePattern.PrependPsk();
                ProcessPreSharedKey(enumerator);
            }

            if (((uint)modifiers & (uint)(4 << num)) != 0)
            {
                messagePattern = messagePattern.AppendPsk();
                ProcessPreSharedKey(enumerator);
            }

            messagePatterns.Enqueue(messagePattern);
            num++;
        }

        if (enumerator.MoveNext())
            throw new ArgumentException("Number of pre-shared keys was greater than the number of PSK modifiers.");
    }

    private void ProcessPreSharedKey(IEnumerator<byte[]> enumerator)
    {
        if (!enumerator.MoveNext())
            throw new ArgumentException("Number of pre-shared keys was less than the number of PSK modifiers.");
        var current = enumerator.Current;
        if (current.Length != 32) throw new ArgumentException($"Pre-shared keys must be {32} bytes in length.");
        psks.Enqueue(current.AsSpan().ToArray());
    }

    internal void SetDh(Dh dh)
    {
        this.dh = dh;
    }

    private Span<byte> WriteE(Span<byte> buffer)
    {
        e = dh.GenerateKeyPair();
        e.PublicKey.CopyTo(buffer);
        state.MixHash(e.PublicKey);
        if (isPsk) state.MixKey(e.PublicKey);
        return buffer.Slice(e.PublicKey.Length);
    }

    private Span<byte> WriteS(Span<byte> buffer)
    {
        var start = state.EncryptAndHash(s.PublicKey, buffer);
        return buffer.Slice(start);
    }

    private ReadOnlySpan<byte> ReadE(ReadOnlySpan<byte> buffer)
    {
        re = buffer.Slice(0, dh.DhLen).ToArray();
        state.MixHash(re);
        if (isPsk) state.MixKey(re);
        return buffer.Slice(re.Length);
    }

    private ReadOnlySpan<byte> ReadS(ReadOnlySpan<byte> message)
    {
        var num = state.HasKey() ? dh.DhLen + 16 : dh.DhLen;
        var ciphertext = message.Slice(0, num);
        rs = new byte[dh.DhLen];
        state.DecryptAndHash(ciphertext, rs);
        return message.Slice(num);
    }

    private void ProcessES()
    {
        if (role == Role.Alice)
            DhAndMixKey(e, rs);
        else
            DhAndMixKey(s, re);
    }

    private void ProcessSE()
    {
        if (role == Role.Alice)
            DhAndMixKey(s, re);
        else
            DhAndMixKey(e, rs);
    }

    private void ProcessPSK()
    {
        var array = psks.Dequeue();
        state.MixKeyAndHash(array);
        Utilities.ZeroMemory(array);
    }

    private (byte[], Transport) Split()
    {
        var (c, cipherState) = state.Split();
        if (isOneWay)
        {
            cipherState.Dispose();
            cipherState = null;
        }

        var handshakeHash = state.GetHandshakeHash();
        var item = new Transport<CipherType>(role == initiator, c, cipherState);
        Clear();
        return (handshakeHash, item);
    }

    private void DhAndMixKey(KeyPair? keyPair, ReadOnlySpan<byte> publicKey)
    {
        Span<byte> span = stackalloc byte[dh.DhLen];
        dh.Dh(keyPair, publicKey, span);
        state.MixKey(span);
    }

    private void Clear()
    {
        state.Dispose();
        e?.Dispose();
        s?.Dispose();
        foreach (var psk in psks) Utilities.ZeroMemory(psk);
    }

    private void ThrowIfDisposed()
    {
        Exceptions.ThrowIfDisposed(disposed, "HandshakeState");
    }

    private enum Role
    {
        Alice,
        Bob
    }
}