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

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                LoadFolder(dialog.FolderName);
            }
        }

        private async void LoadFolder(string folderPath)
        {
            try
            {
                _currentFolderPath = folderPath;
                FolderPathTextBlock.Text = folderPath;

                // Cargar vista Explorador (Asíncrono)
                await ExplorerViewControl.LoadFolder(_currentFolderPath);

                // Cargar vista preliminar
                PreviewViewControl.LoadFolder(_currentFolderPath);

                // Cargar vista Duplicados (Módulo 3)
                DuplicatesViewControl.CurrentFolderPath = _currentFolderPath;
                DuplicatesViewControl.ClearResults();

                // Cargar carpeta en limpieza inteligente
                CleaningViewControl.CurrentFolderPath = _currentFolderPath;
                CleaningViewControl.SetFolder(_currentFolderPath);

                // Cargar carpeta en organización estructural
                OrganizationViewControl.SetFolder(_currentFolderPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar la carpeta: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

            // Actualizar vista explorador
            if (MainTabControl.SelectedIndex == 0 && !string.IsNullOrEmpty(_currentFolderPath))
            {
                ExplorerViewControl.RefreshFolder();
            }


        }
    }
}
