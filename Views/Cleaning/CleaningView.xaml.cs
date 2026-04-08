using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
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
        }

        public void SetFolder(string folderPath)
        {
            CurrentFolderPath = folderPath;
            _trashCandidates.Clear();
            CleanSelectedButton.IsEnabled = false;
        }

        private async void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(CurrentFolderPath) || !Directory.Exists(CurrentFolderPath))
            {
                MessageBox.Show("Selecciona una carpeta válida antes de analizar.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsAnalyzing = true;
            try
            {
                var candidates = await Task.Run(() => FindTrashCandidates(CurrentFolderPath));

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
                IsAnalyzing = false;
            }
        }

        private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff" };

            private IEnumerable<TrashCandidate> FindTrashCandidates(string folderPath)
        {
            var imageFiles = Directory.GetFiles(folderPath)
                .Where(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList();

            foreach (var filePath in imageFiles)
            {
                ImageItem item;
                try
                {
                    item = ImageItem.FromFile(filePath);
                }
                catch
                {
                    continue;
                }

                var candidate = AnalyzeItem(item);
                if (candidate != null)
                {
                    yield return candidate;
                }
            }
        }

        private TrashCandidate? AnalyzeItem(ImageItem item)
        {
            string fileName = item.FileName.ToLowerInvariant();
            if (fileName.Contains("screenshot") || fileName.Contains("captura"))
            {
                return new TrashCandidate(item)
                {
                    Reason = "Captura",
                    Confidence = 0.95
                };
            }

            if (IsStandardMobileAspectRatio(item.Width, item.Height))
            {
                return new TrashCandidate(item)
                {
                    Reason = "Captura",
                    Confidence = 0.85
                };
            }

            var histogram = AnalyzeBrightness(item.FilePath);
            if (histogram.IsDark)
            {
                return new TrashCandidate(item)
                {
                    Reason = "Foto muy oscura",
                    Confidence = histogram.Confidence
                };
            }

            if (histogram.IsBright)
            {
                return new TrashCandidate(item)
                {
                    Reason = "Foto muy clara",
                    Confidence = histogram.Confidence
                };
            }

            return null;
        }

        private (bool IsDark, bool IsBright, double Confidence) AnalyzeBrightness(string filePath)
        {
            using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(filePath);
            int width = image.Width;
            int height = image.Height;
            int sampleX = Math.Max(1, width / 120);
            int sampleY = Math.Max(1, height / 120);

            int blackCount = 0;
            int whiteCount = 0;
            int totalCount = 0;
            double brightnessSum = 0;

            for (int y = 0; y < height; y += sampleY)
            {
                for (int x = 0; x < width; x += sampleX)
                {
                    var pixel = image[x, y];
                    double luminance = (0.2126 * pixel.R + 0.7152 * pixel.G + 0.0722 * pixel.B) / 255.0;
                    brightnessSum += luminance;
                    totalCount++;

                    if (luminance <= 0.05)
                    {
                        blackCount++;
                    }
                    else if (luminance >= 0.95)
                    {
                        whiteCount++;
                    }
                }
            }

            if (totalCount == 0)
            {
                return (false, false, 0.0);
            }

            double blackRatio = blackCount / (double)totalCount;
            double whiteRatio = whiteCount / (double)totalCount;
            double average = brightnessSum / totalCount;

            if (blackRatio > 0.92)
            {
                return (true, false, Math.Min(1.0, 0.7 + blackRatio * 0.3));
            }

            if (whiteRatio > 0.92)
            {
                return (false, true, Math.Min(1.0, 0.7 + whiteRatio * 0.3));
            }

            return (false, false, average);
        }

        private static bool IsStandardMobileAspectRatio(int width, int height)
        {
            if (width == 0 || height == 0) return false;

            var standardRatios = new (int width, int height)[]
            {
                (1920, 1080),
                (1080, 1920),
                (2340, 1080),
                (1080, 2340),
                (2400, 1080),
                (1080, 2400),
                (1170, 2532),
                (1080, 2400),
                (1125, 2436),
                (720, 1280),
                (1080, 2280),
                (1440, 3040)
            };

            foreach (var ratio in standardRatios)
            {
                if ((long)width * ratio.height == (long)height * ratio.width)
                {
                    return true;
                }
            }

            return false;
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

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}