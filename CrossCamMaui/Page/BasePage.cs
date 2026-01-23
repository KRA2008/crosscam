using CrossCam.Wrappers;
using FreshMvvm.Maui;

namespace CrossCam.Page
{
    public abstract class BasePage : FreshBaseContentPage
    {
        protected override void OnAppearing()
        {
            base.OnAppearing();

            Analytics.TrackEvent("navigation",
                new Dictionary<string, object>
                {
                    {"page name", GetType().Name }
                });
        }
    }
}