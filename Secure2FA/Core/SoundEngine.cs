using System.Media;
using System.Runtime.InteropServices;

namespace Secure2FA.Core;

static class SoundEngine
{
    [DllImport("kernel32.dll")] static extern bool Beep(int freq, int duration);

    static void TryBeep(int f, int d)
    {
        try { Beep(f, d); } catch { try { Console.Beep(f, d); } catch { } }
    }

    public static void KeyClick() => Task.Run(() => TryBeep(1800, 35));
    public static void Error() => Task.Run(() => { TryBeep(220, 180); Thread.Sleep(90); TryBeep(180, 250); });
    public static void Success() => Task.Run(() => { TryBeep(900, 90); Thread.Sleep(70); TryBeep(1300, 90); Thread.Sleep(70); TryBeep(1800, 140); });
    public static void Copy() => Task.Run(() => { TryBeep(1500, 60); Thread.Sleep(50); TryBeep(2100, 80); });
    public static void Tick() => Task.Run(() => TryBeep(2400, 22));
    public static void ScanOk() => Task.Run(() => { TryBeep(1200, 70); Thread.Sleep(60); TryBeep(2000, 120); });
    public static void Delete() => Task.Run(() => { TryBeep(800, 80); Thread.Sleep(40); TryBeep(400, 120); });
    public static void Hover() => Task.Run(() => TryBeep(2600, 18));

    static byte[] MakeWav(int freq, int ms, int vol = 20)
    {
        int sr = 22050; int samples = sr * ms / 1000; var data = new byte[samples * 2];
        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / sr; double s = Math.Sin(2 * Math.PI * freq * t) * vol * 10;
            short v = (short)(s * 100);
            data[i * 2] = (byte)(v & 0xff); data[i * 2 + 1] = (byte)((v >> 8) & 0xff);
        }
        using var ms2 = new MemoryStream();
        using var bw = new BinaryWriter(ms2);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); bw.Write(36 + data.Length); bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt ")); bw.Write(16); bw.Write((short)1); bw.Write((short)1); bw.Write(sr); bw.Write(sr * 2); bw.Write((short)2); bw.Write((short)16);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("data")); bw.Write(data.Length); bw.Write(data);
        return ms2.ToArray();
    }

    public static void PlayWav(byte[] wav)
    {
        try { using var ms = new MemoryStream(wav); using var sp = new SoundPlayer(ms); sp.Play(); } catch { }
    }
}
