using System.Globalization;

namespace CrossCam.ValueConverter
{
    [AcceptEmptyServiceProvider]
    public sealed class GuidToStringConverter : IValueConverter, IMarkupExtension
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Guid.TryParse(value.ToString(), out var guid) ? guid.ToString() : null;
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