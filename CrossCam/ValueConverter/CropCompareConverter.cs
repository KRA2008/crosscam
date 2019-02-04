using System;
using System.Globalization;
using CrossCam.ViewModel;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace CrossCam.ValueConverter
{
    public sealed class CropCompareConverter : IValueConverter, IMarkupExtension
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || 
                parameter == null ||
                !(value is CropMode) ||
                !(parameter is CropMode))
            {
                return false;
            }

            var targetMode = (CropMode) parameter;
            var actualMode = (CropMode) value;
            return targetMode == actualMode;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        public object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}
