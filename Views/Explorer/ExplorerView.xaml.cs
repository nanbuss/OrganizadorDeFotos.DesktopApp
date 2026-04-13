using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using OrganizadorDeFotos.DesktopApp.Loader;
using OrganizadorDeFotos.DesktopApp.Modules;

namespace OrganizadorDeFotos.DesktopApp.Views.Explorer
{
    public partial class ExplorerView : UserControl, INotifyPropertyChanged
    {
        private string? _currentFolderPath;
        private ObservableCollection<string> _fileNames = new();
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

        public ExplorerView()
        {
            InitializeComponent();
            DataContext = this;
            FileListBox.ItemsSource = _fileNames;
            NoFileMessage.Visibility = Visibility.Visible;
            ImagePreview.Visibility = Visibility.Collapsed;
            VideoPreview.Visibility = Visibility.Collapsed;
            UpdateDiscardButton();
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void LoadFolder(string folderPath)
        {
            try
            {
                _currentFolderPath = folderPath;
                
                var files = Directory.GetFiles(folderPath);
                var mediaFiles = files.Where(f =>
                    ImageExtensions.Contains(Path.GetExtension(f).ToLower()) ||
                    VideoExtensions.Contains(Path.GetExtension(f).ToLower())
                ).OrderBy(f => Path.GetFileName(f)).ToList();



                // Actualizar UI en el dispatcher thread
                Dispatcher.Invoke(() =>
                {
                    _fileNames.Clear();
                    foreach (var file in mediaFiles)
                    {
                        _fileNames.Add(Path.GetFileName(file));
                    }

                    ClearPreview();

                    if (_fileNames.Count == 0)
                    {
                        MessageBox.Show("No se encontraron archivos en esta carpeta.", "Carpeta vacía", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar la carpeta: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async void RefreshFolder()
        {
            if (!string.IsNullOrEmpty(_currentFolderPath))
            {
                IsProcessing = true;
                try
                {
                    await Task.Run(() => LoadFolder(_currentFolderPath));
                }
                finally
                {
                    IsProcessing = false;
                }
            }
        }

        private void FileListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateDiscardButton();

            if (FileListBox.SelectedItem is not string fileName)
            {
                ClearPreview();
                return;
            }

            try
            {
                string filePath = Path.Combine(_currentFolderPath ?? "", fileName);
                string extension = Path.GetExtension(filePath).ToLower();

                if (ImageExtensions.Contains(extension))
                {
                    ShowImage(filePath);
                }
                else if (VideoExtensions.Contains(extension))
                {
                    ShowVideo(filePath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar el archivo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ClearPreview();
            }
        }

        private void ShowImage(string filePath)
        {
            try
            {
                ImagePreview.Source = ImageLoader.LoadBitmapSource(filePath);
                NoFileMessage.Visibility = Visibility.Collapsed;
                ImagePreview.Visibility = Visibility.Visible;
                VideoPreview.Visibility = Visibility.Collapsed;
                VideoLoader.StopVideo(VideoPreview);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo cargar la imagen: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowVideo(string filePath)
        {
            try
            {
                VideoLoader.ShowVideo(VideoPreview, filePath);
                NoFileMessage.Visibility = Visibility.Collapsed;
                ImagePreview.Visibility = Visibility.Collapsed;
                VideoPreview.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo cargar el video: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearPreview()
        {
            NoFileMessage.Visibility = Visibility.Visible;
            ImagePreview.Visibility = Visibility.Collapsed;
            VideoPreview.Visibility = Visibility.Collapsed;
            ImagePreview.Source = null;
            VideoLoader.StopVideo(VideoPreview);
        }

        private void UpdateDiscardButton()
        {
            DiscardButton.IsEnabled = FileListBox.SelectedItems.Count > 0;
        }



        private void FileListBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                DiscardSelectedFiles();
            }
        }

        private void DiscardSelectedFiles_Click(object sender, RoutedEventArgs e)
        {
            DiscardSelectedFiles();
        }

        private async void DiscardSelectedFiles()
        {
            if (FileListBox.SelectedItems.Count == 0) return;

            var selectedFiles = FileListBox.SelectedItems.Cast<string>().ToList();

            try
            {
                ClearPreview();

                await Task.Run(() => FileManager.MoveFilesToAuxiliar(
                    selectedFiles.Select(f => Path.Combine(_currentFolderPath!, f)).ToList(),
                    _currentFolderPath!,
                    "Descartes_Manuales"));

                // Remove from ObservableCollection on UI thread
                Dispatcher.Invoke(() =>
                {
                    foreach (var file in selectedFiles)
                    {
                        _fileNames.Remove(file);
                    }

                    if (_fileNames.Count > 0)
                    {
                        FileListBox.SelectedIndex = Math.Min(FileListBox.SelectedIndex, _fileNames.Count - 1);
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
