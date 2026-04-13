using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

using OrganizadorDeFotos.DesktopApp.Modules;

namespace OrganizadorDeFotos.DesktopApp.Views.Cleaning
{
    public partial class CleaningView : UserControl, INotifyPropertyChanged
    {
        private readonly ObservableCollection<TrashCandidate> _trashCandidates = new();
        private bool _isAnalyzing;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string CurrentFolderPath { get; set; } = string.Empty;

        public ObservableCollection<TrashCandidate> TrashCandidates => _trashCandidates;

        public bool IsAnalyzing
        {
            get => _isAnalyzing;
            set
            {
                if (_isAnalyzing == value) return;
                _isAnalyzing = value;
                OnPropertyChanged(nameof(IsAnalyzing));
            }
        }

        public CleaningView()
        {
            InitializeComponent();
            DataContext = this;
            CandidatesItemsControl.ItemsSource = _trashCandidates;
            UpdateUnsupportedFilesInfo();
        }

        public void SetFolder(string folderPath)
        {
            CurrentFolderPath = folderPath;
            _trashCandidates.Clear();
            CleanSelectedButton.IsEnabled = false;
            UpdateUnsupportedFilesInfo();
        }

        private void UpdateUnsupportedFilesInfo()
        {
            if (string.IsNullOrEmpty(CurrentFolderPath) || !Directory.Exists(CurrentFolderPath))
            {
                MoveUnsupportedButton.Content = "Mover No Soportados (0)";
                MoveUnsupportedButton.IsEnabled = false;
                return;
            }

            int count = UnsupportedFileFinder.CountUnsupportedFiles(CurrentFolderPath);
            MoveUnsupportedButton.Content = $"Mover No Soportados ({count})";
            MoveUnsupportedButton.IsEnabled = count > 0;
        }

        private async void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(CurrentFolderPath) || !Directory.Exists(CurrentFolderPath))
            {
                MessageBox.Show("Selecciona una carpeta válida antes de analizar.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LoadingOverlay.Visibility = Visibility.Visible;
            AnalyzeButton.IsEnabled = false;
            try
            {
                var candidates = await Task.Run(() => TrashAnalyzer.FindTrashCandidatesAsync(CurrentFolderPath));

                _trashCandidates.Clear();
                foreach (var candidate in candidates)
                {
                    _trashCandidates.Add(candidate);
                }

                CleanSelectedButton.IsEnabled = _trashCandidates.Count > 0;

                if (_trashCandidates.Count == 0)
                {
                    MessageBox.Show("No se encontraron fotos de limpieza inteligente en esta carpeta.", "Resultado", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al analizar las fotos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                AnalyzeButton.IsEnabled = true;
            }
        }



        private async void CleanSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedCandidates = _trashCandidates.Where(c => c.IsSelected).ToList();
            if (selectedCandidates.Count == 0)
            {
                MessageBox.Show("Selecciona al menos una foto para limpiar.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await Task.Run(() => FileManager.MoveFilesToAuxiliar(
                    selectedCandidates.Select(c => c.FilePath).ToList(),
                    CurrentFolderPath,
                    "Limpieza"));

                foreach (var candidate in selectedCandidates)
                {
                    _trashCandidates.Remove(candidate);
                }

                CleanSelectedButton.IsEnabled = _trashCandidates.Count > 0;
                MessageBox.Show($"✓ {selectedCandidates.Count} archivos movidos a _AUXILIAR/Limpieza.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al limpiar las fotos seleccionadas: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void MoveUnsupportedButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(CurrentFolderPath) || !Directory.Exists(CurrentFolderPath))
            {
                MessageBox.Show("Selecciona una carpeta válida primero.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LoadingOverlay.Visibility = Visibility.Visible;
            MoveUnsupportedButton.IsEnabled = false;
            try
            {
                var unsupportedFiles = await Task.Run(() => UnsupportedFileFinder.FindUnsupportedFiles(CurrentFolderPath));

                if (unsupportedFiles.Count == 0)
                {
                    MessageBox.Show("No se encontraron archivos no soportados en esta carpeta.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                int count = unsupportedFiles.Count;
                await Task.Run(() => FileManager.MoveFilesToAuxiliar(unsupportedFiles, CurrentFolderPath, "No_Soportados"));

                UpdateUnsupportedFilesInfo();
                MessageBox.Show($"✓ Se movieron {count} archivos no soportados a la carpeta auxiliar (_AUXILIAR/No_Soportados).", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al mover archivos no soportados: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                UpdateUnsupportedFilesInfo();
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}