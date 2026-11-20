using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImapSyncWin;

namespace IMAPMigrator.WPF.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty] private int selectedTabIndex;
        [ObservableProperty] private string statusText = "Ready";

        public SingleMigrationViewModel SingleMigration { get; }
        public QueueViewModel Queue { get; }
        public MonitorViewModel Monitor { get; }
        public SettingsViewModel Settings { get; }

        private readonly AppSettings _appSettings;
        private MigrationEngine _engine;

        public MainViewModel()
        {
            _appSettings = SettingsStorage.Load();
            _engine = new MigrationEngine(_appSettings);

            Settings = new SettingsViewModel(_appSettings);
            Monitor = new MonitorViewModel(_engine);
            SingleMigration = new SingleMigrationViewModel(_appSettings, _engine, Monitor);
            Queue = new QueueViewModel(_appSettings, _engine, Monitor);

            Settings.SettingsSaved += () =>
            {
                _engine.Settings = _appSettings;
                StatusText = "Settings saved";
            };
        }
    }
}
