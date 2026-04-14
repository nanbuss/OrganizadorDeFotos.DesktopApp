using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using OrganizadorDeFotos.DesktopApp.Modules;
using OrganizadorDeFotos.DesktopApp.Loader;

namespace OrganizadorDeFotos.DesktopApp.Views.Preview
{
    public partial class PreviewView : UserControl, INotifyPropertyChanged
    {
        private string? _currentFolderPath;
        private ObservableCollection<ImageItem> _files = new();
        private bool _isProcessing;
        private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff" };
        private static readonly string[] VideoExtensions = { ".mp4", ".avi", ".mkv", ".mov", ".flv", ".wmv", ".webm" };

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (_isProcessing == value) return;
                _isProcessing = value;
                OnPropertyChanged(nameof(IsProcessing));
            }
        }

        public PreviewView()
        {
            InitializeComponent();
            DataContext = this;
            GalleryItemsControl.ItemsSource = _files;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void LoadFolder(string folderPath)
        {
            _currentFolderPath = folderPath;
            Dispatcher.Invoke(() =>
            {
                _files.Clear();
                UpdateDiscardButton();
            });
        }

        private async void LoadGallery_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFolderPath)) return;

            IsProcessing = true;
            try
            {
                var filesToLoad = new ObservableCollection<ImageItem>();

                await Task.Run(() =>
                {
                    var files = Directory.GetFiles(_currentFolderPath);
                    var mediaFiles = files.Where(f =>
                        ImageExtensions.Contains(Path.GetExtension(f).ToLower()) ||
                        VideoExtensions.Contains(Path.GetExtension(f).ToLower())
                    ).OrderBy(f => Path.GetFileName(f)).ToList();

                    foreach (var file in mediaFiles)
                    {
                        var item = ImageItem.FromFile(file, loadMetadata: false);
                        item.IsSelected = false;
                        item.PreloadImage(); // Preload here
                        filesToLoad.Add(item);
                    }
                });

                Dispatcher.Invoke(() =>
                {
                    _files.Clear();
                    foreach (var item in filesToLoad)
                    {
                        _files.Add(item);
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar galería: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private void GalleryCheckBox_Click(object sender, RoutedEventArgs e)
        {
            UpdateDiscardButton();
        }

        private void UpdateDiscardButton()
        {
            DiscardButton.IsEnabled = _files.Any(f => f.IsSelected);
        }

        private async void DiscardSelectedFiles_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = _files.Where(f => f.IsSelected).ToList();
            if (selectedItems.Count == 0) return;

            try
            {
                await Task.Run(() => FileManager.MoveFilesToAuxiliar(
                    selectedItems.Select(f => f.FilePath).ToList(),
                    _currentFolderPath!,
                    "Descartes_Manuales"));

                Dispatcher.Invoke(() =>
                {
                    foreach (var item in selectedItems)
                    {
                        _files.Remove(item);
                    }

                    UpdateDiscardButton();
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al descartar archivos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
