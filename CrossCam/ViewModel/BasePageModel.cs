using FreshMvvm;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace CrossCam.ViewModel
{
    public abstract class BasePageModel : FreshBasePageModel
    {
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