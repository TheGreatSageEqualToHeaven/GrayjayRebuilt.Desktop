// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// Noise.MessagePattern

namespace Noise;

public sealed class MessagePattern
{
    internal MessagePattern(params Token[] tokens)
    {
        Tokens = tokens;
    }

    internal MessagePattern(IEnumerable<Token> tokens)
    {
        Tokens = tokens;
    }

    public IEnumerable<Token> Tokens { get; }

    internal MessagePattern PrependPsk()
    {
        return new MessagePattern(Prepend(Tokens, Token.PSK));
    }

    internal MessagePattern AppendPsk()
    {
        return new MessagePattern(Append(Tokens, Token.PSK));
    }

    internal int Overhead(int dhLen, bool hasKey, bool isPsk)
    {
        var num = 0;
        using (var enumerator = Tokens.GetEnumerator())
        {
            while (enumerator.MoveNext())
                switch (enumerator.Current)
                {
                    case Token.E:
                        num += dhLen;
                        hasKey = hasKey || isPsk;
                        break;
                    case Token.S:
                        num += hasKey ? dhLen + 16 : dhLen;
                        break;
                    default:
                        hasKey = true;
                        break;
                }
        }

        if (!hasKey) return num;
        return num + 16;
    }

    private static IEnumerable<T> Prepend<T>(IEnumerable<T> source, T element)
    {
        yield return element;
        foreach (var item in source) yield return item;
    }

    private static IEnumerable<T> Append<T>(IEnumerable<T> source, T element)
    {
        foreach (var item in source) yield return item;
        yield return element;
    }
}