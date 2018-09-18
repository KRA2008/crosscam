using System;
using System.ComponentModel;
using CrossCam.Model;
using CrossCam.Wrappers;
using FreshMvvm;

namespace CrossCam.ViewModel
{
    public class SettingsViewModel : FreshBasePageModel
    {
        public Settings Settings { get; set; }

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
            PersistentStorage.Save(PersistentStorage.SETTINGS_KEY, Settings);
        }
    }
}