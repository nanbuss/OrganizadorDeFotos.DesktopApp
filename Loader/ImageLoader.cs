using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OrganizadorDeFotos.DesktopApp.Loader
{
    internal static class ImageLoader
    {
        public static BitmapSource LoadBitmapSource(string filePath)
        {
            using var fileStream = File.OpenRead(filePath);
            using var memoryStream = new MemoryStream();
            fileStream.CopyTo(memoryStream);
            memoryStream.Position = 0;

            BitmapDecoder decoder = BitmapDecoder.Create(memoryStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            BitmapSource frame = decoder.Frames[0];

            if (frame is null)
            {
                throw new InvalidDataException("No se pudo leer la imagen.");
            }

            frame.Freeze();
            return ApplyExifOrientation(frame);
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
    }
}
