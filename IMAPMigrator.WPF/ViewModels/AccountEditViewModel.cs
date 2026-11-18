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
    public partial class AccountEditViewModel : ObservableObject
    {
        private readonly AccountPair _account;
        public Action? RequestClose { get; set; }

        [ObservableProperty]
        private string _sourceHost = "";

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
        private string _destHost = "";

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
        private bool _isBusy;

        public ObservableCollection<string> AvailableDestFolders { get; } = new ObservableCollection<string>
        {
            "INBOX", "Archive", "Sent", "Drafts", "Trash", "Junk", "Spam"
        };

        public ObservableCollection<FolderMappingItem> FolderMappings { get; } = new ObservableCollection<FolderMappingItem>();

        public AccountEditViewModel(AccountPair account)
        {
            _account = account;
            LoadAccount();
        }

        private void LoadAccount()
        {
            SourceHost = _account.SourceHost;
            SourcePort = _account.SourcePort;
            SourceSecurity = _account.SourceSecurity;
            SourceUser = _account.SourceUser;
            SourcePass = _account.SourcePassword;

            DestHost = _account.DestHost;
            DestPort = _account.DestPort;
            DestSecurity = _account.DestSecurity;
            DestUser = _account.DestUser;
            DestPass = _account.DestPassword;

            FolderMappings.Clear();
            if (_account.FolderMappings != null && _account.FolderMappings.Count > 0)
            {
                foreach (var m in _account.FolderMappings)
                {
                    if (!AvailableDestFolders.Contains(m.DestFolder, StringComparer.OrdinalIgnoreCase))
                    {
                        AvailableDestFolders.Add(m.DestFolder);
                    }
                    FolderMappings.Add(new FolderMappingItem(m.SourceFolder, 0, m.DestFolder, AvailableDestFolders));
                }
            }
            else
            {
                FolderMappings.Add(new FolderMappingItem("INBOX", 0, "INBOX", AvailableDestFolders));
            }
        }

        [RelayCommand]
        public void ToggleSourcePass() => IsSourcePassVisible = !IsSourcePassVisible;

        [RelayCommand]
        public void ToggleDestPass() => IsDestPassVisible = !IsDestPassVisible;

        [RelayCommand]
        public async Task TestSourceAsync()
        {
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
                    FolderMappings.Add(new FolderMappingItem(f.Name, f.MessageCount, f.Name, AvailableDestFolders));
                }
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

                // Update available destination folders on all mapping items
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
        }

        [RelayCommand]
        public void ClearAll()
        {
            foreach (var m in FolderMappings) m.IsSelected = false;
        }

        [RelayCommand]
        public void AddCustomFolder()
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

        [RelayCommand]
        public void Save()
        {
            _account.SourceHost = SourceHost.Trim();
            _account.SourcePort = SourcePort;
            _account.SourceSecurity = SourceSecurity;
            _account.SourceUser = SourceUser.Trim();
            _account.SourcePassword = SourcePass;

            _account.DestHost = DestHost.Trim();
            _account.DestPort = DestPort;
            _account.DestSecurity = DestSecurity;
            _account.DestUser = DestUser.Trim();
            _account.DestPassword = DestPass;

            _account.FolderMappings = FolderMappings
                .Where(m => m.IsSelected)
                .Select(m => new FolderMapping
                {
                    SourceFolder = m.SourceFolder,
                    DestFolder = string.IsNullOrWhiteSpace(m.DestinationFolder) ? m.SourceFolder : m.DestinationFolder
                })
                .ToList();

            RequestClose?.Invoke();
        }

        [RelayCommand]
        public void Cancel()
        {
            RequestClose?.Invoke();
        }
    }
}
