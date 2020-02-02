using System;
using CrossCam.Model;
using CrossCam.Wrappers;
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
        public Command OpenFrankfurterTutorialCommand { get; set; }
        public Command OpenVoxParallelVideoCommand { get; set; }
        public Command ViewCrossTutorialCommand { get; set; }
        public Command ViewParallelTutorialCommand { get; set; }
        public Command NavigateToSettingsCommand { get; set; }
        public Command ChooseMethodCommand { get; set; }
        public bool IsCrossViewMode { get; set; }
        private Settings _settings;

        public TechniqueHelpViewModel()
        {
            IsCrossViewMode = true;

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

            OpenFrankfurterTutorialCommand = new Command(() =>
            {
                Device.OpenUri(new Uri("https://www.vision3d.com/fftext.html"));
            });

            OpenVoxParallelVideoCommand = new Command(() =>
            {
                Device.OpenUri(new Uri("https://www.youtube.com/watch?v=v8O8Em_RPNg"));
            });

            ViewCrossTutorialCommand = new Command(() =>
            {
                IsCrossViewMode = true;
            });

            ViewParallelTutorialCommand = new Command(() =>
            {
                IsCrossViewMode = false;
            });

            NavigateToSettingsCommand = new Command(async () =>
            {
                await CoreMethods.PushPageModel<SettingsViewModel>(_settings);
            });

            ChooseMethodCommand = new Command(async isCrossString =>
            {
                if (bool.TryParse((string)isCrossString, out var isCross))
                {
                    _settings.Mode = isCross ? DrawMode.Cross : DrawMode.Parallel;
                    PersistentStorage.Save(PersistentStorage.SETTINGS_KEY, _settings);
                    await CoreMethods.DisplayAlert("Success!", "CrossCam is now in " + _settings.Mode + " mode.", "OK");
                }
            });
        }

        public override void Init(object initData)
        {
            base.Init(initData);
            _settings = (Settings)initData;
        }
    }
}