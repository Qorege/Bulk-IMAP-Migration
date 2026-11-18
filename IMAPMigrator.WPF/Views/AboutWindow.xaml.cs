using System.Diagnostics;
using System.Windows;
using MahApps.Metro.Controls;

namespace IMAPMigrator.WPF.Views
{
    public partial class AboutWindow : MetroWindow
    {
        public AboutWindow()
        {
            InitializeComponent();
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://github.com/Qorege/Bulk-IMAP-Migration") { UseShellExecute = true });
            }
            catch { }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
