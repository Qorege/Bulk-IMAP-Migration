using System;
using System.Windows;

namespace IMAPMigrator.WPF
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                MessageBox.Show(
                    "Unhandled error: " + (ex?.Message ?? "Unknown"),
                    "IMAP Migrator Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show(
                    "UI error: " + args.Exception.Message,
                    "IMAP Migrator Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                args.Handled = true;
            };
        }
    }
}
