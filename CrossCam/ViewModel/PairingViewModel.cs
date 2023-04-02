using CrossCam.Model;
using CrossCam.Wrappers;
using Microsoft.AppCenter.Analytics;
using Xamarin.Forms;

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
                Settings.IsPairedPrimary = true;
                PersistentStorage.Save(PersistentStorage.SETTINGS_KEY, Settings);
                await CoreMethods.DisplayAlert("Primary Role Selected",
                    "This device is now set as the primary.", "OK");
            });

            SetDeviceSecondaryCommand = new Command(async () =>
            {
                Settings.IsPairedPrimary = false;
                PersistentStorage.Save(PersistentStorage.SETTINGS_KEY, Settings);
                await CoreMethods.DisplayAlert("Secondary Role Selected",
                    "This device is now set as the secondary.", "OK");
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