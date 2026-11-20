using System;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImapSyncWin;

namespace IMAPMigrator.WPF.ViewModels
{
    public partial class MonitorViewModel : ObservableObject
    {
        // Overall progress
        [ObservableProperty] private double overallPercentage;
        [ObservableProperty] private int totalMessages;
        [ObservableProperty] private int processedMessages;
        [ObservableProperty] private int movedMessages;
        [ObservableProperty] private int skippedMessages;
        [ObservableProperty] private int errorMessages;
        [ObservableProperty] private double messagesPerSecond;
        [ObservableProperty] private string timeRemaining = "—";
        [ObservableProperty] private string currentAction = "Idle";

        // Folder progress
        [ObservableProperty] private string currentFolder = "";
        [ObservableProperty] private double folderPercentage;
        [ObservableProperty] private string folderStatus = "";

        // State
        [ObservableProperty] private bool isRunning;
        [ObservableProperty] private bool isPaused;

        // Log
        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        private MigrationEngine _engine;
        private bool _attached;

        public MonitorViewModel(MigrationEngine engine)
        {
            _engine = engine;
            AttachEngine(engine);
        }

        public void AttachEngine(MigrationEngine engine)
        {
            if (_attached && _engine == engine) return;

            _engine = engine;
            _attached = true;

            engine.OverallProgressChanged += (s, e) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    OverallPercentage = e.Percentage;
                    TotalMessages = e.TotalMessages;
                    ProcessedMessages = e.ProcessedMessages;
                    MovedMessages = e.MovedMessages;
                    SkippedMessages = e.SkippedMessages;
                    ErrorMessages = e.ErrorMessages;
                    MessagesPerSecond = e.MessagesPerSecond;
                    TimeRemaining = e.TimeRemainingText ?? "—";
                    CurrentAction = e.CurrentAction ?? "";
                });
            };

            engine.FolderProgressChanged += (s, e) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CurrentFolder = $"{e.FolderName} → {e.DestFolder}";
                    FolderPercentage = e.Percentage;
                    FolderStatus = e.Status ?? "";
                });
            };

            engine.LogEmitted += (s, e) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    LogEntries.Add(new LogEntry
                    {
                        Timestamp = e.Timestamp,
                        Level = e.Level,
                        Message = e.Message
                    });

                    // Keep log manageable
                    while (LogEntries.Count > 2000)
                        LogEntries.RemoveAt(0);
                });
            };

            engine.AccountStarted += (s, acc) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsRunning = true;
                    CurrentAction = $"Syncing {acc.SourceUser} → {acc.DestUser}";
                });
            };

            engine.QueueCompleted += (s, e) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsRunning = false;
                    IsPaused = false;
                    CurrentAction = "Complete";
                });
            };
        }

        [RelayCommand]
        private void Pause()
        {
            if (_engine.IsRunning && !_engine.IsPaused)
            {
                _engine.Pause();
                IsPaused = true;
            }
        }

        [RelayCommand]
        private void Resume()
        {
            if (_engine.IsRunning && _engine.IsPaused)
            {
                _engine.Resume();
                IsPaused = false;
            }
        }

        [RelayCommand]
        private void Stop()
        {
            if (_engine.IsRunning)
            {
                _engine.Stop();
            }
        }

        [RelayCommand]
        private void ClearLog()
        {
            LogEntries.Clear();
        }

        [RelayCommand]
        private void ClearJournal()
        {
            MigrationJournal.Instance.Clear();
            LogEntries.Add(new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = "INFO",
                Message = "Migration journal cleared"
            });
        }
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }

        public string TimeString => Timestamp.ToString("HH:mm:ss");
    }
}
