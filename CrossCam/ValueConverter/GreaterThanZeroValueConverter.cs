using System;
using System.Globalization;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace CrossCam.ValueConverter
{
    public sealed class GreaterThanZeroValueConverter : IValueConverter, IMarkupExtension
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                switch (value)
                {
                    case int a:
                        return a > 0;
                    case double b:
                        return b > 0;
                    case decimal c:
                        return c > 0;
                    case long d:
                        return d > 0;
                    case byte e:
                        return e > 0;
                    case float f:
                        return f > 0;
                    case uint g:
                        return g > 0;
                    case ulong h:
                        return h > 0;
                    case short i:
                        return i > 0;
                    case ushort j:
                        return j > 0;
                    case char k:
                        return k > 0;
                }
            }
            catch
            {
                return false;
            }

            return false;
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