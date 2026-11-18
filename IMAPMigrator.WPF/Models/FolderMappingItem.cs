using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace IMAPMigrator.WPF.Models
{
    public partial class FolderMappingItem : ObservableObject
    {
        [ObservableProperty]
        private string _sourceFolder = "";

        [ObservableProperty]
        private bool _isSelected = true;

        [ObservableProperty]
        private int _messageCount;

        [ObservableProperty]
        private string _destinationFolder = "";

        public ObservableCollection<string> AvailableDestinationFolders { get; } = new ObservableCollection<string>();

        public FolderMappingItem()
        {
        }

        public FolderMappingItem(string sourceFolder, int messageCount, string destinationFolder, ObservableCollection<string> choices)
        {
            SourceFolder = sourceFolder;
            MessageCount = messageCount;
            DestinationFolder = destinationFolder;
            IsSelected = true;

            if (choices != null)
            {
                foreach (var choice in choices)
                {
                    AvailableDestinationFolders.Add(choice);
                }
            }
        }
    }
}
