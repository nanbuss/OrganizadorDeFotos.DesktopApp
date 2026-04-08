using System;

namespace OrganizadorDeFotos.DesktopApp.Modules
{
    public class TrashCandidate : ImageItem
    {
        public string Reason { get; set; } = string.Empty;
        public double Confidence { get; set; }

        public TrashCandidate(ImageItem source)
        {
            FilePath = source.FilePath;
            CaptureDate = source.CaptureDate;
            PerceptualHash = source.PerceptualHash;
            FileSize = source.FileSize;
            Width = source.Width;
            Height = source.Height;
            IsSelected = source.IsSelected;
            IsHighestQuality = source.IsHighestQuality;
            HasExifData = source.HasExifData;
        }
    }
}
