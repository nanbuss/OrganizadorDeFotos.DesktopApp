using System.IO;
using CoenM.ImageHash;
using CoenM.ImageHash.HashAlgorithms;
using SixLabors.ImageSharp; // CoenM usa ImageSharp internamente o streams
using SixLabors.ImageSharp.PixelFormats;

public static class ImageHasher
{
    private static readonly PerceptualHash _algorithm = new PerceptualHash();

    public static ulong CalcularPHash(string filePath)
    {
        try
        {
            // Cargamos la imagen usando ImageSharp
            using (var image = Image.Load<Rgba32>(filePath))
            {
                // La librería extiende el objeto Image con el método Hash
                return _algorithm.Hash(image);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error hasheando {filePath}: {ex.Message}");
            return 0;
        }
    }

    public static double CalcularSimilitud(ulong hash1, ulong hash2)
    {
        // La librería nos da la similitud en porcentaje (0 a 100)
        return CompareHash.Similarity(hash1, hash2);
    }
}