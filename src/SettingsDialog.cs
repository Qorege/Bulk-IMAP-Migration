using System;
using System.Drawing;
using System.Windows.Forms;
using ImapSyncWin.Controls;

namespace ImapSyncWin
{
    public class SettingsDialog : Form
    {
        public AppSettings CurrentSettings { get; private set; }

        private TextBox _txtSrcHost;
        private NumericUpDown _numSrcPort;
        private ComboBox _cmbSrcSec;

        private TextBox _txtDstHost;
        private NumericUpDown _numDstPort;
        private ComboBox _cmbDstSec;

        private ComboBox _cmbPeriod;
        private DateTimePicker _dtStart;
        private DateTimePicker _dtEnd;
        private Label _lblStart;
        private Label _lblEnd;
        private TextBox _txtDefaultFolders;
        private Label _lblFoldersHint;

        private ComboBox _cmbConcurrency;
        private NumericUpDown _numRetries;
        private NumericUpDown _numRetryDelay;
        private NumericUpDown _numTimeout;

        private CheckBox _chkDirectStreaming;
        private CheckBox _chkDeduplicate;
        private CheckBox _chkSkipLargerThan;
        private NumericUpDown _numMaxEmailSize;
        private Label _lblMb;

        public SettingsDialog(AppSettings settings)
        {
            CurrentSettings = settings ?? new AppSettings();
            try
            {
                using (var stream = typeof(SettingsDialog).Assembly.GetManifestResourceStream("app.ico"))
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
            LoadSettingsToUi();
        }

        private void InitializeComponent()
        {
            Text = "Settings — Configuration & Defaults";
            Size = new Size(680, 840);
            MinimumSize = new Size(640, 750);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(248, 250, 252);
            Font = new Font("Segoe UI", 9f);

            var pnlMain = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(24, 16, 24, 20)
            };
            Controls.Add(pnlMain);

            int y = 14;

            // Group 1: Default Source Server
            var grpSrc = CreateSection("Default Source IMAP Server", 145, ref y, pnlMain);
            AddLabel("Host / Server:", 18, 59, grpSrc);
            _txtSrcHost = new TextBox { Location = new Point(115, 56), Size = new Size(260, 25) };
            grpSrc.Controls.Add(_txtSrcHost);

            AddLabel("Port:", 385, 59, grpSrc);
            _numSrcPort = new NumericUpDown { Location = new Point(430, 56), Size = new Size(70, 25), Minimum = 1, Maximum = 65535, Value = 993 };
            grpSrc.Controls.Add(_numSrcPort);

            AddLabel("Security:", 18, 99, grpSrc);
            _cmbSrcSec = new ComboBox { Location = new Point(115, 96), Size = new Size(160, 25), DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbSrcSec.Items.AddRange(new object[] { SecurityType.SslTls, SecurityType.StartTls, SecurityType.Plain });
            grpSrc.Controls.Add(_cmbSrcSec);

            // Group 2: Default Destination Server
            var grpDst = CreateSection("Default Destination IMAP Server", 145, ref y, pnlMain);
            AddLabel("Host / Server:", 18, 59, grpDst);
            _txtDstHost = new TextBox { Location = new Point(115, 56), Size = new Size(260, 25) };
            grpDst.Controls.Add(_txtDstHost);

            AddLabel("Port:", 385, 59, grpDst);
            _numDstPort = new NumericUpDown { Location = new Point(430, 56), Size = new Size(70, 25), Minimum = 1, Maximum = 65535, Value = 993 };
            grpDst.Controls.Add(_numDstPort);

            AddLabel("Security:", 18, 99, grpDst);
            _cmbDstSec = new ComboBox { Location = new Point(115, 96), Size = new Size(160, 25), DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbDstSec.Items.AddRange(new object[] { SecurityType.SslTls, SecurityType.StartTls, SecurityType.Plain });
            grpDst.Controls.Add(_cmbDstSec);

            // Group 3: Time Period & Folders
            var grpPeriod = CreateSection("Migration Time Period & Default Folders", 160, ref y, pnlMain);

            AddLabel("Period:", 18, 59, grpPeriod);
            _cmbPeriod = new ComboBox { Location = new Point(115, 56), Size = new Size(150, 25), DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbPeriod.Items.AddRange(new object[] { "All messages", "Last 6 months", "Last year", "Choose dates" });
            _cmbPeriod.SelectedIndexChanged += (s, e) => UpdateDateRangeVisibility();
            grpPeriod.Controls.Add(_cmbPeriod);

            _lblStart = AddLabel("From:", 275, 59, grpPeriod);
            _dtStart = new DateTimePicker { Location = new Point(315, 56), Size = new Size(110, 25), Format = DateTimePickerFormat.Short };
            grpPeriod.Controls.Add(_dtStart);

            _lblEnd = AddLabel("To:", 435, 59, grpPeriod);
            _dtEnd = new DateTimePicker { Location = new Point(460, 56), Size = new Size(110, 25), Format = DateTimePickerFormat.Short };
            grpPeriod.Controls.Add(_dtEnd);

            AddLabel("Default Folders:", 18, 99, grpPeriod);
            _txtDefaultFolders = new TextBox { Location = new Point(115, 96), Size = new Size(455, 25) };
            grpPeriod.Controls.Add(_txtDefaultFolders);

            _lblFoldersHint = new Label
            {
                Text = "(Comma-separated default folders to migrate, e.g. INBOX, Sent, Archive)",
                Location = new Point(115, 126),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 8.25f, FontStyle.Italic)
            };
            grpPeriod.Controls.Add(_lblFoldersHint);

            // Group 4: Queue & Performance
            var grpQueue = CreateSection("Queue Concurrency & Network Resilience", 145, ref y, pnlMain);
            AddLabel("Queue Mode:", 18, 59, grpQueue);
            _cmbConcurrency = new ComboBox { Location = new Point(115, 56), Size = new Size(190, 25), DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbConcurrency.Items.AddRange(new object[] { "1 at a time (Sequential)", "3 at a time", "Sync all accounts" });
            grpQueue.Controls.Add(_cmbConcurrency);

            AddLabel("Auto-Retries:", 18, 99, grpQueue);
            _numRetries = new NumericUpDown { Location = new Point(115, 96), Size = new Size(60, 25), Minimum = 1, Maximum = 50, Value = 10 };
            grpQueue.Controls.Add(_numRetries);

            AddLabel("Backoff (s):", 195, 99, grpQueue);
            _numRetryDelay = new NumericUpDown { Location = new Point(275, 96), Size = new Size(55, 25), Minimum = 1, Maximum = 30, Value = 3 };
            grpQueue.Controls.Add(_numRetryDelay);

            AddLabel("Timeout (s):", 350, 99, grpQueue);
            _numTimeout = new NumericUpDown { Location = new Point(435, 96), Size = new Size(65, 25), Minimum = 5, Maximum = 300, Value = 60 };
            grpQueue.Controls.Add(_numTimeout);

            // Group 5: Protocol, Security & Size Filter
            var grpProto = CreateSection("Direct Transfer, Deduplication & Filters", 195, ref y, pnlMain);

            _chkDirectStreaming = new CheckBox
            {
                Text = "Direct IMAP-to-IMAP transfer (Zero disk buffering, streams directly in memory)",
                Location = new Point(18, 56),
                AutoSize = true,
                Checked = true
            };
            grpProto.Controls.Add(_chkDirectStreaming);

            _chkDeduplicate = new CheckBox
            {
                Text = "True Deduplication: Skip messages already in destination (UID & Message-ID)",
                Location = new Point(18, 86),
                AutoSize = true,
                UseMnemonic = false,
                Checked = true
            };
            grpProto.Controls.Add(_chkDeduplicate);

            // Skip emails larger than [ N ] MB
            _chkSkipLargerThan = new CheckBox
            {
                Text = "Skip emails larger than:",
                Location = new Point(18, 116),
                AutoSize = true,
                Checked = false
            };
            _chkSkipLargerThan.CheckedChanged += (s, e) =>
            {
                _numMaxEmailSize.Enabled = _chkSkipLargerThan.Checked;
            };
            grpProto.Controls.Add(_chkSkipLargerThan);

            _numMaxEmailSize = new NumericUpDown
            {
                Location = new Point(190, 114),
                Size = new Size(70, 25),
                Minimum = 1,
                Maximum = 1024,
                Value = 25,
                Enabled = false
            };
            grpProto.Controls.Add(_numMaxEmailSize);

            _lblMb = new Label
            {
                Text = "MB",
                Location = new Point(268, 117),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105)
            };
            grpProto.Controls.Add(_lblMb);

            var lblReadonlyNote = new Label
            {
                Text = "✓ Security Guarantee: Source mailbox is always opened in EXAMINE (Read-Only) mode.",
                ForeColor = Color.FromArgb(15, 118, 110),
                Location = new Point(18, 154),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic)
            };
            grpProto.Controls.Add(lblReadonlyNote);

            // Bottom Buttons Panel
            int bottomW = Math.Max(580, pnlMain.ClientSize.Width - 40);
            var pnlBtns = new Panel
            {
                Location = new Point(20, y + 8),
                Size = new Size(bottomW, 48),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            pnlMain.Controls.Add(pnlBtns);

            var btnClearJournal = new ModernButton
            {
                Text = "Clear Deduplication Cache",
                StyleMode = ButtonStyleMode.Secondary,
                Location = new Point(0, 6),
                Size = new Size(215, 36)
            };
            btnClearJournal.Click += (s, e) =>
            {
                if (MessageBox.Show("Are you sure you want to clear the local migration journal cache?", "Clear Deduplication Cache", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    MigrationJournal.Instance.Clear();
                    MessageBox.Show("Migration deduplication cache successfully cleared.", "Cache Cleared", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            pnlBtns.Controls.Add(btnClearJournal);

            var btnCancel = new ModernButton
            {
                Text = "Cancel",
                StyleMode = ButtonStyleMode.Secondary,
                Location = new Point(pnlBtns.Width - 235, 6),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Size = new Size(100, 36)
            };
            btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;
            pnlBtns.Controls.Add(btnCancel);

            var btnSave = new ModernButton
            {
                Text = "Save Settings",
                StyleMode = ButtonStyleMode.Primary,
                Location = new Point(pnlBtns.Width - 125, 6),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Size = new Size(125, 36)
            };
            btnSave.Click += (s, e) =>
            {
                SaveUiToSettings();
                SettingsStorage.Save(CurrentSettings);
                DialogResult = DialogResult.OK;
            };
            pnlBtns.Controls.Add(btnSave);
        }

        private CardPanel CreateSection(string title, int height, ref int y, Panel parent)
        {
            int cardW = Math.Max(580, parent.ClientSize.Width - 40);
            var card = new CardPanel
            {
                Location = new Point(20, y),
                Size = new Size(cardW, height),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.White,
                CornerRadius = 10,
                BorderColor = Color.FromArgb(226, 232, 240)
            };

            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                Location = new Point(18, 14),
                AutoSize = true,
                UseMnemonic = false
            };
            card.Controls.Add(lblTitle);

            // Clean subtle divider line
            var pnlDivider = new Panel
            {
                Location = new Point(18, 40),
                Size = new Size(cardW - 36, 1),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.FromArgb(241, 245, 249)
            };
            card.Controls.Add(pnlDivider);

            parent.Controls.Add(card);
            y += height + 16;
            return card;
        }

        private Label AddLabel(string text, int x, int y, Control parent)
        {
            var lbl = new Label
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true,
                ForeColor = Color.FromArgb(71, 85, 105),
                Font = new Font("Segoe UI", 9f)
            };
            parent.Controls.Add(lbl);
            return lbl;
        }

        private void UpdateDateRangeVisibility()
        {
            bool custom = _cmbPeriod.SelectedIndex == 3;
            _lblStart.Visible = custom;
            _dtStart.Visible = custom;
            _lblEnd.Visible = custom;
            _dtEnd.Visible = custom;
        }

        private void LoadSettingsToUi()
        {
            _txtSrcHost.Text = CurrentSettings.DefaultSourceHost;
            _numSrcPort.Value = CurrentSettings.DefaultSourcePort;
            _cmbSrcSec.SelectedItem = CurrentSettings.DefaultSourceSecurity;

            _txtDstHost.Text = CurrentSettings.DefaultDestHost;
            _numDstPort.Value = CurrentSettings.DefaultDestPort;
            _cmbDstSec.SelectedItem = CurrentSettings.DefaultDestSecurity;

            _cmbPeriod.SelectedIndex = (int)CurrentSettings.TimePeriod;
            if (CurrentSettings.CustomStartDate.HasValue) _dtStart.Value = CurrentSettings.CustomStartDate.Value;
            if (CurrentSettings.CustomEndDate.HasValue) _dtEnd.Value = CurrentSettings.CustomEndDate.Value;
            UpdateDateRangeVisibility();

            _txtDefaultFolders.Text = CurrentSettings.DefaultFoldersToMigrate;

            switch (CurrentSettings.QueueConcurrency)
            {
                case QueueConcurrencyOption.OneAtATime: _cmbConcurrency.SelectedIndex = 0; break;
                case QueueConcurrencyOption.ThreeAtATime: _cmbConcurrency.SelectedIndex = 1; break;
                case QueueConcurrencyOption.SyncAll: _cmbConcurrency.SelectedIndex = 2; break;
            }

            _numRetries.Value = CurrentSettings.MaxRetries;
            _numRetryDelay.Value = CurrentSettings.RetryDelaySeconds;
            _numTimeout.Value = Math.Max(5, CurrentSettings.TimeoutSeconds);

            _chkDirectStreaming.Checked = CurrentSettings.DirectStreamingOnly;
            _chkDeduplicate.Checked = CurrentSettings.DeduplicateMessageId;

            _chkSkipLargerThan.Checked = CurrentSettings.SkipLargerThanEnabled;
            _numMaxEmailSize.Value = Math.Max(1, CurrentSettings.MaxEmailSizeMB);
            _numMaxEmailSize.Enabled = CurrentSettings.SkipLargerThanEnabled;
        }

        private void SaveUiToSettings()
        {
            CurrentSettings.DefaultSourceHost = _txtSrcHost.Text.Trim();
            CurrentSettings.DefaultSourcePort = (int)_numSrcPort.Value;
            CurrentSettings.DefaultSourceSecurity = (SecurityType)_cmbSrcSec.SelectedItem;

            CurrentSettings.DefaultDestHost = _txtDstHost.Text.Trim();
            CurrentSettings.DefaultDestPort = (int)_numDstPort.Value;
            CurrentSettings.DefaultDestSecurity = (SecurityType)_cmbDstSec.SelectedItem;

            CurrentSettings.TimePeriod = (TimePeriodOption)_cmbPeriod.SelectedIndex;
            if (CurrentSettings.TimePeriod == TimePeriodOption.CustomDates)
            {
                CurrentSettings.CustomStartDate = _dtStart.Value.Date;
                CurrentSettings.CustomEndDate = _dtEnd.Value.Date;
            }
            else
            {
                CurrentSettings.CustomStartDate = null;
                CurrentSettings.CustomEndDate = null;
            }

            CurrentSettings.DefaultFoldersToMigrate = _txtDefaultFolders.Text.Trim();

            switch (_cmbConcurrency.SelectedIndex)
            {
                case 0: CurrentSettings.QueueConcurrency = QueueConcurrencyOption.OneAtATime; break;
                case 1: CurrentSettings.QueueConcurrency = QueueConcurrencyOption.ThreeAtATime; break;
                case 2: CurrentSettings.QueueConcurrency = QueueConcurrencyOption.SyncAll; break;
            }

            CurrentSettings.MaxRetries = (int)_numRetries.Value;
            CurrentSettings.RetryDelaySeconds = (int)_numRetryDelay.Value;
            CurrentSettings.TimeoutSeconds = (int)_numTimeout.Value;

            CurrentSettings.DirectStreamingOnly = _chkDirectStreaming.Checked;
            CurrentSettings.DeduplicateMessageId = _chkDeduplicate.Checked;

            CurrentSettings.SkipLargerThanEnabled = _chkSkipLargerThan.Checked;
            CurrentSettings.MaxEmailSizeMB = (int)_numMaxEmailSize.Value;
        }
    }
}
