using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using OrganizadorDeFotos.DesktopApp.Modules;

namespace OrganizadorDeFotos.DesktopApp.Views.Duplicates
{
    public partial class DuplicatesView : UserControl, INotifyPropertyChanged
    {
        private ObservableCollection<SimilarityGroup> _similarityGroups = new();
        private SimilarityGroup? _selectedGroup;
        private bool _isProcessing;
        private bool _hasNoDuplicates;
        private bool _hasDuplicates;
        private double _timeThreshold = 10.0;
        private double _similarityThreshold = 0.90;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string CurrentFolderPath { get; set; } = string.Empty;

        public void ClearResults()
        {
            _similarityGroups.Clear();
            HasNoDuplicates = false;
            HasDuplicates = false;
        }

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

        public bool HasNoDuplicates
        {
            get => _hasNoDuplicates;
            set
            {
                if (_hasNoDuplicates == value) return;
                _hasNoDuplicates = value;
                OnPropertyChanged(nameof(HasNoDuplicates));
            }
        }

        public bool HasDuplicates
        {
            get => _hasDuplicates;
            set
            {
                if (_hasDuplicates == value) return;
                _hasDuplicates = value;
                OnPropertyChanged(nameof(HasDuplicates));
            }
        }

        public DuplicatesView()
        {
            InitializeComponent();
            DataContext = this;
            SimilarityGroupsListBox.ItemsSource = _similarityGroups;
            HasNoDuplicates = false;
            HasDuplicates = false;
            _timeThreshold = 10.0;
            _similarityThreshold = 0.90;
        }

        private async void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(CurrentFolderPath) || !System.IO.Directory.Exists(CurrentFolderPath))
            {
                MessageBox.Show("Selecciona una carpeta válida antes de analizar.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AnalyzeButton.IsEnabled = false;
            await LoadDuplicates(CurrentFolderPath);
            AnalyzeButton.IsEnabled = true;
        }

        public async Task LoadDuplicates(string folderPath)
        {
            IsProcessing = true;
            HasNoDuplicates = false;
            HasDuplicates = false;

            try
            {
                // Forzar que la notificación de carga se muestre al menos 2 segundos
                var loadTask = DuplicateComparer.FindSimilarGroupsAsync(folderPath, _timeThreshold, _similarityThreshold);
                var delayTask = Task.Delay(2000);

                await Task.WhenAll(loadTask, delayTask);
                var groups = await loadTask;

                _similarityGroups.Clear();
                foreach (var group in groups)
                {
                    _similarityGroups.Add(group);
                }

                // Mostrar el mensaje de "sin duplicados" si no hay grupos
                if (_similarityGroups.Count == 0)
                {
                    HasNoDuplicates = true;
                    HasDuplicates = false;
                }
                else
                {
                    HasNoDuplicates = false;
                    HasDuplicates = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar duplicados: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                HasNoDuplicates = true;
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private void SimilarityGroupsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedGroup = SimilarityGroupsListBox.SelectedItem as SimilarityGroup;
            if (_selectedGroup != null)
            {
                GroupPreviewItemsControl.ItemsSource = _selectedGroup.Images;
                ProcessGroupButton.IsEnabled = true;
            }
            else
            {
                GroupPreviewItemsControl.ItemsSource = null;
                ProcessGroupButton.IsEnabled = false;
            }
        }

        private async void ProcessGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedGroup == null) return;

            var toMove = _selectedGroup.Images.Where(i => !i.IsSelected).ToList();
            if (toMove.Count == 0)
            {
                MessageBox.Show("No hay imágenes seleccionadas para mover.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                await Task.Run(() => FileManager.MoveFilesToAuxiliar(
                    toMove.Select(i => i.FilePath).ToList(),
                    CurrentFolderPath,
                    "Similares"));

                // Remover del grupo
                foreach (var item in toMove)
                {
                    _selectedGroup.Images.Remove(item);
                }

                // Si el grupo queda con 1 o menos, remover el grupo
                if (_selectedGroup.Images.Count <= 1)
                {
                    _similarityGroups.Remove(_selectedGroup);
                    _selectedGroup = null;
                    GroupPreviewItemsControl.ItemsSource = null;
                    ProcessGroupButton.IsEnabled = false;
                }
                else
                {
                    // Recalcular calidad
                    _selectedGroup.DetermineQuality();
                    // Refrescar UI
                    GroupPreviewItemsControl.ItemsSource = null;
                    GroupPreviewItemsControl.ItemsSource = _selectedGroup.Images;
                }

                MessageBox.Show($"✓ {toMove.Count} imágenes movidas a _AUXILIAR/Similares.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al procesar grupo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ProcessAll_Click(object sender, RoutedEventArgs e)
        {
            if (_similarityGroups.Count == 0) return;

            int totalToKeep = 0;
            int totalToDiscard = 0;
            var pathsToDiscard = new List<string>();

            foreach (var group in _similarityGroups)
            {
                totalToKeep += group.Images.Count(i => i.IsSelected);
                var toDiscard = group.Images.Where(i => !i.IsSelected).ToList();
                totalToDiscard += toDiscard.Count;
                pathsToDiscard.AddRange(toDiscard.Select(i => i.FilePath));
            }

            if (totalToDiscard == 0)
            {
                MessageBox.Show("No hay imágenes marcadas para descartar en ningún grupo.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Se procesarán los {_similarityGroups.Count} grupos analizados:\n\n" +
                $"• Se conservarán: {totalToKeep} imágenes\n" +
                $"• Se descartarán: {totalToDiscard} imágenes\n\n" +
                $"Las imágenes descartadas se moverán físicamente a la carpeta _AUXILIAR/Similares.\n" +
                $"¿Deseas continuar?",
                "Confirmar Procesamiento Global",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            IsProcessing = true;
            try
            {
                await Task.Run(() => FileManager.MoveFilesToAuxiliar(
                    pathsToDiscard,
                    CurrentFolderPath,
                    "Similares"));

                _similarityGroups.Clear();
                _selectedGroup = null;
                GroupPreviewItemsControl.ItemsSource = null;
                ProcessGroupButton.IsEnabled = false;
                HasDuplicates = false;
                HasNoDuplicates = true;

                MessageBox.Show($"✓ Procesamiento completado. {totalToDiscard} imágenes movidas a la carpeta auxiliar.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al procesar todos los grupos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SettingsDialog(_timeThreshold, _similarityThreshold)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
            {
                _timeThreshold = dialog.TimeThreshold;
                _similarityThreshold = dialog.SimilarityThreshold;

                if (!string.IsNullOrEmpty(CurrentFolderPath))
                {
                    AnalyzeButton.IsEnabled = false;
                    await LoadDuplicates(CurrentFolderPath);
                    AnalyzeButton.IsEnabled = true;
                }
            }
        }

        private void PreviewImage_MouseEnter(object sender, MouseEventArgs e)
        {
            PreviewImage_MouseMove(sender, e);
        }

        private void PreviewImage_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is not Image image) return;
            if (image.ActualWidth <= 0 || image.ActualHeight <= 0) return;

            var position = e.GetPosition(image);
            var relative = new Point(position.X / image.ActualWidth, position.Y / image.ActualHeight);
            UpdateAllZoomLenses(relative, true);
        }

        private void PreviewImage_MouseLeave(object sender, MouseEventArgs e)
        {
            UpdateAllZoomLenses(null, false);
        }

        private void UpdateAllZoomLenses(Point? relativePoint, bool visible)
        {
            if (_selectedGroup == null) return;

            foreach (var item in _selectedGroup.Images)
            {
                var container = GroupPreviewItemsControl.ItemContainerGenerator.ContainerFromItem(item) as DependencyObject;
                if (container == null) continue;

                var lens = FindDescendantByName<Ellipse>(container, "ZoomLens");
                var image = FindDescendantByName<Image>(container, "PreviewImage");
                if (lens == null || image == null) continue;

                lens.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                if (!visible || relativePoint == null) continue;

                if (lens.Fill is VisualBrush brush)
                {
                    const double zoomFactor = 0.1;
                    var x = Math.Max(0, Math.Min(1 - zoomFactor, relativePoint.Value.X - (zoomFactor / 2)));
                    var y = Math.Max(0, Math.Min(1 - zoomFactor, relativePoint.Value.Y - (zoomFactor / 2)));
                    brush.Viewbox = new Rect(x, y, zoomFactor, zoomFactor);
                }

                var centerX = relativePoint.Value.X * image.ActualWidth;
                var centerY = relativePoint.Value.Y * image.ActualHeight;
                Canvas.SetLeft(lens, centerX - (lens.Width / 2));
                Canvas.SetTop(lens, centerY - (lens.Height / 2));
            }
        }

        private static T? FindDescendantByName<T>(DependencyObject parent, string name) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed && child is FrameworkElement fe && fe.Name == name)
                {
                    return typed;
                }

                var result = FindDescendantByName<T>(child, name);
                if (result != null) return result;
            }

            return null;
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}