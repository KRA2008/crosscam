using System;
using System.ComponentModel;
using CrossCam.Model;
using CrossCam.Wrappers;
using FreshMvvm;
using Xamarin.Forms;

namespace CrossCam.ViewModel
{
    public class SettingsViewModel : FreshBasePageModel
    {
        public Settings Settings { get; set; }
        public Command ResetToDefaults { get; set; }

        public SettingsViewModel()
        {
            ResetToDefaults = new Command(() =>
            {
                Settings.ResetToDefaults();
            });
        }

        public override void Init(object initData)
        {
            base.Init(initData);
            Settings = (Settings) initData;
            Settings.PropertyChanged += SaveSettings;
        }

        protected override void ViewIsDisappearing(object sender, EventArgs e)
        {
            base.ViewIsDisappearing(sender, e);
            Settings.PropertyChanged -= SaveSettings;
        }

        private void SaveSettings(object sender, PropertyChangedEventArgs e)
        {
            if (!Settings.AreGuideLinesVisible)
            {
                Settings.ShowGuideLinesWithFirstCapture = false;
            }
            if (!Settings.IsGuideDonutVisible)
            {
                Settings.ShowGuideDonutWithFirstCapture = false;
                Settings.IsGuideDonutBothDonuts = false;
            }
            if (Settings.SaveSidesSeparately)
            {
                Settings.SaveRedundantFirstSide = false;
            }

            PersistentStorage.Save(PersistentStorage.SETTINGS_KEY, Settings);
        }
    }
}