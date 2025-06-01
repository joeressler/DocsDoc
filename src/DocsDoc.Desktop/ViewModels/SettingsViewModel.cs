using DocsDoc.Core.Models;
using DocsDoc.Core.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace DocsDoc.Desktop.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly ConfigurationService _configurationService;
        private Configuration? _currentConfiguration;

        public Configuration? CurrentConfiguration
        {
            get => _currentConfiguration;
            set
            {
                if (_currentConfiguration != value)
                {
                    if (_currentConfiguration != null)
                    {
                        _currentConfiguration.PropertyChanged -= OnConfigurationPropertyChanged;
                        RecursivelyRemovePropertyChangedHandlers(_currentConfiguration);
                    }
                    _currentConfiguration = value;
                    if (_currentConfiguration != null)
                    {
                        _currentConfiguration.PropertyChanged += OnConfigurationPropertyChanged;
                        RecursivelyAddPropertyChangedHandlers(_currentConfiguration);
                    }
                    OnPropertyChanged(nameof(CurrentConfiguration));
                    ((RelayCommand)SaveSettingsCommand).RaiseCanExecuteChanged(); 
                }
            }
        }

        public ICommand SaveSettingsCommand { get; }
        public ICommand LoadSettingsCommand { get; }

        public ObservableCollection<string> AvailableBackends { get; } = new ObservableCollection<string> { "Cpu", "Cuda11", "Cuda12", "OpenCL", "Vulkan", "Metal" };
        public ObservableCollection<string> AvailableThemes { get; } = new ObservableCollection<string> { "Light", "Dark", "Default" };
        public ObservableCollection<string> AvailableLogLevels { get; } = new ObservableCollection<string> { "Verbose", "Debug", "Information", "Warning", "Error", "Fatal" };

        public SettingsViewModel(ConfigurationService configurationService)
        {
            _configurationService = configurationService;
            _ = LoadSettingsAsync(); // Call async method without awaiting

            SaveSettingsCommand = new RelayCommand(async _ => await SaveSettingsAsync(), _ => IsConfigurationDirty());
            LoadSettingsCommand = new RelayCommand(async _ => await LoadSettingsAsync());
        }

        private bool _isDirty = false;
        private bool IsConfigurationDirty()
        {
            return _isDirty;
        }

        private void OnConfigurationPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            _isDirty = true;
            ((RelayCommand)SaveSettingsCommand).RaiseCanExecuteChanged();
        }

        private void RecursivelyAddPropertyChangedHandlers(INotifyPropertyChanged obj)
        {
            if (obj == null) return;
            obj.PropertyChanged += OnConfigurationPropertyChanged;
            foreach (var prop in obj.GetType().GetProperties().Where(p => typeof(INotifyPropertyChanged).IsAssignableFrom(p.PropertyType)))
            {
                var childObj = prop.GetValue(obj) as INotifyPropertyChanged;
                if (childObj != null)
                {
                    RecursivelyAddPropertyChangedHandlers(childObj);
                }
            }
        }

        private void RecursivelyRemovePropertyChangedHandlers(INotifyPropertyChanged obj)
        {
            if (obj == null) return;
            obj.PropertyChanged -= OnConfigurationPropertyChanged;
            foreach (var prop in obj.GetType().GetProperties().Where(p => typeof(INotifyPropertyChanged).IsAssignableFrom(p.PropertyType)))
            {
                var childObj = prop.GetValue(obj) as INotifyPropertyChanged;
                if (childObj != null)
                {
                    RecursivelyRemovePropertyChangedHandlers(childObj);
                }
            }
        }

        private async Task LoadSettingsAsync()
        {
            await Task.Run(() =>
            {
                CurrentConfiguration = _configurationService.Load();
                _isDirty = false; 
                ((RelayCommand)SaveSettingsCommand).RaiseCanExecuteChanged(); 
            });
            LoggingService.LogInfo("Settings loaded into ViewModel.");
        }

        private async Task SaveSettingsAsync()
        {
            if (CurrentConfiguration != null)
            {
                await Task.Run(() => _configurationService.Save(CurrentConfiguration));
                _isDirty = false;
                ((RelayCommand)SaveSettingsCommand).RaiseCanExecuteChanged();
                LoggingService.LogInfo("Settings saved from ViewModel.");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 