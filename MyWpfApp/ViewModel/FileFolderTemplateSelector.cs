using FileSystemItemModel.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MyWpfApp.ViewModel
{
    public class FileFolderTemplateSelector : DataTemplateSelector
    {
        public DataTemplate FolderTemplate { get; set; }
        public DataTemplate FileTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var fsItem = item as FileSystemItem;
            if (fsItem == null) return base.SelectTemplate(item, container);
            return fsItem.IsDirectory ? FolderTemplate : FileTemplate;
        }
    }
}