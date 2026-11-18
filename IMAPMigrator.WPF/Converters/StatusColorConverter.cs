using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ImapSyncWin;

namespace IMAPMigrator.WPF.Converters
{
    public class StatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is AccountSyncStatus status)
            {
                switch (status)
                {
                    case AccountSyncStatus.Completed:
                        return new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green
                    case AccountSyncStatus.Syncing:
                    case AccountSyncStatus.Connecting:
                    case AccountSyncStatus.Scanning:
                        return new SolidColorBrush(Color.FromRgb(14, 165, 233)); // Sky
                    case AccountSyncStatus.Failed:
                        return new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
                    case AccountSyncStatus.Retrying:
                    case AccountSyncStatus.Stopped:
                        return new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Amber
                    default:
                        return new SolidColorBrush(Color.FromRgb(148, 163, 184)); // Slate
                }
            }
            return new SolidColorBrush(Color.FromRgb(148, 163, 184));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
