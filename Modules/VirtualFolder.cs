using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OrganizadorDeFotos.DesktopApp.Modules
{
    public class VirtualFolder : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<VirtualFolder> SubFolders { get; } = new();
        public ObservableCollection<ImageItem> Files { get; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public VirtualFolder()
        {
        }

        public VirtualFolder(string name)
        {
            Name = name;
        }
    }
}
