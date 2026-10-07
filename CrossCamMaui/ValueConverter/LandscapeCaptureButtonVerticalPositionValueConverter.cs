using System.Globalization;
using CrossCam.Model;

namespace CrossCam.ValueConverter
{
    [AcceptEmptyServiceProvider]
    public class LandscapeCaptureButtonVerticalPositionValueConverter : IValueConverter, IMarkupExtension
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (int)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (LandscapeCaptureButtonVerticalPosition)value;
        }

        public object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}