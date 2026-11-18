using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImapSyncWin;

namespace IMAPMigrator.WPF.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly AppSettings _settings;
        private readonly MigrationEngine _engine;
        private MockImapServer? _demoSourceServer;
        private MockImapServer? _demoDestServer;

        [ObservableProperty]
        private int _selectedTabIndex;

        [ObservableProperty]
        private bool _isSettingsOpen;

        [ObservableProperty]
        private string _appTitle = "Move email between mailboxes";

        [ObservableProperty]
        private string _appSubtitle = "Direct IMAP-to-IMAP transfer. Streams in-memory with zero disk bloat & true deduplication.";

        public SingleMigrationViewModel SingleVM { get; }
        public QueueViewModel QueueVM { get; }
        public MonitorViewModel MonitorVM { get; }
        public SettingsViewModel SettingsVM { get; }

        public Action<AccountPair>? OpenEditAccountWindow { get; set; }
        public Action? OpenAboutWindow { get; set; }

        public MainViewModel()
        {
            _settings = SettingsStorage.Load();
            _engine = new MigrationEngine(_settings);

            SettingsVM = new SettingsViewModel(_settings);
            SingleVM = new SingleMigrationViewModel(_settings);
            QueueVM = new QueueViewModel(_settings);
            MonitorVM = new MonitorViewModel(_engine);

            // Wire up callbacks
            SingleVM.RequestStartMigration = StartSingleMigration;
            SingleVM.RequestAddToQueue = pair => QueueVM.AddAccount(pair);

            QueueVM.RequestStartQueue = StartQueueMigration;
            QueueVM.RequestPauseQueue = () => MonitorVM.TogglePause();
            QueueVM.RequestStopQueue = () => MonitorVM.Stop();
            QueueVM.RequestEditAccount = pair => OpenEditAccountWindow?.Invoke(pair);

            SettingsVM.OnSettingsSaved = () =>
            {
                SingleVM.LoadDefaultsFromSettings();
            };

            // Hook Engine Events
            _engine.OverallProgressChanged += (s, e) => MonitorVM.HandleProgress(e);
            _engine.FolderProgressChanged += (s, e) => MonitorVM.HandleFolderProgress(e);
            _engine.LogEmitted += (s, e) => MonitorVM.HandleLog(e);

            _engine.AccountStarted += (s, pair) =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    pair.Status = AccountSyncStatus.Syncing;
                });
            };

            _engine.AccountCompleted += (s, pair) =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    pair.Status = AccountSyncStatus.Completed;
                    pair.StatusMessage = "Finished successfully";
                });
            };

            _engine.AccountFailed += (s, pair) =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    pair.Status = AccountSyncStatus.Failed;
                });
            };

            _engine.QueueCompleted += (s, e) =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    QueueVM.IsQueueRunning = false;
                    MonitorVM.SetCompleted(true, "All accounts finished processing.");
                });
            };
        }

        private void StartSingleMigration(AccountPair pair)
        {
            SelectedTabIndex = 2; // Switch to Monitor tab
            MonitorVM.ResetForNewMigration($"{pair.SourceUser} ➔ {pair.DestUser}");

            try
            {
                var list = new List<AccountPair> { pair };
                _engine.Settings.QueueConcurrency = QueueConcurrencyOption.OneAtATime;
                _engine.RunQueueAsync(list);
            }
            catch (Exception ex)
            {
                MonitorVM.SetCompleted(false, "Migration stopped with error: " + ex.Message);
            }
        }

        private void StartQueueMigration()
        {
            SelectedTabIndex = 2; // Switch to Monitor tab
            MonitorVM.ResetForNewMigration($"Batch Queue: {QueueVM.Queue.Count} account(s)");

            try
            {
                var list = new List<AccountPair>(QueueVM.Queue);
                _engine.Settings.QueueConcurrency = QueueVM.Concurrency;
                _engine.RunQueueAsync(list);
            }
            catch (Exception ex)
            {
                QueueVM.IsQueueRunning = false;
                MonitorVM.SetCompleted(false, "Queue execution stopped: " + ex.Message);
            }
        }

        [RelayCommand]
        public void ToggleSettings()
        {
            IsSettingsOpen = !IsSettingsOpen;
        }

        [RelayCommand]
        public void OpenGitHub()
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://github.com/Qorege/Bulk-IMAP-Migration") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open GitHub link: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public void ShowAbout()
        {
            OpenAboutWindow?.Invoke();
        }

        [RelayCommand]
        public async Task RunDemoTestAsync()
        {
            if (MessageBox.Show("Start built-in local live test? This spins up temporary loopback source & destination servers in memory.",
                    "Run Demo Test", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                _demoSourceServer?.Dispose();
                _demoDestServer?.Dispose();

                _demoSourceServer = new MockImapServer(56980);
                _demoSourceServer.PopulateSampleMessages(25, 5);

                _demoDestServer = new MockImapServer(56981);

                var testPair = new AccountPair
                {
                    SourceHost = "127.0.0.1",
                    SourcePort = 56980,
                    SourceSecurity = SecurityType.Plain,
                    SourceUser = "demo_source@example.com",
                    SourcePassword = "password",

                    DestHost = "127.0.0.1",
                    DestPort = 56981,
                    DestSecurity = SecurityType.Plain,
                    DestUser = "demo_dest@example.com",
                    DestPassword = "password",

                    FolderMappings = new List<FolderMapping>
                    {
                        new FolderMapping { SourceFolder = "INBOX", DestFolder = "INBOX" },
                        new FolderMapping { SourceFolder = "Spam", DestFolder = "Spam" }
                    }
                };

                StartSingleMigration(testPair);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error starting demo test: " + ex.Message, "Demo Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
