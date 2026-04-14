using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using OrganizadorDeFotos.DesktopApp.Modules;

namespace OrganizadorDeFotos.DesktopApp.Views.Organization
{
    public partial class OrganizationView : UserControl, INotifyPropertyChanged
    {
        private VirtualFolder? _rootFolder;
        private string _currentBaseFolderPath = string.Empty;
        private bool _isFolderLoaded;

        public event PropertyChangedEventHandler? PropertyChanged;

        public VirtualFolder? RootFolder
        {
            get => _rootFolder;
            set
            {
                _rootFolder = value;
                OnPropertyChanged(nameof(RootFolder));
            }
        }

        public bool IsFolderLoaded
        {
            get => _isFolderLoaded;
            set
            {
                _isFolderLoaded = value;
                OnPropertyChanged(nameof(IsFolderLoaded));
            }
        }

        public OrganizationView()
        {
            InitializeComponent();
            DataContext = this;
            FilesListBox.SelectionChanged += (s, e) => UpdateSelectionInfo();
        }

        public async void SetFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) return;

            _currentBaseFolderPath = folderPath;
            LoadingOverlay.Visibility = Visibility.Visible;
            IsFolderLoaded = false;

            try
            {
                RootFolder = await Task.Run(() => LoadStructure(folderPath));
                FolderTreeView.ItemsSource = new ObservableCollection<VirtualFolder> { RootFolder };
                IsFolderLoaded = true;
                CurrentPathLabel.Text = $"Organizando: {Path.GetFileName(folderPath)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar la estructura: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private VirtualFolder LoadStructure(string path)
        {
            var folder = new VirtualFolder(Path.GetFileName(path));
            
            // Cargar archivos de esta carpeta (raíz o subcarpeta real)
            var files = Directory.GetFiles(path)
                .Where(f => IsMediaFile(f))
                .ToList();

            foreach (var file in files)
            {
                // Usar Dispatcher para cargar miniaturas si es necesario, 
                // pero ImageItem las carga on-demand vía propiedad ImageSource
                folder.Files.Add(ImageItem.FromFile(file));
            }

            // Cargar subcarpetas reales recursivamente
            var subDirs = Directory.GetDirectories(path)
                .Where(d => !Path.GetFileName(d).StartsWith("_")) // Ignorar auxiliares
                .OrderBy(d => d)
                .ToList();

            foreach (var dir in subDirs)
            {
                folder.SubFolders.Add(LoadStructure(dir));
            }

            return folder;
        }

        private bool IsMediaFile(string path)
        {
            string ext = Path.GetExtension(path).ToLower();
            string[] allowed = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".mp4", ".avi", ".mov", ".mkv" };
            return allowed.Contains(ext);
        }

        private void FolderTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var selectedFolder = FolderTreeView.SelectedItem as VirtualFolder;
            if (selectedFolder != null)
            {
                FilesListBox.ItemsSource = selectedFolder.Files;
                CurrentPathLabel.Text = $"Carpeta virtual: {selectedFolder.Name} ({selectedFolder.Files.Count} fotos)";
                AssignToFolderButton.IsEnabled = true;
            }
            else
            {
                FilesListBox.ItemsSource = null;
                CurrentPathLabel.Text = "Selecciona una carpeta para ver fotos";
                AssignToFolderButton.IsEnabled = false;
            }
        }

        private void UpdateSelectionInfo()
        {
            int count = FilesListBox.SelectedItems.Count;
            SelectionInfoLabel.Text = $"{count} fotos seleccionadas";
        }

        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            var selectedFolder = FolderTreeView.SelectedItem as VirtualFolder ?? RootFolder;
            if (selectedFolder == null) return;

            string newName = "Nueva Carpeta";
            int counter = 1;
            while (selectedFolder.SubFolders.Any(f => f.Name == newName))
            {
                newName = $"Nueva Carpeta ({counter++})";
            }

            var newFolder = new VirtualFolder(newName);
            selectedFolder.SubFolders.Add(newFolder);
            
            // Auto-expandir si es posible (requiere manejo del TreeView.ItemContainerGenerator o Bindings)
        }

        private void RenameFolder_Click(object sender, RoutedEventArgs e)
        {
            var selectedFolder = FolderTreeView.SelectedItem as VirtualFolder;
            if (selectedFolder == null || selectedFolder == RootFolder) return;

            // Diálogo simple para renombrar (puedes mejorar esto luego)
            string oldName = selectedFolder.Name;
            var window = new Window
            {
                Title = "Renombrar Carpeta",
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };

            var stack = new StackPanel { Margin = new Thickness(10) };
            var textBox = new TextBox { Text = oldName, Margin = new Thickness(0, 0, 0, 10) };
            var button = new Button { Content = "Renombrar", IsDefault = true };
            
            button.Click += (s, ev) => { window.DialogResult = true; window.Close(); };
            stack.Children.Add(new TextBlock { Text = "Nuevo nombre:", Margin = new Thickness(0, 0, 0, 5) });
            stack.Children.Add(textBox);
            stack.Children.Add(button);
            window.Content = stack;

            if (window.ShowDialog() == true && !string.IsNullOrWhiteSpace(textBox.Text))
            {
                selectedFolder.Name = textBox.Text;
            }
        }

        private void DeleteFolder_Click(object sender, RoutedEventArgs e)
        {
            var selectedFolder = FolderTreeView.SelectedItem as VirtualFolder;
            if (selectedFolder == null || selectedFolder == RootFolder) return;

            if (selectedFolder.Files.Any() || selectedFolder.SubFolders.Any())
            {
                var result = MessageBox.Show($"La carpeta '{selectedFolder.Name}' contiene archivos o subcarpetas virtuales. ¿Seguro que quieres eliminarla? (Los archivos volverán a la carpeta padre)", 
                    "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;
            }

            // Encontrar el padre y remover
            var parent = FindParent(RootFolder!, selectedFolder);
            if (parent != null)
            {
                // Mover archivos al padre
                foreach (var file in selectedFolder.Files.ToList())
                {
                    parent.Files.Add(file);
                }
                parent.SubFolders.Remove(selectedFolder);
            }
        }

        private VirtualFolder? FindParent(VirtualFolder root, VirtualFolder target)
        {
            if (root.SubFolders.Contains(target)) return root;

            foreach (var sub in root.SubFolders)
            {
                var found = FindParent(sub, target);
                if (found != null) return found;
            }
            return null;
        }

        private void AssignToFolder_Click(object sender, RoutedEventArgs e)
        {
            var targetFolder = FolderTreeView.SelectedItem as VirtualFolder;
            if (targetFolder == null || FilesListBox.SelectedItems.Count == 0) return;

            var selectedPhotos = FilesListBox.SelectedItems.Cast<ImageItem>().ToList();
            
            // Encontrar de donde vienen (del View actual, que es FilesListBox.ItemsSource)
            if (FilesListBox.ItemsSource is ObservableCollection<ImageItem> sourceList)
            {
                if (sourceList == targetFolder.Files) return; // Ya están ahí

                foreach (var photo in selectedPhotos)
                {
                    sourceList.Remove(photo);
                    targetFolder.Files.Add(photo);
                }
            }
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
