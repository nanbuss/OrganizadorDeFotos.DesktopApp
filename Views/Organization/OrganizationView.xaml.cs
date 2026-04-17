using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Globalization;
using System.Text.RegularExpressions;
using OrganizadorDeFotos.DesktopApp.Modules;

namespace OrganizadorDeFotos.DesktopApp.Views.Organization
{
    public partial class OrganizationView : UserControl, INotifyPropertyChanged
    {
        private VirtualFolder? _rootFolder;
        private string _currentBaseFolderPath = string.Empty;
        private bool _isFolderLoaded;
        private Point _dragStartPoint;

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
            try
            {
                var files = Directory.GetFiles(path)
                    .Where(f => IsMediaFile(f))
                    .ToList();

                foreach (var file in files)
                {
                    // ¡LAZY LOAD!: No cargamos metadatos profundos (ImageSharp) ni miniaturas aquí.
                    // Esto hace que la carga inicial de la estructura sea casi instantánea.
                    folder.Files.Add(ImageItem.FromFile(file, loadMetadata: false));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al acceder a archivos en {path}: {ex.Message}");
            }

            // Cargar subcarpetas reales recursivamente
            try
            {
                var subDirs = Directory.GetDirectories(path)
                    .Where(d => !Path.GetFileName(d).StartsWith("_")) // Ignorar auxiliares
                    .OrderBy(d => d)
                    .ToList();

                foreach (var dir in subDirs)
                {
                    folder.SubFolders.Add(LoadStructure(dir));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al acceder a subdirectorios en {path}: {ex.Message}");
            }

            return folder;
        }

        private bool IsMediaFile(string path)
        {
            string ext = Path.GetExtension(path).ToLower();
            string[] allowed = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".mp4", ".avi", ".mov", ".mkv" };
            return allowed.Contains(ext);
        }

        private async void FolderTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var selectedFolder = FolderTreeView.SelectedItem as VirtualFolder;
            if (selectedFolder != null)
            {
                FilesListBox.ItemsSource = selectedFolder.Files;
                CurrentPathLabel.Text = $"Carpeta virtual: {selectedFolder.Name} ({selectedFolder.Files.Count} fotos)";

                // --- Lazy Load de Metadatos ---
                // Cargamos miniaturas y datos profundos solo para la carpeta seleccionada.
                // Lo hacemos en segundo plano para no congelar la UI si hay muchas fotos.
                var filesToProcess = selectedFolder.Files.ToList();
                try
                {
                    await Task.Run(() =>
                    {
                        foreach (var file in filesToProcess)
                        {
                            // Si no tiene fecha de captura, es probable que no se haya cargado su metadata aún
                            if (!file.CaptureDate.HasValue)
                            {
                                file.UpdateMetadata();
                                // Forzamos notificación de cambio para que la UI se actualice con los nuevos datos (si los hay)
                                // Nota: ImageSource se cargará on-demand cuando el item se pinte en el wrap panel.
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error en el lazy loading de la carpeta: {ex.Message}");
                }
            }
            else
            {
                FilesListBox.ItemsSource = null;
                CurrentPathLabel.Text = "Selecciona una carpeta para ver fotos";
            }
        }

        private void UpdateSelectionInfo()
        {
            int count = FilesListBox.SelectedItems.Count;
            SelectionInfoLabel.Text = $"{count} fotos seleccionadas";
            AssignToFolderButton.IsEnabled = count > 0;
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
                Width = 350,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ResizeMode = ResizeMode.NoResize
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
            if (FilesListBox.SelectedItems.Count == 0 || RootFolder == null)
            {
                MessageBox.Show("Selecciona al menos una foto para mover.", "Atención", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sourceList = FilesListBox.ItemsSource as ObservableCollection<ImageItem>;
            if (sourceList == null) return;

            var menu = new ContextMenu();
            PopulateFolderMenu(menu, RootFolder, sourceList);

            if (menu.Items.Count == 0)
            {
                menu.Items.Add(new MenuItem { Header = "No hay otras carpetas", IsEnabled = false });
            }

            menu.PlacementTarget = AssignToFolderButton;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
            menu.IsOpen = true;
        }

        private void PopulateFolderMenu(ItemsControl parentMenu, VirtualFolder folder, ObservableCollection<ImageItem> sourceList)
        {
            // Creamos un item para la carpeta actual
            var item = new MenuItem
            {
                Header = $"📁 {folder.Name}",
                Tag = folder
            };

            // Si no es la carpeta donde ya están las fotos, permitimos moverlas aquí
            if (folder.Files != sourceList)
            {
                item.Click += (s, ev) => MoveSelectedPhotosTo(folder, sourceList);
            }
            else
            {
                item.IsEnabled = false;
                item.Header += " (Actual)";
            }

            // Añadimos las subcarpetas como submenús
            foreach (var subFolder in folder.SubFolders)
            {
                PopulateFolderMenu(item, subFolder, sourceList);
            }

            parentMenu.Items.Add(item);
        }

        private void MoveSelectedPhotosTo(VirtualFolder targetFolder, ObservableCollection<ImageItem> sourceList)
        {
            var selectedPhotos = FilesListBox.SelectedItems.Cast<ImageItem>().ToList();

            foreach (var photo in selectedPhotos)
            {
                sourceList.Remove(photo);
                targetFolder.Files.Add(photo);
            }
        }

        private async void AutoOrganize_Click(object sender, RoutedEventArgs e)
        {
            if (RootFolder == null || !RootFolder.Files.Any())
            {
                MessageBox.Show("No hay archivos 'Sin asignar' en la raíz para auto-organizar.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var window = new Window
            {
                Title = "Auto-Organizar",
                Width = 450,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ResizeMode = ResizeMode.NoResize
            };

            var stack = new StackPanel { Margin = new Thickness(15) };
            stack.Children.Add(new TextBlock { Text = "Esta acción organizará las fotos sin asignar de la carpeta raíz automáticamente utilizando metadatos.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 15) });

            stack.Children.Add(new TextBlock { Text = "Selecciona el formato de organización:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) });

            var rbNestedDay = new RadioButton { Content = "Año / Mes / Día - Varias", Margin = new Thickness(0, 0, 0, 5) };
            var rbNestedMonth = new RadioButton { Content = "Año / Mes - Varias", Margin = new Thickness(0, 0, 0, 5), IsChecked = true };
            var rbFlatMonth = new RadioButton { Content = "Año.Mes Varias (Un solo nivel)", Margin = new Thickness(0, 0, 0, 10) };

            stack.Children.Add(rbNestedDay);
            stack.Children.Add(rbNestedMonth);
            stack.Children.Add(rbFlatMonth);

            var txtPreview = new TextBlock { Text = "Estructura:\nAño > Mes (ej. 01.Enero - Varias)\n  > (Tus fotos)", Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 0, 15) };
            stack.Children.Add(txtPreview);

            rbNestedDay.Checked += (s, ev) => txtPreview.Text = "Estructura:\nAño > Mes (ej. 01.Enero) > Día (ej. 15 - Varias)\n  > (Tus fotos)";
            rbNestedMonth.Checked += (s, ev) => txtPreview.Text = "Estructura:\nAño > Mes (ej. 01.Enero - Varias)\n  > (Tus fotos)";
            rbFlatMonth.Checked += (s, ev) => txtPreview.Text = "Estructura:\nAño.Mes - Varias (ej. 2023.10 Varias)\n  > (Tus fotos)";

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnOk = new Button { Content = "Iniciar", Padding = new Thickness(15, 5, 15, 5), IsDefault = true, Margin = new Thickness(0, 0, 10, 0) };
            var btnCancel = new Button { Content = "Cancelar", Padding = new Thickness(15, 5, 15, 5), IsCancel = true };

            btnOk.Click += (s, ev) => { window.DialogResult = true; window.Close(); };

            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            stack.Children.Add(btnPanel);

            window.Content = stack;

            if (window.ShowDialog() == true)
            {
                int format = rbNestedDay.IsChecked == true ? 1 : (rbNestedMonth.IsChecked == true ? 2 : 3);
                await Task.Run(() => PerformAutoOrganization(format));
                ExpandAllNodes(FolderTreeView.Items);
            }
        }

        private async void PerformAutoOrganization(int format)
        {
            var cultureInfo = new CultureInfo("es-ES");

            // 1. Cargamos metadatos en segundo plano (FUERA del Dispatcher)
            // Esto evita que la aplicación se congele mientras procesa cientos de archivos
            var filesToOrganize = RootFolder!.Files.ToList();

            await Task.Run(() =>
            {
                Parallel.ForEach(filesToOrganize, file =>
                {
                    if (!file.CaptureDate.HasValue)
                    {
                        file.UpdateMetadata();
                    }
                });
            });

            // 2. Ejecutamos la organización lógica en el Dispatcher (ya que altera colecciones UI)
            Dispatcher.Invoke(() =>
            {
                foreach (var file in filesToOrganize)
                {
                    // En este punto, CaptureDate ya debería tener valor si fue posible extraerlo
                    var captureDate = file.CaptureDate;

                    if (!captureDate.HasValue)
                    {
                        var fileName = Path.GetFileNameWithoutExtension(file.FilePath);

                        // Si falla DateExtractor (porque la fecha del sistema es basura y el nombre no coincide exactamente con patrones conocidos),
                        // intentamos ver si el archivo YA fue renombrado con nuestro formato patrón: yyyyMMdd_HHmmss
                        var match = Regex.Match(fileName, @"^(\d{8})_(\d{6})$");
                        if (match.Success)
                        {
                            if (DateTime.TryParseExact(fileName, "yyyyMMdd_HHmmss", null, DateTimeStyles.None, out DateTime parsedDate))
                            {
                                if (parsedDate.Year > 2000)
                                    captureDate = parsedDate;
                            }
                        }
                    }

                    if (!captureDate.HasValue)
                    {
                        // Si después de todo no hay fecha válida, mover a una carpeta especial
                        var unknownFolder = GetOrCreateFolder(RootFolder, "Inclasificables");
                        RootFolder.Files.Remove(file);
                        unknownFolder.Files.Add(file);
                        continue;
                    }

                    var date = captureDate.Value;

                    VirtualFolder targetFolder = RootFolder;

                    if (format == 1) // aaaa/mm.MM/dd - varias
                    {
                        string yearName = date.Year.ToString();
                        string monthName = $"{date.Month:D2}.{cultureInfo.TextInfo.ToTitleCase(cultureInfo.DateTimeFormat.GetMonthName(date.Month))}";
                        string dayName = $"{date.Day:D2} - Varias";

                        var yearF = GetOrCreateFolder(RootFolder, yearName);
                        var monthF = GetOrCreateFolder(yearF, monthName);
                        targetFolder = GetOrCreateFolder(monthF, dayName);
                    }
                    else if (format == 2) // aaaa/mm.MM - varias
                    {
                        string yearName = date.Year.ToString();
                        string monthName = $"{date.Month:D2}.{cultureInfo.TextInfo.ToTitleCase(cultureInfo.DateTimeFormat.GetMonthName(date.Month))} - Varias";

                        var yearF = GetOrCreateFolder(RootFolder, yearName);
                        targetFolder = GetOrCreateFolder(yearF, monthName);
                    }
                    else if (format == 3) // aaaa.mm - varias
                    {
                        string folderName = $"{date.Year}.{date.Month:D2} Varias";
                        targetFolder = GetOrCreateFolder(RootFolder, folderName);
                    }

                    RootFolder.Files.Remove(file);
                    targetFolder.Files.Add(file);
                }
            });
        }

        private VirtualFolder GetOrCreateFolder(VirtualFolder parent, string name)
        {
            var folder = parent.SubFolders.FirstOrDefault(f => f.Name == name);
            if (folder == null)
            {
                folder = new VirtualFolder(name);
                parent.SubFolders.Add(folder);
            }
            return folder;
        }

        private void ExpandAllNodes(ItemCollection items)
        {
            foreach (var item in items)
            {
                var treeItem = FolderTreeView.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                if (treeItem != null)
                {
                    treeItem.IsExpanded = true;
                    if (treeItem.HasItems)
                    {
                        ExpandAllNodes(treeItem.Items);
                    }
                }
            }
        }

        private void FilesListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void FilesListBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && FilesListBox.SelectedItems.Count > 0)
            {
                Point mousePos = e.GetPosition(null);
                Vector diff = _dragStartPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    var selectedPhotos = FilesListBox.SelectedItems.Cast<ImageItem>().ToList();
                    if (FilesListBox.ItemsSource is ObservableCollection<ImageItem> sourceList)
                    {
                        var data = new DataObject();
                        data.SetData("Photos", selectedPhotos);
                        data.SetData("SourceList", sourceList);
                        DragDrop.DoDragDrop(FilesListBox, data, DragDropEffects.Move);
                    }
                }
            }
        }

        private void FolderTreeView_DragEnter(object sender, DragEventArgs e)
        {
            bool isCompatible = e.Data.GetDataPresent("Photos") || e.Data.GetDataPresent("VirtualFolder");
            e.Effects = isCompatible ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        private void FolderTreeView_DragOver(object sender, DragEventArgs e)
        {
            bool isCompatible = e.Data.GetDataPresent("Photos") || e.Data.GetDataPresent("VirtualFolder");
            e.Effects = isCompatible ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;

            var item = VisualUpwardSearch<TreeViewItem>(e.OriginalSource as DependencyObject);
            if (item != null)
            {
                // Validación para carpetas: No permitir soltar en sí misma o en hijos
                if (e.Data.GetDataPresent("VirtualFolder"))
                {
                    var draggedFolder = e.Data.GetData("VirtualFolder") as VirtualFolder;
                    var targetFolder = item.DataContext as VirtualFolder;
                    if (draggedFolder != null && targetFolder != null)
                    {
                        if (IsFolderChildOf(targetFolder, draggedFolder))
                        {
                            e.Effects = DragDropEffects.None;
                            item.Background = Brushes.LightCoral; // Feedback de error
                            return;
                        }
                    }
                }
                item.Background = Brushes.LightBlue;
            }
        }

        private bool IsFolderChildOf(VirtualFolder potentialChild, VirtualFolder potentialParent)
        {
            if (potentialChild == potentialParent) return true;
            var current = potentialChild.Parent;
            while (current != null)
            {
                if (current == potentialParent) return true;
                current = current.Parent;
            }
            return false;
        }

        private void FolderTreeView_DragLeave(object sender, DragEventArgs e)
        {
            var item = VisualUpwardSearch<TreeViewItem>(e.OriginalSource as DependencyObject);
            if (item != null)
            {
                item.Background = Brushes.Transparent;
            }
        }

        private void FolderTreeView_Drop(object sender, DragEventArgs e)
        {
            var item = VisualUpwardSearch<TreeViewItem>(e.OriginalSource as DependencyObject);
            if (item != null)
            {
                item.Background = Brushes.Transparent;
                var targetFolder = item.DataContext as VirtualFolder;
                if (targetFolder == null) return;

                // Caso A: Soltando FOTOS
                if (e.Data.GetDataPresent("Photos"))
                {
                    var photos = e.Data.GetData("Photos") as List<ImageItem>;
                    var sourceList = e.Data.GetData("SourceList") as ObservableCollection<ImageItem>;

                    if (photos != null && sourceList != null && sourceList != targetFolder.Files)
                    {
                        foreach (var photo in photos.ToList())
                        {
                            sourceList.Remove(photo);
                            targetFolder.Files.Add(photo);
                        }
                    }
                }
                // Caso B: Soltando UNA CARPETA
                else if (e.Data.GetDataPresent("VirtualFolder"))
                {
                    var draggedFolder = e.Data.GetData("VirtualFolder") as VirtualFolder;
                    if (draggedFolder != null && draggedFolder != targetFolder && draggedFolder != RootFolder)
                    {
                        if (!IsFolderChildOf(targetFolder, draggedFolder))
                        {
                            // Remover del padre actual
                            if (draggedFolder.Parent != null)
                            {
                                draggedFolder.Parent.SubFolders.Remove(draggedFolder);
                            }
                            // Agregar al nuevo padre
                            targetFolder.SubFolders.Add(draggedFolder);
                        }
                    }
                }
            }
        }

        private void FolderTreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void FolderTreeView_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point mousePos = e.GetPosition(null);
                Vector diff = _dragStartPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    var treeViewItem = VisualUpwardSearch<TreeViewItem>(e.OriginalSource as DependencyObject);
                    if (treeViewItem != null && treeViewItem.DataContext is VirtualFolder folder && folder != RootFolder)
                    {
                        var data = new DataObject();
                        data.SetData("VirtualFolder", folder);
                        DragDrop.DoDragDrop(FolderTreeView, data, DragDropEffects.Move);
                    }
                }
            }
        }

        private async void Finalize_Click(object sender, RoutedEventArgs e)
        {
            if (RootFolder == null) return;

            var result = MessageBox.Show(
                "¿Estás seguro de que deseas aplicar los cambios?\n\n- Se crearán carpetas reales en tu disco.\n- Las fotos y videos se moverán físicamente a sus nuevas ubicaciones.\n\n⚠️ ESTA ACCIÓN ES PERMANENTE Y NO SE PUEDE DESHACER.",
                "Confirmar Organización Final",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            LoadingOverlay.Visibility = Visibility.Visible;
            var textBlock = LoadingOverlay.Child as StackPanel;
            if (textBlock?.Children[1] is TextBlock label) label.Text = "Moviendo archivos físicamente...";

            try
            {
                await Task.Run(() => ExecutePhysicalMovement(RootFolder, _currentBaseFolderPath));
                MessageBox.Show("¡Los archivos se han organizado correctamente!", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                // Recargamos la carpeta para ver el resultado real
                SetFolder(_currentBaseFolderPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ocurrió un error parcial durante el movimiento: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void ExecutePhysicalMovement(VirtualFolder folder, string targetPath)
        {
            // 1. Asegurar que la carpeta física existe (excepto si es el root que ya sabemos que existe)
            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
            }

            // 2. Mover los archivos de esta carpeta virtual a la carpeta física
            foreach (var fileItem in folder.Files.ToList())
            {
                string sourceFile = fileItem.FilePath;
                string destFile = Path.Combine(targetPath, Path.GetFileName(sourceFile));

                if (sourceFile != destFile)
                {
                    try
                    {
                        // Si ya existe un archivo con ese nombre en el destino, le agregamos un sufijo
                        if (File.Exists(destFile))
                        {
                            string fileName = Path.GetFileNameWithoutExtension(destFile);
                            string ext = Path.GetExtension(destFile);
                            int counter = 1;
                            while (File.Exists(destFile))
                            {
                                destFile = Path.Combine(targetPath, $"{fileName}_{counter++}{ext}");
                            }
                        }

                        File.Move(sourceFile, destFile);
                    }
                    catch (Exception)
                    {
                        // Ignoramos errores individuales para continuar con el resto
                    }
                }
            }

            // 3. Procesar subcarpetas recursivamente
            foreach (var subFolder in folder.SubFolders)
            {
                string subFolderPath = Path.Combine(targetPath, subFolder.Name);
                ExecutePhysicalMovement(subFolder, subFolderPath);
            }
        }

        private static T? VisualUpwardSearch<T>(DependencyObject? source) where T : DependencyObject
        {
            while (source != null && !(source is T))
            {
                if (source is System.Windows.Documents.Run)
                    source = LogicalTreeHelper.GetParent(source);
                else
                    source = VisualTreeHelper.GetParent(source);
            }
            return source as T;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
