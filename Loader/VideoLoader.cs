using System;
using System.Windows.Controls;

namespace OrganizadorDeFotos.DesktopApp.Loader
{
    internal static class VideoLoader
    {
        public static void ShowVideo(MediaElement mediaElement, string filePath)
        {
            mediaElement.Source = new Uri(filePath, UriKind.Absolute);
            mediaElement.Visibility = System.Windows.Visibility.Visible;
            mediaElement.Play();
        }

        public static void StopVideo(MediaElement mediaElement)
        {
            mediaElement.Stop();
            mediaElement.Source = null;
        }
    }
}

