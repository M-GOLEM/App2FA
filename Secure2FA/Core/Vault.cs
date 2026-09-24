using System.Text;
using System.Text.Json;

namespace Secure2FA.Core;

record VaultEntry(string Id, string Issuer, string Label, string Secret, DateTime Created);

class VaultManager
{
    private readonly string _file;
    private readonly string _pin;
    private readonly object _lock = new();
    public List<VaultEntry> Entries { get; private set; } = new();

    public VaultManager(string pin)
    {
        _pin = pin;
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Secure2FA");
        Directory.CreateDirectory(dir);
        _file = Path.Combine(dir, "vault.dat");
    }

    public string VaultPath => _file;
    public bool Exists => File.Exists(_file);

    public void Load()
    {
        lock (_lock)
        {
            if (!Exists) { Entries = new(); return; }
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    try { if (File.Exists(_file)) File.SetAttributes(_file, FileAttributes.Normal); } catch { }
                    byte[] enc;
                    using (var fs = new FileStream(_file, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        enc = new byte[fs.Length];
                        fs.ReadExactly(enc, 0, enc.Length);
                    }
                    var plain = CryptoEngine.Decrypt(_pin, enc);
                    var json = Encoding.UTF8.GetString(plain);
                    Entries = JsonSerializer.Deserialize<List<VaultEntry>>(json) ?? new();
                    CryptographicClear(plain);
                    return;
                }
                catch (UnauthorizedAccessException)
                {
                    try { File.SetAttributes(_file, FileAttributes.Normal); } catch { }
                    Thread.Sleep(120);
                    if (attempt == 2) throw;
                }
                catch (IOException) { Thread.Sleep(120); if (attempt == 2) throw; }
            }
        }
    }

    public void Save()
    {
        lock (_lock)
        {
            var json = JsonSerializer.Serialize(Entries);
            var plain = Encoding.UTF8.GetBytes(json);
            var enc = CryptoEngine.Encrypt(_pin, plain);
            var dir = Path.GetDirectoryName(_file)!;
            Directory.CreateDirectory(dir);
            var tmp = _file + ".tmp";
            var bak = _file + ".bak";
            for (int attempt = 0; attempt < 4; attempt++)
            {
                try
                {
                    try { if (File.Exists(_file)) File.SetAttributes(_file, FileAttributes.Normal); } catch { }
                    try { if (File.Exists(tmp)) File.SetAttributes(tmp, FileAttributes.Normal); } catch { }
                    if (File.Exists(tmp)) File.Delete(tmp);
                    File.WriteAllBytes(tmp, enc);
                    if (File.Exists(_file))
                    {
                        File.Copy(_file, bak, true);
                        try { File.SetAttributes(bak, FileAttributes.Hidden); } catch { }
                        File.SetAttributes(_file, FileAttributes.Normal);
                        File.Delete(_file);
                    }
                    File.Move(tmp, _file);
                    try { File.SetAttributes(_file, FileAttributes.Hidden); } catch { }
                    if (File.Exists(bak)) try { File.Delete(bak); } catch { }
                    break;
                }
                catch (UnauthorizedAccessException)
                {
                    try { if (File.Exists(_file)) File.SetAttributes(_file, FileAttributes.Normal); } catch { }
                    try { if (File.Exists(tmp)) File.SetAttributes(tmp, FileAttributes.Normal); } catch { }
                    Thread.Sleep(180);
                    if (attempt == 3) throw new UnauthorizedAccessException($"Cannot write vault '{_file}'. تأكد أن الملف غير مفتوح ببرنامج آخر وأعد المحاولة كمسؤول إذا لزم.");
                }
                catch (IOException)
                {
                    Thread.Sleep(180);
                    if (attempt == 3) throw;
                }
            }
            CryptographicClear(plain);
            Array.Clear(enc, 0, enc.Length);
        }
    }

    public void Add(string issuer, string label, string secret)
    {
        Entries.Add(new VaultEntry(Guid.NewGuid().ToString("N"), issuer.Trim(), label.Trim(), secret.Trim().Replace(" ", "").ToUpperInvariant(), DateTime.UtcNow));
        Save();
    }

    public void AddRange(IEnumerable<ParsedOtp> items)
    {
        foreach (var p in items) Entries.Add(new VaultEntry(Guid.NewGuid().ToString("N"), p.Issuer.Trim(), p.Label.Trim(), p.Secret.Trim().Replace(" ", "").ToUpperInvariant(), DateTime.UtcNow));
        Save();
    }

    public void Remove(string id)
    {
        Entries.RemoveAll(x => x.Id == id);
        Save();
    }

    private static void CryptographicClear(byte[] b) => Array.Clear(b, 0, b.Length);
}
