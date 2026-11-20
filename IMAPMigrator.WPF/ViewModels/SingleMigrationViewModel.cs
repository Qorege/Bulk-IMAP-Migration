using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImapSyncWin;

namespace IMAPMigrator.WPF.ViewModels
{
    public partial class SingleMigrationViewModel : ObservableObject
    {
        private readonly AppSettings _settings;
        private readonly MigrationEngine _engine;
        private readonly MonitorViewModel _monitor;

        // Source
        [ObservableProperty] private string sourceHost = "";
        [ObservableProperty] private int sourcePort = 993;
        [ObservableProperty] private SecurityType sourceSecurity = SecurityType.SslTls;
        [ObservableProperty] private string sourceUser = "";
        [ObservableProperty] private string sourcePassword = "";

        // Destination
        [ObservableProperty] private string destHost = "";
        [ObservableProperty] private int destPort = 993;
        [ObservableProperty] private SecurityType destSecurity = SecurityType.SslTls;
        [ObservableProperty] private string destUser = "";
        [ObservableProperty] private string destPassword = "";

        // Provider presets
        public ObservableCollection<EmailProviderPreset> Presets { get; } = new();

        [ObservableProperty] private EmailProviderPreset selectedSourcePreset;
        [ObservableProperty] private EmailProviderPreset selectedDestPreset;

        // Folders
        public ObservableCollection<FolderMappingItem> FolderMappings { get; } = new();

        // Test results
        [ObservableProperty] private string sourceTestResult = "";
        [ObservableProperty] private string destTestResult = "";
        [ObservableProperty] private bool isTesting;
        [ObservableProperty] private bool isScanning;
        [ObservableProperty] private bool isMigrating;
        [ObservableProperty] private string statusMessage = "";

        public SingleMigrationViewModel(AppSettings settings, MigrationEngine engine, MonitorViewModel monitor)
        {
            _settings = settings;
            _engine = engine;
            _monitor = monitor;

            foreach (var p in EmailProviderPreset.GetPresets())
                Presets.Add(p);

            // Apply defaults from settings
            SourceHost = settings.DefaultSourceHost;
            SourcePort = settings.DefaultSourcePort;
            SourceSecurity = settings.DefaultSourceSecurity;
            DestHost = settings.DefaultDestHost;
            DestPort = settings.DefaultDestPort;
            DestSecurity = settings.DefaultDestSecurity;
        }

        partial void OnSelectedSourcePresetChanged(EmailProviderPreset value)
        {
            if (value != null)
            {
                SourceHost = value.DefaultHost;
                SourcePort = value.DefaultPort;
                SourceSecurity = value.Security;
            }
        }

        partial void OnSelectedDestPresetChanged(EmailProviderPreset value)
        {
            if (value != null)
            {
                DestHost = value.DefaultHost;
                DestPort = value.DefaultPort;
                DestSecurity = value.Security;
            }
        }

        [RelayCommand]
        private async Task TestSourceAsync()
        {
            if (string.IsNullOrWhiteSpace(SourceHost) || string.IsNullOrWhiteSpace(SourceUser))
            {
                SourceTestResult = "⚠ Enter host and credentials first";
                return;
            }

            IsTesting = true;
            SourceTestResult = "Testing connection...";
            try
            {
                var result = await Task.Run(() =>
                {
                    var client = new ImapClient(SourceHost, SourcePort, SourceSecurity);
                    return client.TestConnection(SourceUser, SourcePassword);
                });

                if (result.SignInSuccess)
                    SourceTestResult = $"✓ Connected — {result.FolderCount} folders found";
                else if (result.ServerResponds)
                    SourceTestResult = $"✗ Login failed: {result.ErrorMessage}";
                else
                    SourceTestResult = $"✗ Cannot reach server: {result.ErrorMessage}";
            }
            catch (Exception ex)
            {
                SourceTestResult = $"✗ Error: {ex.Message}";
            }
            finally
            {
                IsTesting = false;
            }
        }

        [RelayCommand]
        private async Task TestDestAsync()
        {
            if (string.IsNullOrWhiteSpace(DestHost) || string.IsNullOrWhiteSpace(DestUser))
            {
                DestTestResult = "⚠ Enter host and credentials first";
                return;
            }

            IsTesting = true;
            DestTestResult = "Testing connection...";
            try
            {
                var result = await Task.Run(() =>
                {
                    var client = new ImapClient(DestHost, DestPort, DestSecurity);
                    return client.TestConnection(DestUser, DestPassword);
                });

                if (result.SignInSuccess)
                    DestTestResult = $"✓ Connected — {result.FolderCount} folders found";
                else if (result.ServerResponds)
                    DestTestResult = $"✗ Login failed: {result.ErrorMessage}";
                else
                    DestTestResult = $"✗ Cannot reach server: {result.ErrorMessage}";
            }
            catch (Exception ex)
            {
                DestTestResult = $"✗ Error: {ex.Message}";
            }
            finally
            {
                IsTesting = false;
            }
        }

        [RelayCommand]
        private async Task ScanFoldersAsync()
        {
            if (string.IsNullOrWhiteSpace(SourceHost) || string.IsNullOrWhiteSpace(SourceUser))
            {
                StatusMessage = "Enter source credentials first";
                return;
            }

            IsScanning = true;
            StatusMessage = "Scanning source folders...";
            FolderMappings.Clear();

            try
            {
                var folders = await Task.Run(() =>
                {
                    var client = new ImapClient(SourceHost, SourcePort, SourceSecurity);
                    client.Connect();
                    client.Login(SourceUser, SourcePassword);
                    var list = client.ListFolders();
                    client.Disconnect();
                    return list;
                });

                var defaultFolders = _settings.DefaultFoldersToMigrate
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(f => f.Trim())
                    .ToList();

                foreach (var folder in folders.Where(f => f.IsSelectable))
                {
                    FolderMappings.Add(new FolderMappingItem
                    {
                        SourceFolder = folder.Name,
                        DestFolder = folder.Name,
                        Selected = defaultFolders.Any(d =>
                            d.Equals(folder.Name, StringComparison.OrdinalIgnoreCase)),
                        MessageCount = folder.MessageCount
                    });
                }

                StatusMessage = $"Found {folders.Count} folders";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Scan failed: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        [RelayCommand]
        private void StartMigration()
        {
            if (_engine.IsRunning)
            {
                StatusMessage = "Migration already running";
                return;
            }

            var account = new AccountPair
            {
                SourceHost = SourceHost,
                SourcePort = SourcePort,
                SourceSecurity = SourceSecurity,
                SourceUser = SourceUser,
                SourcePassword = SourcePassword,
                DestHost = DestHost,
                DestPort = DestPort,
                DestSecurity = DestSecurity,
                DestUser = DestUser,
                DestPassword = DestPassword
            };

            // Add folder mappings
            foreach (var fm in FolderMappings.Where(f => f.Selected))
            {
                account.FolderMappings.Add(new FolderMapping
                {
                    SourceFolder = fm.SourceFolder,
                    DestFolder = fm.DestFolder,
                    Selected = true
                });
            }

            // If no folders scanned, use defaults
            if (account.FolderMappings.Count == 0)
            {
                var defaults = _settings.DefaultFoldersToMigrate
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var f in defaults)
                {
                    var trimmed = f.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        account.FolderMappings.Add(new FolderMapping
                        {
                            SourceFolder = trimmed,
                            DestFolder = trimmed,
                            Selected = true
                        });
                    }
                }
            }

            _monitor.AttachEngine(_engine);
            IsMigrating = true;
            StatusMessage = "Migration started...";

            _engine.QueueCompleted += (s, e) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsMigrating = false;
                    StatusMessage = "Migration complete";
                });
            };

            _engine.RunQueueAsync(new List<AccountPair> { account });
        }
    }

    public class FolderMappingItem : ObservableObject
    {
        private bool _selected = true;
        private string _sourceFolder = "";
        private string _destFolder = "";

        public bool Selected
        {
            get => _selected;
            set => SetProperty(ref _selected, value);
        }
        public string SourceFolder
        {
            get => _sourceFolder;
            set => SetProperty(ref _sourceFolder, value);
        }
        public string DestFolder
        {
            get => _destFolder;
            set => SetProperty(ref _destFolder, value);
        }
        public int MessageCount { get; set; }
    }
}
