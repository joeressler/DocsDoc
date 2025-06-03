using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Markup.Xaml;
using DocsDoc.Desktop.ViewModels;

namespace DocsDoc.Desktop.Views
{
    public partial class GroupNameDialog : Window
    {
        public GroupNameDialog()
        {
            InitializeComponent();
            ViewModel = new GroupNameDialogViewModel("", "", "");
            DataContext = ViewModel;
        }

        public GroupNameDialogViewModel ViewModel { get; }

        public class GroupNameDialogResult
        {
            public string EntrypointUrl { get; set; } = string.Empty;
            public string BaseDocsUrl { get; set; } = string.Empty;
            public string GroupName { get; set; } = string.Empty;
        }

        public GroupNameDialog(string entrypointUrl, string baseDocsUrl, string defaultGroupName)
        {
            InitializeComponent();
            ViewModel = new GroupNameDialogViewModel(entrypointUrl, baseDocsUrl, defaultGroupName);
            ViewModel.OkCommand = new RelayCommand(_ => {
                var result = new GroupNameDialogResult {
                    EntrypointUrl = ViewModel.EntrypointUrl,
                    BaseDocsUrl = ViewModel.BaseDocsUrl,
                    GroupName = ViewModel.GroupName
                };
                this.Close(result);
                return Task.CompletedTask;
            });
            ViewModel.CancelCommand = new RelayCommand(_ => { this.Close(null); return Task.CompletedTask; });
            DataContext = ViewModel;
        }

        public async Task<GroupNameDialogResult?> ShowDialogAsync(Window parent)
        {
            var result = await this.ShowDialog<GroupNameDialogResult?>(parent);
            return result;
        }
    }
} 