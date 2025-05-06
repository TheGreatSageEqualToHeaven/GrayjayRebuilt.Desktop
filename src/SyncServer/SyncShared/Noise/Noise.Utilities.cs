// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// Noise.Utilities

using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace Noise;

internal static class Utilities
{
    private static readonly RandomNumberGenerator random = RandomNumberGenerator.Create();

    public static nint Align(nint ptr, int alignment)
    {
        var num = (ulong)alignment - 1uL;
        return (nint)((ptr + (long)num) & (long)~num);
    }

    public static byte[] GetRandomBytes(int n)
    {
        var array = new byte[n];
        random.GetBytes(array);
        return array;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static void ZeroMemory(Span<byte> buffer)
    {
        buffer.Clear();
    }
}