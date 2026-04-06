using System.IO;

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

            File.Move(filePath, destinationPath, overwrite: false);
        }

        public static void MoveFilesToAuxiliar(IEnumerable<string> filePaths, string basePath, string subFolder)
        {
            foreach (var filePath in filePaths)
            {
                MoveFileToAuxiliar(filePath, basePath, subFolder);
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
