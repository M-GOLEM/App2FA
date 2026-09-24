using Secure2FA.Core;
namespace Secure2FA.UI;
class MainForm : Form
{
    private readonly VaultManager vault;
    private readonly string pin;
    private FlowLayoutPanel listPanel = null!;
    private TextBox txtSearch = null!;
    private System.Windows.Forms.Timer timer = null!;
    private ProgressBar progress = null!;
    private Label lblCount = null!, lblTimer = null!, lblMatrix = null!;
    private Random rnd = new();
    public MainForm(string pin)
    {
        this.pin = pin;
        vault = new VaultManager(pin);
        try { vault.Load(); } catch (Exception ex) { SoundEngine.Error(); MessageBox.Show($"VAULT ERROR: {ex.Message}\nسيتم إنشاء vault جديد", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Warning); vault.Entries.Clear(); try { var p = vault.VaultPath; if (File.Exists(p)) { File.SetAttributes(p, FileAttributes.Normal); File.Delete(p); } } catch { } }
        Text = "SECURE_2FA // VAULT  [HACKER EDITION]";
        Size = new Size(560, 760);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.FromArgb(10, 15, 10);
        DoubleBuffered = true;
        AllowDrop = true;
        try { Icon = new Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico")); } catch { try { Icon = new Icon("Assets/app.ico"); } catch { } }
        Build();
        StartTimer();
        RefreshList();
        txtSearch.Focus();
        DragEnter += OnDragEnter; DragDrop += OnDragDrop;
        KeyPreview = true; KeyDown += OnKeyDown;
        MouseDown += DragMove;
    }
    void OnKeyDown(object? s, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.V)
        {
            var clipText = "";
            try { clipText = Clipboard.GetText().Trim(); } catch { }
            if (!string.IsNullOrEmpty(clipText) && clipText.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase))
            {
                var p = QrEngine.ParseOtpAuth(clipText);
                if (p != null) { HandleQrParsed(p); e.Handled = true; return; }
            }
            var img = QrEngine.DecodeFromClipboardImage();
            if (img != null)
            {
                var p = QrEngine.ParseOtpAuth(img);
                if (p != null) HandleQrParsed(p);
                else { SoundEngine.Error(); MessageBox.Show("QR لا يحتوي على بيانات 2FA صالحة", "SCAN", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                e.Handled = true;
            }
            else if (!string.IsNullOrEmpty(clipText))
            {
                var p2 = QrEngine.ParseOtpAuth(clipText);
                if (p2 != null) { HandleQrParsed(p2); e.Handled = true; }
            }
        }
    }
    void OnDragEnter(object? s, DragEventArgs e)
    {
        if (e.Data!.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Bitmap) || e.Data.GetDataPresent(DataFormats.Text)) e.Effect = DragDropEffects.Copy;
    }
    void OnDragDrop(object? s, DragEventArgs e)
    {
        try
        {
            if (e.Data!.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
                var found = new List<ParsedOtp>();
                var failed = 0;
                foreach (var f in files)
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp")
                    {
                        var parsed = QrEngine.DecodeAndParse(f);
                        if (parsed != null) found.Add(parsed);
                        else failed++;
                    }
                    else
                    {
                        var txt = "";
                        try { txt = File.ReadAllText(f).Trim(); } catch { }
                        var ptxt = QrEngine.ParseOtpAuth(txt);
                        if (ptxt != null) found.Add(ptxt);
                        else failed++;
                    }
                }
                if (found.Count > 0) HandleQrBatch(found, failed);
                else { SoundEngine.Error(); MessageBox.Show($"لم يتم العثور على QR صالح في {files.Length} ملف\nفشل: {failed}", "QR SCAN", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                return;
            }
            if (e.Data.GetDataPresent(DataFormats.Text))
            {
                var t = (string)e.Data.GetData(DataFormats.Text)!;
                var p = QrEngine.ParseOtpAuth(t);
                if (p != null) { HandleQrParsed(p); return; }
            }
            if (e.Data.GetDataPresent(DataFormats.Bitmap))
            {
                var bmp = (Bitmap)e.Data.GetData(DataFormats.Bitmap)!;
                var p = QrEngine.DecodeAndParse(bmp);
                if (p != null) HandleQrParsed(p);
                else { SoundEngine.Error(); MessageBox.Show("الصورة لا تحتوي على QR صالح", "QR"); }
            }
        }
        catch (Exception ex) { SoundEngine.Error(); MessageBox.Show($"Drag Error: {ex.Message}", "ERROR"); }
    }
    void HandleQrParsed(ParsedOtp p) => HandleQrBatch(new List<ParsedOtp> { p }, 0);
    void HandleQrBatch(List<ParsedOtp> list, int failed)
    {
        SoundEngine.ScanOk();
        if (list.Count == 1)
        {
            var p = list[0];
            var res = MessageBox.Show($"تم قراءة QR:\n\nIssuer: {p.Issuer}\nLabel: {p.Label}\nSecret: {p.Secret[..Math.Min(6, p.Secret.Length)]}...\n\nهل تريد الحفظ المشفر؟", "QR DETECTED // SAVE?", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (res == DialogResult.Yes) { TrySave(() => vault.Add(p.Issuer, p.Label, p.Secret)); }
            return;
        }
        var msg = $"تم العثور على {list.Count} QR صالح" + (failed > 0 ? $"\nفشل: {failed}" : "") + "\n\n";
        for (int i = 0; i < Math.Min(5, list.Count); i++) msg += $"• {list[i].Issuer} / {list[i].Label}\n";
        if (list.Count > 5) msg += $"... و {list.Count - 5} آخر\n";
        msg += "\nهل تريد حفظ الجميع؟";
        var r = MessageBox.Show(msg, $"QR BATCH [{list.Count}]", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (r == DialogResult.Yes) { TrySave(() => vault.AddRange(list)); }
    }
    void TrySave(Action act)
    {
        try { act(); RefreshList(); SoundEngine.Success(); }
        catch (UnauthorizedAccessException ex) { SoundEngine.Error(); MessageBox.Show($"فشل الحفظ - Access Denied:\n{ex.Message}\n\nالحل:\n• أغلق أي برنامج يفتح vault.dat\n• احذف الملف اليدوي: {vault.VaultPath}\n• شغل التطبيق كـ Administrator", "SAVE ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error); try { if (File.Exists(vault.VaultPath)) File.SetAttributes(vault.VaultPath, FileAttributes.Normal); } catch { } }
        catch (Exception ex) { SoundEngine.Error(); MessageBox.Show($"Save failed: {ex.Message}", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
    void Build()
    {
        var border = new Panel { Dock = DockStyle.Fill, Padding = new Padding(2), BackColor = Color.FromArgb(0, 255, 65) };
        border.MouseDown += DragMove;
        var inner = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(13, 19, 13) };
        inner.MouseDown += DragMove;
        border.Controls.Add(inner);
        Controls.Add(border);
        var top = new Panel { Dock = DockStyle.Top, Height = 62, BackColor = Color.FromArgb(18, 28, 18), Padding = new Padding(12, 8, 12, 8) };
        top.MouseDown += DragMove;
        var picTop = new PictureBox { Dock = DockStyle.Left, Width = 38, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
        picTop.MouseDown += DragMove;
        try
        {
            var p1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.png");
            var pp = File.Exists(p1) ? p1 : "Assets/app.png";
            if (File.Exists(pp)) { using var fs = new FileStream(pp, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); picTop.Image = new Bitmap(Image.FromStream(fs)); }
        }
        catch { }
        var lblTitle = new Label { Text = "◈ SECURE 2FA  HACKER EDITION", ForeColor = Color.FromArgb(0, 255, 65), Font = new Font("Consolas", 11, FontStyle.Bold), Dock = DockStyle.Left, AutoSize = false, Width = 240, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(6, 0, 0, 0) };
        lblTitle.MouseDown += DragMove;
        lblTimer = new Label { Text = "30", ForeColor = Color.FromArgb(0, 255, 65), Font = new Font("Consolas", 18, FontStyle.Bold), Dock = DockStyle.Right, Width = 45, TextAlign = ContentAlignment.MiddleCenter, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(25, 40, 25) };
        var btnMin = MkIcon("—", () => { SoundEngine.KeyClick(); WindowState = FormWindowState.Minimized; });
        var btnClose = MkIcon("", () => { SoundEngine.KeyClick(); Application.Exit(); });
        var topRight = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.RightToLeft, Width = 70, BackColor = Color.Transparent };
        topRight.MouseDown += DragMove;
        topRight.Controls.Add(btnClose); topRight.Controls.Add(btnMin);
        lblCount = new Label { Text = $"{vault.Entries.Count} CODES", ForeColor = Color.FromArgb(120, 180, 120), Font = new Font("Consolas", 7f), Dock = DockStyle.Bottom, Height = 14, TextAlign = ContentAlignment.MiddleLeft };
        lblCount.MouseDown += DragMove;
        var titleWrap = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(4, 0, 0, 0) };
        titleWrap.MouseDown += DragMove;
        titleWrap.Controls.Add(lblCount); titleWrap.Controls.Add(lblTitle);
        top.Controls.Add(lblTimer); top.Controls.Add(topRight); top.Controls.Add(picTop); top.Controls.Add(titleWrap);
        lblMatrix = new Label { Text = "0101 ▓ SYSTEM READY ▓ SCAN QR OR DRAG IMAGE ▓ 01", ForeColor = Color.FromArgb(0, 150, 40), Font = new Font("Consolas", 6.5f), Dock = DockStyle.Top, Height = 14, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.FromArgb(10, 18, 10) };
        lblMatrix.MouseDown += DragMove;
        progress = new ProgressBar { Dock = DockStyle.Top, Height = 3, Maximum = 30, Style = ProgressBarStyle.Continuous, ForeColor = Color.FromArgb(0, 255, 65) };
        var searchBar = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.FromArgb(13, 19, 13), Padding = new Padding(8, 6, 8, 6) };
        searchBar.MouseDown += DragMove;
        txtSearch = new TextBox { Dock = DockStyle.Fill, Font = new Font("Consolas", 10), ForeColor = Color.FromArgb(0, 255, 65), BackColor = Color.FromArgb(22, 32, 22), BorderStyle = BorderStyle.FixedSingle, PlaceholderText = "⌕ SEARCH / أو اسحب صور QR هنا (متعدد)..." };
        txtSearch.TextChanged += (s, e) => RefreshList();
        var btnQr = MkBtn(" QR", Color.FromArgb(0, 255, 65), Color.Black, 68, () => DoScanQr());
        var btnPaste = MkBtn(" PASTE", Color.FromArgb(30, 50, 30), Color.FromArgb(0, 255, 65), 78, () => DoPasteQr());
        var btnScreen = MkBtn(" CAP", Color.FromArgb(30, 50, 30), Color.FromArgb(0, 255, 65), 62, () => DoScreenCapture());
        var btnAdd = MkBtn("+ ADD", Color.FromArgb(0, 255, 65), Color.Black, 60, () => { SoundEngine.KeyClick(); ShowAdd(); });
        btnQr.FlatAppearance.BorderColor = Color.FromArgb(0, 255, 65); btnQr.FlatAppearance.BorderSize = 1;
        btnPaste.FlatAppearance.BorderColor = Color.FromArgb(0, 255, 65); btnScreen.FlatAppearance.BorderColor = Color.FromArgb(0, 255, 65);
        var rightPanel = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.RightToLeft, Width = 280, BackColor = Color.Transparent, Padding = new Padding(6, 0, 0, 0) };
        rightPanel.MouseDown += DragMove;
        rightPanel.Controls.Add(btnAdd); rightPanel.Controls.Add(btnScreen); rightPanel.Controls.Add(btnPaste); rightPanel.Controls.Add(btnQr);
        searchBar.Controls.Add(txtSearch); searchBar.Controls.Add(rightPanel);
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = Color.FromArgb(18, 28, 18), Padding = new Padding(8, 4, 8, 4) };
        footer.MouseDown += DragMove;
        var lblPath = new Label { Text = $"VAULT: {vault.VaultPath}  •   ON • DRAG MULTI-QR", ForeColor = Color.FromArgb(70, 110, 70), Font = new Font("Consolas", 6f), Dock = DockStyle.Left, AutoSize = false, Width = 420, TextAlign = ContentAlignment.MiddleLeft };
        lblPath.MouseDown += DragMove;
        var btnLock = new Button { Text = " LOCK", Dock = DockStyle.Right, Width = 70, FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(0, 255, 65), BackColor = Color.FromArgb(30, 45, 30), Font = new Font("Consolas", 7, FontStyle.Bold), Cursor = Cursors.Hand };
        btnLock.FlatAppearance.BorderColor = Color.FromArgb(0, 255, 65);
        btnLock.Click += (s, e) => { SoundEngine.KeyClick(); Hide(); using var l = new LoginForm(); if (l.ShowDialog() == DialogResult.OK && (string)l.Tag! == pin) Show(); else Application.Exit(); };
        btnLock.MouseEnter += (s, e) => SoundEngine.Hover();
        footer.Controls.Add(lblPath); footer.Controls.Add(btnLock);
        listPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.FromArgb(13, 19, 13), Padding = new Padding(12, 10, 12, 10) };
        listPanel.MouseDown += (s, e) => { if (listPanel.GetChildAtPoint(e.Location) == null) DragMove(s, e); };
        listPanel.ControlAdded += (s, e) => lblCount.Text = $"{vault.Entries.Count} CODES";
        inner.Controls.Add(listPanel);
        inner.Controls.Add(searchBar);
        inner.Controls.Add(progress);
        inner.Controls.Add(lblMatrix);
        inner.Controls.Add(top);
        inner.Controls.Add(footer);
    }
    Button MkBtn(string t, Color bg, Color fg, int w, Action a)
    {
        var b = new Button { Text = t, Width = w, Dock = DockStyle.Right, FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = fg, Font = new Font("Consolas", 8, FontStyle.Bold), Cursor = Cursors.Hand, Margin = new Padding(3, 0, 0, 0), Height = 30 };
        b.FlatAppearance.BorderSize = 0;
        b.Click += (s, e) => a();
        b.MouseEnter += (s, e) => SoundEngine.Hover();
        return b;
    }
    Label MkIcon(string t, Action a)
    {
        var l = new Label { Text = t, ForeColor = Color.FromArgb(0, 255, 65), Font = new Font("Consolas", 10, FontStyle.Bold), Size = new Size(28, 28), TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand, BackColor = Color.FromArgb(30, 45, 30), Margin = new Padding(2) };
        l.Click += (s, e) => a();
        l.MouseEnter += (s, e) => { SoundEngine.Hover(); l.BackColor = Color.FromArgb(45, 65, 45); };
        l.MouseLeave += (s, e) => l.BackColor = Color.FromArgb(30, 45, 30);
        return l;
    }
    void DragMove(object? s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { Native.ReleaseCapture(); Native.SendMessage(Handle, 0x112, 0xf012, 0); } }
    void StartTimer()
    {
        timer = new System.Windows.Forms.Timer { Interval = 500 };
        timer.Tick += (s, e) =>
        {
            int rem = TotpEngine.RemainingSeconds();
            lblTimer.Text = rem.ToString();
            lblTimer.ForeColor = rem <= 5 ? Color.FromArgb(255, 60, 60) : rem <= 10 ? Color.FromArgb(255, 180, 0) : Color.FromArgb(0, 255, 65);
            progress.Value = Math.Clamp(rem, 0, 30);
            progress.ForeColor = lblTimer.ForeColor;
            if (rem <= 5 && rem > 0) SoundEngine.Tick();
            var chars = "▓░█ 01";
            if (rnd.Next(6) == 0) lblMatrix.Text = $"{new string(Enumerable.Range(0, 18).Select(_ => chars[rnd.Next(chars.Length)]).ToArray())}  SYSTEM SECURE  {new string(Enumerable.Range(0, 18).Select(_ => chars[rnd.Next(chars.Length)]).ToArray())}";
            foreach (Control c in listPanel.Controls) if (c.Tag is VaultEntry ve) UpdateCard(c, ve);
        };
        timer.Start();
    }
    void RefreshList()
    {
        listPanel.SuspendLayout();
        listPanel.Controls.Clear();
        var q = txtSearch.Text.Trim().ToLowerInvariant();
        var filtered = vault.Entries.Where(x => string.IsNullOrEmpty(q) || x.Issuer.ToLower().Contains(q) || x.Label.ToLower().Contains(q)).OrderBy(x => x.Issuer).ToList();
        if (filtered.Count == 0)
        {
            var empty = new Panel { Size = new Size(520, 140), BackColor = Color.FromArgb(16, 26, 16), Margin = new Padding(0, 20, 0, 0) };
            empty.Paint += (s, pe) => pe.Graphics.DrawRectangle(new Pen(Color.FromArgb(0, 100, 40), 1), 0, 0, empty.Width - 1, empty.Height - 1);
            var l1 = new Label { Text = vault.Entries.Count == 0 ? "∅ NO CODES YET" : "NO MATCH", ForeColor = Color.FromArgb(0, 255, 65), Font = new Font("Consolas", 11, FontStyle.Bold), Dock = DockStyle.Top, Height = 30, TextAlign = ContentAlignment.MiddleCenter, Padding = new Padding(0, 10, 0, 0) };
            var l2 = new Label { Text = "طرق الإضافة:\n• + ADD  يدوي\n•  QR  تحميل صور متعددة\n•  PASTE  لصق (Ctrl+V)\n•  CAP  التقاط شاشة\n• اسحب صور QR وأفلتها هنا (متعدد)", ForeColor = Color.FromArgb(130, 180, 130), Font = new Font("Consolas", 8), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
            empty.Controls.Add(l2); empty.Controls.Add(l1);
            listPanel.Controls.Add(empty);
        }
        else foreach (var e in filtered) listPanel.Controls.Add(BuildCard(e));
        lblCount.Text = $"{filtered.Count}/{vault.Entries.Count} CODES  •  QR READY";
        listPanel.ResumeLayout();
    }
    Control BuildCard(VaultEntry e)
    {
        var card = new Panel { Width = 518, Height = 96, BackColor = Color.FromArgb(22, 32, 22), Margin = new Padding(0, 0, 0, 10), Padding = new Padding(1), Tag = e, Cursor = Cursors.Hand };
        var inner = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(22, 32, 22), Padding = new Padding(12, 8, 12, 8) };
        card.Controls.Add(inner);
        card.Paint += (s, pe) => pe.Graphics.DrawRectangle(new Pen(Color.FromArgb(0, 100, 40), 1), 0, 0, card.Width - 1, card.Height - 1);
        var lblIssuer = new Label { Text = $" {e.Issuer.ToUpper()}", ForeColor = Color.FromArgb(0, 255, 65), Font = new Font("Consolas", 10, FontStyle.Bold), Dock = DockStyle.Top, Height = 18 };
        var lblLabel = new Label { Text = e.Label, ForeColor = Color.FromArgb(150, 200, 150), Font = new Font("Consolas", 8), Dock = DockStyle.Top, Height = 16 };
        var code = TotpEngine.Generate(e.Secret);
        var lblCode = new Label { Text = $"{code[..3]} {code[3..]}", ForeColor = Color.White, Font = new Font("Consolas", 22, FontStyle.Bold), Dock = DockStyle.Left, AutoSize = false, Width = 150, TextAlign = ContentAlignment.MiddleLeft, Name = "code" };
        var lblCopy = new Label { Text = "⎙ COPY", ForeColor = Color.FromArgb(0, 255, 65), Font = new Font("Consolas", 7, FontStyle.Bold), Dock = DockStyle.Right, Width = 60, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.FromArgb(30, 50, 30), Cursor = Cursors.Hand, BorderStyle = BorderStyle.FixedSingle, Height = 30, Margin = new Padding(0, 6, 0, 0) };
        void DoCopy2() { Clipboard.SetText(code); SoundEngine.Copy(); lblCopy.Text = " COPIED"; lblCopy.ForeColor = Color.Black; lblCopy.BackColor = Color.FromArgb(0, 255, 65); Task.Delay(1200).ContinueWith(_ => Invoke(() => { lblCopy.Text = "⎙ COPY"; lblCopy.ForeColor = Color.FromArgb(0, 255, 65); lblCopy.BackColor = Color.FromArgb(30, 50, 30); })); }
        lblCopy.Click += (s, ev) => DoCopy2();
        lblCopy.MouseEnter += (s, e) => SoundEngine.Hover();
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 36, BackColor = Color.Transparent };
        bottom.Controls.Add(lblCopy); bottom.Controls.Add(lblCode);
        var btnDel = new Label { Text = "", ForeColor = Color.FromArgb(120, 60, 60), Font = new Font("Consolas", 9, FontStyle.Bold), Dock = DockStyle.Right, Width = 24, TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand };
        btnDel.Click += (s, ev) => { SoundEngine.Delete(); if (MessageBox.Show($"DELETE {e.Issuer} : {e.Label} ?", "CONFIRM  [HACKER VAULT]", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) { TrySave(() => vault.Remove(e.Id)); } };
        btnDel.MouseEnter += (s, ev) => { SoundEngine.Hover(); btnDel.ForeColor = Color.Red; };
        btnDel.MouseLeave += (s, ev) => btnDel.ForeColor = Color.FromArgb(120, 60, 60);
        var topRow = new Panel { Dock = DockStyle.Top, Height = 18, BackColor = Color.Transparent };
        topRow.Controls.Add(btnDel); topRow.Controls.Add(lblIssuer);
        inner.Controls.Add(bottom); inner.Controls.Add(lblLabel); inner.Controls.Add(topRow);
        card.Click += (s, ev) => DoCopy2();
        inner.Click += (s, ev) => DoCopy2();
        lblCode.Click += (s, ev) => DoCopy2();
        return card;
    }
    void UpdateCard(Control card, VaultEntry e)
    {
        try
        {
            var code = TotpEngine.Generate(e.Secret);
            var lbl = FindLabel(card, "code");
            if (lbl != null) lbl.Text = $"{code[..3]} {code[3..]}";
        }
        catch { }
    }
    Label? FindLabel(Control p, string name)
    {
        foreach (Control c in p.Controls) { if (c.Name == name) return (Label)c; var r = FindLabel(c, name); if (r != null) return r; }
        return null;
    }
    void DoScanQr()
    {
        SoundEngine.KeyClick();
        using var ofd = new OpenFileDialog { Filter = "QR Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All|*.*", Title = "اختر صور QR - HACKER SCAN (متعدد)", Multiselect = true };
        if (ofd.ShowDialog() != DialogResult.OK) return;
        var list = new List<ParsedOtp>(); int fail = 0;
        foreach (var f in ofd.FileNames) { var p = QrEngine.DecodeAndParse(f); if (p != null) list.Add(p); else fail++; }
        if (list.Count == 0) { SoundEngine.Error(); MessageBox.Show($"لم يتم العثور على QR صالح في {ofd.FileNames.Length} ملف\nفشل: {fail}", "SCAN FAILED", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
        HandleQrBatch(list, fail);
    }
    void DoPasteQr()
    {
        SoundEngine.KeyClick();
        if (Clipboard.ContainsText())
        {
            var t = Clipboard.GetText().Trim();
            var p = QrEngine.ParseOtpAuth(t);
            if (p != null) { HandleQrParsed(p); return; }
        }
        var imgText = QrEngine.DecodeFromClipboardImage();
        if (imgText != null)
        {
            var p2 = QrEngine.ParseOtpAuth(imgText);
            if (p2 != null) { HandleQrParsed(p2); return; }
        }
        SoundEngine.Error();
        MessageBox.Show("الحافظة لا تحتوي على QR\n• انسخ صورة QR (Ctrl+C) ثم اضغط PASTE\n• أو انسخ رابط otpauth://", "PASTE", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
    void DoScreenCapture()
    {
        SoundEngine.KeyClick();
        Hide();
        Thread.Sleep(250);
        using var overlay = new ScreenCaptureOverlay();
        if (overlay.ShowDialog() == DialogResult.OK && overlay.SelectedBitmap != null)
        {
            using var bmp = overlay.SelectedBitmap;
            var txt = QrEngine.DecodeFromBitmap(bmp);
            if (txt == null) { SoundEngine.Error(); MessageBox.Show("لم يتم العثور على QR في المنطقة المحددة", "SCREEN CAP"); }
            else
            {
                var p = QrEngine.ParseOtpAuth(txt);
                if (p == null) { SoundEngine.Error(); MessageBox.Show($"QR محتوى:\n{txt}\n\nليس otpauth صالح", "QR CONTENT"); }
                else HandleQrParsed(p);
            }
        }
        Show();
    }
    void ShowAdd()
    {
        using var dlg = new Form
        {
            Text = "ADD 2FA  //  HACKER VAULT",
            Size = new Size(440, 460),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            BackColor = Color.FromArgb(16, 24, 16),
            ForeColor = Color.FromArgb(0, 255, 65),
            MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false
        };
        try { dlg.Icon = Icon; } catch { }
        var lbl1 = new Label { Text = "> ISSUER (Google, GitHub...)", ForeColor = Color.FromArgb(0, 255, 65), Font = new Font("Consolas", 8, FontStyle.Bold), Location = new Point(20, 14), AutoSize = true };
        var txtIssuer = new TextBox { Location = new Point(20, 32), Width = 390, Font = new Font("Consolas", 10), BackColor = Color.FromArgb(25, 35, 25), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, PlaceholderText = "GitHub" };
        var lbl2 = new Label { Text = "> LABEL (email / username)", ForeColor = Color.FromArgb(0, 255, 65), Font = new Font("Consolas", 8, FontStyle.Bold), Location = new Point(20, 64), AutoSize = true };
        var txtLabel = new TextBox { Location = new Point(20, 82), Width = 390, Font = new Font("Consolas", 10), BackColor = Color.FromArgb(25, 35, 25), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, PlaceholderText = "user@example.com" };
        var lbl3 = new Label { Text = "> SECRET (BASE32)", ForeColor = Color.FromArgb(0, 255, 65), Font = new Font("Consolas", 8, FontStyle.Bold), Location = new Point(20, 114), AutoSize = true };
        var txtSecret =   { Location = new Point(20, 132), Width = 390, Font = new Font("Consolas", 10), BackColor = Color.FromArgb(25, 35, 25), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, PlaceholderText = "JBSWY3DPEHPK3PXP" };
        var lblErr = new Label { ForeColor = Color.FromArgb(255, 70, 70), Font = new Font("Consolas", 7, FontStyle.Bold), Location = new Point(20, 162), Width = 390, Height = 16, TextAlign = ContentAlignment.MiddleCenter };
        var btnQrFile = new Button { Text = " تحميل صور QR", Location = new Point(20, 186), Width = 125, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(30, 50, 30), ForeColor = Color.FromArgb(0, 255, 65), Font = new Font("Consolas", 7.5f, FontStyle.Bold), Cursor = Cursors.Hand };
        btnQrFile.FlatAppearance.BorderColor = Color.FromArgb(0, 255, 65);
        var btnQrPaste = new Button { Text = " لصق QR", Location = new Point(152, 186), Width = 95, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(30, 50, 30), ForeColor = Color.FromArgb(0, 255, 65), Font = new Font("Consolas", 7.5f, FontStyle.Bold), Cursor = Cursors.Hand };
        btnQrPaste.FlatAppearance.BorderColor = Color.FromArgb(0, 255, 65);
        var btnQrCap = new Button { Text = " التقاط", Location = new Point(254, 186), Width = 80, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(30, 50, 30), ForeColor = Color.FromArgb(0, 255, 65), Font = new Font("Consolas", 7.5f, FontStyle.Bold), Cursor = Cursors.Hand };
        btnQrCap.FlatAppearance.BorderColor = Color.FromArgb(0, 255, 65);
        var btnClear = new Button { Text = "C", Location = new Point(340, 186), Width = 34, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(50, 20, 20), ForeColor = Color.White, Font = new Font("Consolas", 8, FontStyle.Bold), Cursor = Cursors.Hand };
        foreach (var b in new[] { btnQrFile, btnQrPaste, btnQrCap }) b.MouseEnter += (s, e) => SoundEngine.Hover();
        btnQrFile.Click += (s, e) =>
        {
            SoundEngine.KeyClick();
            using var ofd = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp", Multiselect = true };
            if (ofd.ShowDialog(dlg) != DialogResult.OK) return;
            if (ofd.FileNames.Length == 1) { var p = QrEngine.DecodeAndParse(ofd.FileName); if (p == null) { SoundEngine.Error(); lblErr.Text = "QR غير صالح"; return; } SoundEngine.ScanOk(); txtIssuer.Text = p.Issuer; txtLabel.Text = p.Label; txtSecret.Text = ; lblErr.ForeColor = Color.FromArgb(0, 255, 65); lblErr.Text = " QR SCANNED // READY TO SAVE"; }
            else { var list = new List<ParsedOtp>(); int fail = 0; foreach (var f in ofd.FileNames) { var p = QrEngine.DecodeAndParse(f); if (p != null) list.Add(p); else fail++; } if (list.Count == 0) { SoundEngine.Error(); lblErr.Text = "لا يوجد QR صالح"; return; } SoundEngine.ScanOk(); var first = list[0]; txtIssuer.Text = first.Issuer; txtLabel.Text = first.Label; txtSecret.Text = ; dlg.Tag = list; lblErr.ForeColor = Color.FromArgb(0, 255, 65); lblErr.Text = $" {list.Count} QR جاهز (سيتم حفظ الكل) فشل:{fail}"; }
        };
        btnQrPaste.Click += (s, e) =>
        {
            SoundEngine.KeyClick();
            string? txt = null;
            if (Clipboard.ContainsText()) txt = Clipboard.GetText();
            else txt = QrEngine.DecodeFromClipboardImage();
            if (txt == null) { SoundEngine.Error(); lblErr.Text = "الحافظة فارغة"; return; }
            var p = QrEngine.ParseOtpAuth(txt);
            if (p == null) { SoundEngine.Error(); lblErr.Text = "QR غير صالح"; return; }
            SoundEngine.ScanOk(); txtIssuer.Text = p.Issuer; txtLabel.Text = p.Label; txtSecret.Text = ; lblErr.ForeColor = Color.FromArgb(0, 255, 65); lblErr.Text = " PASTED QR // READY";
        };
        btnQrCap.Click += (s, e) =>
        {
            dlg.Hide();
            Thread.Sleep(200);
            using var ov = new ScreenCaptureOverlay();
            string? txt = null;
            if (ov.ShowDialog() == DialogResult.OK && ov.SelectedBitmap != null) { using var bmp = ov.SelectedBitmap; txt = QrEngine.DecodeFromBitmap(bmp); }
            dlg.Show();
            if (txt == null) { SoundEngine.Error(); lblErr.Text = "لم يتم العثور على QR"; return; }
            var p = QrEngine.ParseOtpAuth(txt);
            if (p == null) { SoundEngine.Error(); lblErr.Text = "QR ليس otpauth"; return; }
            SoundEngine.ScanOk(); txtIssuer.Text = p.Issuer; txtLabel.Text = p.Label; txtSecret.Text = ; lblErr.ForeColor = Color.FromArgb(0, 255, 65); lblErr.Text = " SCREEN QR // READY";
        };
        btnClear.Click += (s, e) => { SoundEngine.KeyClick(); txtIssuer.Clear(); txtLabel.Clear(); txtSecret.Clear(); lblErr.Text = ""; dlg.Tag = null; };
        var btnOk = new Button { Text = " ENCRYPT & SAVE  [AES-256-GCM]", Location = new Point(20, 232), Width = 390, Height = 42, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 255, 65), ForeColor = Color.Black, Font = new Font("Consolas", 10, FontStyle.Bold), Cursor = Cursors.Hand };
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.MouseEnter += (s, e) => SoundEngine.Hover();
        var lblHint = new Label { Text = "◼ AES-256-GCM + PBKDF2 300K + XOR • متعدد الصور", ForeColor = Color.FromArgb(80, 120, 80), Font = new Font("Consolas", 6.5f), Location = new Point(20, 284), Width = 390, TextAlign = ContentAlignment.MiddleCenter };
        var lblQrHint = new Label { Text = " يدعم: otpauth://  •  Base32  •  صور متعددة  •  سحب وإفلات", ForeColor = Color.FromArgb(100, 140, 100), Font = new Font("Consolas", 7f), Location = new Point(20, 306), Width = 390, Height = 30, TextAlign = ContentAlignment.MiddleCenter };
        var dragHint = new Panel { Location = new Point(20, 340), Size = new Size(390, 50), BackColor = Color.FromArgb(22, 32, 22), BorderStyle = BorderStyle.FixedSingle };
        dragHint.AllowDrop = true;
        var lblDrag = new Label { Text = " اسحب صور QR هنا (متعدد) ", ForeColor = Color.FromArgb(0, 255, 65), Font = new Font("Consolas", 9, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent };
        dragHint.Controls.Add(lblDrag);
        dragHint.DragEnter += (s, e) => { if (e.Data!.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; dragHint.BackColor = Color.FromArgb(30, 50, 30); };
        dragHint.DragLeave += (s, e) => dragHint.BackColor = Color.FromArgb(22, 32, 22);
        dragHint.DragDrop += (s, e) =>
        {
            var files = (string[])e.Data!.GetData(DataFormats.FileDrop)!;
            var list = new List<ParsedOtp>(); int fail = 0;
            foreach (var f in files) { var p = QrEngine.DecodeAndParse(f); if (p != null) list.Add(p); else fail++; }
            if (list.Count == 0) { SoundEngine.Error(); lblErr.Text = "QR غير صالح"; return; }
            if (list.Count == 1) { var p = list[0]; SoundEngine.ScanOk(); txtIssuer.Text = p.Issuer; txtLabel.Text = p.Label; txtSecret.Text = ; lblErr.ForeColor = Color.FromArgb(0, 255, 65); lblErr.Text = " DRAG QR // READY"; }
            else { var first = list[0]; SoundEngine.ScanOk(); txtIssuer.Text = first.Issuer; txtLabel.Text = first.Label; txtSecret.Text = ; dlg.Tag = list; lblErr.ForeColor = Color.FromArgb(0, 255, 65); lblErr.Text = $" {list.Count} QR جاهز فشل:{fail}"; }
            dragHint.BackColor = Color.FromArgb(22, 32, 22);
        };
        dlg.Controls.AddRange(new Control[] { lbl1, txtIssuer, lbl2, txtLabel, lbl3, txtSecret, lblErr, btnQrFile, btnQrPaste, btnQrCap, btnClear, btnOk, lblHint, lblQrHint, dragHint });
        btnOk.Click += (s, e) =>
        {
            SoundEngine.KeyClick();
            if (dlg.Tag is List<ParsedOtp> batch && batch.Count > 1)
            {
                TrySave(() => vault.AddRange(batch));
                dlg.DialogResult = DialogResult.OK; dlg.Close(); return;
            }
            if (string.IsNullOrWhiteSpace(txtIssuer.Text) || string.IsNullOrWhiteSpace(txtSecret.Text)) { SoundEngine.Error(); lblErr.ForeColor = Color.FromArgb(255, 70, 70); lblErr.Text = "FILL ALL FIELDS"; return; }
            if (!TotpEngine.IsValidSecret(txtSecret.Text)) { SoundEngine.Error(); lblErr.Text = "INVALID BASE32 SECRET"; return; }
            TrySave(() => vault.Add(txtIssuer.Text, string.IsNullOrWhiteSpace(txtLabel.Text) ? "default" : txtLabel.Text, txtSecret.Text));
            dlg.DialogResult = DialogResult.OK; dlg.Close();
        };
        if (dlg.ShowDialog(this) == DialogResult.OK) RefreshList();
    }
}
class ScreenCaptureOverlay : Form
{
    public Bitmap? SelectedBitmap { get; private set; }
    Point start, end; bool dragging = false;
    public ScreenCaptureOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Maximized;
        BackColor = Color.Black; Opacity = 0.35;
        Cursor = Cursors.Cross;
        TopMost = true; ShowInTaskbar = false;
        DoubleBuffered = true;
        var lbl = new Label { Text = " اسحب لتحديد منطقة QR ثم حرر • ESC للإلغاء", ForeColor = Color.Lime, BackColor = Color.FromArgb(20, 20, 20), Font = new Font("Consolas", 10, FontStyle.Bold), Dock = DockStyle.Top, Height = 30, TextAlign = ContentAlignment.MiddleCenter };
        Controls.Add(lbl);
        KeyPreview = true; KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel; };
    }
    protected override void OnMouseDown(MouseEventArgs e) { start = e.Location; dragging = true; base.OnMouseDown(e); }
    protected override void OnMouseMove(MouseEventArgs e) { end = e.Location; if (dragging) Invalidate(); base.OnMouseMove(e); }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (!dragging) return; dragging = false;
        var rect = GetRect();
        if (rect.Width < 10 || rect.Height < 10) { DialogResult = DialogResult.Cancel; return; }
        try
        {
            var bmp = new Bitmap(rect.Width, rect.Height);
            using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(PointToScreen(rect.Location), Point.Empty, rect.Size);
            SelectedBitmap = bmp;
            DialogResult = DialogResult.OK;
        }
        catch { DialogResult = DialogResult.Cancel; }
    }
    Rectangle GetRect() => new(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Abs(start.X - end.X), Math.Abs(start.Y - end.Y));
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (dragging)
        {
            var r = GetRect();
            e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(80, 0, 255, 65)), r);
            e.Graphics.DrawRectangle(new Pen(Color.Lime, 2), r);
            e.Graphics.DrawString($"{r.Width}x{r.Height}", new Font("Consolas", 8), Brushes.Lime, r.X, r.Y - 14);
        }
    }
}
static class Native
{
    [System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool ReleaseCapture();
    [System.Runtime.InteropServices.DllImport("user32.dll")] public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
}
