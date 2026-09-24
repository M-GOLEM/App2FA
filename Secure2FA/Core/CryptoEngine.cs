using System.Security.Cryptography;
using System.Text;

namespace Secure2FA.Core;

static class CryptoEngine
{
    private static readonly byte[] Pepper = Convert.FromBase64String("c2VjdXJlX3ZhdWx0X3BlcHBlcl8yMDI2X0hhY2tlcl9FZG1=");
    private const int Iterations = 300000;
    private const int SaltSize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public static string PinHash { get; } = ComputeHash("002827");

    private static string ComputeHash(string pin)
    {
        using var sha = SHA256.Create();
        var h = sha.ComputeHash(Encoding.UTF8.GetBytes(pin + Encoding.UTF8.GetString(Pepper)));
        return Convert.ToHexString(h);
    }

    public static bool VerifyPin(string pin)
    {
        using var sha = SHA256.Create();
        var h = sha.ComputeHash(Encoding.UTF8.GetBytes(pin + Encoding.UTF8.GetString(Pepper)));
        return Convert.ToHexString(h) == PinHash;
    }

    public static byte[] DeriveKey(string pin, byte[] salt)
    {
        var pwd = Encoding.UTF8.GetBytes(pin).Concat(Pepper).ToArray();
        using var kdf = new Rfc2898DeriveBytes(pwd, salt, Iterations, HashAlgorithmName.SHA512);
        var key = kdf.GetBytes(32);
        CryptographicOperations.ZeroMemory(pwd);
        return key;
    }

    public static byte[] Encrypt(string pin, byte[] plain)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var key = DeriveKey(pin, salt);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];
        using (var aes = new AesGcm(key, TagSize))
            aes.Encrypt(nonce, plain, cipher, tag);
        CryptographicOperations.ZeroMemory(key);
        var outBuf = new byte[SaltSize + NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(salt, 0, outBuf, 0, SaltSize);
        Buffer.BlockCopy(nonce, 0, outBuf, SaltSize, NonceSize);
        Buffer.BlockCopy(tag, 0, outBuf, SaltSize + NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, outBuf, SaltSize + NonceSize + TagSize, cipher.Length);
        var obf = XorObfuscate(outBuf);
        return obf;
    }

    public static byte[] Decrypt(string pin, byte[] data)
    {
        var deob = XorObfuscate(data);
        if (deob.Length < SaltSize + NonceSize + TagSize) throw new CryptographicException("Invalid vault");
        var salt = new byte[SaltSize];
        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        Buffer.BlockCopy(deob, 0, salt, 0, SaltSize);
        Buffer.BlockCopy(deob, SaltSize, nonce, 0, NonceSize);
        Buffer.BlockCopy(deob, SaltSize + NonceSize, tag, 0, TagSize);
        var cipher = new byte[deob.Length - SaltSize - NonceSize - TagSize];
        Buffer.BlockCopy(deob, SaltSize + NonceSize + TagSize, cipher, 0, cipher.Length);
        var key = DeriveKey(pin, salt);
        var plain = new byte[cipher.Length];
        using (var aes = new AesGcm(key, TagSize))
            aes.Decrypt(nonce, cipher, tag, plain);
        CryptographicOperations.ZeroMemory(key);
        return plain;
    }

    private static byte[] XorObfuscate(byte[] d)
    {
        byte[] k = Encoding.UTF8.GetBytes("H4CK3R_X0R_0B5_2026");
        var o = new byte[d.Length];
        for (int i = 0; i < d.Length; i++) o[i] = (byte)(d[i] ^ k[i % k.Length] ^ 0x5A);
        return o;
    }

    public static bool IsDebugged()
    {
        if (System.Diagnostics.Debugger.IsAttached) return true;
        if (Environment.GetEnvironmentVariable("COR_ENABLE_PROFILING") == "1") return true;
        return false;
    }
}
