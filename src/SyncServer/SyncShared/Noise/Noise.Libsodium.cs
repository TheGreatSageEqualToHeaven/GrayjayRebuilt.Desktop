// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// Noise.Libsodium

using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Noise;

internal static class Libsodium
{
    private const string Name = "libsodium";

    public const int crypto_scalarmult_curve25519_BYTES = 32;

    public const int crypto_scalarmult_curve25519_SCALARBYTES = 32;

    public static readonly bool IsAes256GcmAvailable;

    static Libsodium()
    {
        if (sodium_init() == -1) throw new CryptographicException("Failed to initialize libsodium.");
        IsAes256GcmAvailable = crypto_aead_aes256gcm_is_available() == 1;
    }

    [DllImport("libsodium", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sodium_init();

    [DllImport("libsodium", CallingConvention = CallingConvention.Cdecl)]
    private static extern int crypto_aead_aes256gcm_is_available();

    [DllImport("libsodium", CallingConvention = CallingConvention.Cdecl)]
    public static extern int crypto_aead_aes256gcm_encrypt(ref byte c, out long clen_p, ref byte m, long mlen,
        ref byte ad, long adlen, nint nsec, ref byte npub, ref byte k);

    [DllImport("libsodium", CallingConvention = CallingConvention.Cdecl)]
    public static extern int crypto_aead_aes256gcm_decrypt(ref byte m, out long mlen_p, nint nsec, ref byte c,
        long clen, ref byte ad, long adlen, ref byte npub, ref byte k);

    [DllImport("libsodium", CallingConvention = CallingConvention.Cdecl)]
    public static extern int crypto_aead_chacha20poly1305_ietf_encrypt(ref byte c, out long clen_p, ref byte m,
        long mlen, ref byte ad, long adlen, nint nsec, ref byte npub, ref byte k);

    [DllImport("libsodium", CallingConvention = CallingConvention.Cdecl)]
    public static extern int crypto_aead_chacha20poly1305_ietf_decrypt(ref byte m, out long mlen_p, nint nsec,
        ref byte c, long clen, ref byte ad, long adlen, ref byte npub, ref byte k);

    [DllImport("libsodium", CallingConvention = CallingConvention.Cdecl)]
    public static extern int crypto_scalarmult_curve25519_base(byte[] q, byte[] n);

    [DllImport("libsodium", CallingConvention = CallingConvention.Cdecl)]
    public static extern int crypto_scalarmult_curve25519(ref byte q, ref byte n, ref byte p);

    [DllImport("libsodium", CallingConvention = CallingConvention.Cdecl)]
    public static extern int crypto_hash_sha256_init(nint state);

    [DllImport("libsodium", CallingConvention = CallingConvention.Cdecl)]
    public static extern int crypto_hash_sha256_update(nint state, ref byte @in, ulong inlen);

    [DllImport("libsodium", CallingConvention = CallingConvention.Cdecl)]
    public static extern int crypto_hash_sha256_final(nint state, ref byte @out);

    [DllImport("libsodium", CallingConvention = CallingConvention.Cdecl)]
    public static extern int crypto_hash_sha512_init(nint state);

    [DllImport("libsodium", CallingConvention = CallingConvention.Cdecl)]
    public static extern int crypto_hash_sha512_update(nint state, ref byte @in, ulong inlen);

    [DllImport("libsodium", CallingConvention = CallingConvention.Cdecl)]
    public static extern int crypto_hash_sha512_final(nint state, ref byte @out);

    [DllImport("libsodium", CallingConvention = CallingConvention.Cdecl)]
    public static extern int crypto_generichash_blake2b_init(nint state, byte[]? key, nuint keylen, nuint outlen);

    [DllImport("libsodium", CallingConvention = CallingConvention.Cdecl)]
    public static extern int crypto_generichash_blake2b_update(nint state, ref byte @in, ulong inlen);

    [DllImport("libsodium", CallingConvention = CallingConvention.Cdecl)]
    public static extern int crypto_generichash_blake2b_final(nint state, ref byte @out, nuint outlen);
}