using System.Collections.Generic;
using FreshMvvm;
using Microsoft.AppCenter.Analytics;
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
                Analytics.TrackEvent("link opened", new Dictionary<string, string>
                {
                    {"url",url.ToString()}
                });
                await Launcher.OpenAsync(url as string);
            });
        }
    }
}