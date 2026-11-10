// Windows installer (C# 5, built by tools/package.py with the .NET Framework compiler every Windows has).
// The game zip is appended to this exe, followed by its length (8 bytes). Installs per user (no admin):
// %LOCALAPPDATA%\Programs\<Folder> by default, Desktop + Start-menu shortcuts, an uninstaller; then starts the game.
// A window (2026-11-09; it was a console prompt): the logo, the folder (Browse), shortcut and launch options,
// a progress bar, then PLAY. `Setup.exe --shot=<png> [--page=done]` draws the window to a file and quits (screenshots);
// `--silent=<folder>` installs there with no window, shortcuts or launch.
// Clean install (2026-11-09): the uninstaller is this same window program (uninstall.exe, the installer without the
// game inside, run with --uninstall), listed in Settings > Apps for the current user. Saves are never touched
// (%APPDATA%\Godot).
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

static class Setup
{
    const string App = "APPNAME", Folder = "APPNAME", Exe = "APPNAME.exe";
    static string Version { get { return "VERSION"; } }
    static bool quiet;

    [STAThread]
    static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        foreach (var a in args) if (a == "--uninstall") return Uninstall();
        var form = new SetupForm();
        foreach (var a in args)
            if (a.StartsWith("--silent="))   // unattended: install there, no shortcuts, no launch (tests, scripted installs)
            {
                try { quiet = true; Install(a.Substring(9), false, false, delegate { }); return 0; }
                catch (Exception e) { Console.Error.WriteLine(e.Message); return 2; }
            }
        foreach (var a in args)
        {
            if (a.StartsWith("--page=done")) form.ShowDone();
            if (a.StartsWith("--shot="))
            {
                form.CreateControl(); form.Show(); Application.DoEvents();
                using (var bmp = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height)); bmp.Save(a.Substring(7)); }
                return 0;
            }
        }
        Application.Run(form);
        return form.Result;
    }

    // ---------------------------------------------------------------- the install itself (on a worker thread)

    static string StartMenu { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft\\Windows\\Start Menu\\Programs"); } }
    static string Desktop { get { return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory); } }
    const string UninstallKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\APPNAME";

    public static void Install(string dest, bool desktop, bool menu, Action<int, string> progress)
    {
        string tmp = Path.Combine(Path.GetTempPath(), "poc_setup_" + Process.GetCurrentProcess().Id + ".zip");
        byte[] stub;
        try
        {
            progress(0, "Unpacking...");
            using (var self = File.OpenRead(Process.GetCurrentProcess().MainModule.FileName))
            {
                var len = new byte[8];
                self.Seek(-8, SeekOrigin.End);
                self.Read(len, 0, 8);
                long size = BitConverter.ToInt64(len, 0);
                if (size <= 0 || size > self.Length) throw new IOException("this installer has no game inside (is it the stub?)");
                self.Seek(-8 - size, SeekOrigin.End);
                using (var o = File.Create(tmp)) Copy(self, o, size, progress);
                stub = new byte[self.Length - 8 - size];   // the window program alone: kept as the uninstaller
                self.Seek(0, SeekOrigin.Begin);
                self.Read(stub, 0, stub.Length);
            }
            progress(50, "Installing...");
            if (Directory.Exists(dest)) Directory.Delete(dest, true);
            using (var zip = ZipFile.OpenRead(tmp))
            {
                int n = zip.Entries.Count, i = 0;
                foreach (var e in zip.Entries)
                {
                    string path = Path.GetFullPath(Path.Combine(dest, e.FullName));
                    if (!path.StartsWith(Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase)) continue;   // never outside the folder
                    if (e.FullName.EndsWith("/")) Directory.CreateDirectory(path);
                    else { Directory.CreateDirectory(Path.GetDirectoryName(path)); e.ExtractToFile(path, true); }
                    if (++i % 20 == 0) progress(50 + i * 45 / n, "Installing...  " + e.Name);
                }
            }
            string un = Path.Combine(dest, "uninstall.exe");
            File.WriteAllBytes(un, stub);
            progress(97, "Shortcuts...");
            if (desktop) Shortcut(Path.Combine(Desktop, App + ".lnk"), Path.Combine(dest, Exe), dest);
            if (menu) Shortcut(Path.Combine(StartMenu, App + ".lnk"), Path.Combine(dest, Exe), dest);
            // Settings > Apps (per user, no admin); not for --silent test installs
            if (!quiet) try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(UninstallKey))
                {
                    k.SetValue("DisplayName", App);
                    k.SetValue("DisplayVersion", Version);
                    k.SetValue("DisplayIcon", Path.Combine(dest, Exe));
                    k.SetValue("InstallLocation", dest);
                    k.SetValue("UninstallString", "\"" + un + "\" --uninstall");
                    k.SetValue("NoModify", 1); k.SetValue("NoRepair", 1);
                    k.SetValue("EstimatedSize", (int)(Directory.GetFiles(dest, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length) >> 10));
                }
            }
            catch { }
            progress(100, "Done");
        }
        finally { try { File.Delete(tmp); } catch { } }
    }

    /// uninstall.exe --uninstall: asks, removes the shortcuts and the Apps entry, then the folder once this exits.
    static int Uninstall()
    {
        string dir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
        if (!File.Exists(Path.Combine(dir, Exe))) { MessageBox.Show(App + " is not installed here.", App, MessageBoxButtons.OK, MessageBoxIcon.Warning); return 1; }
        if (MessageBox.Show("Remove " + App + " from this PC?\n\nYour save and decks stay (in %APPDATA%\\Godot), so a reinstall picks up where you left off.",
                "Uninstall " + App, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return 1;
        foreach (var f in new[] { Path.Combine(Desktop, App + ".lnk"), Path.Combine(StartMenu, App + ".lnk") }) try { File.Delete(f); } catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); } catch { }
        // the folder holds this exe: a hidden helper removes it two seconds after we exit
        Process.Start(new ProcessStartInfo("cmd.exe", "/c timeout /t 2 /nobreak >nul & rmdir /s /q \"" + dir + "\"")
            { CreateNoWindow = true, UseShellExecute = false, WorkingDirectory = Path.GetTempPath() });
        MessageBox.Show(App + " has been removed.", "Uninstall " + App, MessageBoxButtons.OK, MessageBoxIcon.Information);
        return 0;
    }

    static void Copy(Stream from, Stream to, long n, Action<int, string> progress)
    {
        var buf = new byte[1 << 20];
        long done = 0;
        while (done < n)
        {
            int r = from.Read(buf, 0, (int)Math.Min(buf.Length, n - done));
            if (r <= 0) throw new IOException("the installer is damaged (payload cut short)");
            to.Write(buf, 0, r);
            done += r;
            if ((done >> 20) % 16 == 0) progress((int)(done * 50 / n), "Unpacking...  " + (done >> 20) + " / " + (n >> 20) + " MB");
        }
    }

    static void Shortcut(string lnk, string target, string dir)
    {
        // WScript.Shell through late binding (C# 5, no `dynamic`).
        var t = Type.GetTypeFromProgID("WScript.Shell");
        object shell = Activator.CreateInstance(t);
        object sc = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
        var st = sc.GetType();
        st.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { target });
        st.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { dir });
        st.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, sc, null);
    }

    // ---------------------------------------------------------------- the window

    public static string App_ { get { return App; } }
    public static string Exe_ { get { return Exe; } }
    public static string DefaultDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", Folder); } }
    public static string Ver { get { return Version; } }
}

/// The Chaos Rift look: ink on the right, ivory on the left, a glowing crack between, gold for the main button.
class SetupForm : Form
{
    public int Result = 1;
    static readonly Color Ink = Color.FromArgb(14, 14, 18), Ivory = Color.FromArgb(242, 240, 235), Gold = Color.FromArgb(209, 158, 66),
        GoldLight = Color.FromArgb(255, 217, 128), Mist = Color.FromArgb(160, 164, 178), Graphite = Color.FromArgb(52, 52, 58);
    readonly TextBox folder = new TextBox();
    readonly CheckBox desk = new CheckBox(), menu = new CheckBox(), launch = new CheckBox();
    readonly Label status = new Label();
    readonly Bar bar = new Bar();
    readonly Btn install = new Btn("INSTALL", true), cancel = new Btn("CANCEL", false), browse = new Btn("BROWSE", false);
    Image logo;
    bool done;

    public SetupForm()
    {
        Text = Setup.App_ + " Setup";
        ClientSize = new Size(880, 520);
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Ink; DoubleBuffered = true;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        var s = typeof(SetupForm).Assembly.GetManifestResourceStream("Logo.png");
        if (s != null) logo = Image.FromStream(s);
        var body = new Font("Segoe UI", 10f);
        int x = 400, w = 440;

        Add(new Label { Text = "INSTALL TO", ForeColor = GoldLight, Font = new Font("Segoe UI Semibold", 9f), Location = new Point(x, 150), AutoSize = true, BackColor = Color.Transparent });
        folder.Text = Setup.DefaultDir; folder.Font = body; folder.Location = new Point(x, 174); folder.Width = w - 110;
        folder.BackColor = Color.FromArgb(28, 28, 34); folder.ForeColor = Color.White; folder.BorderStyle = BorderStyle.FixedSingle;
        Add(folder);
        browse.Bounds = new Rectangle(x + w - 100, 172, 100, 30); browse.Click += delegate { Browse(); }; Add(browse);
        Check(desk, "Desktop shortcut", 230); Check(menu, "Start menu shortcut", 262); Check(launch, "Start the game when done", 294);
        bar.Bounds = new Rectangle(x, 392, w, 8); bar.Visible = false; Add(bar);
        status.Location = new Point(x, 406); status.Size = new Size(w, 22); status.ForeColor = Mist; status.Font = new Font("Segoe UI", 9f); status.BackColor = Color.Transparent; Add(status);
        install.Bounds = new Rectangle(x + w - 200, 446, 200, 44); install.Click += delegate { if (done) Play(); else Go(); }; Add(install);
        cancel.Bounds = new Rectangle(x + w - 330, 446, 120, 44); cancel.Click += delegate { Close(); }; Add(cancel);
        AcceptButton = null;
    }

    void Add(Control c) { Controls.Add(c); }

    void Check(CheckBox c, string text, int y)
    {
        c.Text = text; c.Checked = true; c.ForeColor = Color.White; c.Font = new Font("Segoe UI", 10f); c.Location = new Point(400, y); c.AutoSize = true; c.BackColor = Color.Transparent;
        Add(c);
    }

    void Browse()
    {
        string start = Path.GetDirectoryName(folder.Text);
        while (!string.IsNullOrEmpty(start) && !Directory.Exists(start)) start = Path.GetDirectoryName(start);
        string picked;
        try { picked = FolderPicker.Pick(Handle, "Choose where to install " + Setup.App_, start); }
        catch (Exception)   // no modern dialog (very old Windows): the classic folder tree
        {
            picked = null;
            using (var d = new FolderBrowserDialog { Description = "Install " + Setup.App_ + " in:", SelectedPath = start })
                if (d.ShowDialog(this) == DialogResult.OK) picked = d.SelectedPath;
        }
        if (picked != null) folder.Text = Path.Combine(picked, "APPNAME");
    }

    void Go()
    {
        string dest = folder.Text.Trim();
        if (dest.Length == 0) return;
        if (Directory.Exists(dest) && Directory.GetFileSystemEntries(dest).Length > 0 && !File.Exists(Path.Combine(dest, Setup.Exe_)))
        {
            MessageBox.Show(this, "That folder already has other files in it. Pick an empty folder or the one the game is already in.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        install.Enabled = cancel.Enabled = browse.Enabled = folder.Enabled = desk.Enabled = menu.Enabled = false;
        bar.Visible = true;
        bool d1 = desk.Checked, m1 = menu.Checked;
        var t = new Thread(delegate ()
        {
            try
            {
                Setup.Install(dest, d1, m1, delegate (int p, string msg) { BeginInvoke((Action)delegate { bar.Value = p; status.Text = msg; }); });
                BeginInvoke((Action)delegate { Finished(dest); });
            }
            catch (Exception e)
            {
                BeginInvoke((Action)delegate
                {
                    status.Text = ""; bar.Visible = false;
                    MessageBox.Show(this, "Install failed:\n" + e.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    install.Enabled = cancel.Enabled = browse.Enabled = folder.Enabled = desk.Enabled = menu.Enabled = true;
                });
            }
        });
        t.IsBackground = true;
        t.Start();
    }

    string installed;

    void Finished(string dest)
    {
        installed = dest; Result = 0;
        if (launch.Checked) { Play(); return; }
        ShowDone();
    }

    /// The last page: done, PLAY and CLOSE.
    public void ShowDone()
    {
        done = true;
        bar.Value = 100; bar.Visible = true;
        status.Text = "Installed in " + (installed ?? folder.Text);
        install.Text = "PLAY"; install.Enabled = true;
        cancel.Text = "CLOSE"; cancel.Enabled = true;
        folder.Enabled = browse.Enabled = desk.Enabled = menu.Enabled = launch.Enabled = false;
        Invalidate();
    }

    void Play()
    {
        string dir = installed ?? folder.Text;
        try { Process.Start(new ProcessStartInfo(Path.Combine(dir, Setup.Exe_)) { WorkingDirectory = dir }); } catch { }
        Close();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        // ivory on the left, a crack running down, ink on the right
        int top = 340, bottom = 300;
        var rng = new Random(7);
        var crack = new PointF[24];
        for (int i = 0; i < crack.Length; i++)
        {
            float t = i / (float)(crack.Length - 1);
            crack[i] = new PointF(top + (bottom - top) * t + (i == 0 || i == crack.Length - 1 ? 0 : rng.Next(-6, 7)), -2 + (ClientSize.Height + 4) * t);
        }
        // the ivory side runs right up to the crack and past the window edges: no dark sliver anywhere on that side
        var light = new System.Collections.Generic.List<PointF> { new PointF(-4, -4) };
        light.AddRange(crack);
        light.Add(new PointF(-4, ClientSize.Height + 4));
        using (var b = new SolidBrush(Ivory)) g.FillPolygon(b, light.ToArray());
        using (var p = new Pen(Color.FromArgb(70, Gold), 12)) g.DrawLines(p, crack);
        using (var p = new Pen(Color.FromArgb(200, GoldLight), 4)) g.DrawLines(p, crack);
        using (var p = new Pen(Color.White, 1.5f)) g.DrawLines(p, crack);
        if (logo != null)
        {
            float lw = 270, lh = lw * logo.Height / logo.Width;
            g.DrawImage(logo, 30, ClientSize.Height / 2 - lh / 2 - 20, lw, lh);
        }
        using (var f = new Font("Segoe UI", 9f)) using (var b = new SolidBrush(Graphite))
            g.DrawString("VERSION " + Setup.Ver, f, b, 32, ClientSize.Height - 40);
        var title = FontFamilyExists("Perpetua Titling MT") ? new Font("Perpetua Titling MT", 24f, FontStyle.Bold) : new Font("Segoe UI Semibold", 22f);
        using (title) using (var b = new SolidBrush(Color.White)) g.DrawString(done ? "READY TO DUEL" : "SETUP", title, b, 398, 50);
        using (var p = new Pen(Gold, 1.5f)) g.DrawLine(p, 400, 112, 520, 112);
        base.OnPaint(e);
    }

    static bool FontFamilyExists(string name) { foreach (var f in FontFamily.Families) if (f.Name == name) return true; return false; }

    /// Flat buttons: gold filled for the main one, gold outline for the rest.
    class Btn : Button
    {
        readonly bool main;
        public Btn(string text, bool main)
        {
            Text = text; this.main = main;
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            Font = new Font("Segoe UI Semibold", main ? 11f : 9.5f); Cursor = Cursors.Hand;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent.BackColor);
            var r = new Rectangle(1, 1, Width - 3, Height - 3);
            using (var path = Round(r, Height / 2 - 2))
            {
                if (main) using (var b = new SolidBrush(Enabled ? Gold : Color.FromArgb(90, 80, 60))) g.FillPath(b, path);
                using (var p = new Pen(Enabled ? (main ? GoldLight : Gold) : Graphite, 1.5f)) g.DrawPath(p, path);
            }
            TextRenderer.DrawText(g, Text, Font, r, main ? Ink : (Enabled ? GoldLight : Graphite), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        static GraphicsPath Round(Rectangle r, int rad)
        {
            var p = new GraphicsPath(); int d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure(); return p;
        }
    }

    /// A thin gold progress line.
    class Bar : Control
    {
        int v;
        public int Value { get { return v; } set { v = Math.Max(0, Math.Min(100, value)); Invalidate(); } }
        public Bar() { DoubleBuffered = true; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.FromArgb(40, 40, 48));
            using (var b = new SolidBrush(Gold)) e.Graphics.FillRectangle(b, 0, 0, Width * v / 100, Height);
        }
    }
}

/// The Explorer-style "Select Folder" window (Windows Vista+ IFileOpenDialog with FOS_PICKFOLDERS) instead of the old
/// folder tree: address bar, Quick access, search, new folder.
static class FolderPicker
{
    [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")] class FileOpenDialog { }

    [ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IFileOpenDialog
    {
        [PreserveSig] int Show(IntPtr parent);
        void SetFileTypes(uint n, IntPtr specs);
        void SetFileTypeIndex(uint i);
        void GetFileTypeIndex(out uint i);
        void Advise(IntPtr events, out uint cookie);
        void Unadvise(uint cookie);
        void SetOptions(uint fos);
        void GetOptions(out uint fos);
        void SetDefaultFolder(IShellItem item);
        void SetFolder(IShellItem item);
        void GetFolder(out IShellItem item);
        void GetCurrentSelection(out IShellItem item);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
        void GetResult(out IShellItem item);
    }

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItem
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint sigdn, [MarshalAs(UnmanagedType.LPWStr)] out string name);
        void GetAttributes(uint mask, out uint attrs);
        void Compare(IShellItem other, uint hint, out int order);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    static extern void SHCreateItemFromParsingName(string path, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItem item);

    const uint FOS_PICKFOLDERS = 0x20, FOS_FORCEFILESYSTEM = 0x40, FOS_PATHMUSTEXIST = 0x800;
    const uint SIGDN_FILESYSPATH = 0x80058000;
    const int ERROR_CANCELLED = unchecked((int)0x800704C7);

    /// The chosen folder, or null if cancelled. Throws when the modern dialog isn't available.
    public static string Pick(IntPtr owner, string title, string start)
    {
        var d = (IFileOpenDialog)new FileOpenDialog();
        try
        {
            d.SetOptions(FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST);
            d.SetTitle(title);
            d.SetOkButtonLabel("Install here");
            if (!string.IsNullOrEmpty(start))
            {
                IShellItem at;
                try { SHCreateItemFromParsingName(start, IntPtr.Zero, typeof(IShellItem).GUID, out at); d.SetFolder(at); } catch (Exception) { }
            }
            int hr = d.Show(owner);
            if (hr == ERROR_CANCELLED) return null;
            if (hr != 0) Marshal.ThrowExceptionForHR(hr);
            IShellItem result;
            d.GetResult(out result);
            string path;
            result.GetDisplayName(SIGDN_FILESYSPATH, out path);
            return path;
        }
        finally { Marshal.ReleaseComObject(d); }
    }
}
