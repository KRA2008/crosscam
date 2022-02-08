using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
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
        public Command ResetAlignmentToDefaults { get; set; }
        public Command ChooseDirectory { get; set; }
        public Command ClearDirectory { get; set; }
        public Command NavigateToPairingPageCommand { get; set; }
        public Command ResetFovCorrectionCommand { get; set; }
        public string SaveDirectory => Settings?.SavingDirectory == null
            ? "Pictures"
            : WebUtility.UrlDecode(Settings.SavingDirectory);
        public string ExternalDirectory { get; set; }
        public bool CanSaveToArbitraryDirectory { get; set; }
        public bool CanSaveToExternalDirectory => !string.IsNullOrWhiteSpace(ExternalDirectory);
        private readonly IDirectorySelector _directorySelector;

        // ReSharper disable MemberCanBeMadeStatic.Global
        public IEnumerable<string> Modes => Enum.GetNames(typeof(DrawMode)).ToList();
        public IEnumerable<int> ZeroToOneThousand => Enumerable.Range(0, 1001).ToList();
        public IEnumerable<int> ZeroToOneHundred => Enumerable.Range(0, 101).ToList();
        public IEnumerable<int> ZeroToTen => Enumerable.Range(0, 11).ToList();
        public IEnumerable<string> BorderColors => Enum.GetNames(typeof(BorderColor)).ToList();
        public IEnumerable<string> PortraitCaptureButtonPosition => Enum.GetNames(typeof(PortraitCaptureButtonPosition)).ToList();
        public IEnumerable<string> LandscapeCaptureButtonHorizontalPosition => Enum.GetNames(typeof(LandscapeCaptureButtonHorizontalPosition)).ToList();
        public IEnumerable<string> LandscapeCaptureButtonVerticalPosition => Enum.GetNames(typeof(LandscapeCaptureButtonVerticalPosition)).ToList();
        public IEnumerable<string> PairButtonHorizontalPosition => Enum.GetNames(typeof(PairButtonHorizontalPosition)).ToList();
        // ReSharper restore MemberCanBeMadeStatic.Global

        public bool EnableFirstSideAloneSwitch { get; set; }

        public SettingsViewModel()
        {
            _directorySelector = DependencyService.Get<IDirectorySelector>();

            ResetToDefaults = new Command(() =>
            {
                Settings.ResetToDefaults();
            });

            ResetAlignmentToDefaults = new Command(() =>
            {
                Settings.AlignmentSettings.ResetToDefaults();
            });

            ChooseDirectory = new Command(async () =>
            {
                var newDirectory = await _directorySelector.SelectDirectory();
                if (newDirectory != null)
                {
                    Settings.SavingDirectory = newDirectory;
                    RaisePropertyChanged(nameof(SaveDirectory));
                }
                SaveSettings(null, null);
            });

            ClearDirectory = new Command(() =>
            {
                Settings.SavingDirectory = null;
                RaisePropertyChanged(nameof(SaveDirectory));
                SaveSettings(null, null);
            });

            NavigateToPairingPageCommand = new Command(async () =>
            {
                await CoreMethods.PushPageModel<PairingViewModel>(Settings);
            });

            ResetFovCorrectionCommand = new Command(() =>
            {
                Settings.IsFovCorrectionSet = false;
                Settings.FovPrimaryCorrection = 0;
                Settings.FovSecondaryCorrection = 0;
                PersistentStorage.Save(PersistentStorage.SETTINGS_KEY, Settings);
            });
        }

        public override void Init(object initData)
        {
            base.Init(initData);
            Settings = (Settings) initData;
            SaveSettings(null, null);
            Settings.PropertyChanged += SaveSettings;

            ExternalDirectory = _directorySelector.GetExternalSaveDirectory();
            CanSaveToArbitraryDirectory = _directorySelector.CanSaveToArbitraryDirectory();
        }

        protected override void ViewIsDisappearing(object sender, EventArgs e)
        {
            base.ViewIsDisappearing(sender, e);
            SaveSettings(null, null);
            Settings.PropertyChanged -= SaveSettings;
        }

        protected override void ViewIsAppearing(object sender, EventArgs e)
        {
            base.ViewIsAppearing(sender, e);
            CameraViewModel.BluetoothOperator.CurrentCoreMethods = CoreMethods;
        }

        private void SaveSettings(object sender, PropertyChangedEventArgs e)
        {
            EnableFirstSideAloneSwitch = (Settings.SaveForCrossView || Settings.SaveForParallel || Settings.SaveForRedCyanAnaglyph) &&
                                         !Settings.SaveSidesSeparately;

            if (!EnableFirstSideAloneSwitch)
            {
                Settings.SaveRedundantFirstSide = false;
            }

            PersistentStorage.Save(PersistentStorage.SETTINGS_KEY, Settings);
        }
    }
}