using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImapSyncWin;
using IMAPMigrator.WPF.Models;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace IMAPMigrator.WPF.ViewModels
{
    public partial class MonitorViewModel : ObservableObject
    {
        private readonly MigrationEngine _engine;
        private readonly ObservableCollection<double> _speedValues = new ObservableCollection<double>();
        private DateTime _lastUpdateTime = DateTime.Now;
        private int _lastProcessedCount;

        [ObservableProperty]
        private string _heading = "Migration in progress";

        [ObservableProperty]
        private string _pairDescription = "Ready to migrate";

        [ObservableProperty]
        private string _processedSummary = "0 of 0 messages processed (0%)";

        [ObservableProperty]
        private string _statusBadgeText = "Idle";

        [ObservableProperty]
        private double _overallProgressPercentage;

        [ObservableProperty]
        private int _totalMessages;

        [ObservableProperty]
        private int _movedCount;

        [ObservableProperty]
        private int _skippedCount;

        [ObservableProperty]
        private int _errorsCount;

        [ObservableProperty]
        private bool _isMigrating;

        [ObservableProperty]
        private bool _isPaused;

        // LiveCharts2 Series
        public ISeries[] PieSeries { get; set; }
        public ISeries[] SpeedSeries { get; set; }
        public Axis[] XAxes { get; set; }
        public Axis[] YAxes { get; set; }

        public ObservableCollection<FolderProgressModel> FolderProgressList { get; } = new ObservableCollection<FolderProgressModel>();
        public ObservableCollection<LogEntry> LogEntries { get; } = new ObservableCollection<LogEntry>();

        public MonitorViewModel(MigrationEngine engine)
        {
            _engine = engine;

            // Initialize LiveCharts2 Donut Breakdown Chart
            PieSeries = new ISeries[]
            {
                new PieSeries<int>
                {
                    Name = "Moved",
                    Values = new[] { 0 },
                    Fill = new SolidColorPaint(SKColor.Parse("#0284c7")), // Blue/Sky
                    InnerRadius = 50
                },
                new PieSeries<int>
                {
                    Name = "Skipped",
                    Values = new[] { 0 },
                    Fill = new SolidColorPaint(SKColor.Parse("#14b8a6")), // Teal
                    InnerRadius = 50
                },
                new PieSeries<int>
                {
                    Name = "Errors",
                    Values = new[] { 0 },
                    Fill = new SolidColorPaint(SKColor.Parse("#ef4444")), // Red
                    InnerRadius = 50
                }
            };

            // Initialize LiveCharts2 Throughput Speed Chart
            _speedValues.Add(0);
            SpeedSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Name = "Emails / sec",
                    Values = _speedValues,
                    Fill = new LinearGradientPaint(
                        new[] { SKColor.Parse("#38bdf8").WithAlpha(80), SKColor.Parse("#38bdf8").WithAlpha(10) },
                        new SKPoint(0.5f, 0),
                        new SKPoint(0.5f, 1)),
                    Stroke = new SolidColorPaint(SKColor.Parse("#0284c7"), 2.5f),
                    GeometrySize = 4,
                    GeometryFill = new SolidColorPaint(SKColor.Parse("#0284c7")),
                    GeometryStroke = new SolidColorPaint(SKColors.White, 1.5f),
                    LineSmoothness = 0.65
                }
            };

            XAxes = new[]
            {
                new Axis
                {
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#94a3b8")),
                    ShowSeparatorLines = false
                }
            };

            YAxes = new[]
            {
                new Axis
                {
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#94a3b8")),
                    MinLimit = 0
                }
            };
        }

        public void ResetForNewMigration(string pairDesc)
        {
            Heading = "Migration in progress";
            PairDescription = pairDesc;
            ProcessedSummary = "0 of 0 messages processed (0%)";
            StatusBadgeText = "Running";
            OverallProgressPercentage = 0;
            TotalMessages = 0;
            MovedCount = 0;
            SkippedCount = 0;
            ErrorsCount = 0;
            IsMigrating = true;
            IsPaused = false;

            _lastProcessedCount = 0;
            _lastUpdateTime = DateTime.Now;

            FolderProgressList.Clear();
            _speedValues.Clear();
            _speedValues.Add(0);

            UpdateCharts(0, 0, 0);
        }

        public void HandleProgress(OverallProgressEventArgs e)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                TotalMessages = e.TotalMessages;
                MovedCount = e.MovedMessages;
                SkippedCount = e.SkippedMessages;
                ErrorsCount = e.ErrorMessages;
                OverallProgressPercentage = Math.Min(100.0, Math.Max(0.0, e.Percentage));

                string timeInfo = !string.IsNullOrEmpty(e.TimeRemainingText) ? $" · {e.TimeRemainingText}" : "";
                ProcessedSummary = $"{e.ProcessedMessages} of {e.TotalMessages} messages ({e.Percentage:F1}%){timeInfo}";

                StatusBadgeText = IsPaused ? "Paused" : "Running";

                // Update Throughput Speed calculation
                var now = DateTime.Now;
                var elapsed = (now - _lastUpdateTime).TotalSeconds;
                if (elapsed >= 1.0)
                {
                    int processedDelta = e.ProcessedMessages - _lastProcessedCount;
                    double rate = elapsed > 0 ? Math.Max(0.0, processedDelta / elapsed) : 0.0;
                    _speedValues.Add(Math.Round(rate, 1));
                    if (_speedValues.Count > 30) _speedValues.RemoveAt(0);

                    _lastProcessedCount = e.ProcessedMessages;
                    _lastUpdateTime = now;
                }

                // Update charts
                UpdateCharts(e.MovedMessages, e.SkippedMessages, e.ErrorMessages);
            });
        }

        public void HandleFolderProgress(FolderProgressEventArgs e)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var existing = FindFolderModel(e.FolderName);
                if (existing == null)
                {
                    existing = new FolderProgressModel
                    {
                        FolderName = e.FolderName
                    };
                    FolderProgressList.Add(existing);
                }

                existing.Status = e.Status;
                existing.Processed = e.Processed;
                existing.Total = e.Total;
                existing.Moved = e.Moved;
                existing.Skipped = e.Skipped;
                existing.Errors = e.Errors;
                existing.Percentage = e.Percentage;
            });
        }

        public void HandleLog(LogEventArgs e)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                LogEntries.Add(new LogEntry
                {
                    Level = e.Level,
                    Message = e.Message
                });

                if (LogEntries.Count > 1000)
                {
                    LogEntries.RemoveAt(0);
                }
            });
        }

        public void SetCompleted(bool success, string summary)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                IsMigrating = false;
                IsPaused = false;
                StatusBadgeText = success ? "Completed" : "Stopped";
                Heading = success ? "Migration completed successfully" : "Migration finished with errors";
                PairDescription = summary;
            });
        }

        private FolderProgressModel? FindFolderModel(string folderName)
        {
            foreach (var item in FolderProgressList)
            {
                if (string.Equals(item.FolderName, folderName, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }
            return null;
        }

        private void UpdateCharts(int moved, int skipped, int errors)
        {
            try
            {
                if (PieSeries.Length >= 3)
                {
                    PieSeries[0].Values = new[] { Math.Max(0, moved) };
                    PieSeries[1].Values = new[] { Math.Max(0, skipped) };
                    PieSeries[2].Values = new[] { Math.Max(0, errors) };
                }
            }
            catch { }
        }

        [RelayCommand]
        public void TogglePause()
        {
            if (_engine.IsPaused)
            {
                _engine.Resume();
                IsPaused = false;
                StatusBadgeText = "Running";
            }
            else
            {
                _engine.Pause();
                IsPaused = true;
                StatusBadgeText = "Paused";
            }
        }

        [RelayCommand]
        public void Stop()
        {
            if (MessageBox.Show("Are you sure you want to stop the migration? Any in-progress transfers will pause safely.",
                    "Confirm Stop", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                _engine.Stop();
                IsMigrating = false;
                IsPaused = false;
                StatusBadgeText = "Stopped";
            }
        }

        [RelayCommand]
        public void ClearLog()
        {
            LogEntries.Clear();
        }

        [RelayCommand]
        public void OpenLogFile()
        {
            try
            {
                string logFile = "migration_journal.tsv";
                if (!File.Exists(logFile))
                {
                    File.WriteAllText(logFile, "Timestamp\tSourceUser\tDestUser\tFolder\tUID\tMessageId\tStatus\n");
                }
                Process.Start(new ProcessStartInfo(logFile) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open log file: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
