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
            newPath = HandleFileNameCollision(newPath);

            if (newPath != filePath)
            {
                File.Move(filePath, newPath, overwrite: false);
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
            var directory = Path.GetDirectoryName(filePaths.FirstOrDefault() ?? "") ?? "";
            var newNames = new Dictionary<string, int>();
            var results = new List<string>();

            foreach (var filePath in filePaths)
            {
                var extension = Path.GetExtension(filePath);
                var baseName = Path.GetFileNameWithoutExtension(GetNewFileName(filePath));
                var newFileName = GetNewFileNameWithSuffix(baseName, extension, directory, newNames);
                var newPath = Path.Combine(directory, newFileName);

                if (newPath != filePath)
                {
                    File.Move(filePath, newPath, overwrite: false);
                }

                results.Add(newPath);
                newNames[baseName] = newNames.GetValueOrDefault(baseName, 0) + 1;
            }

            return results;
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

        private static string GetNewFileNameWithSuffix(string baseName, string extension, string directory, Dictionary<string, int> newNames)
        {
            var count = newNames.GetValueOrDefault(baseName, 0);
            if (count == 0)
                return baseName + extension;

            var newPath = Path.Combine(directory, $"{baseName}_{count:D2}{extension}");
            while (File.Exists(newPath))
            {
                count++;
                newPath = Path.Combine(directory, $"{baseName}_{count:D2}{extension}");
            }

            return Path.GetFileName(newPath);
        }
    }
}
