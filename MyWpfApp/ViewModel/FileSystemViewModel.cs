using FileSystemItemModel.Model;
using System;
using System.Windows;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using MyWpfApp.Model;

namespace FileSystem.ViewModel
{
    public class FileSyetemViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public ObservableCollection<FileSystemItem> RootItems {
            get;
            set;
            
        }

        private FileSystemWatcher _watcher;

        public FileSyetemViewModel()
        {
            
            RootItems = new ObservableCollection<FileSystemItem>();
            //ensure jobwell directory exists
            if (!Directory.Exists(AppSettings.JobWell))
            {
                AppSettings.EnsureDirectoriesExist();
            }

            var rootItem = new FileSystemItem
            {
                Name = Path.GetFileName(AppSettings.JobWell),
                FullPath = AppSettings.JobWell,
                IsDirectory = true,
                Children = new ObservableCollection<FileSystemItem> { null } // dummy to show expand arrow
            };
            RootItems.Add(rootItem);

            //start watching for changes
            InitializeWatcher(AppSettings.JobWell);

        }

        private void InitializeWatcher(string path)
        {
            _watcher = new FileSystemWatcher(path)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            _watcher.Created += OnFileSystemChanged;
            _watcher.Deleted += OnFileSystemChanged;
            _watcher.Renamed += OnFileSystemChanged;
            _watcher.Changed += OnFileSystemChanged;
        }

        private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                RefreshRoot();
            });
        }

        private void RefreshRoot()
        {
            RootItems.Clear();
            var rootItem = new FileSystemItem
            {
                Name = Path.GetFileName(AppSettings.JobWell),
                FullPath = AppSettings.JobWell,
                IsDirectory = true,
                Children = new ObservableCollection<FileSystemItem> { null }
            };
            RootItems.Add(rootItem);
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}