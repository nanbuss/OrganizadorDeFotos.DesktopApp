using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Numerics;
using DupImageLib;
using SixLabors.ImageSharp;

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

        public string SizeKB => $"{FileSize / 1024} KB";
        public string Dimensions => $"{Width}x{Height}";
        public string QualityIndicator => IsHighestQuality ? "Mayor Calidad" : "";
        public bool IsHighestQuality { get; set; }

        private ImageSource? _imageSource;
        public ImageSource ImageSource => _imageSource ??= LoadImageSource();

        public Uri ImageUri => new Uri(FilePath, UriKind.Absolute);
        public bool HasExifData { get; internal set; }

        private ImageSource LoadImageSource()
        {
            try
            {
                if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath))
                {
                    return new BitmapImage();
                }

                using var fileStream = File.OpenRead(FilePath);
                using var memoryStream = new MemoryStream();
                fileStream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                BitmapDecoder decoder = BitmapDecoder.Create(memoryStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                BitmapSource frame = decoder.Frames[0];
                if (frame is null)
                {
                    return new BitmapImage();
                }

                frame.Freeze();
                return ApplyExifOrientation(frame);
            }
            catch
            {
                return new BitmapImage();
            }
        }

        private static BitmapSource ApplyExifOrientation(BitmapSource source)
        {
            if (source.Metadata is not BitmapMetadata metadata)
            {
                return source;
            }

            if (!metadata.ContainsQuery("/app1/ifd/{ushort=274}"))
            {
                return source;
            }

            object? orientationValue = metadata.GetQuery("/app1/ifd/{ushort=274}");
            if (orientationValue is not ushort orientation)
            {
                return source;
            }

            Transform? transform = orientation switch
            {
                2 => new ScaleTransform(-1, 1),
                3 => new RotateTransform(180),
                4 => new ScaleTransform(1, -1),
                5 => new TransformGroup
                {
                    Children = new TransformCollection
                    {
                        new RotateTransform(90),
                        new ScaleTransform(1, -1)
                    }
                },
                6 => new RotateTransform(90),
                7 => new TransformGroup
                {
                    Children = new TransformCollection
                    {
                        new RotateTransform(270),
                        new ScaleTransform(1, -1)
                    }
                },
                8 => new RotateTransform(270),
                _ => null
            };

            if (transform is null)
            {
                return source;
            }

            var transformed = new TransformedBitmap(source, transform);
            transformed.Freeze();
            return transformed;
        }

        public static ImageItem FromFile(string filePath)
        {
            var item = new ImageItem { FilePath = filePath };

            // Obtener tamaño de archivo
            var fi = new FileInfo(filePath);
            item.FileSize = fi.Length;

            // Obtener fecha de captura (usando DateExtractor si existe)
            item.CaptureDate = DateExtractor.GetCaptureDate(filePath);

            item.HasExifData = item.CaptureDate.HasValue; // Si tiene fecha de captura, asumimos que tiene EXIF real    
            // Cargar imagen
            item.PerceptualHash = ImageHasher.CalcularPHash(filePath);

            // Para dimensiones, usar System.Drawing o algo, pero DupImageLib usa ImageSharp, pero no expone dimensiones directamente.
            // Usar SixLabors.ImageSharp para dimensiones
            using var image = SixLabors.ImageSharp.Image.Load(filePath);
            item.Width = image.Width;
            item.Height = image.Height;

            return item;
        }

        public double SimilarityTo(ImageItem other)
        {
            return ImageHashes.CompareHashes(PerceptualHash, other.PerceptualHash);
        }
    }
}