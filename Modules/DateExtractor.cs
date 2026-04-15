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

            // 2. Obtener fecha de modificación del sistema (contiene el horario)
            DateTime systemModification = File.GetLastWriteTime(filePath);
            
            // 3. Intentar extraer fecha del nombre del archivo
            DateTime? nameDate = TryGetDateFromFileName(filePath);

            // Si tenemos fecha en el nombre, la comparamos con la del sistema
            if (nameDate.HasValue)
            {
                // Si el día del nombre es distinto al día de modificación del sistema,
                // O si la fecha del sistema es inválida (<= 2000),
                // asumimos que la fecha del sistema se perdió o es errónea
                // y usamos el DÍA del nombre.
                if (nameDate.Value.Date.Year > 2000 && (nameDate.Value.Date != systemModification.Date || systemModification.Year <= 2000))
                {
                    return new DateTime(
                        nameDate.Value.Year,
                        nameDate.Value.Month,
                        nameDate.Value.Day,
                        systemModification.Year > 2000 ? systemModification.Hour : 12, // Usamos 12 si la hora del sistema también es basura
                        systemModification.Year > 2000 ? systemModification.Minute : 0,
                        systemModification.Year > 2000 ? systemModification.Second : 0
                    );
                }
            }

            // En cualquier otro caso (no hay fecha en el nombre o coinciden), 
            // la fecha de modificación del sistema es la mejor opción porque tiene el horario.
            // Validamos que sea una fecha razonable (> 2000)
            if (systemModification.Year > 2000)
                return systemModification;

            return null;
        }

        private static DateTime? TryGetExifDate(string filePath)
        {
            try
            {
                var directories = ImageMetadataReader.ReadMetadata(filePath);

                // --- LÓGICA PARA FOTOS (EXIF) ---
                var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                if (subIfd != null && subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var dateFoto))
                {
                    if (dateFoto.Year > 2000) return dateFoto;
                }

                // --- LÓGICA PARA VIDEOS (QuickTime / MP4) ---
                var videoHeader = directories.OfType<QuickTimeMovieHeaderDirectory>().FirstOrDefault();
                if (videoHeader != null && videoHeader.TryGetDateTime(QuickTimeMovieHeaderDirectory.TagCreated, out var dateVideo))
                {
                    // OJO: Los videos suelen estar en UTC. Convertimos a hora local.
                    var localDate = dateVideo.ToLocalTime();
                    if (localDate.Year > 2000) return localDate;
                }
            }
            catch (Exception) {  /* Log o ignorar */ }

            return null;
        }

        private static DateTime? TryGetDateFromFileName(string filePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);

            // 1. Intentar formatos específicos de WhatsApp (IMG-YYYYMMDD-WA... o VID-YYYYMMDD-WA...)
            var waMatch = Regex.Match(fileName, @"(IMG|VID)-(\d{4})(\d{2})(\d{2})");
            if (waMatch.Success)
            {
                if (int.TryParse(waMatch.Groups[2].Value, out var year) &&
                    int.TryParse(waMatch.Groups[3].Value, out var month) &&
                    int.TryParse(waMatch.Groups[4].Value, out var day))
                {
                    try { return new DateTime(year, month, day); } catch { }
                }
            }

            // 2. Regex genérico para capturar YYYYMMDD, YYYY-MM-DD, YYYY_MM_DD, etc.
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