using System.ComponentModel;
using System;
using System.Collections.Generic;

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

        public string GroupName
        {
            get
            {
                if (Name.Contains("|"))
                    return Name.Split('|')[0];
                return Name;
            }
        }
        public string BaseUrl
        {
            get
            {
                if (Name.Contains("|"))
                    return Name.Split('|')[1];
                return Path;
            }
        }

        public List<string> AllSources { get; set; } = new List<string>();
        public int PageCount => AllSources?.Count ?? 0;
        public List<PageInfo> Pages { get; set; } = new List<PageInfo>();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class PageInfo
    {
        public string Url { get; set; } = string.Empty;
        // Optionally add Title, etc.
    }
} 