using Avalonia.Controls;
using Avalonia.Interactivity;
using DocsDoc.Desktop.ViewModels;
using DocsDoc.Core.Services;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml;
using System;
using System.Diagnostics;
using Avalonia.VisualTree;

namespace DocsDoc.Desktop.Views
{
    public partial class DocumentManagementView : UserControl
    {
        public DocumentManagementView()
        {
            LoggingService.LogInfo("Initializing DocumentManagementView");
            InitializeComponent();
            this.FindControl<Button>("AddDocumentButton").Click += AddDocumentButton_Click;
            WireUpPageLinkClicks();
            LoggingService.LogInfo("DocumentManagementView initialized successfully");
        }

        private async void AddDocumentButton_Click(object? sender, RoutedEventArgs e)
        {
            LoggingService.LogInfo("Add document button clicked");
            try
            {
                var dlg = new OpenFileDialog
                {
                    AllowMultiple = false,
                    Title = "Select a document to ingest"
                };
                
                var window = TopLevel.GetTopLevel(this) as Window;
                LoggingService.LogInfo("Opening file picker dialog");
                
                var result = await dlg.ShowAsync(window);
                if (result != null && result.Length > 0)
                {
                    var filePath = result[0];
                    LoggingService.LogInfo($"File selected for ingestion: {filePath}");
                    
                    if (DataContext is MainViewModel vm)
                    {
                        await vm.IngestDocumentAsync(filePath);
                    }
                    else
                    {
                        LoggingService.LogInfo("DataContext is not MainViewModel, cannot ingest document");
                    }
                }
                else
                {
                    LoggingService.LogInfo("File picker dialog cancelled or no file selected");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error in file picker dialog", ex);
            }
        }

        private void WireUpPageLinkClicks()
        {
            // Find all PageLink TextBlocks in the visual tree and wire up PointerPressed
            var listBox = this.FindControl<ListBox>("DocumentListBox");
            if (listBox == null) return;
            listBox.AttachedToVisualTree += (s, e) =>
            {
                WireLinksRecursive(listBox);
            };
        }

        private void WireLinksRecursive(Avalonia.Controls.Control parent)
        {
            foreach (var child in parent.GetVisualDescendants())
            {
                if (child is Avalonia.Controls.TextBlock tb && tb.Name == "PageLink")
                {
                    tb.PointerPressed += OnPageLinkClicked;
                }
            }
        }

        private void OnPageLinkClicked(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            if (sender is Avalonia.Controls.TextBlock tb && tb.DataContext is PageInfo page)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = page.Url,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    DocsDoc.Core.Services.LoggingService.LogError($"Failed to open page URL: {page.Url}", ex);
                }
            }
        }
    }
} 