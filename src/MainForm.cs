using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ImapSyncWin.Controls;

namespace ImapSyncWin
{
    public class MainForm : Form
    {
        private AppSettings _settings;
        private MigrationEngine _engine;
        private List<AccountPair> _queue;
        private MockImapServer _demoSourceServer;
        private MockImapServer _demoDestServer;

        // UI Tabs & Header
        private Panel _headerPanel;
        private PictureBox _pbLogo;
        private Label _lblMainTitle;
        private Label _lblSubTitle;
        private TabControl _tabControl;

        // Tab 1: Single Migration Controls
        private Panel _pnlTopCards;
        private CardPanel _cardSource;
        private CardPanel _cardDest;
        private Label _lblArrow;

        private Label _lblSourceTitle;
        private PillBadge _lblSourceStatusPill;
        private Label _lblSourceService;
        private ComboBox _cmbSourceService;
        private Label _lblSourceHost;
        private TextBox _txtSourceHost;
        private Label _lblSourcePort;
        private NumericUpDown _numSourcePort;
        private Label _lblSourceSec;
        private ComboBox _cmbSourceSecurity;
        private Label _lblSourceUser;
        private TextBox _txtSourceUser;
        private Label _lblSourcePass;
        private TextBox _txtSourcePass;
        private ModernButton _btnSourceShowPass;
        private Label _lblSourceChecks;
        private ModernButton _btnSourceTest;
        private ModernButton _btnSourceConnect;

        private Label _lblDestTitle;
        private PillBadge _lblDestStatusPill;
        private Label _lblDestService;
        private ComboBox _cmbDestService;
        private Label _lblDestHost;
        private TextBox _txtDestHost;
        private Label _lblDestPort;
        private NumericUpDown _numDestPort;
        private Label _lblDestSec;
        private ComboBox _cmbDestSecurity;
        private Label _lblDestUser;
        private TextBox _txtDestUser;
        private Label _lblDestPass;
        private TextBox _txtDestPass;
        private ModernButton _btnDestShowPass;
        private Label _lblDestChecks;
        private ModernButton _btnDestTest;
        private ModernButton _btnDestConnect;

        // Folder Mapping Area
        private Panel _pnlFoldersArea;
        private ListView _lvSourceFolders;
        private DataGridView _dgvFolderMappings;
        private DataGridViewComboBoxColumn _colDestFolderSingle;
        private HashSet<string> _destFolderChoicesSingle = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "INBOX", "Archive", "Sent", "Drafts", "Trash", "Junk", "Spam"
        };
        private ComboBox _cmbPeriodFilter;
        private Label _lblPeriodIcon;
        private PillBadge _lblDedupBadge;
        private Label _lblSelectionSummary;
        private ModernButton _btnStartSingle;
        private ModernButton _btnAddToQueue;

        // Tab 2: Queue Controls
        private ListView _lvQueue;
        private ComboBox _cmbQueueConcurrency;
        private ModernButton _btnStartQueue;
        private ModernButton _btnPauseQueue;
        private ModernButton _btnStopQueue;
        private ModernButton _btnEditQueueAccount;
        private ModernButton _btnRemoveQueueAccount;
        private ModernButton _btnImportCsv;
        private ModernButton _btnExportCsv;

        // Tab 3: Monitor Controls
        private Label _lblMonitorHeading;
        private Label _lblMonitorPair;
        private Label _lblMonitorProcessed;
        private PillBadge _lblMonitorTimeLeft;
        private ModernProgressBar _progressBarOverall;
        private Label _lblStatMoved;
        private Label _lblStatSkipped;
        private Label _lblStatErrors;
        private DataGridView _dgvFolderProgress;
        private TextBox _txtLiveLog;
        private ModernButton _btnMonitorStop;
        private ModernButton _btnMonitorPause;

        // Internal State
        private ImapClient _activeSourceClient;
        private ImapClient _activeDestClient;
        private List<ImapFolder> _cachedSourceFolders = new List<ImapFolder>();
        private List<ImapFolder> _cachedDestFolders = new List<ImapFolder>();

        public MainForm()
        {
            _settings = SettingsStorage.Load();
            _engine = new MigrationEngine(_settings);
            _queue = new List<AccountPair>();

            try
            {
                using (var stream = typeof(MainForm).Assembly.GetManifestResourceStream("app.ico"))
                {
                    if (stream != null) this.Icon = new Icon(stream);
                }
                if (this.Icon == null && System.IO.File.Exists("app.ico"))
                {
                    this.Icon = new Icon("app.ico");
                }
                if (this.Icon == null)
                {
                    this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                }
            }
            catch
            {
                try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            }

            InitializeWindow();
            InitializeMenu();
            InitializeHeader();
            InitializeTabs();
            HookEngineEvents();

            ApplySettingsToSingleInputs();
        }

        private void InitializeWindow()
        {
            Text = "IMAP to IMAP Email Migration Tool";
            Size = new Size(1180, 820);
            MinimumSize = new Size(1000, 720);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(248, 250, 252);
            Font = new Font("Segoe UI", 9f);
        }

        private void InitializeMenu()
        {
            var menu = new MenuStrip
            {
                BackColor = Color.White,
                Font = new Font("Segoe UI", 9f),
                RenderMode = ToolStripRenderMode.System
            };

            // File Menu
            var mnuFile = new ToolStripMenuItem("File");
            mnuFile.DropDownItems.Add("Import Accounts from CSV...", null, (s, e) => ImportCsvClicked());
            mnuFile.DropDownItems.Add("Export Sample CSV Template...", null, (s, e) => ExportCsvTemplateClicked());
            mnuFile.DropDownItems.Add("Export Queue Results...", null, (s, e) => ExportQueueResultsClicked());
            mnuFile.DropDownItems.Add(new ToolStripSeparator());
            mnuFile.DropDownItems.Add("Settings...", null, (s, e) => OpenSettingsDialog());
            mnuFile.DropDownItems.Add(new ToolStripSeparator());
            mnuFile.DropDownItems.Add("Exit", null, (s, e) => Close());
            menu.Items.Add(mnuFile);

            // Tools Menu
            var mnuTools = new ToolStripMenuItem("Tools");
            mnuTools.DropDownItems.Add("Run Built-in Live Demo (Self-Verifying Test)", null, (s, e) => RunBuiltInDemoTest());
            mnuTools.DropDownItems.Add("Clear Migration Journal Cache", null, (s, e) =>
            {
                if (MessageBox.Show("Clear migration journal? Already transferred emails might be re-scanned.", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    MigrationJournal.Instance.Clear();
                    MessageBox.Show("Migration journal cleared.", "Journal", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            });
            menu.Items.Add(mnuTools);

            // Help Menu
            var mnuHelp = new ToolStripMenuItem("Help");
            mnuHelp.DropDownItems.Add("Key Architecture & Safety Features", null, (s, e) => ShowKeyFeaturesDialog());
            mnuHelp.DropDownItems.Add("About", null, (s, e) => MessageBox.Show(
                "IMAP to IMAP Email Migration Tool\n\n" +
                "• Direct IMAP-to-IMAP in-memory streaming\n" +
                "• Source mailbox is strictly READ-ONLY (EXAMINE mode)\n" +
                "• True Deduplication with UID & Message-ID tracking\n" +
                "• Auto-Retry with backoff (10+ retries)\n" +
                "• Batch Queue & CSV support\n\n" +
                "Portable Windows Executable (.NET 4.0 - 4.8)",
                "About IMAP Migration Tool", MessageBoxButtons.OK, MessageBoxIcon.Information));
            menu.Items.Add(mnuHelp);

            MainMenuStrip = menu;
            Controls.Add(menu);
        }

        private void InitializeHeader()
        {
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 78,
                BackColor = Color.White,
                Padding = new Padding(20, 10, 20, 10)
            };

            // Right side badges container
            var pnlBadges = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 310,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.White,
                Padding = new Padding(0, 24, 16, 0)
            };
            _headerPanel.Controls.Add(pnlBadges);

            var pillReadOnly = new PillBadge
            {
                Text = "Source Read-Only",
                Width = 142,
                Height = 26,
                BadgeColor = Color.FromArgb(240, 253, 250),
                TextBadgeColor = Color.FromArgb(15, 118, 110),
                DotColor = Color.FromArgb(20, 184, 166)
            };
            pnlBadges.Controls.Add(pillReadOnly);

            var pillDirectStream = new PillBadge
            {
                Text = "Direct Streaming",
                Width = 138,
                Height = 26,
                BadgeColor = Color.FromArgb(240, 249, 255),
                TextBadgeColor = Color.FromArgb(2, 132, 199),
                DotColor = Color.FromArgb(14, 165, 233)
            };
            pnlBadges.Controls.Add(pillDirectStream);

            // Embedded app logo icon
            try
            {
                Image logoImg = null;
                using (var stream = typeof(MainForm).Assembly.GetManifestResourceStream("icon.png"))
                {
                    if (stream != null)
                    {
                        byte[] buffer = new byte[stream.Length];
                        stream.Read(buffer, 0, buffer.Length);
                        using (var ms = new System.IO.MemoryStream(buffer))
                        {
                            logoImg = new Bitmap(ms);
                        }
                    }
                }

                if (logoImg == null && System.IO.File.Exists("icon.png"))
                {
                    using (var fs = System.IO.File.OpenRead("icon.png"))
                    {
                        byte[] buffer = new byte[fs.Length];
                        fs.Read(buffer, 0, buffer.Length);
                        using (var ms = new System.IO.MemoryStream(buffer))
                        {
                            logoImg = new Bitmap(ms);
                        }
                    }
                }

                if (logoImg == null && this.Icon != null)
                {
                    logoImg = this.Icon.ToBitmap();
                }

                if (logoImg != null)
                {
                    _pbLogo = new PictureBox
                    {
                        Image = logoImg,
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Size = new Size(48, 48),
                        Location = new Point(18, 14)
                    };
                    _headerPanel.Controls.Add(_pbLogo);
                }
            }
            catch { }

            int textLeft = _pbLogo != null ? 76 : 18;

            _lblMainTitle = new Label
            {
                Text = "Move email between mailboxes",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                AutoSize = true,
                Location = new Point(textLeft, 12)
            };
            _headerPanel.Controls.Add(_lblMainTitle);

            _lblSubTitle = new Label
            {
                Text = "Direct IMAP-to-IMAP transfer. Streams in-memory with zero disk bloat & true deduplication.",
                Font = new Font("Segoe UI", 8.75f),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize = true,
                UseMnemonic = false,
                Location = new Point(textLeft + 1, 38),
                MaximumSize = new Size(580, 0)
            };
            _headerPanel.Controls.Add(_lblSubTitle);

            Controls.Add(_headerPanel);
        }

        private void InitializeTabs()
        {
            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9f),
                Padding = new Point(16, 7)
            };

            var tabSingle = new TabPage("  Single Mailbox Migration  ");
            var tabQueue = new TabPage("  Bulk Migration Queue  ");
            var tabMonitor = new TabPage("  Migration Monitor  ");

            tabSingle.BackColor = Color.FromArgb(248, 250, 252);
            tabQueue.BackColor = Color.FromArgb(248, 250, 252);
            tabMonitor.BackColor = Color.FromArgb(248, 250, 252);

            SetupSingleTab(tabSingle);
            SetupQueueTab(tabQueue);
            SetupMonitorTab(tabMonitor);

            _tabControl.TabPages.Add(tabSingle);
            _tabControl.TabPages.Add(tabQueue);
            _tabControl.TabPages.Add(tabMonitor);

            Controls.Add(_tabControl);
            _tabControl.BringToFront();
        }

        private void SetupSingleTab(TabPage page)
        {
            _pnlTopCards = new Panel
            {
                Dock = DockStyle.Top,
                Height = 276,
                Padding = new Padding(16, 8, 16, 8),
                BackColor = Color.FromArgb(248, 250, 252)
            };
            page.Controls.Add(_pnlTopCards);

            _cardSource = new CardPanel { CornerRadius = 12 };
            BuildSourceCardControls(_cardSource);
            _pnlTopCards.Controls.Add(_cardSource);

            _lblArrow = new Label
            {
                Text = "➔",
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = Color.FromArgb(14, 165, 233),
                TextAlign = ContentAlignment.MiddleCenter
            };
            _pnlTopCards.Controls.Add(_lblArrow);

            _cardDest = new CardPanel { CornerRadius = 12 };
            BuildDestCardControls(_cardDest);
            _pnlTopCards.Controls.Add(_cardDest);

            // Responsive Card Layout Event
            _pnlTopCards.Resize += (s, e) => LayoutTopCards();
            _cardSource.Resize += (s, e) => LayoutSourceCardControls();
            _cardDest.Resize += (s, e) => LayoutDestCardControls();

            LayoutTopCards();

            // Folder Mapping Area
            _pnlFoldersArea = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16, 4, 16, 8)
            };
            page.Controls.Add(_pnlFoldersArea);
            _pnlFoldersArea.BringToFront();

            BuildFolderMappingArea(_pnlFoldersArea);
        }

        private void LayoutTopCards()
        {
            if (_pnlTopCards == null || _cardSource == null || _cardDest == null) return;
            int totalW = _pnlTopCards.ClientSize.Width - 32;
            int arrowW = 32;
            int gap = 10;
            int cardW = Math.Max(460, (totalW - arrowW - gap * 2) / 2);

            _cardSource.Location = new Point(16, 8);
            _cardSource.Size = new Size(cardW, 258);

            _lblArrow.Location = new Point(16 + cardW + gap, 8 + (258 - 30) / 2);
            _lblArrow.Size = new Size(arrowW, 30);

            _cardDest.Location = new Point(16 + cardW + gap + arrowW + gap, 8);
            _cardDest.Size = new Size(cardW, 258);
        }

        private Label CreateMicroLabel(string text, Control parent)
        {
            var lbl = new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = Color.FromArgb(71, 85, 105),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold)
            };
            parent.Controls.Add(lbl);
            return lbl;
        }

        private void BuildSourceCardControls(CardPanel card)
        {
            _lblSourceTitle = new Label
            {
                Text = "Source Mailbox (Read-Only)",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                Location = new Point(14, 10),
                AutoSize = true
            };
            card.Controls.Add(_lblSourceTitle);

            _lblSourceStatusPill = new PillBadge { Height = 24, Width = 125 };
            _lblSourceStatusPill.SetConnected(false);
            card.Controls.Add(_lblSourceStatusPill);

            // Row 1: Email service
            _lblSourceService = CreateMicroLabel("EMAIL SERVICE PROVIDER", card);
            _cmbSourceService = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 8.75f) };
            foreach (var p in EmailProviderPreset.GetPresets()) _cmbSourceService.Items.Add(p.Name);
            _cmbSourceService.SelectedIndex = 4;
            _cmbSourceService.SelectedIndexChanged += (s, e) => SourceServiceChanged();
            card.Controls.Add(_cmbSourceService);

            // Row 2: Host, Port, Security
            _lblSourceHost = CreateMicroLabel("IMAP SERVER / HOST", card);
            _txtSourceHost = new TextBox { Font = new Font("Segoe UI", 8.75f) };
            card.Controls.Add(_txtSourceHost);

            _lblSourcePort = CreateMicroLabel("PORT", card);
            _numSourcePort = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = 993, Font = new Font("Segoe UI", 8.75f) };
            card.Controls.Add(_numSourcePort);

            _lblSourceSec = CreateMicroLabel("SECURITY", card);
            _cmbSourceSecurity = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 8.75f) };
            _cmbSourceSecurity.Items.AddRange(new object[] { SecurityType.SslTls, SecurityType.StartTls, SecurityType.Plain });
            _cmbSourceSecurity.SelectedItem = SecurityType.SslTls;
            card.Controls.Add(_cmbSourceSecurity);

            // Row 3: User & Pass
            _lblSourceUser = CreateMicroLabel("USER NAME / EMAIL ADDRESS", card);
            _txtSourceUser = new TextBox { Font = new Font("Segoe UI", 8.75f) };
            card.Controls.Add(_txtSourceUser);

            _lblSourcePass = CreateMicroLabel("PASSWORD / APP PASSWORD", card);
            _txtSourcePass = new TextBox { UseSystemPasswordChar = true, Font = new Font("Segoe UI", 8.75f) };
            card.Controls.Add(_txtSourcePass);

            _btnSourceShowPass = new ModernButton
            {
                Text = "Show",
                StyleMode = ButtonStyleMode.Secondary,
                Font = new Font("Segoe UI", 8f),
                CornerRadius = 6
            };
            _btnSourceShowPass.Click += (s, e) =>
            {
                _txtSourcePass.UseSystemPasswordChar = !_txtSourcePass.UseSystemPasswordChar;
                _btnSourceShowPass.Text = _txtSourcePass.UseSystemPasswordChar ? "Show" : "Hide";
            };
            card.Controls.Add(_btnSourceShowPass);

            // Row 4: Checklist (Isolated on its own row)
            _lblSourceChecks = new Label
            {
                Text = "✓ Server responds    ✓ SSL/TLS    ✓ Sign-in    ✓ Folders",
                ForeColor = Color.FromArgb(148, 163, 184),
                AutoSize = true,
                Font = new Font("Segoe UI", 8f)
            };
            card.Controls.Add(_lblSourceChecks);

            // Row 5: Action Buttons (Isolated on their own row)
            _btnSourceTest = new ModernButton
            {
                Text = "⚡ Test Connection",
                StyleMode = ButtonStyleMode.Secondary,
                Size = new Size(130, 32)
            };
            _btnSourceTest.Click += (s, e) => TestSourceClicked();
            card.Controls.Add(_btnSourceTest);

            _btnSourceConnect = new ModernButton
            {
                Text = "Connect",
                StyleMode = ButtonStyleMode.Primary,
                Size = new Size(95, 32)
            };
            _btnSourceConnect.Click += (s, e) => ConnectSourceClicked();
            card.Controls.Add(_btnSourceConnect);

            LayoutSourceCardControls();
        }

        private void LayoutSourceCardControls()
        {
            if (_cardSource == null) return;
            int w = _cardSource.Width;

            _lblSourceStatusPill.Location = new Point(w - 14 - _lblSourceStatusPill.Width, 8);

            // Row 1
            _lblSourceService.Location = new Point(14, 36);
            _cmbSourceService.Location = new Point(14, 52);
            _cmbSourceService.Size = new Size(w - 28, 24);

            // Row 2
            int portW = 68;
            int secW = 115;
            int hostW = Math.Max(120, w - 28 - portW - secW - 16);

            _lblSourceHost.Location = new Point(14, 82);
            _txtSourceHost.Location = new Point(14, 98);
            _txtSourceHost.Size = new Size(hostW, 24);

            int portX = 14 + hostW + 8;
            _lblSourcePort.Location = new Point(portX, 82);
            _numSourcePort.Location = new Point(portX, 98);
            _numSourcePort.Size = new Size(portW, 24);

            int secX = portX + portW + 8;
            _lblSourceSec.Location = new Point(secX, 82);
            _cmbSourceSecurity.Location = new Point(secX, 98);
            _cmbSourceSecurity.Size = new Size(secW, 24);

            // Row 3
            int halfW = (w - 28 - 12) / 2;
            _lblSourceUser.Location = new Point(14, 126);
            _txtSourceUser.Location = new Point(14, 142);
            _txtSourceUser.Size = new Size(halfW, 24);

            int passX = 14 + halfW + 12;
            _lblSourcePass.Location = new Point(passX, 126);
            _txtSourcePass.Location = new Point(passX, 142);
            _txtSourcePass.Size = new Size(halfW - 48, 24);

            _btnSourceShowPass.Location = new Point(w - 14 - 44, 141);
            _btnSourceShowPass.Size = new Size(44, 25);

            // Row 4 (Checklist - Isolated on its own row)
            _lblSourceChecks.Location = new Point(14, 184);

            // Row 5 (Buttons - Isolated on their own row)
            _btnSourceConnect.Location = new Point(w - 14 - _btnSourceConnect.Width, 214);
            _btnSourceTest.Location = new Point(w - 14 - _btnSourceConnect.Width - 8 - _btnSourceTest.Width, 214);
        }

        private void BuildDestCardControls(CardPanel card)
        {
            _lblDestTitle = new Label
            {
                Text = "Destination Mailbox",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                Location = new Point(14, 10),
                AutoSize = true
            };
            card.Controls.Add(_lblDestTitle);

            _lblDestStatusPill = new PillBadge { Height = 24, Width = 125 };
            _lblDestStatusPill.SetConnected(false);
            card.Controls.Add(_lblDestStatusPill);

            // Row 1: Email service
            _lblDestService = CreateMicroLabel("EMAIL SERVICE PROVIDER", card);
            _cmbDestService = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 8.75f) };
            foreach (var p in EmailProviderPreset.GetPresets()) _cmbDestService.Items.Add(p.Name);
            _cmbDestService.SelectedIndex = 4;
            _cmbDestService.SelectedIndexChanged += (s, e) => DestServiceChanged();
            card.Controls.Add(_cmbDestService);

            // Row 2: Host, Port, Security
            _lblDestHost = CreateMicroLabel("IMAP SERVER / HOST", card);
            _txtDestHost = new TextBox { Font = new Font("Segoe UI", 8.75f) };
            card.Controls.Add(_txtDestHost);

            _lblDestPort = CreateMicroLabel("PORT", card);
            _numDestPort = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = 993, Font = new Font("Segoe UI", 8.75f) };
            card.Controls.Add(_numDestPort);

            _lblDestSec = CreateMicroLabel("SECURITY", card);
            _cmbDestSecurity = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 8.75f) };
            _cmbDestSecurity.Items.AddRange(new object[] { SecurityType.SslTls, SecurityType.StartTls, SecurityType.Plain });
            _cmbDestSecurity.SelectedItem = SecurityType.SslTls;
            card.Controls.Add(_cmbDestSecurity);

            // Row 3: User & Pass
            _lblDestUser = CreateMicroLabel("USER NAME / EMAIL ADDRESS", card);
            _txtDestUser = new TextBox { Font = new Font("Segoe UI", 8.75f) };
            card.Controls.Add(_txtDestUser);

            _lblDestPass = CreateMicroLabel("PASSWORD / APP PASSWORD", card);
            _txtDestPass = new TextBox { UseSystemPasswordChar = true, Font = new Font("Segoe UI", 8.75f) };
            card.Controls.Add(_txtDestPass);

            _btnDestShowPass = new ModernButton
            {
                Text = "Show",
                StyleMode = ButtonStyleMode.Secondary,
                Font = new Font("Segoe UI", 8f),
                CornerRadius = 6
            };
            _btnDestShowPass.Click += (s, e) =>
            {
                _txtDestPass.UseSystemPasswordChar = !_txtDestPass.UseSystemPasswordChar;
                _btnDestShowPass.Text = _txtDestPass.UseSystemPasswordChar ? "Show" : "Hide";
            };
            card.Controls.Add(_btnDestShowPass);

            // Row 4: Checklist (Isolated on its own row)
            _lblDestChecks = new Label
            {
                Text = "✓ Server responds    ✓ SSL/TLS    ✓ Sign-in    ✓ Folders",
                ForeColor = Color.FromArgb(148, 163, 184),
                AutoSize = true,
                Font = new Font("Segoe UI", 8f)
            };
            card.Controls.Add(_lblDestChecks);

            // Row 5: Action Buttons (Isolated on their own row)
            _btnDestTest = new ModernButton
            {
                Text = "⚡ Test Connection",
                StyleMode = ButtonStyleMode.Secondary,
                Size = new Size(130, 32)
            };
            _btnDestTest.Click += (s, e) => TestDestClicked();
            card.Controls.Add(_btnDestTest);

            _btnDestConnect = new ModernButton
            {
                Text = "Connect",
                StyleMode = ButtonStyleMode.Primary,
                Size = new Size(95, 32)
            };
            _btnDestConnect.Click += (s, e) => ConnectDestClicked();
            card.Controls.Add(_btnDestConnect);

            LayoutDestCardControls();
        }

        private void LayoutDestCardControls()
        {
            if (_cardDest == null) return;
            int w = _cardDest.Width;

            _lblDestStatusPill.Location = new Point(w - 14 - _lblDestStatusPill.Width, 8);

            // Row 1
            _lblDestService.Location = new Point(14, 36);
            _cmbDestService.Location = new Point(14, 52);
            _cmbDestService.Size = new Size(w - 28, 24);

            // Row 2
            int portW = 68;
            int secW = 115;
            int hostW = Math.Max(120, w - 28 - portW - secW - 16);

            _lblDestHost.Location = new Point(14, 82);
            _txtDestHost.Location = new Point(14, 98);
            _txtDestHost.Size = new Size(hostW, 24);

            int portX = 14 + hostW + 8;
            _lblDestPort.Location = new Point(portX, 82);
            _numDestPort.Location = new Point(portX, 98);
            _numDestPort.Size = new Size(portW, 24);

            int secX = portX + portW + 8;
            _lblDestSec.Location = new Point(secX, 82);
            _cmbDestSecurity.Location = new Point(secX, 98);
            _cmbDestSecurity.Size = new Size(secW, 24);

            // Row 3
            int halfW = (w - 28 - 12) / 2;
            _lblDestUser.Location = new Point(14, 126);
            _txtDestUser.Location = new Point(14, 142);
            _txtDestUser.Size = new Size(halfW, 24);

            int passX = 14 + halfW + 12;
            _lblDestPass.Location = new Point(passX, 126);
            _txtDestPass.Location = new Point(passX, 142);
            _txtDestPass.Size = new Size(halfW - 48, 24);

            _btnDestShowPass.Location = new Point(w - 14 - 44, 141);
            _btnDestShowPass.Size = new Size(44, 25);

            // Row 4 (Checklist - Isolated on its own row)
            _lblDestChecks.Location = new Point(14, 184);

            // Row 5 (Buttons - Isolated on their own row)
            _btnDestConnect.Location = new Point(w - 14 - _btnDestConnect.Width, 214);
            _btnDestTest.Location = new Point(w - 14 - _btnDestConnect.Width - 8 - _btnDestTest.Width, 214);
        }

        private static void SetupModernListView(ListView lv)
        {
            lv.OwnerDraw = true;
            lv.DrawColumnHeader += (s, e) =>
            {
                using (var bgBrush = new SolidBrush(Color.FromArgb(241, 245, 249)))
                using (var borderPen = new Pen(Color.FromArgb(226, 232, 240), 1f))
                using (var font = new Font("Segoe UI", 8.75f, FontStyle.Bold))
                {
                    e.Graphics.FillRectangle(bgBrush, e.Bounds);
                    e.Graphics.DrawLine(borderPen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                    e.Graphics.DrawLine(borderPen, e.Bounds.Right - 1, e.Bounds.Top + 4, e.Bounds.Right - 1, e.Bounds.Bottom - 5);

                    var textRect = new Rectangle(e.Bounds.Left + 8, e.Bounds.Top, e.Bounds.Width - 16, e.Bounds.Height);
                    var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
                    if (e.Header.TextAlign == HorizontalAlignment.Right)
                        flags |= TextFormatFlags.Right;
                    else if (e.Header.TextAlign == HorizontalAlignment.Center)
                        flags |= TextFormatFlags.HorizontalCenter;
                    else
                        flags |= TextFormatFlags.Left;

                    TextRenderer.DrawText(e.Graphics, e.Header.Text, font, textRect, Color.FromArgb(51, 65, 85), flags);
                }
            };
            lv.DrawItem += (s, e) => e.DrawDefault = true;
            lv.DrawSubItem += (s, e) => e.DrawDefault = true;
        }

        private void BuildFolderMappingArea(Panel parent)
        {
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 12,
                BackColor = Color.FromArgb(248, 250, 252)
            };
            parent.Controls.Add(split);

            // Left: Folders to migrate in rounded CardPanel
            var cardLeft = new CardPanel
            {
                Dock = DockStyle.Fill,
                CornerRadius = 10,
                BorderColor = Color.FromArgb(226, 232, 240),
                BackColor = Color.White,
                Padding = new Padding(12, 8, 12, 10)
            };
            split.Panel1.Controls.Add(cardLeft);

            var pnlLeftHeader = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = Color.White };
            var lblF1 = new Label { Text = "Folders to Migrate", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Location = new Point(2, 5), AutoSize = true, ForeColor = Color.FromArgb(15, 23, 42) };
            var lnkSelectAll = new LinkLabel { Text = "Select all", Font = new Font("Segoe UI", 8.5f), Location = new Point(140, 6), AutoSize = true };
            var lblSep = new Label { Text = "·", Font = new Font("Segoe UI", 8.5f), Location = new Point(195, 6), AutoSize = true, ForeColor = Color.FromArgb(148, 163, 184) };
            var lnkClearAll = new LinkLabel { Text = "Clear all", Font = new Font("Segoe UI", 8.5f), Location = new Point(205, 6), AutoSize = true };

            lnkSelectAll.LinkClicked += (s, e) => SetAllSourceFoldersChecked(true);
            lnkClearAll.LinkClicked += (s, e) => SetAllSourceFoldersChecked(false);

            pnlLeftHeader.Controls.AddRange(new Control[] { lblF1, lnkSelectAll, lblSep, lnkClearAll });
            cardLeft.Controls.Add(pnlLeftHeader);

            _lvSourceFolders = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                CheckBoxes = true,
                FullRowSelect = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                Font = new Font("Segoe UI", 8.75f),
                BorderStyle = BorderStyle.None
            };
            _lvSourceFolders.Columns.Add("Folder", 220);
            _lvSourceFolders.Columns.Add("Messages", 85, HorizontalAlignment.Right);
            SetupModernListView(_lvSourceFolders);
            _lvSourceFolders.Resize += (s, e) =>
            {
                int w = _lvSourceFolders.ClientSize.Width;
                if (w > 120)
                {
                    _lvSourceFolders.Columns[0].Width = Math.Max(100, w - 85);
                    _lvSourceFolders.Columns[1].Width = 80;
                }
            };
            _lvSourceFolders.ItemChecked += (s, e) => UpdateFolderMappingTable();
            cardLeft.Controls.Add(_lvSourceFolders);
            _lvSourceFolders.BringToFront();

            // Right: Destination mapping in rounded CardPanel
            var cardRight = new CardPanel
            {
                Dock = DockStyle.Fill,
                CornerRadius = 10,
                BorderColor = Color.FromArgb(226, 232, 240),
                BackColor = Color.White,
                Padding = new Padding(12, 8, 12, 10)
            };
            split.Panel2.Controls.Add(cardRight);

            var pnlRightHeader = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = Color.White };
            var lblF2 = new Label { Text = "Destination Folder Mapping", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Location = new Point(2, 5), AutoSize = true, ForeColor = Color.FromArgb(15, 23, 42) };
            var lnkAddTarget = new LinkLabel { Text = "+ Add new target folder", Font = new Font("Segoe UI", 8.5f), Anchor = AnchorStyles.Top | AnchorStyles.Right, AutoSize = true };
            lnkAddTarget.Location = new Point(pnlRightHeader.Width - 155, 6);
            lnkAddTarget.LinkClicked += (s, e) => PromptAddCustomDestFolderSingle();

            pnlRightHeader.Controls.AddRange(new Control[] { lblF2, lnkAddTarget });
            cardRight.Controls.Add(pnlRightHeader);

            _dgvFolderMappings = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 8.75f),
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                EditMode = DataGridViewEditMode.EditOnEnter,
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeight = 34,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
                GridColor = Color.FromArgb(226, 232, 240),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            _dgvFolderMappings.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
            _dgvFolderMappings.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(51, 65, 85);
            _dgvFolderMappings.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.75f, FontStyle.Bold);
            _dgvFolderMappings.RowTemplate.Height = 28;
            _dgvFolderMappings.DefaultCellStyle.SelectionBackColor = Color.FromArgb(224, 242, 254);
            _dgvFolderMappings.DefaultCellStyle.SelectionForeColor = Color.FromArgb(15, 23, 42);
            _dgvFolderMappings.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
            _dgvFolderMappings.DataError += (s, e) => { e.Cancel = false; };

            var colSource = new DataGridViewTextBoxColumn { HeaderText = "Source Folder", FillWeight = 48, ReadOnly = true };
            var colArrow = new DataGridViewTextBoxColumn { HeaderText = "➔", Width = 32, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true };
            colArrow.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            _colDestFolderSingle = new DataGridViewComboBoxColumn
            {
                HeaderText = "Destination Folder (Dropdown)",
                FillWeight = 52,
                FlatStyle = FlatStyle.Flat
            };
            RefreshDestDropdownChoicesSingle();

            _dgvFolderMappings.Columns.Add(colSource);
            _dgvFolderMappings.Columns.Add(colArrow);
            _dgvFolderMappings.Columns.Add(_colDestFolderSingle);

            cardRight.Controls.Add(_dgvFolderMappings);
            _dgvFolderMappings.BringToFront();

            // Maintain exact 1:1 split
            Action updateSplitter = () =>
            {
                if (split.Width > 50)
                {
                    int half = (split.Width - split.SplitterWidth) / 2;
                    if (half >= 100)
                    {
                        try { split.SplitterDistance = half; } catch { }
                    }
                }
            };
            split.Resize += (s, e) => updateSplitter();
            split.HandleCreated += (s, e) => split.BeginInvoke(updateSplitter);
            parent.Resize += (s, e) => updateSplitter();

            // Bottom Controls Bar (Period, Deduplication note, Selection count, Start button)
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 58,
                BackColor = Color.White,
                Padding = new Padding(16, 10, 16, 10)
            };
            parent.Controls.Add(pnlBottom);

            _lblPeriodIcon = new Label
            {
                Text = "Period:",
                Location = new Point(14, 19),
                AutoSize = true,
                ForeColor = Color.FromArgb(71, 85, 105),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            pnlBottom.Controls.Add(_lblPeriodIcon);

            _cmbPeriodFilter = new ComboBox
            {
                Location = new Point(66, 16),
                Size = new Size(130, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 8.5f)
            };
            _cmbPeriodFilter.Items.AddRange(new object[] { "All messages", "Last 6 months", "Last year", "Choose dates..." });
            _cmbPeriodFilter.SelectedIndex = (int)_settings.TimePeriod;
            _cmbPeriodFilter.SelectedIndexChanged += (s, e) =>
            {
                if (_cmbPeriodFilter.SelectedIndex == 3)
                {
                    OpenSettingsDialog();
                }
                else
                {
                    _settings.TimePeriod = (TimePeriodOption)_cmbPeriodFilter.SelectedIndex;
                }
            };
            pnlBottom.Controls.Add(_cmbPeriodFilter);

            _lblDedupBadge = new PillBadge
            {
                Text = "True Deduplication Active",
                Location = new Point(208, 15),
                Width = 185,
                Height = 26,
                BadgeColor = Color.FromArgb(240, 253, 250),
                TextBadgeColor = Color.FromArgb(15, 118, 110),
                DotColor = Color.FromArgb(20, 184, 166)
            };
            pnlBottom.Controls.Add(_lblDedupBadge);

            _btnStartSingle = new ModernButton
            {
                Text = "Start Migration ➔",
                StyleMode = ButtonStyleMode.Primary,
                Location = new Point(pnlBottom.Width - 16 - 135, 12),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Size = new Size(135, 34)
            };
            _btnStartSingle.Click += (s, e) => StartSingleMigrationClicked();
            pnlBottom.Controls.Add(_btnStartSingle);

            _btnAddToQueue = new ModernButton
            {
                Text = "+ Add to Queue",
                StyleMode = ButtonStyleMode.Secondary,
                Location = new Point(pnlBottom.Width - 16 - 135 - 8 - 125, 12),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Size = new Size(125, 34)
            };
            _btnAddToQueue.Click += (s, e) => AddCurrentPairToQueue();
            pnlBottom.Controls.Add(_btnAddToQueue);

            _lblSelectionSummary = new Label
            {
                Text = "Selected 0 folders · 0 messages",
                Location = new Point(pnlBottom.Width - 16 - 135 - 8 - 125 - 12 - 220, 19),
                Size = new Size(220, 20),
                TextAlign = ContentAlignment.MiddleRight,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                ForeColor = Color.FromArgb(71, 85, 105),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            pnlBottom.Controls.Add(_lblSelectionSummary);
        }

        private void SetupQueueTab(TabPage page)
        {
            var pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                Padding = new Padding(16, 10, 16, 10),
                BackColor = Color.White
            };
            page.Controls.Add(pnlTop);

            _btnImportCsv = new ModernButton
            {
                Text = "Import CSV...",
                StyleMode = ButtonStyleMode.Secondary,
                Location = new Point(16, 11),
                Size = new Size(110, 32)
            };
            _btnImportCsv.Click += (s, e) => ImportCsvClicked();
            pnlTop.Controls.Add(_btnImportCsv);

            _btnExportCsv = new ModernButton
            {
                Text = "CSV Template...",
                StyleMode = ButtonStyleMode.Secondary,
                Location = new Point(132, 11),
                Size = new Size(115, 32)
            };
            _btnExportCsv.Click += (s, e) => ExportCsvTemplateClicked();
            pnlTop.Controls.Add(_btnExportCsv);

            _btnEditQueueAccount = new ModernButton
            {
                Text = "Edit Account...",
                StyleMode = ButtonStyleMode.Secondary,
                Location = new Point(253, 11),
                Size = new Size(115, 32)
            };
            _btnEditQueueAccount.Click += (s, e) => EditSelectedQueueAccount();
            pnlTop.Controls.Add(_btnEditQueueAccount);

            _btnRemoveQueueAccount = new ModernButton
            {
                Text = "Remove",
                StyleMode = ButtonStyleMode.Secondary,
                Location = new Point(374, 11),
                Size = new Size(85, 32)
            };
            _btnRemoveQueueAccount.Click += (s, e) => RemoveSelectedQueueAccount();
            pnlTop.Controls.Add(_btnRemoveQueueAccount);

            var lblConc = new Label
            {
                Text = "Queue Mode:",
                Location = new Point(470, 18),
                AutoSize = true,
                ForeColor = Color.FromArgb(71, 85, 105),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            pnlTop.Controls.Add(lblConc);

            _cmbQueueConcurrency = new ComboBox
            {
                Location = new Point(555, 15),
                Size = new Size(130, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 8.5f)
            };
            _cmbQueueConcurrency.Items.AddRange(new object[] { "1 at a time", "3 at a time", "Sync all accounts" });
            _cmbQueueConcurrency.SelectedIndex = (int)_settings.QueueConcurrency == 3 ? 1 : ((int)_settings.QueueConcurrency == 0 ? 2 : 0);
            _cmbQueueConcurrency.SelectedIndexChanged += (s, e) =>
            {
                if (_cmbQueueConcurrency.SelectedIndex == 0) _settings.QueueConcurrency = QueueConcurrencyOption.OneAtATime;
                else if (_cmbQueueConcurrency.SelectedIndex == 1) _settings.QueueConcurrency = QueueConcurrencyOption.ThreeAtATime;
                else _settings.QueueConcurrency = QueueConcurrencyOption.SyncAll;
            };
            pnlTop.Controls.Add(_cmbQueueConcurrency);

            _btnStartQueue = new ModernButton
            {
                Text = "▶ Start Queue",
                StyleMode = ButtonStyleMode.Primary,
                Location = new Point(695, 11),
                Size = new Size(115, 32)
            };
            _btnStartQueue.Click += (s, e) => StartQueueClicked();
            pnlTop.Controls.Add(_btnStartQueue);

            _btnPauseQueue = new ModernButton
            {
                Text = "⏸ Pause",
                StyleMode = ButtonStyleMode.Secondary,
                Location = new Point(816, 11),
                Size = new Size(80, 32)
            };
            _btnPauseQueue.Click += (s, e) => TogglePauseClicked();
            pnlTop.Controls.Add(_btnPauseQueue);

            _btnStopQueue = new ModernButton
            {
                Text = "⏹ Stop",
                StyleMode = ButtonStyleMode.Danger,
                Location = new Point(902, 11),
                Size = new Size(80, 32)
            };
            _btnStopQueue.Click += (s, e) => StopClicked();
            pnlTop.Controls.Add(_btnStopQueue);

            var btnClearCompleted = new ModernButton
            {
                Text = "Clear Finished",
                StyleMode = ButtonStyleMode.Secondary,
                Location = new Point(988, 11),
                Size = new Size(115, 32)
            };
            btnClearCompleted.Click += (s, e) =>
            {
                _queue.RemoveAll(a => a.Status == AccountSyncStatus.Completed);
                RefreshQueueList();
            };
            pnlTop.Controls.Add(btnClearCompleted);

            // Queue ListView
            _lvQueue = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Font = new Font("Segoe UI", 8.75f),
                BorderStyle = BorderStyle.None
            };
            _lvQueue.DoubleClick += (s, e) => EditSelectedQueueAccount();
            _lvQueue.Columns.Add("Status", 110);
            _lvQueue.Columns.Add("From Source Mailbox", 220);
            _lvQueue.Columns.Add("To Destination Mailbox", 220);
            _lvQueue.Columns.Add("Folders", 110);
            _lvQueue.Columns.Add("Total", 65, HorizontalAlignment.Right);
            _lvQueue.Columns.Add("Moved", 65, HorizontalAlignment.Right);
            _lvQueue.Columns.Add("Skipped", 65, HorizontalAlignment.Right);
            _lvQueue.Columns.Add("Errors", 60, HorizontalAlignment.Right);
            _lvQueue.Columns.Add("Status Message", 200);

            SetupModernListView(_lvQueue);
            _lvQueue.Resize += (s, e) =>
            {
                int totalW = _lvQueue.ClientSize.Width;
                int fixedW = 475;
                int rem = totalW - fixedW - 15;
                if (rem > 200)
                {
                    _lvQueue.Columns[1].Width = (int)(rem * 0.35);
                    _lvQueue.Columns[2].Width = (int)(rem * 0.35);
                    _lvQueue.Columns[8].Width = rem - (int)(rem * 0.70);
                }
            };

            page.Controls.Add(_lvQueue);
            _lvQueue.BringToFront();
        }

        private void SetupMonitorTab(TabPage page)
        {
            var pnlMain = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18, 14, 18, 14),
                AutoScroll = true
            };
            page.Controls.Add(pnlMain);

            int initialW = Math.Max(320, pnlMain.ClientSize.Width - 36);

            // Heading & Pair subtext
            _lblMonitorHeading = new Label
            {
                Text = "Migration in progress",
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                AutoSize = true,
                Location = new Point(18, 10)
            };
            pnlMain.Controls.Add(_lblMonitorHeading);

            _lblMonitorPair = new Label
            {
                Text = "Ready to migrate",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize = true,
                Location = new Point(20, 38)
            };
            pnlMain.Controls.Add(_lblMonitorPair);

            // 1. Overall Progress Card
            var cardProgress = new CardPanel
            {
                Location = new Point(18, 66),
                Size = new Size(initialW, 118),
                CornerRadius = 10,
                BorderColor = Color.FromArgb(226, 232, 240),
                BackColor = Color.White
            };
            pnlMain.Controls.Add(cardProgress);

            _lblMonitorProcessed = new Label
            {
                Text = "0 of 0 messages processed (0%)",
                Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                AutoSize = true,
                Location = new Point(16, 12)
            };
            cardProgress.Controls.Add(_lblMonitorProcessed);

            _lblMonitorTimeLeft = new PillBadge
            {
                Text = "Idle",
                Location = new Point(cardProgress.ClientSize.Width - 146, 12),
                Width = 130,
                Height = 26,
                BadgeColor = Color.FromArgb(240, 249, 255),
                TextBadgeColor = Color.FromArgb(2, 132, 199),
                DotColor = Color.FromArgb(14, 165, 233)
            };
            cardProgress.Controls.Add(_lblMonitorTimeLeft);

            _progressBarOverall = new ModernProgressBar
            {
                Location = new Point(16, 46),
                Size = new Size(Math.Max(50, cardProgress.ClientSize.Width - 32), 16),
                Total = 100,
                Moved = 0,
                Skipped = 0,
                Errors = 0
            };
            cardProgress.Controls.Add(_progressBarOverall);

            // Stat pills
            _lblStatMoved = new Label
            {
                Text = "● Moved: 0",
                Font = new Font("Segoe UI", 8.75f, FontStyle.Bold),
                ForeColor = Color.FromArgb(2, 132, 199),
                Location = new Point(16, 76),
                AutoSize = true
            };
            cardProgress.Controls.Add(_lblStatMoved);

            _lblStatSkipped = new Label
            {
                Text = "● Already in destination (Skipped): 0",
                Font = new Font("Segoe UI", 8.75f, FontStyle.Bold),
                ForeColor = Color.FromArgb(20, 184, 166),
                Location = new Point(135, 76),
                AutoSize = true
            };
            cardProgress.Controls.Add(_lblStatSkipped);

            _lblStatErrors = new Label
            {
                Text = "● Errors: 0",
                Font = new Font("Segoe UI", 8.75f, FontStyle.Bold),
                ForeColor = Color.FromArgb(239, 68, 68),
                Location = new Point(440, 76),
                AutoSize = true
            };
            cardProgress.Controls.Add(_lblStatErrors);

            // 2. Folder Breakdown Card
            var cardFolders = new CardPanel
            {
                Location = new Point(18, 198),
                Size = new Size(initialW, 165),
                CornerRadius = 10,
                BorderColor = Color.FromArgb(226, 232, 240),
                BackColor = Color.White
            };
            pnlMain.Controls.Add(cardFolders);

            var lblFTable = new Label
            {
                Text = "Folder Breakdown",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Location = new Point(16, 10),
                AutoSize = true
            };
            cardFolders.Controls.Add(lblFTable);

            _dgvFolderProgress = new DataGridView
            {
                Location = new Point(12, 34),
                Size = new Size(Math.Max(50, cardFolders.ClientSize.Width - 24), Math.Max(50, cardFolders.ClientSize.Height - 44)),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                Font = new Font("Segoe UI", 8.75f),
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeight = 34,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
                GridColor = Color.FromArgb(226, 232, 240),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            _dgvFolderProgress.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
            _dgvFolderProgress.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(51, 65, 85);
            _dgvFolderProgress.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.75f, FontStyle.Bold);
            _dgvFolderProgress.RowTemplate.Height = 26;
            _dgvFolderProgress.DefaultCellStyle.SelectionBackColor = Color.FromArgb(224, 242, 254);
            _dgvFolderProgress.DefaultCellStyle.SelectionForeColor = Color.FromArgb(15, 23, 42);
            _dgvFolderProgress.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);

            _dgvFolderProgress.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Folder", FillWeight = 28, ReadOnly = true });
            _dgvFolderProgress.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", FillWeight = 22, ReadOnly = true });
            _dgvFolderProgress.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Progress", FillWeight = 26, ReadOnly = true });
            _dgvFolderProgress.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Moved", FillWeight = 8, ReadOnly = true });
            _dgvFolderProgress.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Skipped", FillWeight = 8, ReadOnly = true });
            _dgvFolderProgress.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Errors", FillWeight = 8, ReadOnly = true });
            cardFolders.Controls.Add(_dgvFolderProgress);

            // 3. Activity Log Card (Dark Developer Console Container)
            var cardLog = new CardPanel
            {
                Location = new Point(18, 377),
                Size = new Size(initialW, 230),
                CornerRadius = 10,
                BorderColor = Color.FromArgb(30, 41, 59),
                BackColor = Color.FromArgb(15, 23, 42)
            };
            pnlMain.Controls.Add(cardLog);

            var pnlLogHeader = new Panel
            {
                Location = new Point(14, 8),
                Size = new Size(Math.Max(50, cardLog.ClientSize.Width - 28), 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };
            cardLog.Controls.Add(pnlLogHeader);

            var lblLogHeader = new Label
            {
                Text = "Activity Log",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(241, 245, 249),
                Location = new Point(0, 2),
                AutoSize = true
            };
            pnlLogHeader.Controls.Add(lblLogHeader);

            var lnkClearLog = new LinkLabel
            {
                Text = "Clear log",
                Location = new Point(pnlLogHeader.Width - 145, 4),
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                LinkColor = Color.FromArgb(148, 163, 184),
                ActiveLinkColor = Color.White,
                Font = new Font("Segoe UI", 8.5f)
            };
            lnkClearLog.LinkClicked += (s, e) => _txtLiveLog.Clear();
            pnlLogHeader.Controls.Add(lnkClearLog);

            var lnkOpenLog = new LinkLabel
            {
                Text = "Open log file",
                Location = new Point(pnlLogHeader.Width - 85, 4),
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                LinkColor = Color.FromArgb(148, 163, 184),
                ActiveLinkColor = Color.White,
                Font = new Font("Segoe UI", 8.5f)
            };
            lnkOpenLog.LinkClicked += (s, e) => OpenLogFile();
            pnlLogHeader.Controls.Add(lnkOpenLog);

            _txtLiveLog = new TextBox
            {
                Location = new Point(14, 36),
                Size = new Size(Math.Max(50, cardLog.ClientSize.Width - 28), Math.Max(50, cardLog.ClientSize.Height - 48)),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = Color.FromArgb(226, 232, 240),
                Font = new Font("Consolas", 8.75f),
                BorderStyle = BorderStyle.None
            };
            cardLog.Controls.Add(_txtLiveLog);

            // Responsive Layout Handler for Monitor Tab
            Action updateMonitorLayout = () =>
            {
                int w = Math.Max(320, pnlMain.ClientSize.Width - 36);
                cardProgress.Width = w;
                cardFolders.Width = w;
                cardLog.Width = w;
                _progressBarOverall.Width = Math.Max(50, cardProgress.ClientSize.Width - 32);
                _lblMonitorTimeLeft.Location = new Point(cardProgress.ClientSize.Width - _lblMonitorTimeLeft.Width - 16, 12);
            };
            pnlMain.Resize += (s, e) => updateMonitorLayout();
            pnlMain.HandleCreated += (s, e) => pnlMain.BeginInvoke(updateMonitorLayout);

            // Footer note & Stop button
            var pnlFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                BackColor = Color.White,
                Padding = new Padding(16, 8, 16, 8)
            };
            page.Controls.Add(pnlFooter);

            var lblFooterSafe = new Label
            {
                Text = "Safe to Pause or Resume: Transfer is journaled. Re-running continues from the same place without duplicates.",
                ForeColor = Color.FromArgb(15, 118, 110),
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(16, 17),
                AutoSize = true
            };
            pnlFooter.Controls.Add(lblFooterSafe);

            _btnMonitorPause = new ModernButton
            {
                Text = "⏸ Pause",
                StyleMode = ButtonStyleMode.Secondary,
                Location = new Point(pnlFooter.Width - 180, 10),
                Size = new Size(80, 32),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnMonitorPause.Click += (s, e) => TogglePauseClicked();
            pnlFooter.Controls.Add(_btnMonitorPause);

            _btnMonitorStop = new ModernButton
            {
                Text = "⏹ Stop",
                StyleMode = ButtonStyleMode.Danger,
                Location = new Point(pnlFooter.Width - 92, 10),
                Size = new Size(80, 32),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnMonitorStop.Click += (s, e) => StopClicked();
            pnlFooter.Controls.Add(_btnMonitorStop);
        }

        private void HookEngineEvents()
        {
            _engine.LogEmitted += (s, e) =>
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => AppendLogText(e.Timestamp, e.Level, e.Message)));
                }
                else
                {
                    AppendLogText(e.Timestamp, e.Level, e.Message);
                }
            };

            _engine.OverallProgressChanged += (s, e) =>
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => UpdateOverallProgressUi(e)));
                }
                else
                {
                    UpdateOverallProgressUi(e);
                }
            };

            _engine.FolderProgressChanged += (s, e) =>
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => UpdateFolderProgressRow(e)));
                }
                else
                {
                    UpdateFolderProgressRow(e);
                }
            };

            _engine.AccountStarted += (s, account) =>
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() =>
                    {
                        _lblMonitorPair.Text = string.Format("{0} ➔ {1}", account.SourceUser, account.DestUser);
                        RefreshQueueList();
                    }));
                }
                else
                {
                    _lblMonitorPair.Text = string.Format("{0} ➔ {1}", account.SourceUser, account.DestUser);
                    RefreshQueueList();
                }
            };

            _engine.AccountCompleted += (s, account) =>
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() =>
                    {
                        Text = "IMAP to IMAP Email Migration Tool — Complete";
                        RefreshQueueList();
                    }));
                }
                else
                {
                    Text = "IMAP to IMAP Email Migration Tool — Complete";
                    RefreshQueueList();
                }
            };

            _engine.QueueCompleted += (s, e) =>
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() =>
                    {
                        _btnStartQueue.Enabled = true;
                        _btnStartSingle.Enabled = true;
                        _lblMonitorHeading.Text = "Migration complete";
                        _lblMonitorTimeLeft.Text = "Completed 100%";
                        MessageBox.Show("Migration queue finished successfully!", "Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }));
                }
                else
                {
                    _btnStartQueue.Enabled = true;
                    _btnStartSingle.Enabled = true;
                    _lblMonitorHeading.Text = "Migration complete";
                    _lblMonitorTimeLeft.Text = "Completed 100%";
                    MessageBox.Show("Migration queue finished successfully!", "Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
        }

        private void AppendLogText(DateTime time, string level, string message)
        {
            if (_txtLiveLog == null || _txtLiveLog.IsDisposed) return;
            string line = string.Format("[{0:HH:mm:ss}] [{1}] {2}\r\n", time, level, message);
            _txtLiveLog.AppendText(line);
        }

        private void UpdateOverallProgressUi(OverallProgressEventArgs e)
        {
            int pct = (int)e.Percentage;
            _lblMonitorProcessed.Text = string.Format("{0:N0} of {1:N0} messages processed ({2}%)", e.ProcessedMessages, e.TotalMessages, pct);
            _lblMonitorTimeLeft.Text = string.Format("{0}", e.TimeRemainingText);

            _progressBarOverall.Total = Math.Max(1, e.TotalMessages);
            _progressBarOverall.Moved = e.MovedMessages;
            _progressBarOverall.Skipped = e.SkippedMessages;
            _progressBarOverall.Errors = e.ErrorMessages;

            _lblStatMoved.Text = string.Format("● Moved: {0:N0}", e.MovedMessages);
            _lblStatSkipped.Text = string.Format("● Already in destination (Skipped): {0:N0}", e.SkippedMessages);
            _lblStatErrors.Text = string.Format("● Errors: {0:N0}", e.ErrorMessages);

            Text = string.Format("IMAP to IMAP Email Migration Tool — migrating {0}%", pct);
        }

        private void UpdateFolderProgressRow(FolderProgressEventArgs e)
        {
            foreach (DataGridViewRow row in _dgvFolderProgress.Rows)
            {
                object valObj = row.Cells[0].Value;
                string valStr = valObj != null ? valObj.ToString() : null;
                if (string.Equals(valStr, e.FolderName, StringComparison.OrdinalIgnoreCase))
                {
                    row.Cells[1].Value = e.Status;
                    row.Cells[2].Value = string.Format("{0:F0}% ({1}/{2})", e.Percentage, e.Processed, e.Total);
                    row.Cells[3].Value = e.Moved;
                    row.Cells[4].Value = e.Skipped;
                    row.Cells[5].Value = e.Errors;
                    return;
                }
            }

            // New folder row
            _dgvFolderProgress.Rows.Add(e.FolderName, e.Status, string.Format("{0:F0}% ({1}/{2})", e.Percentage, e.Processed, e.Total), e.Moved, e.Skipped, e.Errors);
        }

        private void ApplySettingsToSingleInputs()
        {
            _txtSourceHost.Text = _settings.DefaultSourceHost;
            _numSourcePort.Value = _settings.DefaultSourcePort;
            _cmbSourceSecurity.SelectedItem = _settings.DefaultSourceSecurity;

            _txtDestHost.Text = _settings.DefaultDestHost;
            _numDestPort.Value = _settings.DefaultDestPort;
            _cmbDestSecurity.SelectedItem = _settings.DefaultDestSecurity;

            _cmbPeriodFilter.SelectedIndex = (int)_settings.TimePeriod;
        }

        private void SourceServiceChanged()
        {
            string sel = _cmbSourceService.SelectedItem != null ? _cmbSourceService.SelectedItem.ToString() : null;
            var preset = EmailProviderPreset.GetPresets().Find(p => p.Name == sel);
            if (preset != null && sel != "Other IMAP server")
            {
                _txtSourceHost.Text = preset.DefaultHost;
                _numSourcePort.Value = preset.DefaultPort;
                _cmbSourceSecurity.SelectedItem = preset.Security;
            }
        }

        private void DestServiceChanged()
        {
            string sel = _cmbDestService.SelectedItem != null ? _cmbDestService.SelectedItem.ToString() : null;
            var preset = EmailProviderPreset.GetPresets().Find(p => p.Name == sel);
            if (preset != null && sel != "Other IMAP server")
            {
                _txtDestHost.Text = preset.DefaultHost;
                _numDestPort.Value = preset.DefaultPort;
                _cmbDestSecurity.SelectedItem = preset.Security;
            }
        }

        private void TestSourceClicked()
        {
            _btnSourceTest.Enabled = false;
            _lblSourceChecks.Text = "Testing connection...";
            _lblSourceChecks.ForeColor = Color.FromArgb(2, 132, 199);

            Task.Factory.StartNew(() =>
            {
                var client = new ImapClient(_txtSourceHost.Text.Trim(), (int)_numSourcePort.Value, (SecurityType)_cmbSourceSecurity.SelectedItem);
                var res = client.TestConnection(_txtSourceUser.Text.Trim(), _txtSourcePass.Text);

                BeginInvoke(new Action(() =>
                {
                    _btnSourceTest.Enabled = true;
                    if (res.SignInSuccess && res.FoldersSuccess)
                    {
                        _lblSourceChecks.Text = string.Format("✓ Server responds   ✓ SSL/TLS   ✓ Sign-in   ✓ {0} Folders", res.FolderCount);
                        _lblSourceChecks.ForeColor = Color.FromArgb(16, 185, 129);
                    }
                    else
                    {
                        _lblSourceChecks.Text = "✗ Test failed: " + res.ErrorMessage;
                        _lblSourceChecks.ForeColor = Color.FromArgb(239, 68, 68);
                    }
                }));
            });
        }

        private void TestDestClicked()
        {
            _btnDestTest.Enabled = false;
            _lblDestChecks.Text = "Testing connection...";
            _lblDestChecks.ForeColor = Color.FromArgb(2, 132, 199);

            Task.Factory.StartNew(() =>
            {
                var client = new ImapClient(_txtDestHost.Text.Trim(), (int)_numDestPort.Value, (SecurityType)_cmbDestSecurity.SelectedItem);
                var res = client.TestConnection(_txtDestUser.Text.Trim(), _txtDestPass.Text);

                BeginInvoke(new Action(() =>
                {
                    _btnDestTest.Enabled = true;
                    if (res.SignInSuccess && res.FoldersSuccess)
                    {
                        _lblDestChecks.Text = string.Format("✓ Server responds   ✓ SSL/TLS   ✓ Sign-in   ✓ {0} Folders", res.FolderCount);
                        _lblDestChecks.ForeColor = Color.FromArgb(16, 185, 129);
                    }
                    else
                    {
                        _lblDestChecks.Text = "✗ Test failed: " + res.ErrorMessage;
                        _lblDestChecks.ForeColor = Color.FromArgb(239, 68, 68);
                    }
                }));
            });
        }

        private void ConnectSourceClicked()
        {
            _btnSourceConnect.Enabled = false;
            _lblSourceStatusPill.SetConnecting("Connecting...");

            Task.Factory.StartNew(() =>
            {
                try
                {
                    if (_activeSourceClient != null) _activeSourceClient.Dispose();
                    _activeSourceClient = new ImapClient(_txtSourceHost.Text.Trim(), (int)_numSourcePort.Value, (SecurityType)_cmbSourceSecurity.SelectedItem);
                    _activeSourceClient.Connect();
                    _activeSourceClient.Login(_txtSourceUser.Text.Trim(), _txtSourcePass.Text);

                    var folders = _activeSourceClient.ListFolders();
                    foreach (var f in folders.Where(x => x.IsSelectable))
                    {
                        f.MessageCount = _activeSourceClient.ExamineFolder(f.Name);
                    }
                    _cachedSourceFolders = folders;

                    BeginInvoke(new Action(() =>
                    {
                        _lblSourceStatusPill.SetConnected(true);
                        _btnSourceConnect.Enabled = true;
                        PopulateSourceFoldersList();
                    }));
                }
                catch (Exception ex)
                {
                    BeginInvoke(new Action(() =>
                    {
                        _lblSourceStatusPill.SetError("Error");
                        _btnSourceConnect.Enabled = true;
                        MessageBox.Show("Source IMAP connection failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
            });
        }

        private void ConnectDestClicked()
        {
            _btnDestConnect.Enabled = false;
            _lblDestStatusPill.SetConnecting("Connecting...");

            Task.Factory.StartNew(() =>
            {
                try
                {
                    if (_activeDestClient != null) _activeDestClient.Dispose();
                    _activeDestClient = new ImapClient(_txtDestHost.Text.Trim(), (int)_numDestPort.Value, (SecurityType)_cmbDestSecurity.SelectedItem);
                    _activeDestClient.Connect();
                    _activeDestClient.Login(_txtDestUser.Text.Trim(), _txtDestPass.Text);

                    var folders = _activeDestClient.ListFolders();
                    _cachedDestFolders = folders;

                    BeginInvoke(new Action(() =>
                    {
                        _lblDestStatusPill.SetConnected(true);
                        _btnDestConnect.Enabled = true;

                        foreach (var f in _cachedDestFolders)
                        {
                            _destFolderChoicesSingle.Add(f.Name);
                        }
                        RefreshDestDropdownChoicesSingle();
                        UpdateFolderMappingTable();
                    }));
                }
                catch (Exception ex)
                {
                    BeginInvoke(new Action(() =>
                    {
                        _lblDestStatusPill.SetError("Error");
                        _btnDestConnect.Enabled = true;
                        MessageBox.Show("Destination IMAP connection failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
            });
        }

        private void PopulateSourceFoldersList()
        {
            _lvSourceFolders.Items.Clear();
            foreach (var f in _cachedSourceFolders.Where(x => x.IsSelectable))
            {
                var item = new ListViewItem(f.Name);
                item.SubItems.Add(f.MessageCount.ToString("N0"));
                item.Checked = true;
                item.Tag = f;
                _lvSourceFolders.Items.Add(item);

                _destFolderChoicesSingle.Add(f.Name);
            }
            RefreshDestDropdownChoicesSingle();
            UpdateFolderMappingTable();
        }

        private void RefreshDestDropdownChoicesSingle()
        {
            if (_colDestFolderSingle == null) return;
            _colDestFolderSingle.Items.Clear();
            foreach (var f in _destFolderChoicesSingle.OrderBy(x => x))
            {
                _colDestFolderSingle.Items.Add(f);
            }
        }

        private void PromptAddCustomDestFolderSingle()
        {
            string folder = PromptInput("Enter new destination target folder name (e.g. Archive, Old_Mails):", "Add Target Folder", "Archive");
            if (!string.IsNullOrWhiteSpace(folder))
            {
                string trimF = folder.Trim();
                _destFolderChoicesSingle.Add(trimF);
                RefreshDestDropdownChoicesSingle();

                if (_dgvFolderMappings.SelectedRows.Count > 0)
                {
                    _dgvFolderMappings.SelectedRows[0].Cells[2].Value = trimF;
                }
            }
        }

        private string PromptInput(string prompt, string title, string defaultValue)
        {
            using (var form = new Form())
            {
                form.Text = title;
                form.Size = new Size(420, 160);
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.Font = new Font("Segoe UI", 9f);

                var lbl = new Label { Text = prompt, Location = new Point(16, 16), AutoSize = true };
                var txt = new TextBox { Text = defaultValue, Location = new Point(16, 42), Size = new Size(370, 24) };
                var btnOk = new ModernButton { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(215, 82), Size = new Size(80, 30) };
                var btnCancel = new ModernButton { Text = "Cancel", StyleMode = ButtonStyleMode.Secondary, DialogResult = DialogResult.Cancel, Location = new Point(305, 82), Size = new Size(80, 30) };

                form.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
                form.AcceptButton = btnOk;
                form.CancelButton = btnCancel;

                return form.ShowDialog(this) == DialogResult.OK ? txt.Text.Trim() : null;
            }
        }

        private void SetAllSourceFoldersChecked(bool check)
        {
            foreach (ListViewItem item in _lvSourceFolders.Items)
            {
                item.Checked = check;
            }
            UpdateFolderMappingTable();
        }

        private void UpdateFolderMappingTable()
        {
            var existing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataGridViewRow r in _dgvFolderMappings.Rows)
            {
                string s = r.Cells[0].Value != null ? r.Cells[0].Value.ToString() : null;
                string d = r.Cells[2].Value != null ? r.Cells[2].Value.ToString() : null;
                if (!string.IsNullOrEmpty(s) && !string.IsNullOrEmpty(d))
                {
                    existing[s] = d;
                }
            }

            _dgvFolderMappings.Rows.Clear();
            int selectedFolders = 0;
            int totalMsgs = 0;

            foreach (ListViewItem item in _lvSourceFolders.Items)
            {
                if (item.Checked)
                {
                    var f = item.Tag as ImapFolder;
                    string src = item.Text;
                    string dst = src;

                    if (existing.ContainsKey(src))
                    {
                        dst = existing[src];
                    }
                    else if (string.Equals(src, "inbox", StringComparison.OrdinalIgnoreCase))
                    {
                        dst = "INBOX";
                    }

                    if (!_destFolderChoicesSingle.Contains(dst))
                    {
                        _destFolderChoicesSingle.Add(dst);
                        RefreshDestDropdownChoicesSingle();
                    }

                    _dgvFolderMappings.Rows.Add(src, "➔", dst);

                    selectedFolders++;
                    if (f != null) totalMsgs += f.MessageCount;
                }
            }

            _lblSelectionSummary.Text = string.Format("Selected {0} folder(s) · {1:N0} messages", selectedFolders, totalMsgs);
        }

        private AccountPair CreateCurrentAccountPair()
        {
            var acc = new AccountPair
            {
                SourceProvider = _cmbSourceService.SelectedItem != null ? _cmbSourceService.SelectedItem.ToString() : null,
                SourceHost = _txtSourceHost.Text.Trim(),
                SourcePort = (int)_numSourcePort.Value,
                SourceSecurity = (SecurityType)_cmbSourceSecurity.SelectedItem,
                SourceUser = _txtSourceUser.Text.Trim(),
                SourcePassword = _txtSourcePass.Text,

                DestProvider = _cmbDestService.SelectedItem != null ? _cmbDestService.SelectedItem.ToString() : null,
                DestHost = _txtDestHost.Text.Trim(),
                DestPort = (int)_numDestPort.Value,
                DestSecurity = (SecurityType)_cmbDestSecurity.SelectedItem,
                DestUser = _txtDestUser.Text.Trim(),
                DestPassword = _txtDestPass.Text
            };

            foreach (DataGridViewRow row in _dgvFolderMappings.Rows)
            {
                object srcObj = row.Cells[0].Value;
                object dstObj = row.Cells[2].Value;
                string src = srcObj != null ? srcObj.ToString() : null;
                string dst = dstObj != null ? dstObj.ToString() : null;
                if (!string.IsNullOrEmpty(src))
                {
                    var cached = _cachedSourceFolders.Find(f => f.Name == src);
                    acc.FolderMappings.Add(new FolderMapping
                    {
                        SourceFolder = src,
                        DestFolder = string.IsNullOrEmpty(dst) ? src : dst,
                        Selected = true,
                        TotalMessages = cached != null ? cached.MessageCount : 0
                    });
                }
            }

            if (acc.FolderMappings.Count == 0)
            {
                acc.FolderMappings.Add(new FolderMapping { SourceFolder = "INBOX", DestFolder = "INBOX", Selected = true });
            }

            return acc;
        }

        private void AddCurrentPairToQueue()
        {
            var pair = CreateCurrentAccountPair();
            if (string.IsNullOrEmpty(pair.SourceUser) || string.IsNullOrEmpty(pair.DestUser))
            {
                MessageBox.Show("Please enter both source and destination user credentials.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _queue.Add(pair);
            RefreshQueueList();
            _tabControl.SelectedTab = _tabControl.TabPages[1];
        }

        private void StartSingleMigrationClicked()
        {
            var pair = CreateCurrentAccountPair();
            if (string.IsNullOrEmpty(pair.SourceUser) || string.IsNullOrEmpty(pair.DestUser))
            {
                MessageBox.Show("Please connect or fill credentials for both source and destination.", "Required Fields", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _tabControl.SelectedTab = _tabControl.TabPages[2];
            _dgvFolderProgress.Rows.Clear();
            _txtLiveLog.Clear();
            _lblMonitorHeading.Text = "Migration in progress";

            _btnStartSingle.Enabled = false;
            _engine.RunQueueAsync(new List<AccountPair> { pair });
        }

        private void StartQueueClicked()
        {
            if (_queue.Count == 0)
            {
                MessageBox.Show("The queue is empty. Add accounts or import a CSV file.", "Empty Queue", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _tabControl.SelectedTab = _tabControl.TabPages[2];
            _dgvFolderProgress.Rows.Clear();
            _txtLiveLog.Clear();
            _lblMonitorHeading.Text = "Migration in progress";

            _btnStartQueue.Enabled = false;
            _engine.RunQueueAsync(_queue);
        }

        private void TogglePauseClicked()
        {
            if (_engine.IsPaused)
            {
                _engine.Resume();
                _btnPauseQueue.Text = "⏸ Pause";
                _btnMonitorPause.Text = "⏸ Pause";
                _lblMonitorHeading.Text = "Migration in progress";
            }
            else
            {
                _engine.Pause();
                _btnPauseQueue.Text = "▶ Resume";
                _btnMonitorPause.Text = "▶ Resume";
                _lblMonitorHeading.Text = "Migration paused";
            }
        }

        private void StopClicked()
        {
            if (MessageBox.Show("Are you sure you want to stop migration? Transfer can be resumed safely anytime.", "Stop Migration", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _engine.Stop();
                _lblMonitorHeading.Text = "Migration stopped";
            }
        }

        private void RefreshQueueList()
        {
            _lvQueue.Items.Clear();
            foreach (var a in _queue)
            {
                var item = new ListViewItem(a.Status.ToString());
                item.SubItems.Add(a.SourceUser + " (" + a.SourceHost + ")");
                item.SubItems.Add(a.DestUser + " (" + a.DestHost + ")");
                item.SubItems.Add(string.Join(", ", a.FolderMappings.Select(f => f.SourceFolder)));
                item.SubItems.Add(a.TotalMessages.ToString("N0"));
                item.SubItems.Add(a.MovedMessages.ToString("N0"));
                item.SubItems.Add(a.SkippedMessages.ToString("N0"));
                item.SubItems.Add(a.ErrorMessages.ToString("N0"));
                item.SubItems.Add(a.StatusMessage ?? "");
                _lvQueue.Items.Add(item);
            }
        }

        private void EditSelectedQueueAccount()
        {
            if (_lvQueue.SelectedIndices.Count > 0)
            {
                int idx = _lvQueue.SelectedIndices[0];
                if (idx >= 0 && idx < _queue.Count)
                {
                    var acc = _queue[idx];
                    using (var dlg = new AccountEditDialog(acc))
                    {
                        if (dlg.ShowDialog(this) == DialogResult.OK)
                        {
                            RefreshQueueList();
                        }
                    }
                }
            }
            else
            {
                MessageBox.Show("Please select an account in the queue to edit.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void RemoveSelectedQueueAccount()
        {
            if (_lvQueue.SelectedIndices.Count > 0)
            {
                int idx = _lvQueue.SelectedIndices[0];
                if (idx >= 0 && idx < _queue.Count)
                {
                    _queue.RemoveAt(idx);
                    RefreshQueueList();
                }
            }
        }

        private void ImportCsvClicked()
        {
            using (var ofd = new OpenFileDialog { Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*", Title = "Import Accounts CSV" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var imported = CsvHandler.ImportCsv(ofd.FileName, _settings);
                        if (imported.Count > 0)
                        {
                            _queue.AddRange(imported);
                            RefreshQueueList();
                            _tabControl.SelectedTab = _tabControl.TabPages[1];
                            MessageBox.Show(string.Format("Successfully imported {0} account pair(s) from CSV.", imported.Count), "Import Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show("No valid account rows found in CSV. Please verify column names.", "Import Notice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("CSV Import Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ExportCsvTemplateClicked()
        {
            using (var sfd = new SaveFileDialog { Filter = "CSV Files (*.csv)|*.csv", FileName = "migration_accounts_template.csv", Title = "Export Sample CSV Template" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    CsvHandler.CreateSampleCsv(sfd.FileName);
                    MessageBox.Show("Sample CSV template saved: " + sfd.FileName, "Exported", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void ExportQueueResultsClicked()
        {
            using (var sfd = new SaveFileDialog { Filter = "CSV Files (*.csv)|*.csv", FileName = "migration_queue_results.csv", Title = "Export Queue Results" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    CsvHandler.ExportQueueToCsv(sfd.FileName, _queue);
                    MessageBox.Show("Queue results exported: " + sfd.FileName, "Exported", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void OpenSettingsDialog()
        {
            using (var dlg = new SettingsDialog(_settings))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _settings = dlg.CurrentSettings;
                    _engine.Settings = _settings;
                    ApplySettingsToSingleInputs();
                }
            }
        }

        private void OpenLogFile()
        {
            try
            {
                string tempLog = Path.Combine(Path.GetTempPath(), "imap_migration_live.log");
                File.WriteAllLines(tempLog, _engine.GetLogHistory());
                Process.Start("notepad.exe", tempLog);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Cannot open log: " + ex.Message, "Log", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ShowKeyFeaturesDialog()
        {
            MessageBox.Show(
                "KEY ADVANTAGES OF OUR IMAP TO IMAP MIGRATION TOOL:\n\n" +
                "1. ALL-IN-ONE INTERFACE:\n" +
                "   Map source and destination folders side-by-side with intuitive dropdown select.\n\n" +
                "2. DIRECT IMAP-TO-IMAP STREAMING:\n" +
                "   Never bloats your local disk. Streams RFC822 raw messages in-memory chunk-by-chunk.\n\n" +
                "3. READ-ONLY SECURITY ON SOURCE:\n" +
                "   Opens source folders in EXAMINE mode and fetches using BODY.PEEK[] so source flags are untouched.\n\n" +
                "4. SAFE MIGRATION JOURNAL & TRUE DEDUPLICATION:\n" +
                "   Continuous tracking by UID and Message-ID prevents duplicate emails upon retry or re-run.\n\n" +
                "5. BYTE-FOR-BYTE PRECISION:\n" +
                "   Preserves original INTERNALDATE, flags (\\Seen, \\Flagged), headers, and attachments intact.\n\n" +
                "6. AUTOMATIC RECOVERY (10+ RETRIES):\n" +
                "   Auto-recovers from socket drops, server rate limits, and network hiccups with exponential backoff.",
                "Key Architecture & Advantages", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void RunBuiltInDemoTest()
        {
            try
            {
                if (_demoSourceServer != null) _demoSourceServer.Dispose();
                if (_demoDestServer != null) _demoDestServer.Dispose();

                _demoSourceServer = new MockImapServer(0);
                _demoSourceServer.PopulateSampleMessages(20, 5);

                _demoDestServer = new MockImapServer(0);

                // Populate UI with loopback demo parameters
                _txtSourceHost.Text = "127.0.0.1";
                _numSourcePort.Value = _demoSourceServer.Port;
                _cmbSourceSecurity.SelectedItem = SecurityType.Plain;
                _txtSourceUser.Text = "demo_source@company.org";
                _txtSourcePass.Text = "password123";

                _txtDestHost.Text = "127.0.0.1";
                _numDestPort.Value = _demoDestServer.Port;
                _cmbDestSecurity.SelectedItem = SecurityType.Plain;
                _txtDestUser.Text = "demo_dest@company.org";
                _txtDestPass.Text = "password456";

                _tabControl.SelectedTab = _tabControl.TabPages[0];

                // Auto-connect both in demo
                ConnectSourceClicked();
                ConnectDestClicked();

                MessageBox.Show(string.Format(
                    "Built-in Demo IMAP Servers started successfully!\n\n" +
                    "Source Mock Server on port {0} (25 test emails in INBOX and Spam)\n" +
                    "Destination Mock Server on port {1}\n\n" +
                    "Click 'Start Migration ➔' to watch direct streaming, deduplication, and animated progress in action!",
                    _demoSourceServer.Port, _demoDestServer.Port),
                    "Demo Servers Active", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to launch demo test servers: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_engine != null) _engine.Stop();
            if (_demoSourceServer != null) _demoSourceServer.Dispose();
            if (_demoDestServer != null) _demoDestServer.Dispose();
            if (_activeSourceClient != null) _activeSourceClient.Dispose();
            if (_activeDestClient != null) _activeDestClient.Dispose();
            base.OnFormClosing(e);
        }
    }
}
