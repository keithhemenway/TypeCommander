// TypeCommander - types the contents of a text file into the focused window.
// Single file, no dependencies beyond .NET Framework 4.x (built into Windows).
// Written in C# 5 so the csc.exe that ships with Windows can compile it.
using System;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace TypeCommander
{
    internal sealed class MainForm : Form
    {
        private const string DefaultFile = @"C:\temp\keys.txt";

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

            minimizeBox.Text = "Minimize while waiting (returns focus to previous window)";
            minimizeBox.SetBounds(160, 46, 290, 22);
            minimizeBox.Checked = true;

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

            secondsLeft = (int)delayBox.Value;
            counting = true;
            SetInputsEnabled(false);
            startButton.Text = "Cancel (Esc)";

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
                SendKeys.SendWait(Escape(text));
                Finish("Done. Typed " + text.Length.ToString("N0") + " characters.");
            }
            catch (Exception ex)
            {
                Finish("Failed: " + ex.Message);
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
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(args.Length > 0 ? args[0] : DefaultFile));
        }
    }
}
