using MahApps.Metro.Controls;
using IMAPMigrator.WPF.ViewModels;

namespace IMAPMigrator.WPF.Views
{
    public partial class AccountEditWindow : MetroWindow
    {
        public AccountEditWindow(AccountEditViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.RequestClose = () => Close();
        }
    }
}
