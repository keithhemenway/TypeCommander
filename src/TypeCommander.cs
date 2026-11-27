// TypeCommander - types text (from a file, the clipboard, or typed in) into the focused window.
// Single file, no dependencies beyond .NET Framework 4.x (built into Windows).
// Written in C# 5 so the csc.exe that ships with Windows can compile it.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace TypeCommander
{
    internal sealed class MainForm : Form
    {
        private const int LargeFileChars = 20000;
        private const int ChunkChars = 8;
        private const int MouseStopPixels = 40;
        private const int MaxKeyDelayMs = 1000;
        private const int HotkeyId = 0x5443;
        private const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, StringBuilder buf, int max);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder buf, int max);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT pt);
        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X; public int Y; }
        private const int VK_ESCAPE = 0x1B;
        private const int VK_PAUSE = 0x13;

        // Windows that should never receive typed text: desktop, taskbar, Explorer.
        private static readonly string[] BlockedClasses = {
            "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "CabinetWClass", "ExploreWClass" };

        private readonly TextBox pathBox = new TextBox();
        private readonly Button browseButton = new Button();
        private readonly NumericUpDown delayBox = new NumericUpDown();
        private readonly NumericUpDown keyDelayBox = new NumericUpDown();
        private readonly CheckBox minimizeBox = new CheckBox();
        private readonly Button pasteButton = new Button();
        private readonly Button clearButton = new Button();
        private readonly TextBox textBox = new TextBox();
        private readonly Label statusLabel = new Label();
        private readonly ProgressBar progressBar = new ProgressBar();
        private readonly Button startButton = new Button();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();

        private int secondsLeft;
        private bool counting;
        private bool hotkeyRegistered;
        private volatile bool stopRequested;
        private volatile bool typing;
        private bool loadingFile;
        private Size savedSize;

        private static readonly string SettingsFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"TypeCommander\settings.ini");

        private static readonly string AppTitle = "TypeCommander v" +
            typeof(MainForm).Assembly.GetName().Version.ToString(3);

        public MainForm(string initialPath)
        {
            Dictionary<string, string> settings = LoadSettings();

            Text = AppTitle;
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(520, 420);
            MinimumSize = new Size(480, 360);
            Font = new Font("Segoe UI", 9F);
            KeyPreview = true;
            AllowDrop = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }

            AnchorStyles topWide = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            AnchorStyles topRight = AnchorStyles.Top | AnchorStyles.Right;
            AnchorStyles bottomWide = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            Label fileLabel = new Label();
            fileLabel.Text = "File:";
            fileLabel.SetBounds(12, 15, 70, 20);

            pathBox.SetBounds(85, 12, 340, 23);
            pathBox.Anchor = topWide;
            pathBox.TextChanged += delegate { LoadFile(false); };

            browseButton.Text = "Browse...";
            browseButton.SetBounds(432, 11, 76, 25);
            browseButton.Anchor = topRight;
            browseButton.Click += delegate { Browse(); };

            Label delayLabel = new Label();
            delayLabel.Text = "Start after:";
            delayLabel.SetBounds(12, 48, 72, 20);

            delayBox.SetBounds(85, 45, 55, 23);
            delayBox.Minimum = 0;
            delayBox.Maximum = 300;
            delayBox.Value = Clamp(GetInt(settings, "delay", 3), 0, 300);

            Label secLabel = new Label();
            secLabel.Text = "sec";
            secLabel.SetBounds(143, 48, 30, 20);

            Label keyDelayLabel = new Label();
            keyDelayLabel.Text = "Key delay:";
            keyDelayLabel.SetBounds(182, 48, 64, 20);

            keyDelayBox.SetBounds(248, 45, 60, 23);
            keyDelayBox.Minimum = 0;
            keyDelayBox.Maximum = MaxKeyDelayMs;
            keyDelayBox.Increment = 10;
            keyDelayBox.Value = Clamp(GetInt(settings, "keyDelayMs", 0), 0, MaxKeyDelayMs);

            Label msLabel = new Label();
            msLabel.Text = "ms";
            msLabel.SetBounds(311, 48, 30, 20);

            ToolTip tips = new ToolTip();
            tips.SetToolTip(keyDelayBox, "Pause between keystrokes. Raise this (e.g. 20-50 ms) if a slow console or KVM viewer drops characters.");
            tips.SetToolTip(delayBox, "Seconds to wait before typing, so you can click into the target window.");

            minimizeBox.Text = "Minimize while waiting";
            minimizeBox.SetBounds(350, 46, 160, 22);
            minimizeBox.Checked = GetInt(settings, "minimize", 0) != 0;

            Label textLabel = new Label();
            textLabel.Text = "Text to type (load a file, paste, or type here):";
            textLabel.SetBounds(12, 80, 330, 20);

            pasteButton.Text = "Paste";
            pasteButton.SetBounds(368, 76, 68, 25);
            pasteButton.Anchor = topRight;
            pasteButton.Click += delegate { PasteClipboard(); };
            tips.SetToolTip(pasteButton, "Replace the text with what's on the clipboard.");

            clearButton.Text = "Clear";
            clearButton.SetBounds(440, 76, 68, 25);
            clearButton.Anchor = topRight;
            clearButton.Click += delegate { pathBox.Text = ""; textBox.Text = ""; textBox.Focus(); };

            textBox.SetBounds(12, 105, 496, 213);
            textBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            textBox.Multiline = true;
            textBox.AcceptsReturn = true;
            textBox.AcceptsTab = true;
            textBox.MaxLength = 0;
            textBox.ScrollBars = ScrollBars.Both;
            textBox.WordWrap = false;
            textBox.Font = new Font("Consolas", 9F);
            textBox.TextChanged += delegate { OnTextChanged(); };

            statusLabel.SetBounds(12, 326, 496, 20);
            statusLabel.Anchor = bottomWide;
            statusLabel.AutoEllipsis = true;

            progressBar.SetBounds(12, 349, 496, 14);
            progressBar.Anchor = bottomWide;
            progressBar.Minimum = 0;
            progressBar.Maximum = 1000;

            startButton.Text = "Start typing";
            startButton.SetBounds(12, 373, 496, 34);
            startButton.Anchor = bottomWide;
            startButton.Click += delegate { if (counting) RequestStop(); else Begin(); };

            timer.Interval = 1000;
            timer.Tick += delegate { Tick(); };

            Controls.AddRange(new Control[] {
                fileLabel, pathBox, browseButton, delayLabel, delayBox, secLabel,
                keyDelayLabel, keyDelayBox, msLabel, minimizeBox,
                textLabel, pasteButton, clearButton, textBox,
                statusLabel, progressBar, startButton });

            KeyDown += OnKeyDown;
            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;

            savedSize = new Size(GetInt(settings, "width", 0), GetInt(settings, "height", 0));

            // A file on the command line wins; otherwise reopen the last file if it still exists.
            string lastFile;
            if (string.IsNullOrEmpty(initialPath) && settings.TryGetValue("lastFile", out lastFile) && File.Exists(lastFile))
                initialPath = lastFile;
            if (!string.IsNullOrEmpty(initialPath)) pathBox.Text = initialPath;
            else OnTextChanged();
        }

        // The saved size is in real pixels, so apply it after DPI scaling has run.
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (savedSize.Width >= MinimumSize.Width && savedSize.Height >= MinimumSize.Height)
            {
                Rectangle area = Screen.FromControl(this).WorkingArea;
                Size = new Size(Math.Min(savedSize.Width, area.Width), Math.Min(savedSize.Height, area.Height));
                CenterToScreen();
            }
        }

        private void Browse()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Choose the file to type";
                dlg.Filter = "All files (*.*)|*.*";
                try
                {
                    string dir = Path.GetDirectoryName(pathBox.Text.Trim());
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) dlg.InitialDirectory = dir;
                }
                catch (ArgumentException) { }
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    if (dlg.FileName == pathBox.Text) LoadFile(true);
                    else pathBox.Text = dlg.FileName;
                }
            }
        }

        // Loads the file named in the path box into the editor. While the path is
        // being typed and doesn't exist yet, the editor is left alone.
        private void LoadFile(bool reportMissing)
        {
            string path = pathBox.Text.Trim();
            if (path.Length == 0) return;
            if (!File.Exists(path))
            {
                if (reportMissing) SetStatus("File not found: " + path);
                return;
            }
            try
            {
                string text = File.ReadAllText(path);
                string problem = CheckText(text);
                if (problem != null)
                {
                    SetStatus(problem);
                    startButton.Enabled = false;
                    return;
                }
                loadingFile = true;
                try { textBox.Text = text.Replace("\r\n", "\n").Replace("\n", "\r\n"); }
                finally { loadingFile = false; }
                OnTextChanged();
            }
            catch (Exception ex)
            {
                SetStatus("Can't read file: " + ex.Message);
                startButton.Enabled = false;
            }
        }

        private void PasteClipboard()
        {
            string text = null;
            try { if (Clipboard.ContainsText()) text = Clipboard.GetText(); }
            catch (ExternalException) { }
            if (string.IsNullOrEmpty(text)) { SetStatus("The clipboard has no text."); return; }
            pathBox.Text = "";
            textBox.Text = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
            textBox.Focus();
        }

        private void OnTextChanged()
        {
            if (loadingFile) return;
            string text = textBox.Text;
            string problem = CheckText(text);
            if (problem != null)
            {
                SetStatus(problem);
                startButton.Enabled = false;
                return;
            }
            if (text.Length == 0)
            {
                SetStatus("Pick a file, paste, or type the text to send.");
                startButton.Enabled = false;
                return;
            }
            int lines = 1;
            foreach (char c in text) if (c == '\n') lines++;
            if (text.EndsWith("\n")) lines--;
            SetStatus(string.Format("{0:N0} characters, {1:N0} lines. Click Start, then click into the target window.", text.Length, lines));
            startButton.Enabled = true;
        }

        private void Begin()
        {
            OnTextChanged();
            if (!startButton.Enabled) return;
            if (textBox.TextLength > LargeFileChars &&
                MessageBox.Show(this, string.Format("This text is {0:N0} characters. Type all of it?", textBox.TextLength),
                    "TypeCommander", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            secondsLeft = (int)delayBox.Value;
            counting = true;
            stopRequested = false;
            SetInputsEnabled(false);
            startButton.Text = "Cancel (Esc)";
            // Esc works from any window while counting down or typing. Text never
            // contains a real Esc keystroke, so any Esc press is the user's.
            hotkeyRegistered = RegisterHotKey(Handle, HotkeyId, 0, VK_ESCAPE);

            if (secondsLeft == 0) { Fire(); return; }

            UpdateCountdown();
            if (minimizeBox.Checked) WindowState = FormWindowState.Minimized;
            timer.Start();
        }

        private void Tick()
        {
            if (stopRequested) { Cancel("Cancelled."); return; }
            secondsLeft--;
            if (secondsLeft <= 0) { timer.Stop(); Fire(); }
            else UpdateCountdown();
        }

        private void UpdateCountdown()
        {
            string msg = string.Format("Typing in {0}...", secondsLeft);
            Text = msg + " - " + AppTitle;
            SetStatus(msg + " Click into the target window now. Esc cancels.");
        }

        private void Fire()
        {
            string text = textBox.Text;
            int keyDelay = (int)keyDelayBox.Value;

            string problem = CheckText(text);
            if (problem != null) { Finish(problem); return; }

            IntPtr target = GetForegroundWindow();
            string why = CheckTarget(target);
            if (why != null) { Finish("Not typed: " + why); return; }

            if (stopRequested) { Finish("Cancelled."); return; }
            typing = true;
            progressBar.Value = 0;
            SetStatus("Typing... press Esc, Pause, or move the mouse to stop.");
            Text = "Typing - " + AppTitle;

            Thread worker = new Thread(delegate() { TypeText(text, target, keyDelay); });
            worker.IsBackground = true;
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
        }

        // Runs on a background thread so the window, Cancel button and Esc
        // hotkey stay responsive. Checks for a stop between small chunks.
        private void TypeText(string text, IntPtr target, int keyDelay)
        {
            string result;
            int typed = 0;
            try
            {
                POINT start;
                GetCursorPos(out start);
                result = null;
                int lastUpdate = Environment.TickCount;
                foreach (string chunk in Chunks(text, keyDelay > 0 ? 1 : ChunkChars))
                {
                    if (stopRequested || (GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0 || (GetAsyncKeyState(VK_PAUSE) & 0x8000) != 0)
                    { result = "Stopped after {0} of {1} characters."; break; }
                    POINT now;
                    GetCursorPos(out now);
                    if (Math.Abs(now.X - start.X) > MouseStopPixels || Math.Abs(now.Y - start.Y) > MouseStopPixels)
                    { result = "Stopped (mouse moved) after {0} of {1} characters."; break; }
                    if (GetForegroundWindow() != target)
                    { result = "Stopped: target window lost focus after {0} of {1} characters."; break; }
                    SendKeys.SendWait(Escape(chunk));
                    typed += chunk.Length;
                    if (keyDelay > 0) Thread.Sleep(keyDelay);
                    if (Environment.TickCount - lastUpdate >= 50)
                    {
                        lastUpdate = Environment.TickCount;
                        int done = typed, total = text.Length;
                        BeginInvoke((MethodInvoker)delegate { ShowProgress(done, total); });
                    }
                }
                if (result == null) result = "Done. Typed {0} characters into \"" + WindowTitle(target).Replace("{", "{{").Replace("}", "}}") + "\".";
                result = string.Format(result, typed.ToString("N0"), text.Length.ToString("N0"));
            }
            catch (Exception ex)
            {
                result = "Failed: " + ex.Message;
            }
            string msg = result;
            int finalTyped = typed, finalTotal = text.Length;
            try { BeginInvoke((MethodInvoker)delegate { typing = false; ShowProgress(finalTyped, finalTotal); Finish(msg); }); }
            catch (InvalidOperationException) { }
        }

        private void ShowProgress(int done, int total)
        {
            int permille = total == 0 ? 1000 : (int)(1000L * done / total);
            progressBar.Value = Math.Max(0, Math.Min(1000, permille));
            if (typing) Text = string.Format("Typing {0}% - {1}", permille / 10, AppTitle);
        }

        private void RequestStop()
        {
            stopRequested = true;
            if (!typing) Cancel("Cancelled.");
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HotkeyId) { RequestStop(); return; }
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            stopRequested = true;
            if (hotkeyRegistered) { UnregisterHotKey(Handle, HotkeyId); hotkeyRegistered = false; }
            SaveSettings();
            base.OnFormClosing(e);
        }

        // Rejects binary or non-text files. Control characters would turn into
        // Ctrl/Alt key combinations and can launch things.
        internal static string CheckText(string text)
        {
            int bad = 0;
            foreach (char c in text)
                if (c < 32 && c != '\t' && c != '\r' && c != '\n') bad++;
            if (bad > 0)
                return string.Format("This doesn't look like a plain text file ({0:N0} control characters). Not typing it.", bad);
            return null;
        }

        private static string CheckTarget(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return "no window has focus.";
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            if (pid == (uint)Process.GetCurrentProcess().Id) return "TypeCommander itself had focus. Click into the target window during the countdown.";
            StringBuilder cls = new StringBuilder(256);
            GetClassName(hwnd, cls, cls.Capacity);
            if (Array.IndexOf(BlockedClasses, cls.ToString()) >= 0)
                return "the desktop, taskbar or File Explorer had focus. Click into the target window during the countdown.";
            return null;
        }

        private static string WindowTitle(IntPtr hwnd)
        {
            StringBuilder sb = new StringBuilder(256);
            GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        // Splits text into small pieces (never splitting a CRLF) so typing can be
        // stopped between pieces.
        private static IEnumerable<string> Chunks(string text, int size)
        {
            int i = 0;
            while (i < text.Length)
            {
                int len = Math.Min(size, text.Length - i);
                if (i + len < text.Length && text[i + len - 1] == '\r' && text[i + len] == '\n') len++;
                yield return text.Substring(i, len);
                i += len;
            }
        }

        // Same rules as the original PowerShell script: escape SendKeys
        // special characters, then turn line breaks into {ENTER}.
        internal static string Escape(string text)
        {
            string s = Regex.Replace(text, @"([+^%~(){}\[\]])", "{$1}");
            return s.Replace("\r\n", "{ENTER}").Replace("\n", "{ENTER}").Replace("\r", "{ENTER}");
        }

        private void Cancel(string msg)
        {
            timer.Stop();
            Finish(msg);
        }

        private void Finish(string msg)
        {
            if (hotkeyRegistered) { UnregisterHotKey(Handle, HotkeyId); hotkeyRegistered = false; }
            counting = false;
            Text = AppTitle;
            startButton.Text = "Start typing";
            SetInputsEnabled(true);
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            SetStatus(msg);
        }

        private void SetInputsEnabled(bool on)
        {
            pathBox.Enabled = on;
            browseButton.Enabled = on;
            delayBox.Enabled = on;
            keyDelayBox.Enabled = on;
            minimizeBox.Enabled = on;
            pasteButton.Enabled = on;
            clearButton.Enabled = on;
            textBox.ReadOnly = !on;
        }

        private void SetStatus(string msg) { statusLabel.Text = msg; }

        // Settings live in %APPDATA%\TypeCommander\settings.ini as key=value lines.
        // Failing to read or write them is never an error.
        private static Dictionary<string, string> LoadSettings()
        {
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(SettingsFile)) return d;
                foreach (string line in File.ReadAllLines(SettingsFile))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0) d[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
            }
            catch (Exception) { }
            return d;
        }

        private void SaveSettings()
        {
            try
            {
                Size size = WindowState == FormWindowState.Normal ? Size : RestoreBounds.Size;
                string path = pathBox.Text.Trim();
                string[] lines = {
                    "lastFile=" + (path.Length > 0 && File.Exists(path) ? Path.GetFullPath(path) : ""),
                    "delay=" + (int)delayBox.Value,
                    "keyDelayMs=" + (int)keyDelayBox.Value,
                    "minimize=" + (minimizeBox.Checked ? 1 : 0),
                    "width=" + size.Width,
                    "height=" + size.Height };
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile));
                File.WriteAllLines(SettingsFile, lines);
            }
            catch (Exception) { }
        }

        private static int GetInt(Dictionary<string, string> d, string key, int fallback)
        {
            string v;
            int n;
            return d.TryGetValue(key, out v) && int.TryParse(v, out n) ? n : fallback;
        }

        private static int Clamp(int v, int lo, int hi) { return Math.Max(lo, Math.Min(hi, v)); }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && counting) { RequestStop(); e.Handled = true; }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (!counting && e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0) return;
            if (files[0] == pathBox.Text) LoadFile(true);
            else pathBox.Text = files[0];
        }

        [STAThread]
        private static void Main(string[] args)
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e) { ShowError(e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { ShowError(e.ExceptionObject as Exception); };
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm(args.Length > 0 ? args[0] : null));
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private static void ShowError(Exception ex)
        {
            MessageBox.Show("TypeCommander hit an error:\r\n\r\n" + (ex == null ? "Unknown error" : ex.ToString()),
                "TypeCommander", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
