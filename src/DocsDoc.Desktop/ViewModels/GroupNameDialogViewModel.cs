using System.ComponentModel;
using System.Windows.Input;

namespace DocsDoc.Desktop.ViewModels
{
    public class GroupNameDialogViewModel : INotifyPropertyChanged
    {
        private string _entrypointUrl = string.Empty;
        public string EntrypointUrl { get => _entrypointUrl; set { _entrypointUrl = value; OnPropertyChanged(nameof(EntrypointUrl)); } }
        private string _baseDocsUrl = string.Empty;
        public string BaseDocsUrl { get => _baseDocsUrl; set { _baseDocsUrl = value; OnPropertyChanged(nameof(BaseDocsUrl)); } }
        private string _groupName = string.Empty;
        public string GroupName { get => _groupName; set { _groupName = value; OnPropertyChanged(nameof(GroupName)); } }
        public ICommand OkCommand { get; set; } = null!;
        public ICommand CancelCommand { get; set; } = null!;

        public GroupNameDialogViewModel(string entrypointUrl, string baseDocsUrl, string defaultGroupName)
        {
            EntrypointUrl = entrypointUrl;
            BaseDocsUrl = baseDocsUrl;
            GroupName = defaultGroupName;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
