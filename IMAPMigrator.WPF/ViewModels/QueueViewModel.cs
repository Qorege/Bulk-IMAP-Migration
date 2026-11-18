using System;
using System.Collections.ObjectModel;
using System.IO;
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
        public Action? RequestStartQueue { get; set; }
        public Action? RequestPauseQueue { get; set; }
        public Action? RequestStopQueue { get; set; }
        public Action<AccountPair>? RequestEditAccount { get; set; }

        public ObservableCollection<AccountPair> Queue { get; } = new ObservableCollection<AccountPair>();

        [ObservableProperty]
        private AccountPair? _selectedAccount;

        [ObservableProperty]
        private QueueConcurrencyOption _concurrency = QueueConcurrencyOption.OneAtATime;

        [ObservableProperty]
        private bool _isQueueRunning;

        [ObservableProperty]
        private bool _isQueuePaused;

        public QueueViewModel(AppSettings settings)
        {
            _settings = settings;
            Concurrency = _settings.QueueConcurrency;
        }

        partial void OnConcurrencyChanged(QueueConcurrencyOption value)
        {
            _settings.QueueConcurrency = value;
            SettingsStorage.Save(_settings);
        }

        [RelayCommand]
        public void StartQueue()
        {
            if (Queue.Count == 0)
            {
                MessageBox.Show("The queue is empty. Add accounts or import from CSV first.", "Empty Queue", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsQueueRunning = true;
            IsQueuePaused = false;
            RequestStartQueue?.Invoke();
        }

        [RelayCommand]
        public void PauseQueue()
        {
            IsQueuePaused = !IsQueuePaused;
            RequestPauseQueue?.Invoke();
        }

        [RelayCommand]
        public void StopQueue()
        {
            if (MessageBox.Show("Are you sure you want to stop the queue?", "Stop Queue", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                IsQueueRunning = false;
                IsQueuePaused = false;
                RequestStopQueue?.Invoke();
            }
        }

        [RelayCommand]
        public void ImportCsv()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Import Accounts from CSV",
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var accounts = CsvHandler.ImportCsv(dlg.FileName, _settings);
                    if (accounts != null && accounts.Count > 0)
                    {
                        foreach (var a in accounts)
                        {
                            Queue.Add(a);
                        }
                        MessageBox.Show($"Successfully imported {accounts.Count} account(s) from CSV.", "Import Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("No valid accounts found in the selected CSV file.", "Import Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error reading CSV file: " + ex.Message, "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        public void ExportCsvTemplate()
        {
            var dlg = new SaveFileDialog
            {
                Title = "Export Sample CSV Template",
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = "sample_accounts.csv"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    CsvHandler.CreateSampleCsv(dlg.FileName);
                    MessageBox.Show("Sample CSV template exported successfully.", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error saving CSV template: " + ex.Message, "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        public void EditAccount()
        {
            if (SelectedAccount == null)
            {
                MessageBox.Show("Please select an account in the queue to edit.", "Edit Account", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            RequestEditAccount?.Invoke(SelectedAccount);
        }

        [RelayCommand]
        public void RemoveAccount()
        {
            if (SelectedAccount != null)
            {
                Queue.Remove(SelectedAccount);
            }
        }

        [RelayCommand]
        public void ClearFinished()
        {
            var finished = Queue.Where(a => a.Status == AccountSyncStatus.Completed).ToList();
            foreach (var item in finished)
            {
                Queue.Remove(item);
            }
        }

        public void AddAccount(AccountPair account)
        {
            Queue.Add(account);
        }
    }
}
