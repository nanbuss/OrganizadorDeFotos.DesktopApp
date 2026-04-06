using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace OrganizadorDeFotos.DesktopApp.Modules
{
    public class DuplicateComparer
    {
        private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff" };

        public static async Task<ObservableCollection<SimilarityGroup>> FindSimilarGroupsAsync(string folderPath)
        {
            var groups = new ObservableCollection<SimilarityGroup>();

            // 1. Obtener archivos (rápido)
            var imageFiles = Directory.GetFiles(folderPath)
                .Where(f => ImageExtensions.Contains(Path.GetExtension(f).ToLower()))
                .ToList();

            // 2. Cargar Metadata y Hashes en paralelo (mucho más rápido)
            var images = new List<ImageItem>();
            await Task.Run(() => {
                Parallel.ForEach(imageFiles, file => {
                    try {
                        var item = ImageItem.FromFile(file);
                        lock(images) { images.Add(item); }
                    } catch { /* Ignorar corruptos */ }
                });
            });

            // 3. ORDENAR POR FECHA (Vital para la optimización)
            var sortedImages = images.OrderBy(i => i.CaptureDate ?? DateTime.MinValue).ToList();

            // 4. Agrupar con ventana de tiempo deslizante
            var processed = new HashSet<string>();

            for (int i = 0; i < sortedImages.Count; i++)
            {
                var current = sortedImages[i];
                if (processed.Contains(current.FilePath)) continue;

                var group = new SimilarityGroup();
                group.Images.Add(current);
                processed.Add(current.FilePath);

                // Solo miramos hacia adelante en la lista ordenada
                for (int j = i + 1; j < sortedImages.Count; j++)
                {
                    var next = sortedImages[j];
                    if (processed.Contains(next.FilePath)) continue;

                    // Si ya pasamos los 10 segundos, no habrá más similares en este grupo
                    if (current.CaptureDate.HasValue && next.CaptureDate.HasValue)
                    {
                        var timeDiff = (next.CaptureDate.Value - current.CaptureDate.Value).TotalSeconds;
                        if (timeDiff > 10) break; // Optimización clave: rompemos el bucle interno
                    }

                    // Verificar similitud visual
                    if (current.SimilarityTo(next) > 0.90)
                    {
                        group.Images.Add(next);
                        processed.Add(next.FilePath);
                    }
                }

                if (group.Images.Count > 1)
                {
                    group.DetermineQuality();
                    groups.Add(group);
                }
            }

            return groups;
        }
    }
}