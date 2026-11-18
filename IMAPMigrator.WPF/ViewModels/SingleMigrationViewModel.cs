using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImapSyncWin;
using IMAPMigrator.WPF.Models;

namespace IMAPMigrator.WPF.ViewModels
{
    public partial class SingleMigrationViewModel : ObservableObject
    {
        private readonly AppSettings _settings;
        public Action<AccountPair>? RequestStartMigration { get; set; }
        public Action<AccountPair>? RequestAddToQueue { get; set; }

        [ObservableProperty]
        private string _sourceHost = "imap.example.com";

        [ObservableProperty]
        private int _sourcePort = 993;

        [ObservableProperty]
        private SecurityType _sourceSecurity = SecurityType.SslTls;

        [ObservableProperty]
        private string _sourceUser = "";

        [ObservableProperty]
        private string _sourcePass = "";

        [ObservableProperty]
        private bool _isSourcePassVisible;

        [ObservableProperty]
        private string _sourceStatusText = "○ Not connected";

        [ObservableProperty]
        private string _destHost = "imap.example.com";

        [ObservableProperty]
        private int _destPort = 993;

        [ObservableProperty]
        private SecurityType _destSecurity = SecurityType.SslTls;

        [ObservableProperty]
        private string _destUser = "";

        [ObservableProperty]
        private string _destPass = "";

        [ObservableProperty]
        private bool _isDestPassVisible;

        [ObservableProperty]
        private string _destStatusText = "○ Not connected";

        [ObservableProperty]
        private TimePeriodOption _timePeriod = TimePeriodOption.AllMessages;

        [ObservableProperty]
        private string _selectionSummary = "Selected 0 folders · 0 messages";

        [ObservableProperty]
        private bool _isBusy;

        public ObservableCollection<string> AvailableDestFolders { get; } = new ObservableCollection<string>
        {
            "INBOX", "Archive", "Sent", "Drafts", "Trash", "Junk", "Spam"
        };

        public ObservableCollection<FolderMappingItem> FolderMappings { get; } = new ObservableCollection<FolderMappingItem>();

        public ObservableCollection<EmailProviderPreset> Presets { get; } = new ObservableCollection<EmailProviderPreset>();

        [ObservableProperty]
        private EmailProviderPreset? _selectedSourcePreset;

        [ObservableProperty]
        private EmailProviderPreset? _selectedDestPreset;

        public SingleMigrationViewModel(AppSettings settings)
        {
            _settings = settings;

            foreach (var p in EmailProviderPreset.GetPresets())
            {
                Presets.Add(p);
            }

            SelectedSourcePreset = Presets.FirstOrDefault(p => p.Name == "Other IMAP server") ?? Presets.FirstOrDefault();
            SelectedDestPreset = Presets.FirstOrDefault(p => p.Name == "Other IMAP server") ?? Presets.FirstOrDefault();

            LoadDefaultsFromSettings();
        }

        public void LoadDefaultsFromSettings()
        {
            if (!string.IsNullOrEmpty(_settings.DefaultSourceHost)) SourceHost = _settings.DefaultSourceHost;
            SourcePort = _settings.DefaultSourcePort;
            SourceSecurity = _settings.DefaultSourceSecurity;

            if (!string.IsNullOrEmpty(_settings.DefaultDestHost)) DestHost = _settings.DefaultDestHost;
            DestPort = _settings.DefaultDestPort;
            DestSecurity = _settings.DefaultDestSecurity;

            TimePeriod = _settings.TimePeriod;
        }

        partial void OnTimePeriodChanged(TimePeriodOption value)
        {
            _settings.TimePeriod = value;
            SettingsStorage.Save(_settings);
        }

        partial void OnSelectedSourcePresetChanged(EmailProviderPreset? value)
        {
            if (value != null && !string.IsNullOrEmpty(value.DefaultHost))
            {
                SourceHost = value.DefaultHost;
                SourcePort = value.DefaultPort;
                SourceSecurity = value.Security;
            }
        }

        partial void OnSelectedDestPresetChanged(EmailProviderPreset? value)
        {
            if (value != null && !string.IsNullOrEmpty(value.DefaultHost))
            {
                DestHost = value.DefaultHost;
                DestPort = value.DefaultPort;
                DestSecurity = value.Security;
            }
        }

        [RelayCommand]
        public void ToggleSourcePass() => IsSourcePassVisible = !IsSourcePassVisible;

        [RelayCommand]
        public void ToggleDestPass() => IsDestPassVisible = !IsDestPassVisible;

        [RelayCommand]
        public async Task TestSourceAsync()
        {
            if (string.IsNullOrWhiteSpace(SourceHost) || string.IsNullOrWhiteSpace(SourceUser))
            {
                MessageBox.Show("Please enter the source server host and username.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            SourceStatusText = "Connecting...";
            try
            {
                var result = await Task.Run(() =>
                {
                    using (var client = new ImapClient(SourceHost, SourcePort, SourceSecurity))
                    {
                        return client.TestConnection(SourceUser, SourcePass);
                    }
                });

                if (result.SignInSuccess && result.ServerResponds)
                {
                    SourceStatusText = "✔ Connected";
                    MessageBox.Show($"Connection successful! Found {result.FolderCount} folder(s).", "Source Connection", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    SourceStatusText = "Error";
                    MessageBox.Show("Connection test failed: " + result.ErrorMessage, "Source Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                SourceStatusText = "Error";
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task TestDestAsync()
        {
            if (string.IsNullOrWhiteSpace(DestHost) || string.IsNullOrWhiteSpace(DestUser))
            {
                MessageBox.Show("Please enter the destination server host and username.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            DestStatusText = "Connecting...";
            try
            {
                var result = await Task.Run(() =>
                {
                    using (var client = new ImapClient(DestHost, DestPort, DestSecurity))
                    {
                        return client.TestConnection(DestUser, DestPass);
                    }
                });

                if (result.SignInSuccess && result.ServerResponds)
                {
                    DestStatusText = "✔ Connected";
                    MessageBox.Show($"Connection successful! Found {result.FolderCount} folder(s).", "Destination Connection", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    DestStatusText = "Error";
                    MessageBox.Show("Connection test failed: " + result.ErrorMessage, "Destination Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                DestStatusText = "Error";
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task ConnectSourceAsync()
        {
            if (string.IsNullOrWhiteSpace(SourceHost) || string.IsNullOrWhiteSpace(SourceUser))
            {
                MessageBox.Show("Please enter the source server host and username.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            SourceStatusText = "Fetching folders...";
            try
            {
                var folders = await Task.Run(() =>
                {
                    using (var client = new ImapClient(SourceHost, SourcePort, SourceSecurity))
                    {
                        client.Connect();
                        client.Login(SourceUser, SourcePass);
                        return client.ListFolders();
                    }
                });

                SourceStatusText = "✔ Connected";
                FolderMappings.Clear();
                foreach (var f in folders)
                {
                    var item = new FolderMappingItem(f.Name, f.MessageCount, f.Name, AvailableDestFolders);
                    item.PropertyChanged += (s, e) => UpdateSelectionSummary();
                    FolderMappings.Add(item);
                }
                UpdateSelectionSummary();
            }
            catch (Exception ex)
            {
                SourceStatusText = "Error";
                MessageBox.Show("Could not fetch folders: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task ConnectDestAsync()
        {
            if (string.IsNullOrWhiteSpace(DestHost) || string.IsNullOrWhiteSpace(DestUser))
            {
                MessageBox.Show("Please enter the destination server host and username.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            DestStatusText = "Fetching folders...";
            try
            {
                var folders = await Task.Run(() =>
                {
                    using (var client = new ImapClient(DestHost, DestPort, DestSecurity))
                    {
                        client.Connect();
                        client.Login(DestUser, DestPass);
                        return client.ListFolders();
                    }
                });

                DestStatusText = "✔ Connected";
                foreach (var f in folders)
                {
                    if (!AvailableDestFolders.Contains(f.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        AvailableDestFolders.Add(f.Name);
                    }
                }

                foreach (var m in FolderMappings)
                {
                    foreach (var f in AvailableDestFolders)
                    {
                        if (!m.AvailableDestinationFolders.Contains(f))
                        {
                            m.AvailableDestinationFolders.Add(f);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DestStatusText = "Error";
                MessageBox.Show("Could not fetch destination folders: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void SelectAll()
        {
            foreach (var m in FolderMappings) m.IsSelected = true;
            UpdateSelectionSummary();
        }

        [RelayCommand]
        public void ClearAll()
        {
            foreach (var m in FolderMappings) m.IsSelected = false;
            UpdateSelectionSummary();
        }

        [RelayCommand]
        public void AddCustomDestFolder()
        {
            string newFolder = Microsoft.VisualBasic.Interaction.InputBox("Enter custom destination folder name:", "Add Destination Folder", "Archive");
            if (!string.IsNullOrWhiteSpace(newFolder))
            {
                newFolder = newFolder.Trim();
                if (!AvailableDestFolders.Contains(newFolder, StringComparer.OrdinalIgnoreCase))
                {
                    AvailableDestFolders.Add(newFolder);
                    foreach (var m in FolderMappings)
                    {
                        if (!m.AvailableDestinationFolders.Contains(newFolder))
                        {
                            m.AvailableDestinationFolders.Add(newFolder);
                        }
                    }
                }
            }
        }

        public void UpdateSelectionSummary()
        {
            int selectedFolders = FolderMappings.Count(m => m.IsSelected);
            int selectedMsgs = FolderMappings.Where(m => m.IsSelected).Sum(m => m.MessageCount);
            SelectionSummary = $"Selected {selectedFolders} folders · {selectedMsgs} messages";
        }

        public AccountPair CreateAccountPair()
        {
            var pair = new AccountPair
            {
                SourceHost = SourceHost.Trim(),
                SourcePort = SourcePort,
                SourceSecurity = SourceSecurity,
                SourceUser = SourceUser.Trim(),
                SourcePassword = SourcePass,

                DestHost = DestHost.Trim(),
                DestPort = DestPort,
                DestSecurity = DestSecurity,
                DestUser = DestUser.Trim(),
                DestPassword = DestPass,

                FolderMappings = FolderMappings
                    .Where(m => m.IsSelected)
                    .Select(m => new FolderMapping
                    {
                        SourceFolder = m.SourceFolder,
                        DestFolder = string.IsNullOrWhiteSpace(m.DestinationFolder) ? m.SourceFolder : m.DestinationFolder
                    })
                    .ToList()
            };

            if (pair.FolderMappings.Count == 0)
            {
                pair.FolderMappings.Add(new FolderMapping { SourceFolder = "INBOX", DestFolder = "INBOX" });
            }

            return pair;
        }

        [RelayCommand]
        public void StartMigration()
        {
            if (string.IsNullOrWhiteSpace(SourceUser) || string.IsNullOrWhiteSpace(DestUser))
            {
                MessageBox.Show("Please enter both source and destination email accounts.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var pair = CreateAccountPair();
            RequestStartMigration?.Invoke(pair);
        }

        [RelayCommand]
        public void AddToQueue()
        {
            if (string.IsNullOrWhiteSpace(SourceUser) || string.IsNullOrWhiteSpace(DestUser))
            {
                MessageBox.Show("Please enter both source and destination email accounts.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var pair = CreateAccountPair();
            RequestAddToQueue?.Invoke(pair);
            MessageBox.Show($"Account {pair.SourceUser} added to migration queue.", "Queue", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
