using Secure2FA.Core;
using System.Runtime.InteropServices;

namespace Secure2FA.UI;

class LoginForm : Form
{
    TextBox txtPin = null!;
    Label lblInfo = null!, lblAttempts = null!, lblMatrix = null!;
    Button btnUnlock = null!;
    int fails = 0;
    DateTime lockUntil = DateTime.MinValue;
    System.Windows.Forms.Timer matrixTimer = null!;
    Random rnd = new();

    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    public LoginForm()
    {
        Text = "SECURE_2FA // ACCESS CONTROL";
        Size = new Size(440, 560);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.FromArgb(10, 15, 10);
        DoubleBuffered = true;
        ShowInTaskbar = true;
        try { Icon = new Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico")); } catch { try { Icon = new Icon("Assets/app.ico"); } catch { } }
        Build();
        StartMatrix();
        if (CryptoEngine.IsDebugged()) { SoundEngine.Error(); MessageBox.Show("Debugger detected!", "SECURITY", MessageBoxButtons.OK, MessageBoxIcon.Stop); Environment.Exit(1); }
    }

    void StartMatrix()
    {
        matrixTimer = new System.Windows.Forms.Timer { Interval = 90 };
        matrixTimer.Tick += (s, e) =>
        {
            var chars = "01░▓█▓01";
            var t = "";
            for (int i = 0; i < 42; i++) t += chars[rnd.Next(chars.Length)];
            lblMatrix.Text = t;
            lblMatrix.ForeColor = rnd.Next(3) == 0 ? Color.FromArgb(0, 255, 65) : Color.FromArgb(0, 180, 40);
        };
        matrixTimer.Start();
    }

    void DragAnywhere(object? s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0x112, 0xf012, 0); } }

    void Build()
    {
        var border = new Panel { Dock = DockStyle.Fill, Padding = new Padding(2), BackColor = Color.FromArgb(0, 255, 65) };
        border.MouseDown += DragAnywhere;
        var inner = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(13, 19, 13), Padding = new Padding(22) };
        inner.MouseDown += DragAnywhere;
        border.Controls.Add(inner);
        Controls.Add(border);

        var drag = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = Color.Transparent };
        drag.MouseDown += DragAnywhere;
        inner.Controls.Add(drag);

        lblMatrix = new Label { Text = "010101010101010101010101010101010101010101", ForeColor = Color.FromArgb(0, 120, 40), Font = new Font("Consolas", 6f), Dock = DockStyle.Top, Height = 12, TextAlign = ContentAlignment.MiddleCenter };
        lblMatrix.MouseDown += DragAnywhere;
        var lblTitle = new Label { Text = "▓ SECURE 2FA VAULT ▓", ForeColor = Color.FromArgb(0, 255, 65), Font = new Font("Consolas", 15, FontStyle.Bold), Dock = DockStyle.Top, Height = 30, TextAlign = ContentAlignment.MiddleCenter };
        lblTitle.MouseDown += DragAnywhere;
        var lblSub = new Label { Text = "ENCRYPTED // AES-256-GCM // PBKDF2 300K // QR-SCAN", ForeColor = Color.FromArgb(120, 255, 120), Font = new Font("Consolas", 6.5f), Dock = DockStyle.Top, Height = 15, TextAlign = ContentAlignment.MiddleCenter };
        lblSub.MouseDown += DragAnywhere;
        var lblLine = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Color.FromArgb(0, 255, 65) };
        lblLine.MouseDown += DragAnywhere;

        var iconPanel = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Color.Transparent, Padding = new Padding(0, 6, 0, 0) };
        iconPanel.MouseDown += DragAnywhere;
        var pic = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
        pic.MouseDown += DragAnywhere;
        try
        {
            var p1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.png");
            var p2 = "Assets/app.png";
            var pp = File.Exists(p1) ? p1 : p2;
            if (File.Exists(pp))
            {
                using var fs = new FileStream(pp, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                pic.Image = new Bitmap(Image.FromStream(fs));
            }
            else throw new Exception();
        }
        catch
        {
            var lblFallback = new Label { Text = "◈", ForeColor = Color.FromArgb(0, 255, 65), Font = new Font("Consolas", 42, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
            lblFallback.MouseDown += DragAnywhere;
            iconPanel.Controls.Add(lblFallback);
        }
        if (pic.Image != null) iconPanel.Controls.Add(pic);

        var lblPrompt = new Label { Text = "> ENTER ACCESS PIN", ForeColor = Color.FromArgb(0, 255, 65), Font = new Font("Consolas", 9, FontStyle.Bold), Dock = DockStyle.Top, Height = 22, TextAlign = ContentAlignment.MiddleCenter };
        lblPrompt.MouseDown += DragAnywhere;

        txtPin = new TextBox
        {
            Font = new Font("Consolas", 22, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 255, 65),
            BackColor = Color.FromArgb(20, 30, 20),
            BorderStyle = BorderStyle.FixedSingle,
            TextAlign = HorizontalAlignment.Center,
            UseSystemPasswordChar = ,
            MaxLength = 6,
            Dock = DockStyle.Top,
            Height = 48
        };
        txtPin.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) TryUnlock(); else if (e.KeyCode != Keys.Back) SoundEngine.KeyClick(); };
        txtPin.TextChanged += (s, e) => { if (txtPin.Text.Length == 6) TryUnlock(); };

        var pad = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Color.Transparent };

        btnUnlock = new Button
        {
            Text = " UNLOCK VAULT  [▓]",
            Dock = DockStyle.Top,
            Height = 44,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.Black,
            BackColor = Color.FromArgb(0, 255, 65),
            Font = new Font("Consolas", 11, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnUnlock.FlatAppearance.BorderSize = 0;
        btnUnlock.Click += (s, e) => { SoundEngine.KeyClick(); TryUnlock(); };
        btnUnlock.MouseEnter += (s, e) => { SoundEngine.Hover(); btnUnlock.BackColor = Color.FromArgb(0, 230, 60); };
        btnUnlock.MouseLeave += (s, e) => btnUnlock.BackColor = Color.FromArgb(0, 255, 65);

        lblInfo = new Label { Text = "SECURE LOCAL STORAGE  •  QR-SCAN • SOUNDS • HACKER UI", ForeColor = Color.FromArgb(90, 130, 90), Font = new Font("Consolas", 7f), Dock = DockStyle.Top, Height = 16, TextAlign = ContentAlignment.MiddleCenter };
        lblInfo.MouseDown += DragAnywhere;
        lblAttempts = new Label { Text = "", ForeColor = Color.FromArgb(255, 60, 60), Font = new Font("Consolas", 8, FontStyle.Bold), Dock = DockStyle.Top, Height = 18, TextAlign = ContentAlignment.MiddleCenter };
        lblAttempts.MouseDown += DragAnywhere;

        var footer = new Label { Text = " ANTI-DEBUG • XOR-OBFUSCATED • HIDDEN VAULT • BIP-BEEP FX", ForeColor = Color.FromArgb(60, 100, 60), Font = new Font("Consolas", 6.5f), Dock = DockStyle.Bottom, Height = 20, TextAlign = ContentAlignment.MiddleCenter };
        footer.MouseDown += DragAnywhere;

        var keypad = BuildKeypad();

        inner.Controls.Add(footer);
        inner.Controls.Add(lblAttempts);
        inner.Controls.Add(lblInfo);
        inner.Controls.Add(btnUnlock);
        inner.Controls.Add(pad);
        inner.Controls.Add(txtPin);
        inner.Controls.Add(lblPrompt);
        inner.Controls.Add(iconPanel);
        inner.Controls.Add(lblLine);
        inner.Controls.Add(lblSub);
        inner.Controls.Add(lblTitle);
        inner.Controls.Add(lblMatrix);
        inner.Controls.Add(drag);

        var btnClose = new Label { Text = "", ForeColor = Color.FromArgb(0, 255, 65), Font = new Font("Consolas", 10, FontStyle.Bold), Size = new Size(30, 28), Location = new Point(Width - 34, 2), Cursor = Cursors.Hand, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent };
        btnClose.Click += (s, e) => { SoundEngine.KeyClick(); Application.Exit(); };
        drag.Controls.Add(btnClose);
        MouseDown += DragAnywhere;
        border.MouseDown += DragAnywhere;
        inner.MouseDown += DragAnywhere;
    }

    Panel BuildKeypad()
    {
        var p = new TableLayoutPanel { Dock = DockStyle.Top, Height = 170, ColumnCount = 3, RowCount = 4, BackColor = Color.Transparent, Padding = new Padding(40, 6, 40, 0) };
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        for (int i = 1; i <= 9; i++) p.Controls.Add(KeyBtn(i.ToString()));
        p.Controls.Add(KeyBtn("C", true));
        p.Controls.Add(KeyBtn("0"));
        p.Controls.Add(KeyBtn("⌫", true));
        var parent = Controls[0].Controls[0];
        parent.Controls.Add(p);
        p.BringToFront();
        parent.Controls.SetChildIndex(p, 4);
        return p;
    }

    Button KeyBtn(string t, bool alt = false)
    {
        var b = new Button
        {
            Text = t,
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 11, FontStyle.Bold),
            ForeColor = alt ? Color.FromArgb(0, 255, 65) : Color.FromArgb(220, 255, 220),
            BackColor = Color.FromArgb(25, 35, 25),
            Margin = new Padding(4),
            Cursor = Cursors.Hand,
            Tag = t
        };
        b.FlatAppearance.BorderColor = Color.FromArgb(0, 120, 40);
        b.FlatAppearance.BorderSize = 1;
        b.Click += (s, e) =>
        {
            SoundEngine.KeyClick();
            if (t == "C") txtPin.Clear();
            else if (t == "⌫") { if (txtPin.TextLength > 0) txtPin.Text = txtPin.Text[..^1]; }
            else if (txtPin.TextLength < 6) txtPin.Text += t;
            txtPin.SelectionStart = txtPin.TextLength;
            txtPin.Focus();
        };
        b.MouseEnter += (s, e) => SoundEngine.Hover();
        return b;
    }

    void TryUnlock()
    {
        if (DateTime.Now < lockUntil) { lblAttempts.Text = $"LOCKED {Math.Ceiling((lockUntil - DateTime.Now).TotalSeconds)}s"; SoundEngine.Error(); return; }
        var pin = txtPin.Text.Trim();
        if (pin.Length != 6) { lblAttempts.Text = "> PIN MUST BE 6 DIGITS"; SoundEngine.Error(); return; }
        if (!CryptoEngine.VerifyPin(pin))
        {
            fails++;
            txtPin.Clear();
            SoundEngine.Error();
            int wait = fails >= 5 ? 30 : fails >= 3 ? 10 : 0;
            if (wait > 0) lockUntil = DateTime.Now.AddSeconds(wait);
            lblAttempts.Text = $" ACCESS DENIED [{fails}/5] {(wait > 0 ? $"WAIT {wait}s" : "")}";
            txtPin.BackColor = Color.FromArgb(50, 15, 15);
            Task.Delay(400).ContinueWith(_ => Invoke(() => txtPin.BackColor = Color.FromArgb(20, 30, 20)));
            if (fails >= 5) { lblInfo.Text = "BRUTE-FORCE PROTECTION ACTIVE"; fails = 0; }
            return;
        }
        SoundEngine.Success();
        DialogResult = DialogResult.OK;
        Tag = pin;
        matrixTimer?.Stop();
        Close();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.DrawRectangle(new Pen(Color.FromArgb(0, 255, 65), 1), 0, 0, Width - 1, Height - 1);
    }
}
