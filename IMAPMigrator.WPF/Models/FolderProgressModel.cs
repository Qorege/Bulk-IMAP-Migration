using CommunityToolkit.Mvvm.ComponentModel;

namespace IMAPMigrator.WPF.Models
{
    public partial class FolderProgressModel : ObservableObject
    {
        [ObservableProperty]
        private string _folderName = "";

        [ObservableProperty]
        private string _status = "Pending";

        [ObservableProperty]
        private int _processed;

        [ObservableProperty]
        private int _total;

        [ObservableProperty]
        private int _moved;

        [ObservableProperty]
        private int _skipped;

        [ObservableProperty]
        private int _errors;

        [ObservableProperty]
        private double _percentage;

        public string SummaryText => $"{Percentage:F0}% ({Processed}/{Total})";

        partial void OnPercentageChanged(double value)
        {
            OnPropertyChanged(nameof(SummaryText));
        }

        partial void OnProcessedChanged(int value)
        {
            OnPropertyChanged(nameof(SummaryText));
        }

        partial void OnTotalChanged(int value)
        {
            OnPropertyChanged(nameof(SummaryText));
        }
    }
}
