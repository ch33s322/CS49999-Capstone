using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using FontAwesome.Sharp;

namespace FileSystemItemModel.Model
{
    public class FileSystemItem
    {
        public string Name { get; set; }
        public IconChar Item { get; set; }
        public string IconColor { get; set; }
        public string IconFont { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; }
        public ObservableCollection<FileSystemItem> Children { get; set; }
        public bool HasDummyChild => IsDirectory;

        public FileSystemItem()
        {
            Children = new ObservableCollection<FileSystemItem>();
            Item = IconChar.Folder;
            IconColor = "DarkGoldenrod";
            IconFont = "Solid";
        }

        public void LoadChildren()
        {
            if (!IsDirectory || Children.Count > 0 && Children[0] != null) return;

            Children.Clear();

            try
            {
                foreach (var dir in Directory.GetDirectories(FullPath))
                {
                    Children.Add(new FileSystemItem
                    {
                        Name = Path.GetFileName(dir),
                        Item = IconChar.Folder,
                        IconColor = "DarkGoldenrod",
                        IconFont = "Solid",
                        FullPath = dir,
                        IsDirectory = true,
                        Children = new ObservableCollection<FileSystemItem> { null }
                    });
                }

                foreach (var file in Directory.GetFiles(FullPath))
                {
                    Children.Add(new FileSystemItem
                    {
                        Name = Path.GetFileName(file),
                        Item = IconChar.File,
                        IconColor = "Black",
                        IconFont = "Regular",
                        FullPath = file,
                        IsDirectory = false
                    });
                }
            }
            catch
            {
                // TODO: error handling
            }
        }
    }
}