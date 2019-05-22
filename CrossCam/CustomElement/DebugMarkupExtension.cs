using System;
using Xamarin.Forms.Xaml;

namespace CrossCam.CustomElement
{
    public class DebugMarkupExtension : IMarkupExtension<bool>
    {
        public bool ProvideValue(IServiceProvider serviceProvider)
        {
            var isDebug = false;
#if DEBUG
            isDebug = true;
#endif
            return isDebug;
        }

        object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider)
        {
            return (this as IMarkupExtension<bool>).ProvideValue(serviceProvider);
        }
    }
}