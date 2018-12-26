using System;
using FreshMvvm;
using Xamarin.Forms;

namespace CrossCam.ViewModel
{
    public class TechniqueHelpViewModel : FreshBasePageModel
    {
        public Command OpenWeirdVideoTutorialCommand { get; set; }

        public Command OpenShortVideoTutorialCommand { get; set; }

        public Command OpenCrossViewSubredditCommand { get; set; }

        public TechniqueHelpViewModel()
        {
            OpenShortVideoTutorialCommand = new Command(() =>
            {
                Device.OpenUri(new Uri("https://www.youtube.com/watch?v=zBa-bCxsZDk"));
            });

            OpenWeirdVideoTutorialCommand = new Command(() =>
            {
                Device.OpenUri(new Uri("https://www.youtube.com/watch?v=cvShotHl1As"));
            });

            OpenCrossViewSubredditCommand = new Command(() =>
            {
                Device.OpenUri(new Uri("https://old.reddit.com/r/crossview"));
            });
        }
    }
}