using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImapSyncWin;

namespace IMAPMigrator.WPF.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly AppSettings _settings;
        public event Action SettingsSaved;

        // Server defaults
        [ObservableProperty] private string defaultSourceHost;
        [ObservableProperty] private int defaultSourcePort;
        [ObservableProperty] private SecurityType defaultSourceSecurity;
        [ObservableProperty] private string defaultDestHost;
        [ObservableProperty] private int defaultDestPort;
        [ObservableProperty] private SecurityType defaultDestSecurity;

        // Time period
        [ObservableProperty] private TimePeriodOption timePeriod;
        [ObservableProperty] private DateTime? customStartDate;
        [ObservableProperty] private DateTime? customEndDate;

        // Queue
        [ObservableProperty] private QueueConcurrencyOption queueConcurrency;
        [ObservableProperty] private int maxRetries;
        [ObservableProperty] private int retryDelaySeconds;
        [ObservableProperty] private int timeoutSeconds;

        // Options
        [ObservableProperty] private bool directStreamingOnly;
        [ObservableProperty] private bool deduplicateMessageId;
        [ObservableProperty] private string defaultFoldersToMigrate;
        [ObservableProperty] private bool skipLargerThanEnabled;
        [ObservableProperty] private int maxEmailSizeMB;

        public SettingsViewModel(AppSettings settings)
        {
            _settings = settings;
            LoadFromSettings();
        }

        private void LoadFromSettings()
        {
            DefaultSourceHost = _settings.DefaultSourceHost;
            DefaultSourcePort = _settings.DefaultSourcePort;
            DefaultSourceSecurity = _settings.DefaultSourceSecurity;
            DefaultDestHost = _settings.DefaultDestHost;
            DefaultDestPort = _settings.DefaultDestPort;
            DefaultDestSecurity = _settings.DefaultDestSecurity;

            TimePeriod = _settings.TimePeriod;
            CustomStartDate = _settings.CustomStartDate;
            CustomEndDate = _settings.CustomEndDate;

            QueueConcurrency = _settings.QueueConcurrency;
            MaxRetries = _settings.MaxRetries;
            RetryDelaySeconds = _settings.RetryDelaySeconds;
            TimeoutSeconds = _settings.TimeoutSeconds;

            DirectStreamingOnly = _settings.DirectStreamingOnly;
            DeduplicateMessageId = _settings.DeduplicateMessageId;
            DefaultFoldersToMigrate = _settings.DefaultFoldersToMigrate;
            SkipLargerThanEnabled = _settings.SkipLargerThanEnabled;
            MaxEmailSizeMB = _settings.MaxEmailSizeMB;
        }

        [RelayCommand]
        private void Save()
        {
            _settings.DefaultSourceHost = DefaultSourceHost;
            _settings.DefaultSourcePort = DefaultSourcePort;
            _settings.DefaultSourceSecurity = DefaultSourceSecurity;
            _settings.DefaultDestHost = DefaultDestHost;
            _settings.DefaultDestPort = DefaultDestPort;
            _settings.DefaultDestSecurity = DefaultDestSecurity;

            _settings.TimePeriod = TimePeriod;
            _settings.CustomStartDate = CustomStartDate;
            _settings.CustomEndDate = CustomEndDate;

            _settings.QueueConcurrency = QueueConcurrency;
            _settings.MaxRetries = MaxRetries;
            _settings.RetryDelaySeconds = RetryDelaySeconds;
            _settings.TimeoutSeconds = TimeoutSeconds;

            _settings.DirectStreamingOnly = DirectStreamingOnly;
            _settings.DeduplicateMessageId = DeduplicateMessageId;
            _settings.DefaultFoldersToMigrate = DefaultFoldersToMigrate;
            _settings.SkipLargerThanEnabled = SkipLargerThanEnabled;
            _settings.MaxEmailSizeMB = MaxEmailSizeMB;

            SettingsStorage.Save(_settings);
            SettingsSaved?.Invoke();
        }

        [RelayCommand]
        private void Reset()
        {
            var defaults = new AppSettings();
            _settings.DefaultSourceHost = defaults.DefaultSourceHost;
            _settings.DefaultSourcePort = defaults.DefaultSourcePort;
            _settings.DefaultSourceSecurity = defaults.DefaultSourceSecurity;
            _settings.DefaultDestHost = defaults.DefaultDestHost;
            _settings.DefaultDestPort = defaults.DefaultDestPort;
            _settings.DefaultDestSecurity = defaults.DefaultDestSecurity;
            _settings.TimePeriod = defaults.TimePeriod;
            _settings.CustomStartDate = defaults.CustomStartDate;
            _settings.CustomEndDate = defaults.CustomEndDate;
            _settings.QueueConcurrency = defaults.QueueConcurrency;
            _settings.MaxRetries = defaults.MaxRetries;
            _settings.RetryDelaySeconds = defaults.RetryDelaySeconds;
            _settings.TimeoutSeconds = defaults.TimeoutSeconds;
            _settings.DirectStreamingOnly = defaults.DirectStreamingOnly;
            _settings.DeduplicateMessageId = defaults.DeduplicateMessageId;
            _settings.DefaultFoldersToMigrate = defaults.DefaultFoldersToMigrate;
            _settings.SkipLargerThanEnabled = defaults.SkipLargerThanEnabled;
            _settings.MaxEmailSizeMB = defaults.MaxEmailSizeMB;

            LoadFromSettings();
            SettingsStorage.Save(_settings);
            SettingsSaved?.Invoke();
        }
    }
}
