using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;

namespace OrganizadorDeFotos.DesktopApp.Modules
{
    public class VirtualFolder : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private VirtualFolder? _parent;

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

        public VirtualFolder? Parent
        {
            get => _parent;
            private set => _parent = value;
        }

        public ObservableCollection<VirtualFolder> SubFolders { get; } = new();
        public ObservableCollection<ImageItem> Files { get; } = new();

        public int TotalCount => Files.Count + SubFolders.Sum(f => f.TotalCount);

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            
            // Si cambia el nombre o algo interno, no burbujeamos. 
            // Pero si el TotalCount cambia, avisamos al padre.
            if (propertyName == nameof(TotalCount) && Parent != null)
            {
                Parent.OnPropertyChanged(nameof(TotalCount));
            }
        }

        public VirtualFolder()
        {
            SetupCollections();
        }

        public VirtualFolder(string name)
        {
            Name = name;
            SetupCollections();
        }

        private void SetupCollections()
        {
            Files.CollectionChanged += (s, e) => OnPropertyChanged(nameof(TotalCount));
            SubFolders.CollectionChanged += (s, e) => 
            {
                if (e.NewItems != null)
                {
                    foreach (VirtualFolder sub in e.NewItems) sub.Parent = this;
                }
                if (e.OldItems != null)
                {
                    foreach (VirtualFolder sub in e.OldItems) sub.Parent = null;
                }
                OnPropertyChanged(nameof(TotalCount));
            };
        }
    }
}
