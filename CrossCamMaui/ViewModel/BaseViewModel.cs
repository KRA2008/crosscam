using CrossCam.Wrappers;
using FreshMvvm.Maui;

namespace CrossCam.ViewModel
{
    public abstract class BaseViewModel : FreshBasePageModel
    {
        public Command OpenLink { get; }

        protected BaseViewModel()
        {
            OpenLink = new Command(async url =>
            {
                Analytics.TrackEvent("link opened", new Dictionary<string, object>
                {
                    {"url opened",url.ToString()}
                });
                await Launcher.OpenAsync(url as string);
            });
        }
    }
}