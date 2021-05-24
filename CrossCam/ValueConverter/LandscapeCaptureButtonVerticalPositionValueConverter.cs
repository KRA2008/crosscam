using System;
using System.Globalization;
using CrossCam.Model;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace CrossCam.ValueConverter
{
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