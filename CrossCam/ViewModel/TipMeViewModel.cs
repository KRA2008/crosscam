using FreshMvvm;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace CrossCam.ViewModel
{
    public class TipMeViewModel : FreshBasePageModel
    {
        public Command OpenLink { get; }

        public TipMeViewModel()
        {
            OpenLink = new Command(async url =>
            {
                await Launcher.OpenAsync(url as string);
            });
        }
    }
}