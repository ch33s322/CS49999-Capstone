using FileSystemItemModel.Model;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;

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

            foreach (var drive in DriveInfo.GetDrives())
            {
                var item = new FileSystemItem
                {
                    Name = drive.Name,
                    FullPath = drive.Name,
                    IsDirectory = true,
                    Children = new ObservableCollection<FileSystemItem> { null } // dummy to show expand arrow
                };
                RootItems.Add(item);
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}