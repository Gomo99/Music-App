using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MP3_Player
{
    // ════════════════════════════════════════════════════════════════════
    //  DESIGN TOKENS  —  warm amber / analog dark (theme support)
    // ════════════════════════════════════════════════════════════════════
    internal static class DS
    {
        private static Color _accent = Color.FromArgb(228, 158, 28);
        private static Color _accentDim = Color.FromArgb(88, 58, 10);
        private static Color _accentGlow = Color.FromArgb(55, 228, 158, 28);

        public static Color Accent
        {
            get => _accent;
            set => _accent = value;
        }
        public static Color AccentDim
        {
            get => _accentDim;
            set => _accentDim = value;
        }
        public static Color AccentGlow
        {
            get => _accentGlow;
            set => _accentGlow = value;
        }

        public static readonly Color BG = Color.FromArgb(13, 12, 11);
        public static readonly Color Surface1 = Color.FromArgb(20, 19, 17);
        public static readonly Color Surface2 = Color.FromArgb(30, 28, 24);
        public static readonly Color Surface3 = Color.FromArgb(44, 40, 34);
        public static readonly Color Border = Color.FromArgb(60, 54, 44);
        public static readonly Color Danger = Color.FromArgb(200, 68, 68);
        public static readonly Color TextPri = Color.FromArgb(238, 232, 218);
        public static readonly Color TextMuted = Color.FromArgb(108, 98, 84);
        public static readonly Color SelBg = Color.FromArgb(38, 35, 28);

        public static readonly Font FTitle = new Font("Segoe UI", 16f, FontStyle.Bold);
        public static readonly Font FArtist = new Font("Segoe UI", 10f);
        public static readonly Font FSmall = new Font("Segoe UI", 8.5f);
        public static readonly Font FTiny = new Font("Segoe UI", 8f);
        public static readonly Font FMono = new Font("Lucida Console", 9f);
        public static readonly Font FMonoLg = new Font("Lucida Console", 17f, FontStyle.Bold);
        public static readonly Font FMonoSm = new Font("Lucida Console", 7.5f);
        public static readonly Font FIcon = new Font("Segoe UI Symbol", 14f);
        public static readonly Font FIconSm = new Font("Segoe UI Symbol", 11f);
        public static readonly Font FLabel = new Font("Segoe UI", 7f, FontStyle.Bold);
        public static readonly Font FList = new Font("Segoe UI", 9.5f);
        public static readonly Font FListB = new Font("Segoe UI", 9.5f, FontStyle.Bold);

        public static GraphicsPath RR(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            if (rad <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, rad * 2, rad * 2, 180, 90);
            p.AddArc(r.Right - rad * 2, r.Y, rad * 2, rad * 2, 270, 90);
            p.AddArc(r.Right - rad * 2, r.Bottom - rad * 2, rad * 2, rad * 2, 0, 90);
            p.AddArc(r.X, r.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void SetThemeFromAccent(Color accent)
        {
            Accent = accent;
            AccentDim = Color.FromArgb(Clamp(accent.R * 0.35f), Clamp(accent.G * 0.35f), Clamp(accent.B * 0.35f));
            AccentGlow = Color.FromArgb(55, accent.R, accent.G, accent.B);
        }

        private static byte Clamp(float v) => (byte)Math.Max(0, Math.Min(255, v + 0.5f));
    }

    // ════════════════════════════════════════════════════════════════════
    //  THEME SETTINGS
    // ════════════════════════════════════════════════════════════════════
    internal static class ThemeSettings
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TAPE_Player", "theme.cfg");

        public static void Save(int themeIndex, Color accent)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, $"{themeIndex}\n{accent.ToArgb()}");
            }
            catch { }
        }

        public static (int themeIndex, Color accent) Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var lines = File.ReadAllLines(FilePath);
                    if (lines.Length >= 2 && int.TryParse(lines[0], out int idx) && int.TryParse(lines[1], out int argb))
                        return (idx, Color.FromArgb(argb));
                }
            }
            catch { }
            return (0, Color.FromArgb(228, 158, 28));
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  MINI PLAYER FORM
    // ════════════════════════════════════════════════════════════════════
    internal class MiniPlayerForm : Form
    {
        private Form1 mainForm;
        private PictureBox picArt;
        private Label lblTitle, lblArtist;
        private GlowCircle btnPlayPause;
        private FlatIcon btnPrev, btnNext, btnExpand;

        public MiniPlayerForm(Form1 owner)
        {
            this.mainForm = owner;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(360, 70);
            this.StartPosition = FormStartPosition.Manual;
            var screen = Screen.PrimaryScreen.WorkingArea;
            this.Location = new Point(screen.Right - this.Width - 30, 30);
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.BackColor = DS.BG;
            this.Padding = new Padding(4);
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            BuildMiniUI();
            this.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, 0x2, 0); } };
        }

        private void BuildMiniUI()
        {
            var mainPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            mainPanel.Paint += (s, e) =>
            {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(DS.Surface2))
                using (var path = DS.RR(new RectangleF(0, 0, mainPanel.Width - 1, mainPanel.Height - 1), 10))
                    g.FillPath(brush, path);
                using (var pen = new Pen(DS.Accent, 2f))
                using (var path = DS.RR(new RectangleF(1, 1, mainPanel.Width - 2, mainPanel.Height - 2), 10))
                    g.DrawPath(pen, path);
            };
            picArt = new PictureBox { Size = new Size(46, 46), Location = new Point(10, 12), SizeMode = PictureBoxSizeMode.Zoom, BackColor = DS.Surface2 };
            lblTitle = new Label { Location = new Point(64, 10), Size = new Size(150, 18), Text = "No Track", ForeColor = DS.TextPri, Font = new Font("Segoe UI", 9f, FontStyle.Bold), AutoEllipsis = true, BackColor = Color.Transparent };
            lblArtist = new Label { Location = new Point(64, 30), Size = new Size(150, 16), Text = "Artist", ForeColor = DS.Accent, Font = new Font("Segoe UI", 8f), AutoEllipsis = true, BackColor = Color.Transparent };
            btnPrev = new FlatIcon { Text = "⏮", Location = new Point(224, 16), Size = new Size(30, 30), Font = DS.FIconSm, Fg = DS.TextPri };
            btnPrev.Click += (s, e) => mainForm.Previous();
            btnPlayPause = new GlowCircle { Text = "▶", Location = new Point(258, 8), Size = new Size(42, 42), Font = new Font("Segoe UI Symbol", 14f), ForeColor = Color.Black, Bg = DS.Accent, Glow = DS.AccentGlow };
            btnPlayPause.Click += (s, e) => mainForm.PlayPauseMini();
            btnNext = new FlatIcon { Text = "⏭", Location = new Point(304, 16), Size = new Size(30, 30), Font = DS.FIconSm, Fg = DS.TextPri };
            btnNext.Click += (s, e) => mainForm.Next();
            btnExpand = new FlatIcon { Text = "⤢", Location = new Point(334, 24), Size = new Size(18, 18), Font = new Font("Segoe UI Symbol", 10f), Fg = DS.TextMuted };
            btnExpand.Click += (s, e) => mainForm.ToggleMiniPlayer();
            mainPanel.Controls.AddRange(new Control[] { picArt, lblTitle, lblArtist, btnPrev, btnPlayPause, btnNext, btnExpand });
            this.Controls.Add(mainPanel);
        }

        public void UpdateTrack(string title, string artist, Image art)
        {
            if (lblTitle.Text != title) lblTitle.Text = title;
            if (lblArtist.Text != artist) lblArtist.Text = artist;
            if (art != null) picArt.Image = new Bitmap(art, 46, 46);
            else
            {
                if (picArt.Image == null || picArt.Image.Size != new Size(46, 46))
                {
                    using (var bmp = new Bitmap(46, 46))
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.Clear(DS.Surface2);
                        using (var b = new SolidBrush(DS.Border))
                        using (var f = new Font("Segoe UI Symbol", 20f))
                            g.DrawString("♪", f, b, new RectangleF(0, 0, 46, 46),
                                         new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                        picArt.Image = new Bitmap(bmp);
                    }
                }
            }
        }

        public void UpdatePlayPauseButton(bool playing)
        {
            btnPlayPause.Text = playing ? "⏸" : "▶";
            btnPlayPause.Invalidate();
        }

        public void ApplyCurrentTheme()
        {
            btnPlayPause.Bg = DS.Accent;
            btnPlayPause.Glow = DS.AccentGlow;
            btnPlayPause.Invalidate();
            lblArtist.ForeColor = DS.Accent;
            this.Invalidate(true);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
    }

    // ════════════════════════════════════════════════════════════════════
    //  TOAST NOTIFICATION FORM
    // ════════════════════════════════════════════════════════════════════
    internal class ToastNotification : Form
    {
        private Timer fadeTimer;
        private int fadePhase = 0, tickCount = 0;
        private const int FadeInSteps = 10, HoldTicks = 30, FadeOutSteps = 10;

        public ToastNotification(string title, string artist, Image albumArt)
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(260, 70);
            this.StartPosition = FormStartPosition.Manual;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.BackColor = DS.Surface2;
            this.Opacity = 0;
            this.Padding = new Padding(4);
            var screen = Screen.PrimaryScreen.WorkingArea;
            this.Location = new Point(screen.Right - this.Width - 20, screen.Bottom - this.Height - 20);
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            BuildContent(title, artist, albumArt);
            fadeTimer = new Timer { Interval = 50 };
            fadeTimer.Tick += FadeTimer_Tick;
            fadeTimer.Start();
        }

        private void BuildContent(string title, string artist, Image albumArt)
        {
            var mainPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            mainPanel.Paint += (s, e) =>
            {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(DS.Surface2))
                using (var path = DS.RR(new RectangleF(0, 0, mainPanel.Width - 1, mainPanel.Height - 1), 10))
                    g.FillPath(brush, path);
                using (var pen = new Pen(DS.Accent, 2f))
                using (var path = DS.RR(new RectangleF(1, 1, mainPanel.Width - 2, mainPanel.Height - 2), 10))
                    g.DrawPath(pen, path);
            };
            var picArt = new PictureBox { Size = new Size(48, 48), Location = new Point(12, 11), SizeMode = PictureBoxSizeMode.Zoom, Image = albumArt, BackColor = DS.Surface2 };
            if (albumArt == null)
            {
                using (var bmp = new Bitmap(48, 48))
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(DS.Surface2);
                    using (var b = new SolidBrush(DS.Border))
                    using (var f = new Font("Segoe UI Symbol", 24f))
                        g.DrawString("♪", f, b, new RectangleF(0, 0, 48, 48), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    picArt.Image = new Bitmap(bmp);
                }
            }
            var lblTitle = new Label { Location = new Point(72, 12), Size = new Size(176, 18), Text = title, ForeColor = DS.TextPri, Font = new Font("Segoe UI", 9f, FontStyle.Bold), AutoEllipsis = true, BackColor = Color.Transparent };
            var lblArtist = new Label { Location = new Point(72, 32), Size = new Size(176, 16), Text = artist, ForeColor = DS.Accent, Font = new Font("Segoe UI", 8f), AutoEllipsis = true, BackColor = Color.Transparent };
            mainPanel.Controls.AddRange(new Control[] { picArt, lblTitle, lblArtist });
            this.Controls.Add(mainPanel);
        }

        private void FadeTimer_Tick(object sender, EventArgs e)
        {
            tickCount++;
            if (fadePhase == 0)
            {
                this.Opacity = Math.Min(0.95, (double)tickCount / FadeInSteps * 0.95);
                if (tickCount >= FadeInSteps) { fadePhase = 1; tickCount = 0; }
            }
            else if (fadePhase == 1)
            {
                if (tickCount >= HoldTicks) { fadePhase = 2; tickCount = 0; }
            }
            else
            {
                this.Opacity = Math.Max(0, 0.95 - (double)tickCount / FadeOutSteps * 0.95);
                if (tickCount >= FadeOutSteps) { fadeTimer?.Stop(); this.Close(); }
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e) { fadeTimer?.Dispose(); base.OnFormClosed(e); }
        protected override bool ShowWithoutActivation => true;
    }

    // ════════════════════════════════════════════════════════════════════
    //  CROSSFADE PROVIDER
    // ════════════════════════════════════════════════════════════════════
    internal class CrossFadeProvider : ISampleProvider
    {
        public AudioFileReader CurrentReader { get; private set; }
        public AudioFileReader NextReader { get; private set; }
        public bool IsCrossfading { get; private set; }

        private float currentGain = 1f, nextGain = 0f;
        private int crossfadeSamples, crossfadeElapsed;
        private readonly object lockObj = new object();
        public event Action CrossfadeCompleted;

        public WaveFormat WaveFormat => CurrentReader?.WaveFormat;

        public CrossFadeProvider(AudioFileReader initialReader) { CurrentReader = initialReader; }

        public void SetNextReader(AudioFileReader next, float durationSeconds)
        {
            lock (lockObj)
            {
                if (IsCrossfading) return;
                NextReader = next;
                IsCrossfading = true;
                currentGain = 1f; nextGain = 0f;
                crossfadeSamples = (int)(durationSeconds * WaveFormat.SampleRate * WaveFormat.Channels);
                crossfadeElapsed = 0;
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            lock (lockObj)
            {
                if (CurrentReader == null) return 0;
                int samplesRead = CurrentReader.Read(buffer, offset, count);
                if (IsCrossfading && NextReader != null)
                {
                    float[] nextBuf = new float[count];
                    int nextRead = NextReader.Read(nextBuf, 0, count);
                    if (nextRead == 0) { FinishCrossfade(); return samplesRead; }
                    int max = Math.Max(samplesRead, nextRead);
                    for (int i = 0; i < max; i++)
                    {
                        if (crossfadeElapsed >= crossfadeSamples) { FinishCrossfade(); return max; }
                        float progress = (float)crossfadeElapsed / crossfadeSamples;
                        currentGain = 1f - progress; nextGain = progress;
                        crossfadeElapsed++;
                        float sA = i < samplesRead ? buffer[offset + i] : 0;
                        float sB = i < nextRead ? nextBuf[i] : 0;
                        buffer[offset + i] = sA * currentGain + sB * nextGain;
                    }
                    if (crossfadeElapsed >= crossfadeSamples) FinishCrossfade();
                    return Math.Max(samplesRead, nextRead);
                }
                return samplesRead;
            }
        }

        private void FinishCrossfade()
        {
            IsCrossfading = false;
            var old = CurrentReader;
            CurrentReader = NextReader;
            NextReader = null;
            old?.Dispose();
            crossfadeElapsed = 0;
            CrossfadeCompleted?.Invoke();
        }

        public void AbortCrossfade()
        {
            lock (lockObj)
            {
                if (!IsCrossfading) return;
                IsCrossfading = false;
                NextReader?.Dispose(); NextReader = null;
                crossfadeElapsed = 0;
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  MAIN FORM
    // ════════════════════════════════════════════════════════════════════
    public partial class Form1 : Form
    {
        private IWavePlayer waveOut;
        private CrossFadeProvider crossFader;
        private SampleAnalyzer sampleAnalyzer;
        private Timer clockTimer, animTimer;
        private bool autoAdvance;

        private List<string> library = new List<string>();
        private List<string> queue = new List<string>();
        private int currentTrackIndex = -1;
        private int crossfadeNextIdx = -1;
        private bool isPlaying = false;
        private bool userSeeking = false;
        private const float FADE_DURATION = 3.0f;

        private bool shuffleEnabled = false;
        private List<int> shuffledIndices = null;
        private int shufflePosition = 0;
        private readonly Random random = new Random();

        private enum RepeatMode { Off, All, One }
        private RepeatMode repeatMode = RepeatMode.Off;

        private float discAngle = 0f;
        private Image albumArtImage = null;
        private float[] oscData = null, oscSmooth = null;
        private readonly object oscLock = new object();

        private ToastNotification currentToast = null;
        private MiniPlayerForm miniPlayer = null;
        private bool isMini = false;

        private int currentThemeIndex = 0;

        private TapeListBox libListBox, queueListBox;
        private SlimBar sbProgress, sbVolume;
        private GlowCircle btnPlay;
        private FlatIcon btnPrev, btnNext, btnStop, btnShuffle, btnRepeat, btnMini, btnTheme;
        private TextPill btnAddLib, btnRemoveLib, btnPlayNow, btnEnqueue, btnDequeue, btnClearQueue, btnSavePlaylist, btnLoadPlaylist;
        private Panel panMain, panLeft, panDisc, panOsc, accentLine;
        private Label lblTitle, lblArtist, lblAlbum, lblTime, lblStatus;

        private const double SEEK_STEP_SECONDS = 5.0;

        public Form1()
        {
            Text = "TAPE";
            Size = new Size(980, 680);
            MinimumSize = new Size(860, 600);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = DS.BG;
            ForeColor = DS.TextPri;
            DoubleBuffered = true;
            Font = DS.FSmall;

            var saved = ThemeSettings.Load();
            currentThemeIndex = saved.themeIndex;
            if (currentThemeIndex == 2) DS.SetThemeFromAccent(saved.accent);
            else if (currentThemeIndex == 1) DS.SetThemeFromAccent(Color.FromArgb(0, 180, 200));
            else DS.SetThemeFromAccent(Color.FromArgb(228, 158, 28));

            Build();
            BuildTimers();
            UpdateAllThemeDependentUI();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Space)
            {
                if (waveOut != null)
                    PauseResume();
                else if (queue.Count > 0)
                {
                    int idx = queueListBox.SelectedIndex < 0 ? 0 : queueListBox.SelectedIndex;
                    PlayTrack(idx);
                }
                return true;
            }

            if (keyData == Keys.Left || keyData == Keys.Right)
            {
                SeekByKeyboard(keyData == Keys.Right);
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_APPCOMMAND = 0x0319;
            if (m.Msg == WM_APPCOMMAND)
            {
                int cmd = (int)(m.LParam.ToInt64() >> 16) & ~0xf000;
                switch (cmd)
                {
                    case 14: // Play/Pause
                        if (waveOut != null) PauseResume();
                        else if (queue.Count > 0) PlayTrack(queueListBox.SelectedIndex < 0 ? 0 : queueListBox.SelectedIndex);
                        break;
                    case 13: Stop(); break;
                    case 11: Next(); break;
                    case 12: Previous(); break;
                }
                return;
            }
            base.WndProc(ref m);
        }

        private void SeekByKeyboard(bool forward)
        {
            var reader = crossFader?.CurrentReader;
            if (reader == null) return;
            double newSeconds = reader.CurrentTime.TotalSeconds + (forward ? SEEK_STEP_SECONDS : -SEEK_STEP_SECONDS);
            newSeconds = Math.Max(0, Math.Min(reader.TotalTime.TotalSeconds, newSeconds));
            reader.CurrentTime = TimeSpan.FromSeconds(newSeconds);
            sbProgress.Value = (int)newSeconds;
            lblTime.Text = Fmt(reader.CurrentTime) + " / " + Fmt(reader.TotalTime);
        }

        private void Build()
        {
            // ── SIDEBAR ──────────────────────────────────────────────────
            panLeft = new Panel { Dock = DockStyle.Left, Width = 264, BackColor = DS.Surface1 };
            var hdr = new Panel { Dock = DockStyle.Top, Height = 62, BackColor = DS.Surface1 };
            hdr.Paint += (s, e) =>
            {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(DS.Accent))
                using (var f = new Font("Lucida Console", 20f, FontStyle.Bold))
                    g.DrawString("TAPE", f, b, 16, 12);
                using (var b = new SolidBrush(DS.TextMuted))
                    g.DrawString("AUDIO PLAYER", DS.FLabel, b, 20, 44);
                using (var p = new Pen(DS.Border))
                    g.DrawLine(p, 0, 61, hdr.Width, 61);
            };

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                Panel1MinSize = 80,
                Panel2MinSize = 80,
                SplitterDistance = 180,
                BackColor = DS.Surface1,
                BorderStyle = BorderStyle.None,
            };
            split.Panel1.BackColor = DS.Surface1;
            split.Panel2.BackColor = DS.Surface1;
            split.SplitterWidth = 3;
            split.Paint += (s, e) =>
            {
                var g = e.Graphics;
                using (var b = new SolidBrush(DS.Border))
                    g.FillRectangle(b, split.SplitterDistance, 0, split.SplitterWidth, split.Height);
            };

            // ---------- LIBRARY PANEL ----------
            var panLib = split.Panel1;
            var lblLib = new Label { Dock = DockStyle.Top, Height = 22, Text = "LIBRARY", ForeColor = DS.TextMuted, Font = DS.FLabel, BackColor = Color.Transparent, Padding = new Padding(16, 4, 0, 0) };
            libListBox = new TapeListBox { Dock = DockStyle.Fill, BackColor = DS.Surface1 };
            libListBox.DoubleClick += (s, e) =>
            {
                int sel = libListBox.SelectedIndex;
                if (sel >= 0)
                {
                    queue.Clear();
                    queue.Add(library[sel]);
                    UpdateQueueDisplay();
                    currentTrackIndex = 0;
                    if (shuffleEnabled) ToggleShuffle(false);
                    StopPlayback();
                    PlayTrack(0);
                }
            };

            var panLibFooter = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = DS.Surface1, Padding = new Padding(4) };
            panLibFooter.Paint += (s, e) => { using (var p = new Pen(DS.Border)) e.Graphics.DrawLine(p, 0, 0, panLibFooter.Width, 0); };
            btnAddLib = new TextPill { Text = "＋ ADD", Location = new Point(4, 4), Size = new Size(100, 26), Accent = DS.Accent, Font = new Font("Segoe UI", 7.5f) };
            btnAddLib.Click += BtnAddLib_Click;
            btnRemoveLib = new TextPill { Text = "− REM", Location = new Point(110, 4), Size = new Size(88, 26), Accent = DS.Danger, Font = new Font("Segoe UI", 7.5f) };
            btnRemoveLib.Click += BtnRemoveLib_Click;
            panLibFooter.Controls.AddRange(new Control[] { btnAddLib, btnRemoveLib });

            panLib.Controls.Add(libListBox);
            panLib.Controls.Add(lblLib);
            panLib.Controls.Add(panLibFooter);

            // ---------- QUEUE PANEL ----------
            var panQueue = split.Panel2;
            var lblQueue = new Label { Dock = DockStyle.Top, Height = 22, Text = "QUEUE", ForeColor = DS.TextMuted, Font = DS.FLabel, BackColor = Color.Transparent, Padding = new Padding(16, 4, 0, 0) };
            queueListBox = new TapeListBox { Dock = DockStyle.Fill, BackColor = DS.Surface1 };
            queueListBox.DoubleClick += (s, e) =>
            {
                int idx = queueListBox.SelectedIndex;
                if (idx >= 0) PlayTrack(idx);
            };

            var panQueueFooter = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = DS.Surface1, Padding = new Padding(4) };
            panQueueFooter.Paint += (s, e) => { using (var p = new Pen(DS.Border)) e.Graphics.DrawLine(p, 0, 0, panQueueFooter.Width, 0); };

            // Row 1
            btnPlayNow = new TextPill { Text = "▶ PLAY", Location = new Point(4, 4), Size = new Size(60, 22), Accent = DS.Accent, Font = new Font("Segoe UI", 7.5f) };
            btnPlayNow.Click += (s, e) => { int sel = queueListBox.SelectedIndex; if (sel >= 0) PlayTrack(sel); };

            btnEnqueue = new TextPill { Text = "＋ ENQ", Location = new Point(68, 4), Size = new Size(60, 22), Accent = DS.Accent, Font = new Font("Segoe UI", 7.5f) };
            btnEnqueue.Click += (s, e) =>
            {
                int libSel = libListBox.SelectedIndex;
                if (libSel >= 0)
                {
                    string file = library[libSel];
                    if (!queue.Contains(file))
                    {
                        queue.Add(file);
                        UpdateQueueDisplay();
                    }
                }
            };

            btnDequeue = new TextPill { Text = "✕ DEQ", Location = new Point(132, 4), Size = new Size(60, 22), Accent = DS.Danger, Font = new Font("Segoe UI", 7.5f) };
            btnDequeue.Click += (s, e) =>
            {
                int qSel = queueListBox.SelectedIndex;
                if (qSel >= 0)
                {
                    if (qSel == currentTrackIndex) Stop();
                    queue.RemoveAt(qSel);
                    if (qSel < currentTrackIndex) currentTrackIndex--;
                    else if (qSel == currentTrackIndex) currentTrackIndex = -1;
                    UpdateQueueDisplay();
                    queueListBox.CurrentIndex = currentTrackIndex;
                    queueListBox.Invalidate();
                }
            };

            // Row 2
            btnClearQueue = new TextPill { Text = "🗑 CLR", Location = new Point(4, 30), Size = new Size(60, 22), Accent = DS.Danger, Font = new Font("Segoe UI", 7.5f) };
            btnClearQueue.Click += (s, e) => { Stop(); queue.Clear(); UpdateQueueDisplay(); };

            btnSavePlaylist = new TextPill { Text = "💾 SAVE", Location = new Point(68, 30), Size = new Size(60, 22), Accent = DS.Accent, Font = new Font("Segoe UI", 7.5f) };
            btnSavePlaylist.Click += BtnSavePlaylist_Click;

            btnLoadPlaylist = new TextPill { Text = "📂 LOAD", Location = new Point(132, 30), Size = new Size(60, 22), Accent = DS.Accent, Font = new Font("Segoe UI", 7.5f) };
            btnLoadPlaylist.Click += BtnLoadPlaylist_Click;

            panQueueFooter.Controls.AddRange(new Control[] { btnPlayNow, btnEnqueue, btnDequeue, btnClearQueue, btnSavePlaylist, btnLoadPlaylist });

            panQueue.Controls.Add(queueListBox);
            panQueue.Controls.Add(lblQueue);
            panQueue.Controls.Add(panQueueFooter);

            panLeft.Controls.Add(split);
            panLeft.Controls.Add(hdr);

            var div = new Panel { Dock = DockStyle.Left, Width = 1, BackColor = DS.Border };
            panMain = new Panel { Dock = DockStyle.Fill, BackColor = DS.BG };

            panDisc = new Panel { Location = new Point(36, 28), Size = new Size(248, 248), BackColor = DS.BG, Anchor = AnchorStyles.Top | AnchorStyles.Left };
            panDisc.Paint += DrawDisc;

            var panInfo = new Panel { Location = new Point(304, 28), Size = new Size(356, 248), BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            lblTitle = MkLbl("NO TRACK LOADED", 0, 14, 356, 46, DS.TextPri, DS.FTitle);
            lblArtist = MkLbl("Unknown Artist", 2, 66, 356, 24, DS.Accent, DS.FArtist);
            var sep = new Panel { Location = new Point(0, 100), Size = new Size(320, 1), BackColor = DS.Border };
            lblAlbum = MkLbl("", 2, 110, 340, 18, DS.TextMuted, DS.FTiny);
            lblTime = MkLbl("0:00 / 0:00", 0, 140, 300, 46, DS.Accent, DS.FMonoLg);
            lblStatus = MkLbl("● STOPPED", 2, 192, 200, 18, DS.TextMuted, DS.FMonoSm);
            panInfo.Controls.AddRange(new Control[] { lblTitle, lblArtist, sep, lblAlbum, lblTime, lblStatus });

            panOsc = new Panel { Location = new Point(28, 296), Size = new Size(640, 104), BackColor = DS.Surface1, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            panOsc.Paint += DrawOsc;
            RoundRegion(panOsc, 10);

            sbProgress = new SlimBar { Location = new Point(28, 418), Size = new Size(640, 18), AccentColor = DS.Accent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            sbProgress.Scroll += SbProgress_Scroll;
            sbProgress.MouseDown += (s, e) => userSeeking = true;
            sbProgress.MouseUp += (s, e) => userSeeking = false;

            var panTrans = new Panel { Location = new Point(28, 448), Size = new Size(640, 78), BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

            btnTheme = new FlatIcon { Text = "🎨", Location = new Point(14, 20), Size = new Size(36, 36), Font = DS.FIconSm, Fg = DS.TextMuted };
            btnTheme.Click += BtnTheme_Click;
            btnMini = new FlatIcon { Text = "📟", Location = new Point(58, 20), Size = new Size(36, 36), Font = DS.FIconSm, Fg = DS.TextMuted };
            btnMini.Click += (s, e) => ToggleMiniPlayer();
            btnRepeat = new FlatIcon { Text = "🔁", Location = new Point(104, 20), Size = new Size(36, 36), Font = DS.FIconSm, Fg = DS.TextMuted };
            btnRepeat.Click += BtnRepeat_Click;
            btnShuffle = new FlatIcon { Text = "🔀", Location = new Point(152, 20), Size = new Size(36, 36), Font = DS.FIconSm, Fg = DS.TextMuted };
            btnShuffle.Click += BtnShuffle_Click;
            btnStop = new FlatIcon { Text = "⏹", Location = new Point(202, 20), Size = new Size(36, 36), Font = DS.FIconSm, Fg = DS.TextMuted };
            btnStop.Click += (s, e) => Stop();
            btnPrev = new FlatIcon { Text = "⏮", Location = new Point(248, 12), Size = new Size(50, 50), Font = DS.FIcon, Fg = DS.TextPri };
            btnPrev.Click += (s, e) => Previous();
            btnPlay = new GlowCircle { Location = new Point(310, 4), Size = new Size(62, 62), Text = "▶", Font = new Font("Segoe UI Symbol", 18f), ForeColor = Color.Black, Bg = DS.Accent, Glow = DS.AccentGlow };
            btnPlay.Click += BtnPlay_Click;
            btnNext = new FlatIcon { Text = "⏭", Location = new Point(384, 12), Size = new Size(50, 50), Font = DS.FIcon, Fg = DS.TextPri };
            btnNext.Click += (s, e) => Next();

            var volLbl = MkLbl("VOL", 458, 30, 28, 16, DS.TextMuted, DS.FLabel);
            sbVolume = new SlimBar { Location = new Point(492, 28), Size = new Size(132, 18), Minimum = 0, Maximum = 100, Value = 80, AccentColor = DS.Accent };
            sbVolume.Scroll += SbVolume_Scroll;

            panTrans.Controls.AddRange(new Control[] { btnTheme, btnMini, btnRepeat, btnShuffle, btnStop, btnPrev, btnPlay, btnNext, volLbl, sbVolume });

            accentLine = new Panel { Location = new Point(28, 558), Size = new Size(640, 2), BackColor = Color.FromArgb(38, DS.Accent.R, DS.Accent.G, DS.Accent.B), Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };

            panMain.Controls.AddRange(new Control[] { panDisc, panInfo, panOsc, sbProgress, panTrans, accentLine });
            Controls.Add(panMain);
            Controls.Add(div);
            Controls.Add(panLeft);

            UpdateQueueDisplay();
        }

        private static Label MkLbl(string text, int x, int y, int w, int h, Color fg, Font f)
            => new Label { Text = text, Location = new Point(x, y), Size = new Size(w, h), ForeColor = fg, Font = f, BackColor = Color.Transparent, AutoEllipsis = true };

        private void RoundRegion(Control c, int r)
            => c.Region = new Region(DS.RR(new RectangleF(0, 0, c.Width, c.Height), r));

        private void BuildTimers()
        {
            clockTimer = new Timer { Interval = 200 };
            clockTimer.Tick += ClockTick;
            clockTimer.Start();
            animTimer = new Timer { Interval = 33 };
            animTimer.Tick += AnimTick;
            animTimer.Start();
        }

        // ── LIBRARY / QUEUE HELPERS ────────────
        private void UpdateQueueDisplay()
        {
            queueListBox.BeginUpdate();
            queueListBox.Items.Clear();
            foreach (var f in queue) queueListBox.Items.Add(Path.GetFileName(f));
            queueListBox.EndUpdate();
            queueListBox.CurrentIndex = currentTrackIndex;
            queueListBox.Invalidate();
        }

        // ── PLAYLIST SAVE / LOAD ──────────────
        private void BtnSavePlaylist_Click(object sender, EventArgs e)
        {
            if (queue.Count == 0)
            {
                MessageBox.Show("The queue is empty. Nothing to save.", "TAPE", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var sfd = new SaveFileDialog
            {
                Filter = "Playlist Files (*.txt)|*.txt|All Files|*.*",
                DefaultExt = "txt",
                FileName = "playlist.txt"
            })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        File.WriteAllLines(sfd.FileName, queue);
                        MessageBox.Show($"Playlist saved to:\n{sfd.FileName}", "TAPE", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving playlist:\n{ex.Message}", "TAPE", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnLoadPlaylist_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog
            {
                Filter = "Playlist Files (*.txt)|*.txt|All Files|*.*",
                Multiselect = false
            })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var lines = File.ReadAllLines(ofd.FileName);
                        var newTracks = lines.Where(l => !string.IsNullOrWhiteSpace(l) && File.Exists(l)).ToList();
                        if (newTracks.Count == 0)
                        {
                            MessageBox.Show("No valid audio files found in the playlist.", "TAPE", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        // Replace the current queue
                        Stop();
                        queue.Clear();
                        foreach (var f in newTracks)
                        {
                            queue.Add(f);
                            // Optionally add to library if not already present
                            if (!library.Contains(f))
                            {
                                library.Add(f);
                                libListBox.Items.Add(Path.GetFileName(f));
                            }
                        }
                        UpdateQueueDisplay();
                        MessageBox.Show($"{newTracks.Count} tracks loaded into the queue.", "TAPE", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading playlist:\n{ex.Message}", "TAPE", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        // ── THEME ───────────────────────────────
        private void BtnTheme_Click(object sender, EventArgs e)
        {
            if (currentThemeIndex == 0) { currentThemeIndex = 1; DS.SetThemeFromAccent(Color.FromArgb(0, 180, 200)); }
            else if (currentThemeIndex == 1)
            {
                using (var cd = new ColorDialog { Color = DS.Accent })
                {
                    if (cd.ShowDialog() == DialogResult.OK)
                    {
                        currentThemeIndex = 2;
                        DS.SetThemeFromAccent(cd.Color);
                    }
                    else return;
                }
            }
            else { currentThemeIndex = 0; DS.SetThemeFromAccent(Color.FromArgb(228, 158, 28)); }
            ThemeSettings.Save(currentThemeIndex, DS.Accent);
            UpdateAllThemeDependentUI();
        }

        private void UpdateAllThemeDependentUI()
        {
            lblArtist.ForeColor = DS.Accent;
            lblTime.ForeColor = DS.Accent;
            accentLine.BackColor = Color.FromArgb(38, DS.Accent.R, DS.Accent.G, DS.Accent.B);
            sbProgress.AccentColor = DS.Accent; sbProgress.Invalidate();
            sbVolume.AccentColor = DS.Accent; sbVolume.Invalidate();
            btnPlay.Bg = DS.Accent; btnPlay.Glow = DS.AccentGlow; btnPlay.Invalidate();
            UpdateShuffleButton();
            UpdateRepeatButton();
            RefreshStatus();
            miniPlayer?.ApplyCurrentTheme();
            this.Invalidate(true);
        }

        // ── MINI PLAYER ──────────────────────────
        public void ToggleMiniPlayer()
        {
            if (isMini)
            {
                if (miniPlayer != null && !miniPlayer.IsDisposed) miniPlayer.Hide();
                this.Show();
                this.WindowState = FormWindowState.Normal;
                isMini = false;
                btnMini.Fg = DS.TextMuted;
                btnMini.Invalidate();
            }
            else
            {
                if (miniPlayer == null || miniPlayer.IsDisposed)
                {
                    miniPlayer = new MiniPlayerForm(this);
                    miniPlayer.FormClosed += (s, e) => { if (isMini) ToggleMiniPlayer(); };
                }
                UpdateMiniPlayer();
                miniPlayer.UpdatePlayPauseButton(isPlaying);
                miniPlayer.Show();
                this.Hide();
                isMini = true;
                btnMini.Fg = DS.Accent;
                btnMini.Invalidate();
            }
        }

        private void UpdateMiniPlayer()
        {
            if (miniPlayer != null && !miniPlayer.IsDisposed)
                miniPlayer.UpdateTrack(lblTitle.Text, lblArtist.Text, albumArtImage);
        }

        public void PlayPauseMini()
        {
            if (waveOut != null) { PauseResume(); }
            else if (queue.Count > 0)
            {
                int idx = queueListBox.SelectedIndex < 0 ? 0 : queueListBox.SelectedIndex;
                PlayTrack(idx);
            }
            if (miniPlayer != null && !miniPlayer.IsDisposed)
                miniPlayer.UpdatePlayPauseButton(isPlaying);
        }

        // ── TOAST ─────────────────────────────────
        private void ShowTrackChangeToast()
        {
            if (currentToast != null && !currentToast.IsDisposed) { currentToast.Close(); currentToast = null; }
            string title = lblTitle.Text;
            string artist = lblArtist.Text;
            Image art = albumArtImage != null ? new Bitmap(albumArtImage, 48, 48) : null;
            currentToast = new ToastNotification(title, artist, art);
            currentToast.Show();
        }

        // ── SHUFFLE ───────────────────────────────
        private void ToggleShuffle(bool enable)
        {
            shuffleEnabled = enable;
            if (enable && queue.Count > 0)
            {
                shuffledIndices = Enumerable.Range(0, queue.Count).OrderBy(x => random.Next()).ToList();
                if (currentTrackIndex >= 0 && shuffledIndices.Contains(currentTrackIndex))
                    shufflePosition = shuffledIndices.IndexOf(currentTrackIndex);
                else shufflePosition = 0;
            }
            else shuffledIndices = null;
            UpdateShuffleButton();
        }

        private void UpdateShuffleButton() { if (btnShuffle != null) { btnShuffle.Fg = shuffleEnabled ? DS.Accent : DS.TextMuted; btnShuffle.Invalidate(); } }
        private void BtnShuffle_Click(object sender, EventArgs e) => ToggleShuffle(!shuffleEnabled);

        // ── REPEAT ───────────────────────────────
        private void CycleRepeatMode()
        {
            switch (repeatMode) { case RepeatMode.Off: repeatMode = RepeatMode.All; break; case RepeatMode.All: repeatMode = RepeatMode.One; break; case RepeatMode.One: repeatMode = RepeatMode.Off; break; }
            UpdateRepeatButton();
        }

        private void UpdateRepeatButton()
        {
            if (btnRepeat == null) return;
            switch (repeatMode)
            {
                case RepeatMode.Off: btnRepeat.Text = "🔁"; btnRepeat.Fg = DS.TextMuted; break;
                case RepeatMode.All: btnRepeat.Text = "🔁"; btnRepeat.Fg = DS.Accent; break;
                case RepeatMode.One: btnRepeat.Text = "🔂"; btnRepeat.Fg = DS.Accent; break;
            }
            btnRepeat.Invalidate();
        }
        private void BtnRepeat_Click(object sender, EventArgs e) => CycleRepeatMode();

        // ── PLAYBACK ──────────────────────────────
        private void PlayTrack(int idx)
        {
            if (idx < 0 || idx >= queue.Count) return;
            autoAdvance = false;
            crossfadeNextIdx = -1;
            crossFader?.AbortCrossfade();
            StopPlayback();

            var reader = new AudioFileReader(queue[idx]);
            crossFader = new CrossFadeProvider(reader);
            crossFader.CrossfadeCompleted += OnCrossfadeDone;
            sampleAnalyzer = new SampleAnalyzer(crossFader);
            waveOut = new WaveOutEvent();
            waveOut.Init(sampleAnalyzer);
            waveOut.Volume = sbVolume.Value / 100f;
            waveOut.PlaybackStopped += OnStopped;
            autoAdvance = true;
            waveOut.Play();

            currentTrackIndex = idx;
            isPlaying = true;
            sbProgress.Maximum = Math.Max(1, (int)reader.TotalTime.TotalSeconds);
            oscSmooth = new float[2048];

            if (shuffleEnabled && shuffledIndices != null)
            {
                if (shuffledIndices.Contains(idx)) shufflePosition = shuffledIndices.IndexOf(idx);
                else ToggleShuffle(false);
            }

            LoadTags(queue[idx]);
            queueListBox.CurrentIndex = idx;
            queueListBox.Invalidate();
            RefreshBtn();
            RefreshStatus();
            ShowTrackChangeToast();
            UpdateMiniPlayer();
        }

        private void OnCrossfadeDone()
        {
            if (crossfadeNextIdx < 0 || crossfadeNextIdx >= queue.Count) return;
            currentTrackIndex = crossfadeNextIdx;
            queueListBox.CurrentIndex = currentTrackIndex;
            queueListBox.Invalidate();
            if (shuffleEnabled && shuffledIndices != null && shuffledIndices.Contains(currentTrackIndex))
                shufflePosition = shuffledIndices.IndexOf(currentTrackIndex);
            LoadTags(queue[currentTrackIndex]);
            crossfadeNextIdx = -1;
            RefreshStatus();
            ShowTrackChangeToast();
            UpdateMiniPlayer();
        }

        private void OnStopped(object sender, StoppedEventArgs e)
        {
            if (!autoAdvance) return;
            autoAdvance = false;
            BeginInvoke((MethodInvoker)(() =>
            {
                if (repeatMode == RepeatMode.One && currentTrackIndex >= 0)
                    PlayTrack(currentTrackIndex);
                else Next();
            }));
        }

        private void StopPlayback()
        {
            if (waveOut != null)
            {
                try { waveOut.Stop(); } catch { }
                waveOut.PlaybackStopped -= OnStopped;
                waveOut.Dispose(); waveOut = null;
            }
            crossFader?.AbortCrossfade();
            crossFader?.CurrentReader?.Dispose();
            crossFader?.NextReader?.Dispose();
            crossFader = null;
            sampleAnalyzer?.Dispose(); sampleAnalyzer = null;
            isPlaying = false;
        }

        private void PauseResume()
        {
            if (waveOut == null) return;
            if (isPlaying) { waveOut.Pause(); isPlaying = false; } else { waveOut.Play(); isPlaying = true; }
            RefreshBtn();
            RefreshStatus();
            if (miniPlayer != null && !miniPlayer.IsDisposed) miniPlayer.UpdatePlayPauseButton(isPlaying);
        }

        private void Stop()
        {
            autoAdvance = false;
            crossfadeNextIdx = -1;
            StopPlayback();
            currentTrackIndex = -1;
            sbProgress.Value = 0;
            lblTime.Text = "0:00 / 0:00";
            albumArtImage = null;
            oscData = null; oscSmooth = null;
            lblTitle.Text = "NO TRACK LOADED";
            lblArtist.Text = "Unknown Artist"; lblAlbum.Text = "";
            queueListBox.CurrentIndex = -1; queueListBox.Invalidate();
            RefreshBtn(); RefreshStatus();
            panDisc.Invalidate(); panOsc.Invalidate();
            if (currentToast != null && !currentToast.IsDisposed) { currentToast.Close(); currentToast = null; }
            if (miniPlayer != null && !miniPlayer.IsDisposed) { miniPlayer.UpdateTrack("No Track", "Artist", null); miniPlayer.UpdatePlayPauseButton(false); }
        }

        public void Next()
        {
            crossFader?.AbortCrossfade();
            crossfadeNextIdx = -1;
            int idx = GetNextIndex(+1);
            if (idx < 0) { Stop(); return; }
            PlayTrack(idx);
        }

        public void Previous()
        {
            crossFader?.AbortCrossfade();
            crossfadeNextIdx = -1;
            int idx = GetNextIndex(-1);
            if (idx < 0) { Stop(); return; }
            PlayTrack(idx);
        }

        private int GetNextIndex(int step)
        {
            if (queue.Count == 0) return -1;
            bool wrap = repeatMode == RepeatMode.All;

            if (shuffleEnabled && shuffledIndices != null)
            {
                int pos = shufflePosition + step;
                if (pos >= shuffledIndices.Count) { if (wrap) pos = 0; else return -1; }
                else if (pos < 0) { if (wrap) pos = shuffledIndices.Count - 1; else return -1; }
                shufflePosition = pos;
                return shuffledIndices[pos];
            }
            else
            {
                int n = currentTrackIndex + step;
                if (n >= queue.Count) { if (wrap) n = 0; else return -1; }
                else if (n < 0) { if (wrap) n = queue.Count - 1; else return -1; }
                return n;
            }
        }

        private void RefreshBtn() { btnPlay.Text = isPlaying ? "⏸" : "▶"; btnPlay.Invalidate(); }
        private void RefreshStatus()
        {
            if (lblStatus == null) return;
            if (isPlaying) { lblStatus.Text = "● PLAYING"; lblStatus.ForeColor = DS.Accent; }
            else if (waveOut != null) { lblStatus.Text = "● PAUSED"; lblStatus.ForeColor = DS.TextMuted; }
            else { lblStatus.Text = "● STOPPED"; lblStatus.ForeColor = DS.TextMuted; }
        }

        // ── TAG LOADING ──────────────────────────
        private void LoadTags(string path)
        {
            try
            {
                using (var tf = TagLib.File.Create(path))
                {
                    lblTitle.Text = string.IsNullOrEmpty(tf.Tag.Title) ? Path.GetFileNameWithoutExtension(path) : tf.Tag.Title;
                    lblArtist.Text = tf.Tag.FirstPerformer ?? "Unknown Artist";
                    var parts = new List<string>();
                    if (!string.IsNullOrEmpty(tf.Tag.Album)) parts.Add(tf.Tag.Album);
                    if (tf.Tag.Year > 0) parts.Add(tf.Tag.Year.ToString());
                    lblAlbum.Text = string.Join("  ·  ", parts);
                    albumArtImage = null;
                    if (tf.Tag.Pictures?.Length > 0)
                        using (var ms = new MemoryStream(tf.Tag.Pictures[0].Data.Data))
                            albumArtImage = Image.FromStream(ms);
                }
            }
            catch { lblTitle.Text = Path.GetFileNameWithoutExtension(path); lblArtist.Text = ""; lblAlbum.Text = ""; albumArtImage = null; }
            panDisc.Invalidate();
        }

        // ── LIBRARY BUTTONS ──────────────────────
        private void BtnAddLib_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog { Filter = "Audio Files|*.mp3;*.wav;*.flac;*.aac;*.ogg|All Files|*.*", Multiselect = true })
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    foreach (var f in dlg.FileNames)
                    {
                        if (!library.Contains(f))
                        {
                            library.Add(f);
                            libListBox.Items.Add(Path.GetFileName(f));
                        }
                    }
                }
            }
        }

        private void BtnRemoveLib_Click(object sender, EventArgs e)
        {
            int sel = libListBox.SelectedIndex;
            if (sel < 0) return;
            string file = library[sel];
            if (queue.Contains(file))
            {
                int qIdx = queue.IndexOf(file);
                if (qIdx == currentTrackIndex) Stop();
                queue.RemoveAt(qIdx);
                if (qIdx < currentTrackIndex) currentTrackIndex--;
                else if (qIdx == currentTrackIndex) currentTrackIndex = -1;
                UpdateQueueDisplay();
            }
            library.RemoveAt(sel);
            libListBox.Items.RemoveAt(sel);
            libListBox.Invalidate();
        }

        private void BtnPlay_Click(object sender, EventArgs e)
        {
            if (waveOut != null) { PauseResume(); return; }
            if (queue.Count == 0) return;
            int idx = queueListBox.SelectedIndex < 0 ? 0 : queueListBox.SelectedIndex;
            PlayTrack(idx);
        }

        // ── PROGRESS / VOLUME ─────────────────────
        private void ClockTick(object sender, EventArgs e)
        {
            if (crossFader == null || crossFader.CurrentReader == null || !isPlaying || userSeeking) return;
            var reader = crossFader.CurrentReader;
            sbProgress.Value = Math.Min(sbProgress.Maximum, (int)reader.CurrentTime.TotalSeconds);
            lblTime.Text = Fmt(reader.CurrentTime) + " / " + Fmt(reader.TotalTime);

            if (!crossFader.IsCrossfading && waveOut?.PlaybackState == PlaybackState.Playing && repeatMode != RepeatMode.One)
            {
                double remaining = (reader.TotalTime - reader.CurrentTime).TotalSeconds;
                if (remaining <= FADE_DURATION)
                {
                    int nextIdx = GetNextIndex(+1);
                    if (nextIdx >= 0)
                    {
                        crossfadeNextIdx = nextIdx;
                        var nextReader = new AudioFileReader(queue[nextIdx]);
                        crossFader.SetNextReader(nextReader, FADE_DURATION);
                    }
                }
            }
        }

        private void SbProgress_Scroll(object sender, EventArgs e)
        {
            if (crossFader?.CurrentReader != null && userSeeking)
            {
                crossFader.CurrentReader.CurrentTime = TimeSpan.FromSeconds(sbProgress.Value);
                lblTime.Text = Fmt(crossFader.CurrentReader.CurrentTime) + " / " + Fmt(crossFader.CurrentReader.TotalTime);
            }
        }

        private void SbVolume_Scroll(object sender, EventArgs e) { if (waveOut != null) waveOut.Volume = sbVolume.Value / 100f; }
        private string Fmt(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:D2}";

        // ── ANIM ──────────────────────────────────
        private void AnimTick(object sender, EventArgs e)
        {
            if (isPlaying) discAngle = (discAngle + 0.60f) % 360f;
            panDisc.Invalidate();
            if (sampleAnalyzer != null && waveOut?.PlaybackState == PlaybackState.Playing)
            {
                float[] raw = sampleAnalyzer.GetLatestSamples();
                if (raw != null)
                {
                    if (oscSmooth == null || oscSmooth.Length != raw.Length) oscSmooth = new float[raw.Length];
                    for (int i = 0; i < raw.Length; i++) oscSmooth[i] = oscSmooth[i] * 0.5f + raw[i] * 0.5f;
                    lock (oscLock) { oscData = (float[])oscSmooth.Clone(); }
                }
            }
            panOsc.Invalidate();
        }

        private void DrawDisc(object sender, PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(DS.BG);
            int w = panDisc.Width, h = panDisc.Height;
            int cx = w / 2, cy = h / 2;
            int R = Math.Min(cx, cy) - 6, LR = 66;

            g.TranslateTransform(cx, cy);
            g.RotateTransform(discAngle);
            using (var b = new SolidBrush(Color.FromArgb(27, 25, 21))) g.FillEllipse(b, -R, -R, R * 2, R * 2);
            for (int r = R - 5; r > LR + 14; r -= 5)
            {
                int alpha = 10 + (R - r) / 7;
                using (var pen = new Pen(Color.FromArgb(Math.Min(alpha, 42), 110, 95, 65), 0.7f)) g.DrawEllipse(pen, -r, -r, r * 2, r * 2);
            }
            using (var pen = new Pen(Color.FromArgb(32, DS.Accent.R, DS.Accent.G, DS.Accent.B), 3f)) g.DrawEllipse(pen, -(R - 1), -(R - 1), (R - 1) * 2, (R - 1) * 2);

            if (albumArtImage != null)
            {
                var clip = new GraphicsPath(); clip.AddEllipse(-LR, -LR, LR * 2, LR * 2);
                var st = g.Save(); g.SetClip(clip); g.DrawImage(albumArtImage, -LR, -LR, LR * 2, LR * 2); g.Restore(st);
            }
            else
            {
                using (var b = new LinearGradientBrush(new PointF(-LR, -LR), new PointF(LR, LR), Color.FromArgb(60, 54, 42), Color.FromArgb(36, 32, 24)))
                    g.FillEllipse(b, -LR, -LR, LR * 2, LR * 2);
                using (var b = new SolidBrush(Color.FromArgb(62, DS.Accent.R, DS.Accent.G, DS.Accent.B)))
                using (var f = new Font("Segoe UI Symbol", 26f))
                    g.DrawString("♫", f, b, new RectangleF(-LR, -LR, LR * 2, LR * 2), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            }

            using (var pen = new Pen(Color.FromArgb(68, DS.Accent.R, DS.Accent.G, DS.Accent.B), 1.5f)) g.DrawEllipse(pen, -(LR + 1), -(LR + 1), (LR + 1) * 2, (LR + 1) * 2);
            using (var b = new SolidBrush(DS.BG)) g.FillEllipse(b, -7, -7, 14, 14);
            using (var pen = new Pen(Color.FromArgb(55, DS.TextMuted.R, DS.TextMuted.G, DS.TextMuted.B))) g.DrawEllipse(pen, -7, -7, 14, 14);
            g.ResetTransform();

            if (isPlaying)
                for (int i = 1; i <= 3; i++)
                {
                    int alpha = 12 - i * 3;
                    using (var pen = new Pen(Color.FromArgb(alpha, DS.Accent.R, DS.Accent.G, DS.Accent.B), i * 2.5f))
                        g.DrawEllipse(pen, cx - R - i * 3, cy - R - i * 3, (R + i * 3) * 2, (R + i * 3) * 2);
                }
        }

        private void DrawOsc(object sender, PaintEventArgs e)
        {
            float[] data; lock (oscLock) { data = oscData; }
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(DS.Surface1);
            int pw = panOsc.Width, ph = panOsc.Height;
            float mid = ph * 0.5f;
            using (var gp = new Pen(Color.FromArgb(16, 255, 255, 255)))
            {
                g.DrawLine(gp, 0, mid, pw, mid);
                g.DrawLine(gp, 0, ph * 0.25f, pw, ph * 0.25f);
                g.DrawLine(gp, 0, ph * 0.75f, pw, ph * 0.75f);
                for (int x = 80; x < pw; x += 80) g.DrawLine(gp, x, 6, x, ph - 6);
            }
            using (var b = new SolidBrush(DS.TextMuted)) g.DrawString("WAVEFORM", DS.FMonoSm, b, 8, 5);
            if (!isPlaying || data == null || data.Length < 4)
            {
                using (var pen = new Pen(Color.FromArgb(40, DS.Accent.R, DS.Accent.G, DS.Accent.B), 1.5f)) g.DrawLine(pen, 12, mid, pw - 12, mid);
                return;
            }
            int step = Math.Max(1, data.Length / pw);
            var pts = new List<PointF>(pw);
            for (int x = 0; x < pw; x++)
            {
                int di = x * step; if (di >= data.Length) break;
                float y = mid - data[di] * (ph * 0.40f);
                y = Math.Max(2f, Math.Min(ph - 2f, y));
                pts.Add(new PointF(x, y));
            }
            if (pts.Count < 2) return;
            var arr = pts.ToArray();
            int[] alphas = { 28, 70, 185 };
            float[] widths = { 5.5f, 2.5f, 1.2f };
            for (int pass = 0; pass < 3; pass++)
                using (var pen = new Pen(Color.FromArgb(alphas[pass], DS.Accent.R, DS.Accent.G, DS.Accent.B), widths[pass]))
                { pen.LineJoin = LineJoin.Round; g.DrawLines(pen, arr); }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopPlayback();
            clockTimer?.Stop(); animTimer?.Stop();
            if (currentToast != null && !currentToast.IsDisposed) currentToast.Close();
            if (miniPlayer != null && !miniPlayer.IsDisposed) miniPlayer.Close();
            base.OnFormClosing(e);
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  CUSTOM CONTROLS  (unchanged)
    // ════════════════════════════════════════════════════════════════════
    internal class TapeListBox : ListBox
    {
        public int CurrentIndex { get; set; } = -1;
        private readonly Font _numFont = new Font("Lucida Console", 8f);
        private readonly Font _normFont = new Font("Segoe UI", 9.5f);
        private readonly Font _boldFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);

        public TapeListBox()
        {
            DrawMode = DrawMode.OwnerDrawFixed; ItemHeight = 41; BorderStyle = BorderStyle.None;
            BackColor = DS.Surface1; ForeColor = DS.TextPri;
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count) return;
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            bool play = e.Index == CurrentIndex;

            g.FillRectangle(new SolidBrush(sel ? DS.SelBg : DS.Surface1), e.Bounds);
            if (play)
            {
                using (var br = new LinearGradientBrush(new Point(e.Bounds.X, e.Bounds.Y), new Point(e.Bounds.X + 80, e.Bounds.Y), Color.FromArgb(34, DS.Accent.R, DS.Accent.G, DS.Accent.B), Color.Transparent))
                    g.FillRectangle(br, e.Bounds);
                g.FillRectangle(new SolidBrush(DS.Accent), e.Bounds.X, e.Bounds.Y + 7, 2, e.Bounds.Height - 14);
            }
            TextRenderer.DrawText(g, (e.Index + 1).ToString("D2"), _numFont, new Rectangle(e.Bounds.X + 10, e.Bounds.Y, 28, e.Bounds.Height), play ? DS.Accent : DS.TextMuted, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, Items[e.Index]?.ToString() ?? "", play ? _boldFont : _normFont, new Rectangle(e.Bounds.X + 46, e.Bounds.Y, e.Bounds.Width - 64, e.Bounds.Height), play ? DS.TextPri : (sel ? DS.TextPri : Color.FromArgb(195, 188, 175)), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (play) TextRenderer.DrawText(g, "▶", _numFont, new Rectangle(e.Bounds.Right - 22, e.Bounds.Y, 18, e.Bounds.Height), DS.Accent, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            if (!sel && e.Index < Items.Count - 1)
                using (var pen = new Pen(Color.FromArgb(20, 255, 255, 255)))
                    g.DrawLine(pen, e.Bounds.X + 46, e.Bounds.Bottom - 1, e.Bounds.Right - 6, e.Bounds.Bottom - 1);
        }

        protected override void Dispose(bool disposing) { if (disposing) { _numFont?.Dispose(); _normFont?.Dispose(); _boldFont?.Dispose(); } base.Dispose(disposing); }
    }

    internal class SlimBar : Control
    {
        public int Minimum { get; set; } = 0;
        public int Maximum { get; set; } = 100;
        public Color AccentColor { get; set; } = DS.Accent;
        private int _val;
        public int Value { get => _val; set { _val = Math.Max(Minimum, Math.Min(Maximum, value)); Invalidate(); } }
        public event EventHandler Scroll;
        private bool _drag;

        public SlimBar() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true); Height = 18; Cursor = Cursors.Hand; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? DS.BG);
            const int TH = 3, M = 8;
            int TY = (Height - TH) / 2, TW = Width - M * 2;
            using (var b = new SolidBrush(DS.Surface2))
            using (var p = DS.RR(new RectangleF(M, TY, TW, TH), TH / 2f))
                g.FillPath(b, p);
            float pct = Maximum > Minimum ? (float)(_val - Minimum) / (Maximum - Minimum) : 0f;
            float fw = TW * pct;
            if (fw >= TH)
                using (var br = new LinearGradientBrush(new PointF(M, 0), new PointF(M + fw, 0), AccentColor, Color.FromArgb(155, AccentColor.R, AccentColor.G, AccentColor.B)))
                using (var p = DS.RR(new RectangleF(M, TY, fw, TH), TH / 2f))
                    g.FillPath(br, p);
            int tx = M + (int)(TW * pct);
            const int TR = 6;
            using (var b = new SolidBrush(AccentColor))
                g.FillEllipse(b, tx - TR, Height / 2 - TR, TR * 2, TR * 2);
            using (var pen = new Pen(Color.FromArgb(55, 0, 0, 0), 1f))
                g.DrawEllipse(pen, tx - TR, Height / 2 - TR, TR * 2, TR * 2);
        }

        private void Seek(int x) { float pct = Math.Max(0f, Math.Min(1f, (float)(x - 8) / (Width - 16))); _val = Minimum + (int)(pct * (Maximum - Minimum)); Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { _drag = true; Seek(e.X); base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (_drag) Seek(e.X); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _drag = false; base.OnMouseUp(e); }
    }

    internal class GlowCircle : Control
    {
        public Color Bg { get; set; } = DS.Accent;
        public Color Glow { get; set; } = DS.AccentGlow;
        private bool _hover, _press;
        public GlowCircle() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true); Cursor = Cursors.Hand; }
        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = _press = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _press = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _press = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? DS.BG);
            if (_hover) using (var b = new SolidBrush(Glow)) g.FillEllipse(b, -5, -5, Width + 10, Height + 10);
            using (var sh = new SolidBrush(Color.FromArgb(48, 0, 0, 0))) g.FillEllipse(sh, 3, 4, Width - 2, Height - 2);
            float s = _press ? 3f : 0f;
            var rc = new RectangleF(s, s, Width - s * 2 - 1, Height - s * 2 - 1);
            Color top = _press ? Color.FromArgb(Math.Max(0, Bg.R - 22), Math.Max(0, Bg.G - 22), Math.Max(0, Bg.B - 22)) : Bg;
            using (var br = new LinearGradientBrush(new PointF(rc.X, rc.Y), new PointF(rc.X, rc.Bottom), top, Color.FromArgb(Math.Max(0, Bg.R - 45), Bg.G, Bg.B)))
                g.FillEllipse(br, rc);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using (var b = new SolidBrush(ForeColor))
                g.DrawString(Text, Font, b, Text == "▶" ? new RectangleF(3, 0, Width, Height) : new RectangleF(0, 0, Width, Height), sf);
        }
    }

    internal class FlatIcon : Control
    {
        public Color Fg { get; set; } = DS.TextPri;
        private bool _hover;
        public FlatIcon() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true); Cursor = Cursors.Hand; }
        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? DS.BG);
            if (_hover) using (var b = new SolidBrush(Color.FromArgb(28, Fg.R, Fg.G, Fg.B)))
                using (var p = DS.RR(new RectangleF(2, 2, Width - 4, Height - 4), 8)) g.FillPath(b, p);
            var c = _hover ? Fg : Color.FromArgb(168, Fg.R, Fg.G, Fg.B);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using (var b = new SolidBrush(c)) g.DrawString(Text, Font ?? DS.FIcon, b, new RectangleF(0, 0, Width, Height), sf);
        }
    }

    internal class TextPill : Control
    {
        public Color Accent { get; set; } = DS.Accent;
        private bool _hover;
        public TextPill() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true); Cursor = Cursors.Hand; ForeColor = DS.TextPri; }
        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? DS.Surface1);
            using (var b = new SolidBrush(_hover ? DS.Surface3 : DS.Surface2))
            using (var p = DS.RR(new RectangleF(0, 0, Width - 1, Height - 1), Height / 2f)) g.FillPath(b, p);
            using (var pen = new Pen(Color.FromArgb(_hover ? 75 : 40, Accent.R, Accent.G, Accent.B), 1f))
            using (var p = DS.RR(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Height / 2f)) g.DrawPath(pen, p);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using (var b = new SolidBrush(_hover ? Accent : ForeColor))
                g.DrawString(Text, Font ?? DS.FSmall, b, new RectangleF(0, 0, Width, Height), sf);
        }
    }

    internal class SampleAnalyzer : ISampleProvider, IDisposable
    {
        private readonly ISampleProvider _src;
        private readonly float[] _ring;
        private int _pos;
        private readonly object _lk = new object();
        public WaveFormat WaveFormat => _src.WaveFormat;
        public SampleAnalyzer(ISampleProvider src, int len = 2048) { _src = src; _ring = new float[len]; }
        public int Read(float[] buf, int offset, int count)
        {
            int n = _src.Read(buf, offset, count);
            lock (_lk) for (int i = 0; i < n; i++) { _ring[_pos] = buf[offset + i]; _pos = (_pos + 1) % _ring.Length; }
            return n;
        }
        public float[] GetLatestSamples()
        {
            lock (_lk) { var r = new float[_ring.Length]; for (int i = 0; i < _ring.Length; i++) r[i] = _ring[(_pos + i) % _ring.Length]; return r; }
        }
        public void Dispose() { }
    }
}