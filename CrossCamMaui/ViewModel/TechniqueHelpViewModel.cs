using CrossCam.Model;
using CrossCam.Wrappers;

namespace CrossCam.ViewModel
{
    public class TechniqueHelpViewModel : BaseViewModel
    {
        public Command ViewCrossTutorialCommand { get; set; }
        public Command ViewParallelTutorialCommand { get; set; }
        public Command NavigateToSettingsCommand { get; set; }
        public Command ChooseMethodCommand { get; set; }
        public Command<string> OpenLinkCommand { get; set; }
        public bool IsCrossViewMode { get; set; }
        private Settings _settings;
        
        public TechniqueHelpViewModel()
        {
            IsCrossViewMode = true;

            OpenLinkCommand = new Command<string>(async url =>
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Browser.OpenAsync(new Uri(url));
                });
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
                if (bool.TryParse((string)isCrossString, out var isCross) &&
                    _settings != null)
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