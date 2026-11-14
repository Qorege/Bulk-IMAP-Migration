using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ImapSyncWin.Controls;

namespace ImapSyncWin
{
    public class AccountEditDialog : Form
    {
        public AccountPair Account { get; private set; }

        private Panel _pnlCards;
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

        private ListView _lvSourceFolders;
        private DataGridView _dgvFolderMappings;
        private DataGridViewComboBoxColumn _colDestFolder;

        private ImapClient _activeSourceClient;
        private ImapClient _activeDestClient;
        private List<ImapFolder> _cachedSourceFolders;
        private List<ImapFolder> _cachedDestFolders;
        private HashSet<string> _destFolderChoices;

        public AccountEditDialog(AccountPair account)
        {
            Account = account ?? new AccountPair();
            _cachedSourceFolders = new List<ImapFolder>();
            _cachedDestFolders = new List<ImapFolder>();
            _destFolderChoices = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "INBOX", "Archive", "Sent", "Drafts", "Trash", "Junk", "Spam"
            };

            try
            {
                using (var stream = typeof(AccountEditDialog).Assembly.GetManifestResourceStream("app.ico"))
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
            catch { }

            InitializeComponent();
            LoadAccountToUi();
        }

        private void InitializeComponent()
        {
            Text = "Edit Account Migration Pair & Folder Mappings";
            Size = new Size(1160, 800);
            MinimumSize = new Size(1000, 700);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(248, 250, 252);
            Font = new Font("Segoe UI", 9f);

            // Header Panel
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = Color.White,
                Padding = new Padding(20, 10, 20, 10)
            };
            Controls.Add(pnlHeader);

            var lblHeaderTitle = new Label
            {
                Text = "Edit Account Migration Pair",
                Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                Location = new Point(18, 10),
                AutoSize = true
            };
            pnlHeader.Controls.Add(lblHeaderTitle);

            var lblHeaderSub = new Label
            {
                Text = "Configure mail server credentials, test connectivity, and map folders via dropdown menu.",
                Font = new Font("Segoe UI", 8.75f),
                ForeColor = Color.FromArgb(100, 116, 139),
                Location = new Point(20, 36),
                AutoSize = true
            };
            pnlHeader.Controls.Add(lblHeaderSub);

            // Main Content Area
            var pnlBody = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16, 8, 16, 8),
                AutoScroll = true
            };
            Controls.Add(pnlBody);
            pnlBody.BringToFront();

            // Cards Panel (Top)
            _pnlCards = new Panel { Dock = DockStyle.Top, Height = 276 };
            pnlBody.Controls.Add(_pnlCards);

            _cardSource = new CardPanel { CornerRadius = 12 };
            BuildSourceCardControls(_cardSource);
            _pnlCards.Controls.Add(_cardSource);

            _lblArrow = new Label
            {
                Text = "➔",
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = Color.FromArgb(14, 165, 233),
                TextAlign = ContentAlignment.MiddleCenter
            };
            _pnlCards.Controls.Add(_lblArrow);

            _cardDest = new CardPanel { CornerRadius = 12 };
            BuildDestCardControls(_cardDest);
            _pnlCards.Controls.Add(_cardDest);

            _pnlCards.Resize += (s, e) => LayoutCards();
            _cardSource.Resize += (s, e) => LayoutSourceCard();
            _cardDest.Resize += (s, e) => LayoutDestCard();
            LayoutCards();

            // Folder Mapping Area (Bottom)
            var pnlFolders = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(4, 4, 4, 8)
            };
            pnlBody.Controls.Add(pnlFolders);
            pnlFolders.BringToFront();

            BuildFolderMappingArea(pnlFolders);

            // Bottom Buttons Bar
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                BackColor = Color.White,
                Padding = new Padding(16, 10, 16, 10)
            };
            Controls.Add(pnlBottom);
            pnlBottom.BringToFront();

            var btnCancel = new ModernButton
            {
                Text = "Cancel",
                StyleMode = ButtonStyleMode.Secondary,
                Location = new Point(pnlBottom.Width - 240, 11),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Size = new Size(95, 34)
            };
            btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;
            pnlBottom.Controls.Add(btnCancel);

            var btnSave = new ModernButton
            {
                Text = "Save Changes",
                StyleMode = ButtonStyleMode.Primary,
                Location = new Point(pnlBottom.Width - 135, 11),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Size = new Size(125, 34)
            };
            btnSave.Click += (s, e) => SaveAndClose();
            pnlBottom.Controls.Add(btnSave);
        }

        private void LayoutCards()
        {
            if (_pnlCards == null || _cardSource == null || _cardDest == null) return;
            int totalW = _pnlCards.ClientSize.Width - 20;
            int arrowW = 32;
            int gap = 10;
            int cardW = Math.Max(460, (totalW - arrowW - gap * 2) / 2);

            _cardSource.Location = new Point(10, 6);
            _cardSource.Size = new Size(cardW, 260);

            _lblArrow.Location = new Point(10 + cardW + gap, 6 + (260 - 30) / 2);
            _lblArrow.Size = new Size(arrowW, 30);

            _cardDest.Location = new Point(10 + cardW + gap + arrowW + gap, 6);
            _cardDest.Size = new Size(cardW, 260);
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

            _lblSourceService = CreateMicroLabel("EMAIL SERVICE PROVIDER", card);
            _cmbSourceService = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 8.75f) };
            foreach (var p in EmailProviderPreset.GetPresets()) _cmbSourceService.Items.Add(p.Name);
            _cmbSourceService.SelectedIndex = 4;
            _cmbSourceService.SelectedIndexChanged += (s, e) => SourceServicePresetChanged();
            card.Controls.Add(_cmbSourceService);

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

            _lblSourceChecks = new Label
            {
                Text = "✓ Server responds    ✓ SSL/TLS    ✓ Sign-in    ✓ Folders",
                ForeColor = Color.FromArgb(148, 163, 184),
                AutoSize = true,
                Font = new Font("Segoe UI", 8f)
            };
            card.Controls.Add(_lblSourceChecks);

            _btnSourceTest = new ModernButton { Text = "⚡ Test Connection", StyleMode = ButtonStyleMode.Secondary, Size = new Size(130, 32) };
            _btnSourceTest.Click += (s, e) => TestSourceClicked();
            card.Controls.Add(_btnSourceTest);

            _btnSourceConnect = new ModernButton { Text = "Connect", StyleMode = ButtonStyleMode.Primary, Size = new Size(95, 32) };
            _btnSourceConnect.Click += (s, e) => ConnectSourceClicked();
            card.Controls.Add(_btnSourceConnect);

            LayoutSourceCard();
        }

        private void LayoutSourceCard()
        {
            if (_cardSource == null) return;
            int w = _cardSource.Width;

            _lblSourceStatusPill.Location = new Point(w - 14 - _lblSourceStatusPill.Width, 8);

            _lblSourceService.Location = new Point(14, 36);
            _cmbSourceService.Location = new Point(14, 52);
            _cmbSourceService.Size = new Size(w - 28, 24);

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

            _lblDestService = CreateMicroLabel("EMAIL SERVICE PROVIDER", card);
            _cmbDestService = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 8.75f) };
            foreach (var p in EmailProviderPreset.GetPresets()) _cmbDestService.Items.Add(p.Name);
            _cmbDestService.SelectedIndex = 4;
            _cmbDestService.SelectedIndexChanged += (s, e) => DestServicePresetChanged();
            card.Controls.Add(_cmbDestService);

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

            _lblDestChecks = new Label
            {
                Text = "✓ Server responds    ✓ SSL/TLS    ✓ Sign-in    ✓ Folders",
                ForeColor = Color.FromArgb(148, 163, 184),
                AutoSize = true,
                Font = new Font("Segoe UI", 8f)
            };
            card.Controls.Add(_lblDestChecks);

            _btnDestTest = new ModernButton { Text = "⚡ Test Connection", StyleMode = ButtonStyleMode.Secondary, Size = new Size(130, 32) };
            _btnDestTest.Click += (s, e) => TestDestClicked();
            card.Controls.Add(_btnDestTest);

            _btnDestConnect = new ModernButton { Text = "Connect", StyleMode = ButtonStyleMode.Primary, Size = new Size(95, 32) };
            _btnDestConnect.Click += (s, e) => ConnectDestClicked();
            card.Controls.Add(_btnDestConnect);

            LayoutDestCard();
        }

        private void LayoutDestCard()
        {
            if (_cardDest == null) return;
            int w = _cardDest.Width;

            _lblDestStatusPill.Location = new Point(w - 14 - _lblDestStatusPill.Width, 8);

            _lblDestService.Location = new Point(14, 36);
            _cmbDestService.Location = new Point(14, 52);
            _cmbDestService.Size = new Size(w - 28, 24);

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
            var lnkSelectAll = new LinkLabel { Text = "Select all", Font = new Font("Segoe UI", 8.5f), Location = new Point(135, 6), AutoSize = true };
            var lblSep1 = new Label { Text = "·", Font = new Font("Segoe UI", 8.5f), Location = new Point(188, 6), AutoSize = true, ForeColor = Color.FromArgb(148, 163, 184) };
            var lnkClearAll = new LinkLabel { Text = "Clear all", Font = new Font("Segoe UI", 8.5f), Location = new Point(198, 6), AutoSize = true };
            var lblSep2 = new Label { Text = "·", Font = new Font("Segoe UI", 8.5f), Location = new Point(245, 6), AutoSize = true, ForeColor = Color.FromArgb(148, 163, 184) };
            var lnkAddFolder = new LinkLabel { Text = "+ Add folder", Font = new Font("Segoe UI", 8.5f), Location = new Point(255, 6), AutoSize = true };

            lnkSelectAll.LinkClicked += (s, e) => SetAllSourceFoldersChecked(true);
            lnkClearAll.LinkClicked += (s, e) => SetAllSourceFoldersChecked(false);
            lnkAddFolder.LinkClicked += (s, e) => PromptAddCustomSourceFolder();

            pnlLeftHeader.Controls.AddRange(new Control[] { lblF1, lnkSelectAll, lblSep1, lnkClearAll, lblSep2, lnkAddFolder });
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
            var lnkAddDestTarget = new LinkLabel { Text = "+ Add new target folder", Font = new Font("Segoe UI", 8.5f), Anchor = AnchorStyles.Top | AnchorStyles.Right, AutoSize = true };
            lnkAddDestTarget.Location = new Point(pnlRightHeader.Width - 155, 6);
            lnkAddDestTarget.LinkClicked += (s, e) => PromptAddCustomDestFolder();

            pnlRightHeader.Controls.AddRange(new Control[] { lblF2, lnkAddDestTarget });
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

            _colDestFolder = new DataGridViewComboBoxColumn
            {
                HeaderText = "Destination Folder (Dropdown)",
                FillWeight = 52,
                FlatStyle = FlatStyle.Flat
            };
            RefreshDestDropdownChoices();

            _dgvFolderMappings.Columns.Add(colSource);
            _dgvFolderMappings.Columns.Add(colArrow);
            _dgvFolderMappings.Columns.Add(_colDestFolder);

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
        }

        private void RefreshDestDropdownChoices()
        {
            _colDestFolder.Items.Clear();
            foreach (var f in _destFolderChoices.OrderBy(x => x))
            {
                _colDestFolder.Items.Add(f);
            }
        }

        private void LoadAccountToUi()
        {
            _txtSourceHost.Text = Account.SourceHost;
            _numSourcePort.Value = Account.SourcePort > 0 ? Account.SourcePort : 993;
            _cmbSourceSecurity.SelectedItem = Account.SourceSecurity;
            _txtSourceUser.Text = Account.SourceUser;
            _txtSourcePass.Text = Account.SourcePassword;

            _txtDestHost.Text = Account.DestHost;
            _numDestPort.Value = Account.DestPort > 0 ? Account.DestPort : 993;
            _cmbDestSecurity.SelectedItem = Account.DestSecurity;
            _txtDestUser.Text = Account.DestUser;
            _txtDestPass.Text = Account.DestPassword;

            // Load folders
            _lvSourceFolders.Items.Clear();
            foreach (var map in Account.FolderMappings)
            {
                _destFolderChoices.Add(map.SourceFolder);
                if (!string.IsNullOrEmpty(map.DestFolder))
                {
                    _destFolderChoices.Add(map.DestFolder);
                }

                var item = new ListViewItem(map.SourceFolder);
                item.SubItems.Add(map.TotalMessages > 0 ? map.TotalMessages.ToString("N0") : "-");
                item.Checked = map.Selected;
                item.Tag = map;
                _lvSourceFolders.Items.Add(item);
            }

            RefreshDestDropdownChoices();
            UpdateFolderMappingTable();
        }

        private void UpdateFolderMappingTable()
        {
            var existingMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataGridViewRow r in _dgvFolderMappings.Rows)
            {
                string src = r.Cells[0].Value != null ? r.Cells[0].Value.ToString() : null;
                string dst = r.Cells[2].Value != null ? r.Cells[2].Value.ToString() : null;
                if (!string.IsNullOrEmpty(src) && !string.IsNullOrEmpty(dst))
                {
                    existingMappings[src] = dst;
                }
            }

            _dgvFolderMappings.Rows.Clear();

            foreach (ListViewItem item in _lvSourceFolders.Items)
            {
                if (item.Checked)
                {
                    string src = item.Text;
                    string destTarget = src;

                    if (existingMappings.ContainsKey(src))
                    {
                        destTarget = existingMappings[src];
                    }
                    else if (item.Tag is FolderMapping && !string.IsNullOrEmpty(((FolderMapping)item.Tag).DestFolder))
                    {
                        destTarget = ((FolderMapping)item.Tag).DestFolder;
                    }
                    else if (string.Equals(src, "inbox", StringComparison.OrdinalIgnoreCase))
                    {
                        destTarget = "INBOX";
                    }

                    if (!_destFolderChoices.Contains(destTarget))
                    {
                        _destFolderChoices.Add(destTarget);
                        RefreshDestDropdownChoices();
                    }

                    _dgvFolderMappings.Rows.Add(src, "➔", destTarget);
                }
            }
        }

        private void PromptAddCustomSourceFolder()
        {
            string folder = PromptInput("Enter source folder name to add:", "Add Source Folder", "Archive");
            if (!string.IsNullOrWhiteSpace(folder))
            {
                string trimF = folder.Trim();
                var item = new ListViewItem(trimF);
                item.SubItems.Add("-");
                item.Checked = true;
                item.Tag = new FolderMapping { SourceFolder = trimF, DestFolder = trimF, Selected = true };
                _lvSourceFolders.Items.Add(item);

                _destFolderChoices.Add(trimF);
                RefreshDestDropdownChoices();
                UpdateFolderMappingTable();
            }
        }

        private void PromptAddCustomDestFolder()
        {
            string folder = PromptInput("Enter new destination target folder name (e.g. Archive, Old_Mails):", "Add Target Folder", "Archive");
            if (!string.IsNullOrWhiteSpace(folder))
            {
                string trimF = folder.Trim();
                _destFolderChoices.Add(trimF);
                RefreshDestDropdownChoices();

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

        private void SourceServicePresetChanged()
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

        private void DestServicePresetChanged()
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

                        _lvSourceFolders.Items.Clear();
                        foreach (var f in _cachedSourceFolders.Where(x => x.IsSelectable))
                        {
                            var item = new ListViewItem(f.Name);
                            item.SubItems.Add(f.MessageCount.ToString("N0"));
                            item.Checked = true;
                            item.Tag = f;
                            _lvSourceFolders.Items.Add(item);

                            _destFolderChoices.Add(f.Name);
                        }
                        RefreshDestDropdownChoices();
                        UpdateFolderMappingTable();
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
                            _destFolderChoices.Add(f.Name);
                        }
                        RefreshDestDropdownChoices();
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

        private void SaveAndClose()
        {
            Account.SourceProvider = _cmbSourceService.SelectedItem != null ? _cmbSourceService.SelectedItem.ToString() : "Other IMAP server";
            Account.SourceHost = _txtSourceHost.Text.Trim();
            Account.SourcePort = (int)_numSourcePort.Value;
            Account.SourceSecurity = (SecurityType)_cmbSourceSecurity.SelectedItem;
            Account.SourceUser = _txtSourceUser.Text.Trim();
            Account.SourcePassword = _txtSourcePass.Text;

            Account.DestProvider = _cmbDestService.SelectedItem != null ? _cmbDestService.SelectedItem.ToString() : "Other IMAP server";
            Account.DestHost = _txtDestHost.Text.Trim();
            Account.DestPort = (int)_numDestPort.Value;
            Account.DestSecurity = (SecurityType)_cmbDestSecurity.SelectedItem;
            Account.DestUser = _txtDestUser.Text.Trim();
            Account.DestPassword = _txtDestPass.Text;

            Account.FolderMappings.Clear();

            foreach (DataGridViewRow row in _dgvFolderMappings.Rows)
            {
                string src = row.Cells[0].Value != null ? row.Cells[0].Value.ToString() : null;
                string dst = row.Cells[2].Value != null ? row.Cells[2].Value.ToString() : null;

                if (!string.IsNullOrEmpty(src))
                {
                    Account.FolderMappings.Add(new FolderMapping
                    {
                        SourceFolder = src,
                        DestFolder = string.IsNullOrEmpty(dst) ? src : dst,
                        Selected = true
                    });
                }
            }

            if (Account.FolderMappings.Count == 0)
            {
                Account.FolderMappings.Add(new FolderMapping { SourceFolder = "INBOX", DestFolder = "INBOX", Selected = true });
            }

            DialogResult = DialogResult.OK;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_activeSourceClient != null) _activeSourceClient.Dispose();
            if (_activeDestClient != null) _activeDestClient.Dispose();
            base.OnFormClosing(e);
        }
    }
}
