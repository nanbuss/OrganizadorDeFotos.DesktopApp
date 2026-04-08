using System.Collections.ObjectModel;
using System.Linq;

namespace OrganizadorDeFotos.DesktopApp.Modules
{
    public class SimilarityGroup
    {
        public ObservableCollection<ImageItem> Images { get; set; } = new();
        public string DisplayName => $"{Images.Count} imágenes similares";

        public void DetermineQuality()
        {
            if (Images.Count == 0) return;

            // 1. Buscamos la de mayor resolución
            // 2. Si empatan, buscamos la que tenga metadatos EXIF reales (no nulos)
            // 3. Si siguen empatando, la de mayor tamaño en bytes
            var highestQuality = Images
                .OrderByDescending(i => i.Width * i.Height)
                .ThenByDescending(i => i.HasExifData ? 1 : 0) // Necesitás esta prop en ImageItem
                .ThenByDescending(i => i.FileSize)
                .First();

            foreach (var image in Images)
            {
                var isBest = image == highestQuality;
                image.IsHighestQuality = isBest;
                image.IsSelected = isBest;
            }
        }
    }
}