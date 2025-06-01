using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DocsDoc.Desktop.ViewModels;
using System.Collections.Specialized;
using System;

namespace DocsDoc.Desktop.Views
{
    public partial class ChatView : UserControl
    {
        private ListBox? _chatListBox;
        
        public ChatView()
        {
            InitializeComponent();
            _chatListBox = this.FindControl<ListBox>("ChatListBox");
            this.DataContextChanged += OnDataContextChanged;
        }
        
        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (DataContext is ChatViewModel chatViewModel)
            {
                // Subscribe to chat history changes for auto-scrolling
                chatViewModel.ChatHistory.CollectionChanged += OnChatHistoryChanged;
            }
        }
        
        private void OnChatHistoryChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && _chatListBox != null)
            {
                // Auto-scroll to the latest message
                if (_chatListBox.ItemCount > 0)
                {
                    _chatListBox.ScrollIntoView(_chatListBox.ItemCount - 1);
                }
            }
        }
    }
} 