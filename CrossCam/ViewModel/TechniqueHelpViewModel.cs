using System;
using FreshMvvm;
using Xamarin.Forms;

namespace CrossCam.ViewModel
{
    public class TechniqueHelpViewModel : FreshBasePageModel
    {
        public Command OpenWeirdVideoTutorialCommand { get; set; }
        public Command OpenShortVideoTutorialCommand { get; set; }
        public Command OpenPromotionalAlbumCommand { get; set; }
        public Command OpenIAStateTutorialCommand { get; set; }

        public TechniqueHelpViewModel()
        {
            OpenIAStateTutorialCommand = new Command(() =>
            {
                Device.OpenUri(new Uri("https://mslagell.public.iastate.edu/xtut/index.html"));
            });

            OpenShortVideoTutorialCommand = new Command(() =>
            {
                Device.OpenUri(new Uri("https://www.youtube.com/watch?v=zBa-bCxsZDk"));
            });

            OpenWeirdVideoTutorialCommand = new Command(() =>
            {
                Device.OpenUri(new Uri("https://www.youtube.com/watch?v=cvShotHl1As"));
            });

            OpenPromotionalAlbumCommand = new Command(() =>
            {
                Device.OpenUri(new Uri("https://imgur.com/a/Crw232n"));
            });
        }
    }
}