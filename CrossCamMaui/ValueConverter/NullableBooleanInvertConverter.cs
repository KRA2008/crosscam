using System.Globalization;

namespace CrossCam.ValueConverter
{
    [AcceptEmptyServiceProvider]
    public class NullableBooleanInvertConverter : IValueConverter, IMarkupExtension
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var nullable = value as bool?;
            return nullable.HasValue && !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}