using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using OrganizadorDeFotos.DesktopApp.Loader;
using OrganizadorDeFotos.DesktopApp.Modules;

namespace OrganizadorDeFotos.DesktopApp
{
    public partial class MainWindow : Window
    {
        private string? _currentFolderPath;
        private ObservableCollection<string> _fileNames = new();
        private List<string> _unsupportedFiles = new();
        private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff" };
        private static readonly string[] VideoExtensions = { ".mp4", ".avi", ".mkv", ".mov", ".flv", ".wmv", ".webm" };

        public MainWindow()
        {
            InitializeComponent();
            FileListBox.ItemsSource = _fileNames;
            NoFileMessage.Visibility = Visibility.Visible;
            ImagePreview.Visibility = Visibility.Collapsed;
            VideoPreview.Visibility = Visibility.Collapsed;
            UpdateDiscardButton();
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                LoadFolder(dialog.FolderName);
            }
        }

        private void LoadFolder(string folderPath)
        {
            try
            {
                _currentFolderPath = folderPath;
                FolderPathTextBlock.Text = folderPath;
                _fileNames.Clear();

                var files = Directory.GetFiles(folderPath);
                var mediaFiles = files.Where(f => 
                    ImageExtensions.Contains(Path.GetExtension(f).ToLower()) ||
                    VideoExtensions.Contains(Path.GetExtension(f).ToLower())
                ).OrderBy(f => Path.GetFileName(f));

                foreach (var file in mediaFiles)
                {
                    _fileNames.Add(Path.GetFileName(file));
                }

                // Detectar archivos no soportados (Módulo 1)
                _unsupportedFiles = UnsupportedFileFinder.FindUnsupportedFiles(folderPath);
                UpdateUnsupportedFilesUI();

                // Cargar grupos de duplicados (Módulo 3)
                DuplicatesViewControl.CurrentFolderPath = _currentFolderPath;
                DuplicatesViewControl.LoadDuplicates(_currentFolderPath);

                // Cargar carpeta en limpieza inteligente
                CleaningViewControl.CurrentFolderPath = _currentFolderPath;
                CleaningViewControl.SetFolder(_currentFolderPath);

                // Limpiar vista previa
                ClearPreview();

                if (_fileNames.Count == 0 && _unsupportedFiles.Count == 0)
                {
                    MessageBox.Show("No se encontraron archivos en esta carpeta.", "Carpeta vacía", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar la carpeta: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void UpdateUnsupportedFilesUI()
        {
            UnsupportedCountLabel.Text = $"{_unsupportedFiles.Count} archivos";
            MoveUnsupportedButton.IsEnabled = _unsupportedFiles.Count > 0;
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
                // Clear preview to release file locks
                ClearPreview();

                // Move files asynchronously
                await Task.Run(() => FileManager.MoveFilesToAuxiliar(
                    selectedFiles.Select(f => Path.Combine(_currentFolderPath!, f)).ToList(),
                    _currentFolderPath!,
                    "Descartes_Manuales"));

                // Remove from ObservableCollection
                foreach (var file in selectedFiles)
                {
                    _fileNames.Remove(file);
                }

                // Select the next available item (if any)
                if (_fileNames.Count > 0)
                {
                    FileListBox.SelectedIndex = Math.Min(FileListBox.SelectedIndex, _fileNames.Count - 1);
                }

                UpdateDiscardButton();
                ShowNotification($"✓ {selectedFiles.Count} archivos descartados exitosamente.", isSuccess: true);
            }
            catch (Exception ex)
            {
                ShowNotification($"✗ Error al descartar archivos: {ex.Message}", isSuccess: false);
            }
        }

        // ==================== MÓDULO 1: Archivos No Soportados ====================

        private void ShowUnsupportedFiles_Click(object sender, RoutedEventArgs e)
        {
            if (_unsupportedFiles.Count == 0)
            {
                MessageBox.Show("No hay archivos no soportados.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var fileList = string.Join("\n", _unsupportedFiles.Select(f => Path.GetFileName(f)));
            var window = new Window
            {
                Title = "Archivos No Soportados",
                Width = 500,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var textBox = new TextBox
            {
                Text = fileList,
                IsReadOnly = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10)
            };

            window.Content = textBox;
            window.ShowDialog();
        }

        private void MoveUnsupportedFiles_Click(object sender, RoutedEventArgs e)
        {
            if (_unsupportedFiles.Count == 0)
            {
                ShowNotification("No hay archivos no soportados para mover.", isSuccess: false);
                return;
            }

            try
            {
                FileManager.MoveFilesToAuxiliar(_unsupportedFiles, _currentFolderPath!, "NoSoportados");
                _unsupportedFiles.Clear();
                UpdateUnsupportedFilesUI();
                ShowNotification("✓ Archivos no soportados movidos exitosamente.", isSuccess: true);
            }
            catch (Exception ex)
            {
                ShowNotification($"✗ Error al mover archivos: {ex.Message}", isSuccess: false);
            }
        }

        private void RenameByDate_Click(object sender, RoutedEventArgs e)
        {
            if (_fileNames.Count == 0)
            {
                ShowNotification("No hay archivos para renombrar.", isSuccess: false);
                return;
            }

            try
            {
                var filePaths = _fileNames.Select(f => Path.Combine(_currentFolderPath ?? "", f)).ToList();
                var renamedFiles = FileRenamer.RenameFilesWithCollisionHandling(filePaths);

                _fileNames.Clear();
                foreach (var file in renamedFiles.OrderBy(f => f))
                {
                    _fileNames.Add(Path.GetFileName(file));
                }

                ClearPreview();
                ShowNotification($"✓ {renamedFiles.Count} archivos renombrados exitosamente.", isSuccess: true);
            }
            catch (Exception ex)
            {
                ShowNotification($"✗ Error al renombrar archivos: {ex.Message}", isSuccess: false);
            }
        }

        private async void ShowNotification(string message, bool isSuccess)
        {
            // Set colors based on success/error
            NotificationPanel.BorderBrush = new System.Windows.Media.SolidColorBrush(
                isSuccess ? System.Windows.Media.Color.FromRgb(0x27, 0xAE, 0x60) : System.Windows.Media.Color.FromRgb(0xE7, 0x4C, 0x3C));
            NotificationPanel.Background = new System.Windows.Media.SolidColorBrush(
                isSuccess ? System.Windows.Media.Color.FromRgb(0x27, 0xAE, 0x60) : System.Windows.Media.Color.FromRgb(0xE7, 0x4C, 0x3C));
            NotificationText.Text = message;

            // Show notification with fade-in animation
            var storyboard = new System.Windows.Media.Animation.Storyboard();
            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new System.TimeSpan(0, 0, 0, 0, 300),
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            System.Windows.Media.Animation.Storyboard.SetTarget(fadeIn, NotificationPanel);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(fadeIn, new System.Windows.PropertyPath(OpacityProperty));
            storyboard.Children.Add(fadeIn);
            storyboard.Begin();

            // Wait 5 seconds then fade out
            await Task.Delay(5000);

            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new System.TimeSpan(0, 0, 0, 0, 300),
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
            };
            var storyboardOut = new System.Windows.Media.Animation.Storyboard();
            System.Windows.Media.Animation.Storyboard.SetTarget(fadeOut, NotificationPanel);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(fadeOut, new System.Windows.PropertyPath(OpacityProperty));
            storyboardOut.Children.Add(fadeOut);
            storyboardOut.Begin();
        }

        // ==================== Manejo de Tabs ====================

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Ignorar cambios de selección que provengan de controles internos como ListBox.
            if (e.OriginalSource != MainTabControl) return;

            // Cargar duplicados cuando se selecciona el tab de Duplicados
            if (MainTabControl.SelectedIndex == 2 && !string.IsNullOrEmpty(_currentFolderPath))
            {
                DuplicatesViewControl.LoadDuplicates(_currentFolderPath);
            }
        }
    }
}
