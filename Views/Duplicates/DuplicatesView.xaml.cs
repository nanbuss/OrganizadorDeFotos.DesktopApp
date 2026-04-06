using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using OrganizadorDeFotos.DesktopApp.Modules;

namespace OrganizadorDeFotos.DesktopApp.Views.Duplicates
{
    public partial class DuplicatesView : UserControl
    {
        private ObservableCollection<SimilarityGroup> _similarityGroups = new();
        private SimilarityGroup? _selectedGroup;
        public string CurrentFolderPath { get; set; }

        public DuplicatesView()
        {
            InitializeComponent();
            SimilarityGroupsListBox.ItemsSource = _similarityGroups;
        }

        public async void LoadDuplicates(string folderPath)
        {
            try
            {
                var groups = await DuplicateComparer.FindSimilarGroupsAsync(folderPath);
                _similarityGroups.Clear();
                foreach (var group in groups)
                {
                    _similarityGroups.Add(group);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar duplicados: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
    }
}