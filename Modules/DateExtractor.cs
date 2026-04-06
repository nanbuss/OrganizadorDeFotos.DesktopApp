using System.IO;
using System.Text.RegularExpressions;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;

namespace OrganizadorDeFotos.DesktopApp.Modules
{
    internal static class DateExtractor
    {
        public static DateTime? GetCaptureDate(string filePath)
        {
            // 1. Intentar EXIF (La verdad absoluta si existe)
            var exifDate = TryGetExifDate(filePath);
            if (exifDate.HasValue) return exifDate.Value;

            // 2. Si no hay EXIF, obtenemos los datos del Sistema y del Nombre
            DateTime systemModification = File.GetLastWriteTime(filePath);
            DateTime? nameDate = TryGetDateFromFileName(filePath);

            // CASO A: Tenemos fecha en el nombre (Ej: WhatsApp IMG-20240130-...)
            if (nameDate.HasValue)
            {
                // Si la fecha de modificación del archivo es del mismo día o anterior (poco probable pero posible)
                // Usamos la de modificación porque tiene la hora "real" de guardado.
                if (systemModification.Date <= nameDate.Value.Date)
                {
                    return systemModification;
                }
                else
                {
                    // Si la modificación es posterior (ej: moviste el archivo hoy), 
                    // rescatamos el DÍA del nombre pero le "pegamos" la HORA de la modificación.
                    return new DateTime(
                        nameDate.Value.Year, 
                        nameDate.Value.Month, 
                        nameDate.Value.Day, 
                        systemModification.Hour, 
                        systemModification.Minute, 
                        systemModification.Second
                    );
                }
            }

            // CASO B: No hay fecha en el nombre ni EXIF
            // Devolvemos la modificación del sistema como último recurso.
            return systemModification;
        }

        private static DateTime? TryGetExifDate(string filePath)
        {
            try       
            {
                var directories = ImageMetadataReader.ReadMetadata(filePath);

                // --- LÓGICA PARA FOTOS (EXIF) ---
                var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                if (subIfd != null && subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var dateFoto))
                    return dateFoto;

                // --- LÓGICA PARA VIDEOS (QuickTime / MP4) ---
                var videoHeader = directories.OfType<QuickTimeMovieHeaderDirectory>().FirstOrDefault();
                if (videoHeader != null && videoHeader.TryGetDateTime(QuickTimeMovieHeaderDirectory.TagCreated, out var dateVideo))
                {
                    // OJO: Los videos suelen estar en UTC. Convertimos a hora local.
                    return dateVideo.ToLocalTime(); 
                }
            }
            catch (Exception) {  /* Log o ignorar */ }

            return null;
        }

        private static DateTime? TryGetDateFromFileName(string filePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);

            // Regex mejorado para capturar YYYYMMDD y YYYY-MM-DD
            var match = Regex.Match(fileName, @"(\d{4})[-_]?(\d{2})[-_]?(\d{2})");
            
            if (match.Success)
            {
                if (int.TryParse(match.Groups[1].Value, out var year) &&
                    int.TryParse(match.Groups[2].Value, out var month) &&
                    int.TryParse(match.Groups[3].Value, out var day))
                {
                    try
                    {
                        // Validamos que sea una fecha real (evita meses 13 o días 32)
                        return new DateTime(year, month, day);
                    }
                    catch { return null; }
                }
            }

            return null;
        }
    }
}