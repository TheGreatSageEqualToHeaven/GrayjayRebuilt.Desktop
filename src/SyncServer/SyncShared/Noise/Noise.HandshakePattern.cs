// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// Noise.HandshakePattern

namespace Noise;

public sealed class HandshakePattern
{
    public static readonly HandshakePattern N = new("N", PreMessagePattern.Empty, PreMessagePattern.S,
        new MessagePattern(Token.E, Token.ES));

    public static readonly HandshakePattern K = new("K", PreMessagePattern.S, PreMessagePattern.S,
        new MessagePattern(Token.E, Token.ES, Token.SS));

    public static readonly HandshakePattern X = new("X", PreMessagePattern.Empty, PreMessagePattern.S,
        new MessagePattern(Token.E, Token.ES, Token.S, Token.SS));

    public static readonly HandshakePattern NN = new("NN", PreMessagePattern.Empty, PreMessagePattern.Empty,
        new MessagePattern(default(Token)), new MessagePattern(Token.E, Token.EE));

    public static readonly HandshakePattern NK = new("NK", PreMessagePattern.Empty, PreMessagePattern.S,
        new MessagePattern(Token.E, Token.ES), new MessagePattern(Token.E, Token.EE));

    public static readonly HandshakePattern NX = new("NX", PreMessagePattern.Empty, PreMessagePattern.Empty,
        new MessagePattern(default(Token)), new MessagePattern(Token.E, Token.EE, Token.S, Token.ES));

    public static readonly HandshakePattern XN = new("XN", PreMessagePattern.Empty, PreMessagePattern.Empty,
        new MessagePattern(default(Token)), new MessagePattern(Token.E, Token.EE),
        new MessagePattern(Token.S, Token.SE));

    public static readonly HandshakePattern XK = new("XK", PreMessagePattern.Empty, PreMessagePattern.S,
        new MessagePattern(Token.E, Token.ES), new MessagePattern(Token.E, Token.EE),
        new MessagePattern(Token.S, Token.SE));

    public static readonly HandshakePattern XX = new("XX", PreMessagePattern.Empty, PreMessagePattern.Empty,
        new MessagePattern(default(Token)), new MessagePattern(Token.E, Token.EE, Token.S, Token.ES),
        new MessagePattern(Token.S, Token.SE));

    public static readonly HandshakePattern KN = new("KN", PreMessagePattern.S, PreMessagePattern.Empty,
        new MessagePattern(default(Token)), new MessagePattern(Token.E, Token.EE, Token.SE));

    public static readonly HandshakePattern KK = new("KK", PreMessagePattern.S, PreMessagePattern.S,
        new MessagePattern(Token.E, Token.ES, Token.SS), new MessagePattern(Token.E, Token.EE, Token.SE));

    public static readonly HandshakePattern KX = new("KX", PreMessagePattern.S, PreMessagePattern.Empty,
        new MessagePattern(default(Token)), new MessagePattern(Token.E, Token.EE, Token.SE, Token.S, Token.ES));

    public static readonly HandshakePattern IN = new("IN", PreMessagePattern.Empty, PreMessagePattern.Empty,
        new MessagePattern(Token.E, Token.S), new MessagePattern(Token.E, Token.EE, Token.SE));

    public static readonly HandshakePattern IK = new("IK", PreMessagePattern.Empty, PreMessagePattern.S,
        new MessagePattern(Token.E, Token.ES, Token.S, Token.SS), new MessagePattern(Token.E, Token.EE, Token.SE));

    public static readonly HandshakePattern IX = new("IX", PreMessagePattern.Empty, PreMessagePattern.Empty,
        new MessagePattern(Token.E, Token.S), new MessagePattern(Token.E, Token.EE, Token.SE, Token.S, Token.ES));

    internal HandshakePattern(string name, PreMessagePattern initiator, PreMessagePattern responder,
        params MessagePattern[] patterns)
    {
        Name = name;
        Initiator = initiator;
        Responder = responder;
        Patterns = patterns;
    }

    public string Name { get; }

    public PreMessagePattern Initiator { get; }

    public PreMessagePattern Responder { get; }

    public IEnumerable<MessagePattern> Patterns { get; }

    internal bool LocalStaticRequired(bool initiator)
    {
        if ((initiator ? Initiator : Responder).Tokens.Contains(Token.S)) return true;
        var flag = initiator;
        foreach (var pattern in Patterns)
        {
            if (flag && pattern.Tokens.Contains(Token.S)) return true;
            flag = !flag;
        }

        return false;
    }

    internal bool RemoteStaticRequired(bool initiator)
    {
        return (initiator ? Responder : Initiator).Tokens.Contains(Token.S);
    }
}