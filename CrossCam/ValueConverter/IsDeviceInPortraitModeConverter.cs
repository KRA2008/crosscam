using System;
using System.Globalization;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

//TODO: don't use this, do monitoring in the VM
namespace CrossCam.ValueConverter
{
    public class IsDeviceInPortraitModeConverter : IValueConverter, IMarkupExtension
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (Application.Current == null || Application.Current.MainPage == null)
            {
                return false;
            }
            return Application.Current.MainPage.Height > Application.Current.MainPage.Width;
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