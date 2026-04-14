using System;
using System.Globalization;
using System.Windows.Data;

namespace OrganizadorDeFotos.DesktopApp.Views.Explorer
{
    public class BoolToGalleryTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isGallery && isGallery)
                return "Vista Lista";
            return "Vista Galería";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
