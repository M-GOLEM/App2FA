using System.Security.Cryptography;

namespace Secure2FA.Core;

static class TotpEngine
{
    public static string Generate(string base32Secret, int digits = 6, int period = 30)
    {
        var key = Base32Decode(base32Secret);
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / period;
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);
        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counterBytes);
        int offset = hash[^1] & 0x0F;
        int code = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        int otp = code % (int)Math.Pow(10, digits);
        return otp.ToString(new string('0', digits));
    }

    public static int RemainingSeconds(int period = 30) => period - (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % period);

    public static byte[] Base32Decode(string input)
    {
        input = input.Trim().Replace(" ", "").Replace("-", "").ToUpperInvariant().TrimEnd('=');
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = "";
        foreach (char c in input)
        {
            int v = alphabet.IndexOf(c);
            if (v < 0) throw new FormatException("Invalid Base32");
            bits += Convert.ToString(v, 2).PadLeft(5, '0');
        }
        var bytes = new List<byte>();
        for (int i = 0; i + 8 <= bits.Length; i += 8) bytes.Add(Convert.ToByte(bits.Substring(i, 8), 2));
        return bytes.ToArray();
    }

    public static bool IsValidSecret(string s)
    {
        try { Base32Decode(s); return true; } catch { return false; }
    }
}
