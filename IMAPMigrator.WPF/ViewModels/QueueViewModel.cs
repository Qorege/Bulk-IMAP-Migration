using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImapSyncWin;
using Microsoft.Win32;

namespace IMAPMigrator.WPF.ViewModels
{
    public partial class QueueViewModel : ObservableObject
    {
        private readonly AppSettings _settings;
        private readonly MigrationEngine _engine;
        private readonly MonitorViewModel _monitor;

        public ObservableCollection<AccountPairViewModel> Accounts { get; } = new();
        [ObservableProperty] private AccountPairViewModel selectedAccount;
        [ObservableProperty] private bool isRunning;
        [ObservableProperty] private string statusMessage = "No accounts in queue";

        public QueueViewModel(AppSettings settings, MigrationEngine engine, MonitorViewModel monitor)
        {
            _settings = settings;
            _engine = engine;
            _monitor = monitor;

            _engine.QueueCompleted += (s, e) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsRunning = false;
                    StatusMessage = "Queue completed";
                });
            };
        }

        [RelayCommand]
        private void AddAccount()
        {
            var vm = new AccountPairViewModel(_settings);
            Accounts.Add(vm);
            SelectedAccount = vm;
            StatusMessage = $"{Accounts.Count} account(s) in queue";
        }

        [RelayCommand]
        private void RemoveAccount()
        {
            if (SelectedAccount != null && !IsRunning)
            {
                Accounts.Remove(SelectedAccount);
                SelectedAccount = Accounts.LastOrDefault();
                StatusMessage = Accounts.Count > 0 ? $"{Accounts.Count} account(s) in queue" : "No accounts in queue";
            }
        }

        [RelayCommand]
        private void ImportCsv()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                Title = "Import Accounts from CSV"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var imported = CsvHandler.ImportCsv(dlg.FileName, _settings);
                    foreach (var acc in imported)
                    {
                        Accounts.Add(new AccountPairViewModel(acc));
                    }
                    StatusMessage = $"Imported {imported.Count} account(s). Total: {Accounts.Count}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Import error: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void ExportCsv()
        {
            var dlg = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                Title = "Export Queue to CSV",
                FileName = "migration_queue.csv"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var list = Accounts.Select(a => a.ToAccountPair()).ToList();
                    CsvHandler.ExportQueueToCsv(dlg.FileName, list);
                    StatusMessage = "Queue exported";
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Export error: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void ExportSampleCsv()
        {
            var dlg = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                Title = "Export Sample CSV Template",
                FileName = "sample_accounts.csv"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    CsvHandler.CreateSampleCsv(dlg.FileName);
                    StatusMessage = "Sample CSV created";
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void StartQueue()
        {
            if (_engine.IsRunning || Accounts.Count == 0) return;

            var accountPairs = Accounts.Select(a => a.ToAccountPair()).ToList();

            // Ensure each account has at least default folder mappings
            foreach (var acc in accountPairs)
            {
                if (acc.FolderMappings.Count == 0)
                {
                    var defaults = _settings.DefaultFoldersToMigrate
                        .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var f in defaults)
                    {
                        var trimmed = f.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                        {
                            acc.FolderMappings.Add(new FolderMapping
                            {
                                SourceFolder = trimmed,
                                DestFolder = trimmed,
                                Selected = true
                            });
                        }
                    }
                }
            }

            _monitor.AttachEngine(_engine);
            IsRunning = true;
            StatusMessage = "Queue running...";
            _engine.RunQueueAsync(accountPairs);
        }

        [RelayCommand]
        private void PauseQueue()
        {
            if (_engine.IsRunning && !_engine.IsPaused)
            {
                _engine.Pause();
                StatusMessage = "Queue paused";
            }
        }

        [RelayCommand]
        private void ResumeQueue()
        {
            if (_engine.IsRunning && _engine.IsPaused)
            {
                _engine.Resume();
                StatusMessage = "Queue resumed";
            }
        }

        [RelayCommand]
        private void StopQueue()
        {
            if (_engine.IsRunning)
            {
                _engine.Stop();
                StatusMessage = "Stopping...";
            }
        }
    }

    public partial class AccountPairViewModel : ObservableObject
    {
        [ObservableProperty] private string sourceHost = "";
        [ObservableProperty] private int sourcePort = 993;
        [ObservableProperty] private SecurityType sourceSecurity = SecurityType.SslTls;
        [ObservableProperty] private string sourceUser = "";
        [ObservableProperty] private string sourcePassword = "";

        [ObservableProperty] private string destHost = "";
        [ObservableProperty] private int destPort = 993;
        [ObservableProperty] private SecurityType destSecurity = SecurityType.SslTls;
        [ObservableProperty] private string destUser = "";
        [ObservableProperty] private string destPassword = "";

        [ObservableProperty] private string status = "Idle";
        [ObservableProperty] private string folders = "";

        public AccountPairViewModel(AppSettings settings)
        {
            SourceHost = settings.DefaultSourceHost;
            SourcePort = settings.DefaultSourcePort;
            SourceSecurity = settings.DefaultSourceSecurity;
            DestHost = settings.DefaultDestHost;
            DestPort = settings.DefaultDestPort;
            DestSecurity = settings.DefaultDestSecurity;
            Folders = settings.DefaultFoldersToMigrate;
        }

        public AccountPairViewModel(AccountPair pair)
        {
            SourceHost = pair.SourceHost;
            SourcePort = pair.SourcePort;
            SourceSecurity = pair.SourceSecurity;
            SourceUser = pair.SourceUser;
            SourcePassword = pair.SourcePassword;
            DestHost = pair.DestHost;
            DestPort = pair.DestPort;
            DestSecurity = pair.DestSecurity;
            DestUser = pair.DestUser;
            DestPassword = pair.DestPassword;
            Status = pair.Status.ToString();

            if (pair.FolderMappings.Count > 0)
                Folders = string.Join(", ", pair.FolderMappings.Select(f => f.SourceFolder));
        }

        public AccountPair ToAccountPair()
        {
            var acc = new AccountPair
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

            if (!string.IsNullOrWhiteSpace(Folders))
            {
                var parts = Folders.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var f in parts)
                {
                    var trimmed = f.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        acc.FolderMappings.Add(new FolderMapping
                        {
                            SourceFolder = trimmed,
                            DestFolder = trimmed,
                            Selected = true
                        });
                    }
                }
            }

            return acc;
        }

        public string DisplayName => string.IsNullOrEmpty(SourceUser) ? "(new account)" : $"{SourceUser} → {DestUser}";
    }
}
