using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using CrossCam.Model;
using CrossCam.Wrappers;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Xamarin.CommunityToolkit.UI.Views;
using Xamarin.Forms;

namespace CrossCam.ViewModel
{
    public class SettingsViewModel : BaseViewModel
    {
        public Settings Settings { get; set; }
        public Command ResetToDefaults { get; set; }
        public Command ResetAlignmentToDefaults { get; set; }
        public Command ResetEditsToDefaults { get; set; }
        public Command ResetCardboardToDefaults { get; set; }
        public Command ResetPairToDefaults { get; set; }
        public Command ChooseDirectory { get; set; }
        public Command ClearDirectory { get; set; }
        public Command NavigateToPairingPageCommand { get; set; }
        public Command ResetFovCorrectionCommand { get; set; }
        public Command NavigateToFaqPageAndSection { get; set; }
        public Command SetAnalyticsToDebugModeCommand { get; set; }
        public Command CloseOtherExpandersCommand { get; set; }
        public Expander OpenExpander { get; set; }
        public string SaveDirectory => Settings?.SavingDirectory == null
            ? "Pictures"
            : WebUtility.UrlDecode(Settings.SavingDirectory);
        public string ExternalDirectory { get; set; }
        public bool CanSaveToArbitraryDirectory { get; set; }
        public bool CanSaveToExternalDirectory => !string.IsNullOrWhiteSpace(ExternalDirectory);
        private readonly IDirectorySelector _directorySelector;

        // ReSharper disable MemberCanBeMadeStatic.Global
        public IEnumerable<string> Modes => Enum.GetNames(typeof(DrawMode)).ToList();
        public IEnumerable<string> MotionTypes => Enum.GetNames(typeof(EccMotionType)).ToList();
        public IEnumerable<string> TransformationFindingMethods =>
            Enum.GetNames(typeof(TransformationFindingMethod)).ToList();
        public IEnumerable<int> ZeroToTenThousand => Enumerable.Range(0, 10001).ToList();
        public IEnumerable<int> ZeroToOneThousand => Enumerable.Range(0, 1001).ToList();
        public IEnumerable<int> ZeroToOneHundred => Enumerable.Range(0, 101).ToList();
        public IEnumerable<int> ZeroToTen => Enumerable.Range(0, 11).ToList();
        public IEnumerable<string> BorderColors => Enum.GetNames(typeof(BorderColor)).ToList();
        public IEnumerable<string> PortraitCaptureButtonPosition =>
            Enum.GetNames(typeof(PortraitCaptureButtonPosition)).ToList();
        public IEnumerable<string> LandscapeCaptureButtonHorizontalPosition =>
            Enum.GetNames(typeof(LandscapeCaptureButtonHorizontalPosition)).ToList();
        public IEnumerable<string> LandscapeCaptureButtonVerticalPosition =>
            Enum.GetNames(typeof(LandscapeCaptureButtonVerticalPosition)).ToList();
        public IEnumerable<string> PairButtonHorizontalPosition =>
            Enum.GetNames(typeof(PairButtonHorizontalPosition)).ToList();
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

            ResetEditsToDefaults = new Command(() =>
            {
                Settings.EditsSettings.ResetToDefaults();
            });

            ResetCardboardToDefaults = new Command(() =>
            {
                Settings.CardboardSettings.ResetToDefaults();
            });

            ResetPairToDefaults = new Command(() =>
            {
                Settings.PairSettings.ResetToDefaults();
            });

            ChooseDirectory = new Command(async () =>
            {
                var newDirectory = await _directorySelector.SelectDirectory();
                if (newDirectory != null)
                {
                    Settings.SavingDirectory = newDirectory;
                    RaisePropertyChanged(nameof(SaveDirectory));
                }
                HandleSettingsPropertyChanged(null, null);
            });

            ClearDirectory = new Command(() =>
            {
                Settings.SavingDirectory = null;
                RaisePropertyChanged(nameof(SaveDirectory));
                HandleSettingsPropertyChanged(null, null);
            });

            NavigateToPairingPageCommand = new Command(async () =>
            {
                await CoreMethods.PushPageModel<PairingViewModel>(Settings);
            });

            ResetFovCorrectionCommand = new Command(() =>
            {
                Settings.PairSettings.IsFovCorrectionSet = false;
                Settings.PairSettings.FovPrimaryCorrection = 0;
                Settings.PairSettings.FovSecondaryCorrection = 0;
                PersistentStorage.Save(PersistentStorage.SETTINGS_KEY, Settings);
            });

            NavigateToFaqPageAndSection = new Command(async section =>
            {
                await CoreMethods.PushPageModel<FaqViewModel>(section);
            });

            SetAnalyticsToDebugModeCommand = new Command(async () =>
            {
                App.IsAnalyticsInDebugMode = true;
                if (await Analytics.IsEnabledAsync())
                {
                    var id = await AppCenter.GetInstallIdAsync();
                    Analytics.TrackEvent("start DEBUG: " + id);
                    await CoreMethods.DisplayAlert("Activated", "Please send a screenshot of this to the developer: " + id, "OK");
                }
                else
                {
                    await CoreMethods.DisplayAlert("Analytics are turned off",
                        "Reactivate analytics in order to activate verbose mode", "OK");
                }
            }); 
            
            CloseOtherExpandersCommand = new Command(e =>
            {
                if (e is Expander {IsExpanded: true} expander)
                {
                    OpenExpander = expander;
                }
            });
        }

        public override void Init(object initData)
        {
            base.Init(initData);
            Settings = (Settings) initData;
            Settings.PropertyChanged += HandleSettingsPropertyChanged;
            CheckAnalyticsEnabledStatus();
            ValidateSwitchStatuses();

            ExternalDirectory = _directorySelector.GetExternalSaveDirectory();
            CanSaveToArbitraryDirectory = _directorySelector.CanSaveToArbitraryDirectory();
        }

        public override void ReverseInit(object returnedData)
        {
            SaveSettings();
            Settings.PropertyChanged -= HandleSettingsPropertyChanged;
            base.ReverseInit(returnedData);
        }

        protected override void ViewIsAppearing(object sender, EventArgs e)
        {
            base.ViewIsAppearing(sender, e);
            CameraViewModel.PairOperator.CurrentCoreMethods = CoreMethods;
        }

        private async void CheckAnalyticsEnabledStatus()
        {
            try
            {
                Settings.IsAnalyticsEnabled = await Analytics.IsEnabledAsync();
            }
            catch (Exception ex)
            {
                Crashes.TrackError(ex);
            }
        }

        private async void SetAnalyticsEnabledStatus()
        {
            try
            {
                await Analytics.SetEnabledAsync(Settings.IsAnalyticsEnabled);
            }
            catch (Exception ex)
            {
                Crashes.TrackError(ex);
            }
        }

        private void HandleSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e?.PropertyName == nameof(Settings.IsAnalyticsEnabled))
            {
                SetAnalyticsEnabledStatus();
            }

            SaveSettings();
        }

        private void ValidateSwitchStatuses()
        {
            EnableFirstSideAloneSwitch = (Settings.SaveForCrossView || 
                                          Settings.SaveForParallel || 
                                          Settings.SaveForRedCyanAnaglyph) &&
                                         !Settings.SaveSidesSeparately;

            if (!EnableFirstSideAloneSwitch)
            {
                Settings.SaveRedundantFirstSide = false;
            }
        }

        private void SaveSettings()
        {
            ValidateSwitchStatuses();
            PersistentStorage.Save(PersistentStorage.SETTINGS_KEY, Settings);
        }
    }
}