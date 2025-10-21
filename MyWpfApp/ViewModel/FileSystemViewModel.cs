using FileSystemItemModel.Model;
using System;
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
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}