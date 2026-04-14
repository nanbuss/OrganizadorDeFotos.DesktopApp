using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Numerics;
using DupImageLib;
using SixLabors.ImageSharp;
using Microsoft.WindowsAPICodePack.Shell;

namespace OrganizadorDeFotos.DesktopApp.Modules
{
    public class ImageItem
    {
        public string FilePath { get; set; }
        public string FileName => Path.GetFileName(FilePath);
        public DateTime? CaptureDate { get; set; }
        public ulong PerceptualHash { get; set; }
        public long FileSize { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsSelected { get; set; } = true; // Por defecto seleccionado para conservar
        public string RelativePath { get; set; } = string.Empty;

        public string SizeKB => $"{FileSize / 1024} KB";
        public string Dimensions => $"{Width}x{Height}";
        public string QualityIndicator => IsHighestQuality ? "Mayor Calidad" : "";
        public bool IsHighestQuality { get; set; }

        private ImageSource? _imageSource;
        public ImageSource ImageSource => _imageSource ??= LoadImageSource();

        public void PreloadImage()
        {
            if (_imageSource == null)
            {
                _imageSource = LoadImageSource(200); // Cargar miniatura de 200px para la galería
            }
        }

        public Uri ImageUri => new Uri(FilePath, UriKind.Absolute);
        public bool HasExifData { get; internal set; }
        private static readonly string[] RecognizedImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff" };
        private static readonly string[] RecognizedVideoExtensions = { ".mp4", ".avi", ".mkv", ".mov", ".flv", ".wmv", ".webm" };

        public bool IsVideo
        {
            get
            {
                if (string.IsNullOrEmpty(FilePath)) return false;
                string extension = Path.GetExtension(FilePath).ToLower();
                return System.Linq.Enumerable.Contains(RecognizedVideoExtensions, extension);
            }
        }

        private ImageSource LoadImageSource(int decodeWidth = 0)
        {
            try
            {
                if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath))
                {
                    return new BitmapImage();
                }

                // Utilizamos directamente la API de Windows Shell para obtener la miniatura.
                // Esto es mucho más rápido porque usa la caché del sistema operativo, 
                // ya tiene la orientación EXIF correcta aplicada, y soporta tanto imágenes como videos.
                using var shellFile = ShellFile.FromParsingName(FilePath);
                
                // ExtraLargeBitmapSource provee buena calidad (usualmente 256px) ideal para nuestra galería.
                var thumbnail = shellFile.Thumbnail.ExtraLargeBitmapSource;
                if (thumbnail != null)
                {
                    thumbnail.Freeze();
                    return thumbnail; // Retorna la miniatura limpia
                }

                return new BitmapImage();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo miniatura de Shell para {FilePath}: {ex.Message}");
                return new BitmapImage();
            }
        }

        public static ImageItem FromFile(string filePath, bool loadMetadata = true)
        {
            var item = new ImageItem { FilePath = filePath };

            try
            {
                var fi = new FileInfo(filePath);
                item.FileSize = fi.Length;

                if (loadMetadata)
                {
                    item.UpdateMetadata();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error inicializando {filePath}: {ex.Message}");
            }

            return item;
        }

        public void UpdateMetadata()
        {
            try
            {
                // Obtener fecha de captura
                CaptureDate = DateExtractor.GetCaptureDate(FilePath);
                HasExifData = CaptureDate.HasValue;

                string extension = Path.GetExtension(FilePath).ToLower();
                string[] imageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff" };

                if (imageExtensions.Contains(extension))
                {
                    // Solo para imágenes: hash y dimensiones
                    PerceptualHash = ImageHasher.CalcularPHash(FilePath);
                    
                    using var image = SixLabors.ImageSharp.Image.Load(FilePath);
                    Width = image.Width;
                    Height = image.Height;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error actualizando metadatos de {FilePath}: {ex.Message}");
            }
        }

        public double SimilarityTo(ImageItem other)
        {
            return ImageHashes.CompareHashes(PerceptualHash, other.PerceptualHash);
        }
    }
}