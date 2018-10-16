using System;
using FreshMvvm;
using Xamarin.Forms;

namespace CrossCam.ViewModel
{
    public class AppExplanationViewModel : FreshBasePageModel
    {
        public Command WikipediaCommand { get; set; }

        public Command HowToViewCommand { get; set; }

        public AppExplanationViewModel()
        {
            WikipediaCommand = new Command(() =>
            {
                Device.OpenUri(new Uri("https://en.wikipedia.org/wiki/Autostereogram"));
            });

            HowToViewCommand = new Command(() =>
            {
                Device.OpenUri(new Uri("http://stereosketcher.com/viewing.html"));
            });
        }
    }
}