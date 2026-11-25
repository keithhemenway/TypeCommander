// TypeCommander - types the contents of a text file into the focused window.
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
using System.Windows.Forms;

namespace TypeCommander
{
    internal sealed class MainForm : Form
    {
        private const string DefaultFile = @"C:\temp\keys.txt";
        private const int LargeFileChars = 20000;
        private const int ChunkChars = 40;

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, StringBuilder buf, int max);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder buf, int max);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
        private const int VK_ESCAPE = 0x1B;

        // Windows that should never receive typed text: desktop, taskbar, Explorer.
        private static readonly string[] BlockedClasses = {
            "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "CabinetWClass", "ExploreWClass" };

        private readonly TextBox pathBox = new TextBox();
        private readonly Button browseButton = new Button();
        private readonly NumericUpDown delayBox = new NumericUpDown();
        private readonly CheckBox minimizeBox = new CheckBox();
        private readonly TextBox previewBox = new TextBox();
        private readonly Label statusLabel = new Label();
        private readonly Button startButton = new Button();
        private readonly Timer timer = new Timer();

        private int secondsLeft;
        private bool counting;

        private static readonly string AppTitle = "TypeCommander v" +
            typeof(MainForm).Assembly.GetName().Version.ToString(3);

        public MainForm(string initialPath)
        {
            Text = AppTitle;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(460, 330);
            Font = new Font("Segoe UI", 9F);
            KeyPreview = true;
            AllowDrop = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }

            Label fileLabel = new Label();
            fileLabel.Text = "Text file:";
            fileLabel.SetBounds(12, 15, 70, 20);

            pathBox.SetBounds(85, 12, 280, 23);
            pathBox.Text = initialPath;
            pathBox.TextChanged += delegate { LoadPreview(); };

            browseButton.Text = "Browse...";
            browseButton.SetBounds(372, 11, 76, 25);
            browseButton.Click += delegate { Browse(); };

            Label delayLabel = new Label();
            delayLabel.Text = "Delay (sec):";
            delayLabel.SetBounds(12, 48, 72, 20);

            delayBox.SetBounds(85, 45, 60, 23);
            delayBox.Minimum = 0;
            delayBox.Maximum = 300;
            delayBox.Value = 3;

            minimizeBox.Text = "Minimize while waiting";
            minimizeBox.SetBounds(160, 46, 290, 22);
            minimizeBox.Checked = false;

            previewBox.SetBounds(12, 78, 436, 170);
            previewBox.Multiline = true;
            previewBox.ReadOnly = true;
            previewBox.ScrollBars = ScrollBars.Both;
            previewBox.WordWrap = false;
            previewBox.Font = new Font("Consolas", 9F);
            previewBox.BackColor = SystemColors.Window;

            statusLabel.SetBounds(12, 256, 436, 20);

            startButton.Text = "Start typing";
            startButton.SetBounds(12, 285, 436, 34);
            startButton.Click += delegate { if (counting) Cancel("Cancelled."); else Begin(); };

            timer.Interval = 1000;
            timer.Tick += delegate { Tick(); };

            Controls.AddRange(new Control[] {
                fileLabel, pathBox, browseButton, delayLabel, delayBox,
                minimizeBox, previewBox, statusLabel, startButton });

            AcceptButton = startButton;
            KeyDown += OnKeyDown;
            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;

            LoadPreview();
        }

        private void Browse()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Choose the text to type";
                dlg.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
                try
                {
                    string dir = Path.GetDirectoryName(pathBox.Text);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) dlg.InitialDirectory = dir;
                }
                catch (ArgumentException) { }
                if (dlg.ShowDialog(this) == DialogResult.OK) pathBox.Text = dlg.FileName;
            }
        }

        private void LoadPreview()
        {
            string path = pathBox.Text.Trim();
            if (path.Length == 0 || !File.Exists(path))
            {
                previewBox.Text = "";
                SetStatus("File not found. Pick a text file to type.");
                startButton.Enabled = false;
                return;
            }
            try
            {
                string text = File.ReadAllText(path);
                string problem = CheckText(text);
                if (problem != null)
                {
                    previewBox.Text = "";
                    SetStatus(problem);
                    startButton.Enabled = false;
                    return;
                }
                previewBox.Text = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
                int lines = text.Length == 0 ? 0 : text.Split('\n').Length;
                SetStatus(string.Format("{0:N0} characters, {1:N0} lines. Click Start, then click into the target window.", text.Length, lines));
                startButton.Enabled = text.Length > 0;
            }
            catch (Exception ex)
            {
                previewBox.Text = "";
                SetStatus("Can't read file: " + ex.Message);
                startButton.Enabled = false;
            }
        }

        private void Begin()
        {
            LoadPreview();
            if (!startButton.Enabled) return;
            if (previewBox.TextLength > LargeFileChars &&
                MessageBox.Show(this, string.Format("This file is {0:N0} characters. Type all of it?", previewBox.TextLength),
                    "TypeCommander", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            secondsLeft = (int)delayBox.Value;
            counting = true;
            SetInputsEnabled(false);
            startButton.Text = "Cancel (Esc)";
            SetStatus("Click into the target window. Hold Esc to stop typing.");

            if (secondsLeft == 0) { Fire(); return; }

            UpdateCountdown();
            if (minimizeBox.Checked) WindowState = FormWindowState.Minimized;
            timer.Start();
        }

        private void Tick()
        {
            secondsLeft--;
            if (secondsLeft <= 0) { timer.Stop(); Fire(); }
            else UpdateCountdown();
        }

        private void UpdateCountdown()
        {
            string msg = string.Format("Typing in {0}...", secondsLeft);
            Text = msg + " - " + AppTitle;
            SetStatus(msg + " Click into the target window now.");
        }

        private void Fire()
        {
            try
            {
                string text = File.ReadAllText(pathBox.Text.Trim());
                string problem = CheckText(text);
                if (problem != null) { Finish(problem); return; }

                IntPtr target = GetForegroundWindow();
                string why = CheckTarget(target);
                if (why != null) { Finish("Not typed: " + why); return; }

                int typed = 0;
                foreach (string chunk in Chunks(text))
                {
                    if ((GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0) { Finish("Stopped (Esc) after " + typed.ToString("N0") + " characters."); return; }
                    if (GetForegroundWindow() != target) { Finish("Stopped: the target window lost focus after " + typed.ToString("N0") + " characters."); return; }
                    SendKeys.SendWait(Escape(chunk));
                    typed += chunk.Length;
                }
                Finish("Done. Typed " + typed.ToString("N0") + " characters into \"" + WindowTitle(target) + "\".");
            }
            catch (Exception ex)
            {
                Finish("Failed: " + ex.Message);
            }
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
        private static IEnumerable<string> Chunks(string text)
        {
            int i = 0;
            while (i < text.Length)
            {
                int len = Math.Min(ChunkChars, text.Length - i);
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
            minimizeBox.Enabled = on;
        }

        private void SetStatus(string msg) { statusLabel.Text = msg; }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && counting) { Cancel("Cancelled."); e.Handled = true; }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (!counting && e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0) pathBox.Text = files[0];
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
                Application.Run(new MainForm(args.Length > 0 ? args[0] : DefaultFile));
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
