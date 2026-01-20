using CrossCam.Wrappers;
using FreshMvvm.Maui;

namespace CrossCam.Page
{
    public abstract class BasePage : FreshBaseContentPage
    {
        protected override void OnAppearing()
        {
            base.OnAppearing();

            Analytics.TrackEvent("page_nav",
                new Dictionary<string, object>
                {
                    {"name", GetType().Name}
                });
        }
    }
}