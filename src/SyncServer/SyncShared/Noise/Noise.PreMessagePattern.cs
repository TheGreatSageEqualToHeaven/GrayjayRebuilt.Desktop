// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// Noise.PreMessagePattern

namespace Noise;

public sealed class PreMessagePattern
{
    public static readonly PreMessagePattern E = new(default(Token));

    public static readonly PreMessagePattern S = new(Token.S);

    public static readonly PreMessagePattern ES = new(Token.E, Token.S);

    public static readonly PreMessagePattern Empty = new();

    private PreMessagePattern(params Token[] tokens)
    {
        Tokens = tokens;
    }

    public IEnumerable<Token> Tokens { get; }
}