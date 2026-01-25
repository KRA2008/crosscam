using System.Globalization;
using CrossCam.Resources.Localization;
using CrossCam.ViewModel;

namespace CrossCam.ValueConverter
{
    public sealed class EnumGetLocalizationConverter : IValueConverter, IMarkupExtension
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not Enum localizeMe)
            {
                return null;
            }

            if (localizeMe is DrawMode enumValue)
            {
                switch (enumValue)
                {
                    case DrawMode.Cross:
                        return AppResources.SaveModes_Cross;
                    case DrawMode.Parallel:
                        return AppResources.SaveModes_Parallel;
                    case DrawMode.RedCyanAnaglyph:
                        return AppResources.SaveModes_Anaglyph;
                    case DrawMode.GrayscaleRedCyanAnaglyph:
                        return AppResources.SaveModes_GrayscaleAnaglyph;
                    case DrawMode.DuboisRedCyanAnaglyph:
                        return AppResources.SaveModes_DuboisAnaglyph;
                    case DrawMode.Cardboard:
                        return AppResources.SaveModes_Cardboard;
                    default:
                        return null;
                }
            }
            return value.Equals(parameter);
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
