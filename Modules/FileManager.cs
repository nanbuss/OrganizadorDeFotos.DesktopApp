using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace OrganizadorDeFotos.DesktopApp.Modules
{
    internal static class FileManager
    {
        private const string AuxiliarFolder = "_AUXILIAR";

        public static string GetAuxiliarPath(string basePath, string subFolder)
        {
            var auxiliarPath = Path.Combine(basePath, AuxiliarFolder, subFolder);
            Directory.CreateDirectory(auxiliarPath);
            return auxiliarPath;
        }

        public static void MoveFileToAuxiliar(string filePath, string basePath, string subFolder)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"El archivo no existe: {filePath}");

            var auxiliarPath = GetAuxiliarPath(basePath, subFolder);
            var fileName = Path.GetFileName(filePath);
            var destinationPath = Path.Combine(auxiliarPath, fileName);

            // Manejar colisiones de nombres
            destinationPath = HandleFileNameCollision(destinationPath);

            TryMoveFileWithRetry(filePath, destinationPath);
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
                catch (IOException) when (i < maxRetries - 1)
                {
                    System.Threading.Thread.Sleep(200 * (i + 1));
                }
            }
            File.Move(source, destination, overwrite: false);
        }

        public static void MoveFilesToAuxiliar(IEnumerable<string> filePaths, string basePath, string subFolder)
        {
            foreach (var filePath in filePaths)
            {
                MoveFileToAuxiliar(filePath, basePath, subFolder);
            }
        }

        public static List<string> GetMediaFilesRecursively(string rootPath, string[] extensions)
        {
            var files = new List<string>();
            var excludedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { 
                AuxiliarFolder, 
                "Excluir", 
                "cache", 
                "temp", 
                "tmp",
                "$RECYCLE.BIN",
                "System Volume Information"
            };

            ScanDirectory(rootPath, extensions, excludedFolders, files);
            return files;
        }

        private static void ScanDirectory(string path, string[] extensions, HashSet<string> excludedFolders, List<string> files)
        {
            try
            {
                // 1. Obtener archivos de la carpeta actual
                var directoryFiles = Directory.GetFiles(path);
                foreach (var file in directoryFiles)
                {
                    var ext = Path.GetExtension(file).ToLower();
                    if (extensions.Contains(ext))
                    {
                        files.Add(file);
                    }
                }

                // 2. Procesar subcarpetas recursivamente
                var subDirectories = Directory.GetDirectories(path);
                foreach (var subDir in subDirectories)
                {
                    var dirName = Path.GetFileName(subDir);

                    // Excluir carpetas ocultas (empiezan con .) o en la lista de excluidas
                    if (dirName.StartsWith(".") || excludedFolders.Contains(dirName))
                        continue;

                    ScanDirectory(subDir, extensions, excludedFolders, files);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Ignorar carpetas protegidas
                System.Diagnostics.Debug.WriteLine($"Acceso denegado a la carpeta: {path}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al escanear carpeta {path}: {ex.Message}");
            }
        }

        private static string HandleFileNameCollision(string filePath)
        {
            if (!File.Exists(filePath))
                return filePath;

            var directory = Path.GetDirectoryName(filePath) ?? "";
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);
            var counter = 1;

            while (File.Exists(filePath))
            {
                filePath = Path.Combine(directory, $"{fileName}_{counter:D2}{extension}");
                counter++;
            }

            return filePath;
        }
    }
}
