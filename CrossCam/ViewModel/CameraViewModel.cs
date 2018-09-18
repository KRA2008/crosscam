using System;
using System.IO;
using System.Threading.Tasks;
using CrossCam.Model;
using CrossCam.Wrappers;
using FreshMvvm;
using SkiaSharp;
using Xamarin.Forms;

namespace CrossCam.ViewModel
{
    public sealed class CameraViewModel : FreshBasePageModel
    {
        public ImageSource LeftImageSource { get; set; }
        public byte[] LeftByteArray { get; set; }
        public bool IsLeftCameraVisible { get; set; }
        public Command RetakeLeftCommand { get; set; }
        public bool LeftCaptureSuccess { get; set; }

        public ImageSource RightImageSource { get; set; }
        public byte[] RightByteArray { get; set; }
        public bool IsRightCameraVisible { get; set; }
        public Command RetakeRightCommand { get; set; }
        public bool RightCaptureSuccess { get; set; }

        public Command CapturePictureCommand { get; set; }
        public bool CapturePictureTrigger { get; set; }

        public bool IsCaptureComplete { get; set; }
        public Command SaveCapturesCommand { get; set; }

        public Command ToggleViewModeCommand { get; set; }
        public bool IsViewMode { get; set; }

        public Command ClearCapturesCommand { get; set; }

        public Command NavigateToSettingsCommand { get; set; }

        public Settings Settings { get; set; }

        public bool IsPortrait { get; set; }

        public bool FailFadeTrigger { get; set; }
        public bool SuccessFadeTrigger { get; set; }
        public bool IsSaving { get; set; }

        public bool ShouldLeftRetakeBeVisible => LeftByteArray != null && !IsSaving && !IsViewMode;
        public bool ShouldRightRetakeBeVisible => RightByteArray != null && !IsSaving && !IsViewMode;
        public bool ShouldEndButtonsBeVisible => IsCaptureComplete && !IsSaving && !IsViewMode;
        public bool ShouldSettingsBeVisible => LeftByteArray == null && RightByteArray == null && !IsSaving && !IsViewMode;
        public bool ShouldLineGuidesBeVisible => !IsCaptureComplete && Settings.AreGuideLinesVisible;
        public bool ShouldDonutGuideBeVisible => !IsCaptureComplete && Settings.IsGuideDonutVisible;

        public string HelpText => "1) Face your subject straight on and frame it up in the center of the screen" + 
                                  "\n2) Place your feet shoulder-width apart and lean towards your right foot" +
                                  "\n3) Drag the horizontal guide lines over some recognizable features of the subject" +
                                  "\n4) Take the left picture (but finish reading these directions first)" +
                                  "\n5) A preview for the right picture will take the place of this text => start cross viewing" + 
                                  "\n6) While keeping the subject centered on the screen and the horizontal guide lines over the same features on the right as they are on the left, begin shifting your weight to lean towards your left foot" +
                                  "\n7) Take the right picture when the desired level of 3D is achieved";

        public CameraViewModel()
        {
            var photoSaver = DependencyService.Get<IPhotoSaver>();
            IsLeftCameraVisible = true;

            Settings = PersistentStorage.LoadOrDefault(PersistentStorage.SETTINGS_KEY, new Settings());

            PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(LeftByteArray) &&
                    LeftByteArray != null)
                {
                    LeftImageSource = ImageSource.FromStream(() => new MemoryStream(LeftByteArray));
                    IsLeftCameraVisible = false;
                    if (RightByteArray == null)
                    {
                        IsRightCameraVisible = true;
                    }
                    else
                    {
                        IsCaptureComplete = true;
                    }
                }
                else if (args.PropertyName == nameof(RightByteArray) &&
                         RightByteArray != null)
                {
                    RightImageSource = ImageSource.FromStream(() => new MemoryStream(RightByteArray));
                    IsRightCameraVisible = false;
                    IsCaptureComplete = true;
                }
            };

            RetakeLeftCommand = new Command(() =>
            {
                IsRightCameraVisible = false;
                IsLeftCameraVisible = true;
                IsCaptureComplete = false;
                LeftByteArray = null;
                LeftImageSource = null;
            });

            RetakeRightCommand = new Command(() =>
            {
                if (!IsLeftCameraVisible)
                {
                    IsRightCameraVisible = true;
                    IsCaptureComplete = false;
                    RightByteArray = null;
                    RightImageSource = null;
                }
            });

            ClearCapturesCommand = new Command(ClearCaptures);

            CapturePictureCommand = new Command(() =>
            {
                CapturePictureTrigger = !CapturePictureTrigger;
            });

            ToggleViewModeCommand = new Command(() =>
            {
                IsViewMode = !IsViewMode;
            });

            NavigateToSettingsCommand = new Command(async () =>
            {
                await CoreMethods.PushPageModel<SettingsViewModel>(Settings);
            });

            SaveCapturesCommand = new Command(async () =>
            {
                IsSaving = true;
                LeftImageSource = null;
                RightImageSource = null;

                await Task.Delay(500); // breathing room for screen to update

                SKBitmap leftBitmap = null;
                SKBitmap rightBitmap = null;
                SKImage finalImage = null;
                try
                {
                    leftBitmap = SKBitmap.Decode(LeftByteArray);
                    LeftByteArray = null;

                    rightBitmap = SKBitmap.Decode(RightByteArray);
                    RightByteArray = null;

                    double eachSideWidth;
                    if (IsPortrait)
                    {
                        eachSideWidth = leftBitmap.Width;
                    }
                    else
                    {
                        var pictureHeightToScreenHeightRatio = leftBitmap.Height / Application.Current.MainPage.Height;
                        eachSideWidth = Application.Current.MainPage.Width * pictureHeightToScreenHeightRatio / 2d;
                    }

                    var imageLeftTrimWidth = (leftBitmap.Width - eachSideWidth) / 2d;

                    var finalImageWidth = eachSideWidth * 2;

                    using (var tempSurface = SKSurface.Create(new SKImageInfo((int)finalImageWidth, leftBitmap.Height)))
                    {
                        var canvas = tempSurface.Canvas;
                        
                        canvas.Clear(SKColors.Transparent);

                        var floatedTrim = (float)imageLeftTrimWidth;
                        var floatedWidth = (float)eachSideWidth;

                        canvas.DrawBitmap(leftBitmap,
                            SKRect.Create(floatedTrim, 0, floatedWidth, leftBitmap.Height),
                            SKRect.Create(0, 0, floatedWidth, leftBitmap.Height));
                        canvas.DrawBitmap(rightBitmap,
                            SKRect.Create(floatedTrim, 0, floatedWidth, leftBitmap.Height),
                            SKRect.Create(floatedWidth, 0, floatedWidth, leftBitmap.Height));

                        finalImage = tempSurface.Snapshot();
                    }

                    byte[] finalImageByteArray;
                    using (var encoded = finalImage.Encode(SKEncodedImageFormat.Jpeg, 100))
                    {
                        finalImageByteArray = encoded.ToArray();
                    }

                    finalImage.Dispose();
                    leftBitmap.Dispose();
                    rightBitmap.Dispose();

                    var didSave = await photoSaver.SavePhoto(finalImageByteArray);
                    IsSaving = false;

                    if (didSave)
                    {
                        SuccessFadeTrigger = !SuccessFadeTrigger;
                    }
                    else
                    {
                        FailFadeTrigger = !FailFadeTrigger;
                    }
                }
                catch
                {
                    finalImage?.Dispose();
                    leftBitmap?.Dispose();
                    rightBitmap?.Dispose();
                }

                ClearCaptures();
            });
        }

        protected override void ViewIsAppearing(object sender, EventArgs e)
        {
            base.ViewIsAppearing(sender, e);
            RaisePropertyChanged(nameof(ShouldLineGuidesBeVisible));
            RaisePropertyChanged(nameof(ShouldDonutGuideBeVisible));
        }

        private void ClearCaptures()
        {
            LeftByteArray = null;
            RightByteArray = null;
            LeftImageSource = null;
            RightImageSource = null;
            IsCaptureComplete = false;
            IsRightCameraVisible = false;
            IsLeftCameraVisible = true;
        }
    }
}