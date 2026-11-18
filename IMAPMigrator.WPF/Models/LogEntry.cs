using System;
using System.Windows.Media;

namespace IMAPMigrator.WPF.Models
{
    public class LogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Level { get; set; } = "INFO";
        public string Message { get; set; } = "";

        public string FormattedTime => Timestamp.ToString("HH:mm:ss");

        public Brush LevelBrush
        {
            get
            {
                switch (Level.ToUpperInvariant())
                {
                    case "ERROR":
                        return new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
                    case "WARN":
                        return new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Amber
                    case "DEBUG":
                        return new SolidColorBrush(Color.FromRgb(148, 163, 184)); // Slate
                    case "INFO":
                    default:
                        return new SolidColorBrush(Color.FromRgb(14, 165, 233)); // Cyan/Sky
                }
            }
        }
    }
}
