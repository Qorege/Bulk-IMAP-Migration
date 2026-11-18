using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImapSyncWin;

namespace IMAPMigrator.WPF.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly AppSettings _settings;
        public Action? OnSettingsSaved { get; set; }

        [ObservableProperty]
        private string _srcHost = "imap.example.com";

        [ObservableProperty]
        private int _srcPort = 993;

        [ObservableProperty]
        private SecurityType _srcSecurity = SecurityType.SslTls;

        [ObservableProperty]
        private string _destHost = "imap.example.com";

        [ObservableProperty]
        private int _destPort = 993;

        [ObservableProperty]
        private SecurityType _destSecurity = SecurityType.SslTls;

        [ObservableProperty]
        private TimePeriodOption _timePeriod = TimePeriodOption.AllMessages;

        [ObservableProperty]
        private DateTime _startDate = DateTime.Today.AddMonths(-6);

        [ObservableProperty]
        private DateTime _endDate = DateTime.Today;

        [ObservableProperty]
        private string _defaultFolders = "INBOX, Sent, Archive";

        [ObservableProperty]
        private QueueConcurrencyOption _queueConcurrency = QueueConcurrencyOption.OneAtATime;

        [ObservableProperty]
        private int _autoRetries = 10;

        [ObservableProperty]
        private int _retryDelaySeconds = 3;

        [ObservableProperty]
        private int _timeoutSeconds = 45;

        [ObservableProperty]
        private bool _directStreaming = true;

        [ObservableProperty]
        private bool _deduplicate = true;

        [ObservableProperty]
        private bool _skipLargerThan;

        [ObservableProperty]
        private int _maxEmailSizeBytes = 25 * 1024 * 1024;

        public int MaxEmailSizeMb
        {
            get => Math.Max(1, MaxEmailSizeBytes / (1024 * 1024));
            set
            {
                MaxEmailSizeBytes = value * 1024 * 1024;
                OnPropertyChanged();
            }
        }

        public bool IsCustomDateRange => TimePeriod == TimePeriodOption.CustomDates;

        partial void OnTimePeriodChanged(TimePeriodOption value)
        {
            OnPropertyChanged(nameof(IsCustomDateRange));
        }

        public SettingsViewModel(AppSettings settings)
        {
            _settings = settings ?? SettingsStorage.Load();
            LoadFromSettings();
        }

        public void LoadFromSettings()
        {
            SrcHost = _settings.DefaultSourceHost;
            SrcPort = _settings.DefaultSourcePort;
            SrcSecurity = _settings.DefaultSourceSecurity;

            DestHost = _settings.DefaultDestHost;
            DestPort = _settings.DefaultDestPort;
            DestSecurity = _settings.DefaultDestSecurity;

            TimePeriod = _settings.TimePeriod;
            StartDate = _settings.CustomStartDate ?? DateTime.Today.AddMonths(-6);
            EndDate = _settings.CustomEndDate ?? DateTime.Today;

            DefaultFolders = _settings.DefaultFoldersToMigrate ?? "INBOX, Sent, Archive";
            QueueConcurrency = _settings.QueueConcurrency;

            AutoRetries = _settings.MaxRetries;
            RetryDelaySeconds = _settings.RetryDelaySeconds;
            TimeoutSeconds = _settings.TimeoutSeconds;

            DirectStreaming = _settings.DirectStreamingOnly;
            Deduplicate = _settings.DeduplicateMessageId;
            SkipLargerThan = _settings.SkipLargerThanEnabled;
            MaxEmailSizeMb = _settings.MaxEmailSizeMB > 0 ? _settings.MaxEmailSizeMB : 25;
        }

        [RelayCommand]
        public void Save()
        {
            _settings.DefaultSourceHost = SrcHost;
            _settings.DefaultSourcePort = SrcPort;
            _settings.DefaultSourceSecurity = SrcSecurity;

            _settings.DefaultDestHost = DestHost;
            _settings.DefaultDestPort = DestPort;
            _settings.DefaultDestSecurity = DestSecurity;

            _settings.TimePeriod = TimePeriod;
            _settings.CustomStartDate = StartDate;
            _settings.CustomEndDate = EndDate;

            _settings.DefaultFoldersToMigrate = DefaultFolders;

            _settings.QueueConcurrency = QueueConcurrency;
            _settings.MaxRetries = AutoRetries;
            _settings.RetryDelaySeconds = RetryDelaySeconds;
            _settings.TimeoutSeconds = TimeoutSeconds;

            _settings.DirectStreamingOnly = DirectStreaming;
            _settings.DeduplicateMessageId = Deduplicate;
            _settings.SkipLargerThanEnabled = SkipLargerThan;
            _settings.MaxEmailSizeMB = MaxEmailSizeMb;

            SettingsStorage.Save(_settings);
            OnSettingsSaved?.Invoke();
            MessageBox.Show("Settings saved successfully.", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        public void ClearDeduplicationCache()
        {
            if (MessageBox.Show("Clear migration deduplication cache? Already transferred emails might be re-scanned on subsequent runs.",
                    "Clear Journal", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                MigrationJournal.Instance.Clear();
                MessageBox.Show("Migration journal cache cleared successfully.", "Journal Cleared", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
