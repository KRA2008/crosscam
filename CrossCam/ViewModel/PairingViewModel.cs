using CrossCam.Model;
using CrossCam.Wrappers;
using FreshMvvm;
using Xamarin.Forms;

namespace CrossCam.ViewModel
{
    public class PairingViewModel : FreshBasePageModel
    {
        public Command SetDevicePrimaryCommand { get; set; }
        public Command SetDeviceSecondaryCommand { get; set; }

        private Settings _settings;

        public PairingViewModel()
        {
            SetDevicePrimaryCommand = new Command(async () =>
            {
                _settings.IsPairedPrimary = true;
                PersistentStorage.Save(PersistentStorage.SETTINGS_KEY, _settings);
                await CoreMethods.DisplayAlert("Primary Role Selected",
                    "This device is now set as the primary.", "OK");
            });

            SetDeviceSecondaryCommand = new Command(async () =>
            {
                _settings.IsPairedPrimary = false;
                PersistentStorage.Save(PersistentStorage.SETTINGS_KEY, _settings);
                await CoreMethods.DisplayAlert("Secondary Role Selected",
                    "This device is now set as the secondary.", "OK");
            });
        }

        public override void Init(object initData)
        {
            if (initData is Settings settings)
            {
                _settings = settings;
            }
            base.Init(initData);
        }
    }
}