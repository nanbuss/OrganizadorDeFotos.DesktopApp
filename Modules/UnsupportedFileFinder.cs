using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OrganizadorDeFotos.DesktopApp.Modules
{
    internal static class UnsupportedFileFinder
    {
        private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff" };
        private static readonly string[] VideoExtensions = { ".mp4", ".avi", ".mkv", ".mov", ".flv", ".wmv", ".webm" };

        public static List<string> FindUnsupportedFiles(string folderPath)
        {
            var allFiles = Directory.GetFiles(folderPath);
            var supportedExtensions = ImageExtensions.Concat(VideoExtensions).Select(e => e.ToLower()).ToHashSet();

            return allFiles
                .Where(file => !supportedExtensions.Contains(Path.GetExtension(file).ToLower()))
                .ToList();
        }

        public static int CountUnsupportedFiles(string folderPath)
        {
            return FindUnsupportedFiles(folderPath).Count;
        }
    }
}
