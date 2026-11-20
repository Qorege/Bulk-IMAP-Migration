using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ImapSyncWin;

namespace IMAPMigrator.WPF.Converters
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is AccountSyncStatus status)
            {
                return status switch
                {
                    AccountSyncStatus.Completed => new SolidColorBrush(Color.FromRgb(0x1B, 0x6D, 0x2F)),   // success
                    AccountSyncStatus.Failed => new SolidColorBrush(Color.FromRgb(0xBA, 0x1A, 0x1A)),      // error
                    AccountSyncStatus.Syncing => new SolidColorBrush(Color.FromRgb(0x00, 0x6A, 0x6A)),     // primary
                    AccountSyncStatus.Connecting => new SolidColorBrush(Color.FromRgb(0x00, 0x6A, 0x6A)),
                    AccountSyncStatus.Scanning => new SolidColorBrush(Color.FromRgb(0x00, 0x6A, 0x6A)),
                    AccountSyncStatus.Retrying => new SolidColorBrush(Color.FromRgb(0x7D, 0x57, 0x00)),    // warning
                    AccountSyncStatus.Stopped => new SolidColorBrush(Color.FromRgb(0x6F, 0x79, 0x79)),     // outline
                    AccountSyncStatus.Pending => new SolidColorBrush(Color.FromRgb(0x4A, 0x63, 0x63)),     // secondary
                    _ => new SolidColorBrush(Color.FromRgb(0x3F, 0x49, 0x48)),                              // onSurfaceVariant
                };
            }

            if (value is string level)
            {
                return level.ToUpperInvariant() switch
                {
                    "ERROR" => new SolidColorBrush(Color.FromRgb(0xBA, 0x1A, 0x1A)),
                    "WARN" => new SolidColorBrush(Color.FromRgb(0x7D, 0x57, 0x00)),
                    "DEBUG" => new SolidColorBrush(Color.FromRgb(0x6F, 0x79, 0x79)),
                    _ => new SolidColorBrush(Color.FromRgb(0x17, 0x1D, 0x1D)),
                };
            }

            return new SolidColorBrush(Color.FromRgb(0x17, 0x1D, 0x1D));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : value;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : value;
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool invert = parameter is string s && s == "Invert";
            bool visible = value is bool b && b;
            if (invert) visible = !visible;
            return visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility v && v == Visibility.Visible;
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value != null ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    public class EnumToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            return value.ToString() == parameter.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b && parameter is string s)
            {
                return Enum.Parse(targetType, s);
            }
            return Binding.DoNothing;
        }
    }
}
