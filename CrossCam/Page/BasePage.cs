using System.Collections.Generic;
using FreshMvvm;
using Microsoft.AppCenter.Analytics;

namespace CrossCam.Page
{
    public abstract class BasePage : FreshBaseContentPage
    {
        protected override void OnAppearing()
        {
            base.OnAppearing();

            Analytics.TrackEvent("page nav",
                new Dictionary<string, string>
                {
                    {"name", GetType().Name}
                });
        }
    }
}