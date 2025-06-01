using System.ComponentModel;
using System;

namespace DocsDoc.Desktop.ViewModels
{
    /// <summary>
    /// Represents a document for the UI with selection state.
    /// </summary>
    public class DocumentInfo : INotifyPropertyChanged
    {
        /// <summary>
        /// Event fired when any document's RAG selection changes.
        /// </summary>
        public static event EventHandler? RagSelectionChanged;
        
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        
        private bool _isSelectedForRag = true; // Default to selected
        /// <summary>
        /// Whether this document source should be used for RAG queries.
        /// </summary>
        public bool IsSelectedForRag 
        { 
            get => _isSelectedForRag; 
            set 
            { 
                _isSelectedForRag = value; 
                OnPropertyChanged(nameof(IsSelectedForRag));
                RagSelectionChanged?.Invoke(this, EventArgs.Empty);
            } 
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
} 