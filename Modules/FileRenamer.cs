using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OrganizadorDeFotos.DesktopApp.Modules
{
    internal static class FileRenamer
    {
        public static string GetNewFileName(string filePath)
        {
            var captureDate = DateExtractor.GetCaptureDate(filePath);

            if (!captureDate.HasValue)
                return Path.GetFileName(filePath);
            
            var extension = Path.GetExtension(filePath);
            var newName = captureDate.Value.ToString("yyyyMMdd_HHmmss") + extension;

            return newName;
        }

        public static string RenameFile(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath) ?? "";
            var newFileName = GetNewFileName(filePath);
            var newPath = Path.Combine(directory, newFileName);

            // Manejar colisiones
            newPath = HandleFileNameCollision(newPath, filePath);

            if (newPath != filePath)
            {
                TryMoveFileWithRetry(filePath, newPath);
            }

            return newPath;
        }

        public static List<string> RenameFiles(IEnumerable<string> filePaths)
        {
            var results = new List<string>();
            foreach (var filePath in filePaths)
            {
                results.Add(RenameFile(filePath));
            }
            return results;
        }

        public static List<string> RenameFilesWithCollisionHandling(IEnumerable<string> filePaths)
        {
            var results = new List<string>();
            // Diccionario para rastrear nombres usados POR CARPETA para manejar colisiones en el mismo lote
            var directoryNewNames = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

            foreach (var filePath in filePaths)
            {
                var directory = Path.GetDirectoryName(filePath) ?? "";
                if (!directoryNewNames.ContainsKey(directory))
                {
                    directoryNewNames[directory] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                }

                var newNames = directoryNewNames[directory];
                var extension = Path.GetExtension(filePath);
                var baseName = Path.GetFileNameWithoutExtension(GetNewFileName(filePath));
                
                // 1. Obtener el sufijo basado en lo que ya hemos procesado en esta ejecución
                var count = newNames.GetValueOrDefault(baseName, 0);
                string newFileName;
                string newPath;

                if (count == 0)
                {
                    newFileName = baseName + extension;
                }
                else
                {
                    newFileName = $"{baseName}_{count:D2}{extension}";
                }

                newPath = Path.Combine(directory, newFileName);

                // 2. IMPORTANTE: Verificar colisiones físicas en el disco (archivos que ya existían antes)
                newPath = HandleFileNameCollision(newPath, filePath);

                // 3. Proceder al renombrado
                if (newPath != filePath)
                {
                    TryMoveFileWithRetry(filePath, newPath);
                }

                results.Add(newPath);
                
                // Actualizar el contador para esta baseName en esta carpeta
                newNames[baseName] = count + 1;
            }

            return results;
        }

        private static void TryMoveFileWithRetry(string source, string destination, int maxRetries = 3)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    File.Move(source, destination, overwrite: false);
                    return;
                }
                catch (IOException ex) when (i < maxRetries - 1)
                {
                    // Si el archivo está en uso, esperamos un poco y reintentamos.
                    // Esto suele suceder si el sistema de miniaturas o un buscador aún tiene el handle abierto.
                    System.Threading.Thread.Sleep(200 * (i + 1));
                }
            }

            // Si llegamos aquí, fallamos todos los intentos
            File.Move(source, destination, overwrite: false);
        }

        private static string HandleFileNameCollision(string filePath, string? originalPath = null)
        {
            // Si el archivo de destino es el mismo que el original, no hay colisión
            if (originalPath != null && string.Equals(Path.GetFullPath(filePath), Path.GetFullPath(originalPath), StringComparison.OrdinalIgnoreCase))
                return filePath;

            if (!File.Exists(filePath))
                return filePath;

            var directory = Path.GetDirectoryName(filePath) ?? "";
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);
            var counter = 1;

            // Intentar encontrar un nombre que no exista
            string newPath = filePath;
            while (File.Exists(newPath))
            {
                // Si el nombre con el contador actual coincide con el original, cortamos aquí
                if (originalPath != null && string.Equals(Path.GetFullPath(newPath), Path.GetFullPath(originalPath), StringComparison.OrdinalIgnoreCase))
                    return newPath;

                newPath = Path.Combine(directory, $"{fileName}_{counter:D2}{extension}");
                counter++;
            }

            return newPath;
        }
    }
}
