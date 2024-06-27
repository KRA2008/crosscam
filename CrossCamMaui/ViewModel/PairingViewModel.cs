using CrossCam.Model;
using CrossCam.Resources.Localization;
using CrossCam.Wrappers;
using Microsoft.AppCenter.Analytics;

namespace CrossCam.ViewModel
{
    public class PairingViewModel : BaseViewModel
    {
        public Command SetDevicePrimaryCommand { get; set; }
        public Command SetDeviceSecondaryCommand { get; set; }

        public Settings Settings;

        public PairingViewModel()
        {
            SetDevicePrimaryCommand = new Command(async () =>
            {
                Analytics.TrackEvent("pair role assigned");
                Settings.PairSettings.IsPairedPrimary = true;
                PersistentStorage.Save(PersistentStorage.SETTINGS_KEY, Settings);
                await CoreMethods.DisplayAlert(AppResources.Page_Pair_PrimarySelected,
                    AppResources.Page_Pair_PrimaryExplanation, AppResources.Button_OK);
            });

            SetDeviceSecondaryCommand = new Command(async () =>
            {
                Settings.PairSettings.IsPairedPrimary = false;
                PersistentStorage.Save(PersistentStorage.SETTINGS_KEY, Settings);
                await CoreMethods.DisplayAlert(AppResources.Page_Pair_SecondarySelected,
                    AppResources.Page_Pair_SecondaryExplanation, AppResources.Button_OK);
            });
        }

        public override void Init(object initData)
        {
            if (initData is Settings settings)
            {
                Settings = settings;
            }
            base.Init(initData);
        }
    }
}