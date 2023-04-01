using FreshMvvm;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace CrossCam.ViewModel
{
    public class BasePageModel : FreshBasePageModel
    {
        // ReSharper disable once MemberCanBePrivate.Global
        public Command OpenLink { get; }

        protected BasePageModel()
        {
            OpenLink = new Command(async url =>
            {
                await Launcher.OpenAsync(url as string);
            });
        }
    }
}