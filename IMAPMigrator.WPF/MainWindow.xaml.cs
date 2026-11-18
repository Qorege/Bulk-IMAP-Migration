using MahApps.Metro.Controls;
using IMAPMigrator.WPF.ViewModels;
using IMAPMigrator.WPF.Views;

namespace IMAPMigrator.WPF
{
    public partial class MainWindow : MetroWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            var vm = new MainViewModel();
            DataContext = vm;

            vm.OpenEditAccountWindow = pair =>
            {
                var editVm = new AccountEditViewModel(pair);
                var win = new AccountEditWindow(editVm)
                {
                    Owner = this
                };
                win.ShowDialog();
            };

            vm.OpenAboutWindow = () =>
            {
                var about = new AboutWindow
                {
                    Owner = this
                };
                about.ShowDialog();
            };
        }
    }
}
