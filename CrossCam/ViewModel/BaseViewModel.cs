using FreshMvvm;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace CrossCam.ViewModel
{
    public abstract class BaseViewModel : FreshBasePageModel
    {
        public Command OpenLink { get; }

        protected BaseViewModel()
        {
            OpenLink = new Command(async url =>
            {
                await Launcher.OpenAsync(url as string);
            });
        }
    }
}