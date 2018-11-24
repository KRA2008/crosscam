using System;
using System.Globalization;
using CrossCam.ViewModel;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace CrossCam.ValueConverter
{
    public sealed class EnumCompareConverter : IValueConverter, IMarkupExtension
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || 
                parameter == null ||
                !(value is WorkflowStage) ||
                !(parameter is WorkflowStage))
            {
                return false;
            }

            var targetStage = (WorkflowStage) parameter;
            var actualStage = (WorkflowStage) value;
            return targetStage == actualStage;
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
