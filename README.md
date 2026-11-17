# App2FA

![C#](https://img.shields.io/badge/Language-C%23-512BD4?style=flat-square)
![.NET](https://img.shields.io/badge/.NET-8-512BD4?style=flat-square)
![WinForms](https://img.shields.io/badge/UI-WinForms-0078D6?style=flat-square)
![TOTP](https://img.shields.io/badge/Standard-TOTP-00C853?style=flat-square)

> A self-contained desktop TOTP authenticator for Windows, built with C# and
> WinForms on .NET 8.

`Secure2FA` keeps 2FA secrets in a local encrypted vault, generates TOTP codes,
imports accounts from `otpauth://` URIs or QR images (paste, drag-and-drop), and
plays a short sound when codes refresh.

---

## Features

- **Standard TOTP** — RFC 6238 codes (`TotpEngine`), compatible with Google
  Authenticator, Authy, 1Password and others.
- **Encrypted vault** — one file at `%APPDATA%\Secure2FA\vault.dat`, encrypted
  with AES-256-GCM. The key is derived from your PIN with PBKDF2-SHA512
  (300,000 iterations, 32-byte salt), then the container gets a light XOR
  obfuscation pass.
- **QR import** — paste `otpauth://` text, paste an image, or drop image files
  onto the window (`QrEngine`).
- **Unlock screen** — PIN gate with attempt counter, temporary lockout and a
  basic debugger check (`LoginForm`).
- **Account grid** — searchable list with live codes, countdown and delete
  (`MainForm`).
- **Audible refresh prompts** (`SoundEngine`).
- **Offline only** — no network client, no cloud sync, no telemetry.

---

## Project structure

```
Secure2FA/
├── Program.cs
├── Secure2FA.csproj
├── Core/
│   ├── TotpEngine.cs     # TOTP code generation
│   ├── CryptoEngine.cs   # AES-GCM vault encryption + PIN check
│   ├── QrEngine.cs       # QR decoding / rendering
│   ├── SoundEngine.cs    # code-refresh prompts
│   └── Vault.cs          # persistent encrypted vault
└── UI/
    ├── LoginForm.cs      # unlock screen
    └── MainForm.cs       # account grid + TOTP display
```

There is no solution file; build the single project directly.

---

## Build & run

```powershell
dotnet run     --project Secure2FA\Secure2FA.csproj -c Release
dotnet publish Secure2FA\Secure2FA.csproj -c Release -r win-x64 --self-contained false
```

Or open `Secure2FA.csproj` in Visual Studio 2022 and press **Run**.

---

## Requirements

- .NET 8 SDK
- Windows 10/11 (WinForms)
- Optional: Visual Studio 2022+

---

## Using the vault

1. First launch asks for the vault PIN.
2. Add an account by pasting an `otpauth://totp/...` URI, pressing `Ctrl+V` with
   a QR image in the clipboard, or dragging QR images onto the window.
3. Codes refresh every 30 seconds; the app plays a short tone on refresh.
4. The vault is saved automatically after each change.

**Backups:** copy `%APPDATA%\Secure2FA\vault.dat` somewhere safe. Without the
PIN the file is unreadable — there is no recovery code, no password reset and
no export feature. If you lose the PIN, the secrets are gone.

---

## Security notes — read this

This is a personal tool, not a hardened password manager:

- **The PIN check is a hard-coded constant in `CryptoEngine.cs`**, and the
  vault key is derived from the PIN you type. Anyone who can read the source
  and knows the constant can attempt to open a vault. Change the constant
  (and rebuild) before storing real accounts, or keep the vault on a machine
  where the source is not available.
- A fixed application "pepper" is compiled into the binary. It is not a secret.
- PBKDF2-SHA512 at 300k iterations is reasonable, but AES-GCM keys live in
  process memory while unlocked; a process with debug rights can read them.
- The debugger check is a speed bump, not anti-tamper.
- There is no backup/export and no recovery. Losing the PIN or the
  `vault.dat` file means losing every account in it.
- Keep your provider's own backup/recovery codes outside this app.

For accounts you cannot afford to lose, use a maintained authenticator with
encrypted backup and recovery (Bitwarden, 1Password, Aegis, etc.).

---

## License

No license file is included in this repository. The source is published for
reference; contact the author before redistributing.

---

<div align="center">
  <sub>Built by <a href="https://github.com/M-GOLEM"><b>Mr.GOLEM</b></a> ·
  watch the work on <a href="https://www.youtube.com/@golemsec">YouTube — GOLEM SEC</a></sub>
</div>
