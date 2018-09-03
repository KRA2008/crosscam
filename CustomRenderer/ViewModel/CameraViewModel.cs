using System;
using System.IO;
using CustomRenderer.CustomElement;
using FreshMvvm;
using SkiaSharp;
using Xamarin.Forms;
using Xamarin.Forms.Internals;

namespace CustomRenderer.ViewModel
{
    public sealed class CameraViewModel : FreshBasePageModel
    {
        public ImageSource LeftImageSource { get; set; }
        public byte[] LeftByteArray { get; set; }
        public bool IsLeftCameraVisible { get; set; }
        public Command RetakeLeftCommand { get; set; }

        public ImageSource RightImageSource { get; set; }
        public byte[] RightByteArray { get; set; }
        public bool IsRightCameraVisible { get; set; }
        public Command RetakeRightCommand { get; set; }

        public Command CapturePictureCommand { get; set; }
        public bool CapturePictureTrigger { get; set; }

        public bool IsCaptureComplete { get; set; }
        public Command SaveCaptures { get; set; }
        public bool SuccessFadeTrigger { get; set; }

        public CameraViewModel()
        {
            var photoSaver = DependencyService.Get<IPhotoSaver>();
            IsLeftCameraVisible = true;

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
            });

            RetakeRightCommand = new Command(() =>
            {
                if (!IsLeftCameraVisible)
                {
                    IsRightCameraVisible = true;
                    IsCaptureComplete = false;
                }
            });

            CapturePictureCommand = new Command(() =>
            {
                CapturePictureTrigger = !CapturePictureTrigger;
            });

            SaveCaptures = new Command(() =>
            {
                SKBitmap leftBitmap = null;
                SKBitmap rightBitmap = null;
                SKImage finalImage = null;
                try
                {
                    leftBitmap = SKBitmap.Decode(LeftByteArray);
                    rightBitmap = SKBitmap.Decode(RightByteArray);
                    
                    var screenWidth = (int)Application.Current.MainPage.Width;
                    var halfScreenWidth = screenWidth / 2f;
                    var screenHeight = (int)Application.Current.MainPage.Height;

                    if (Device.RuntimePlatform == Device.Android)
                    {
                        screenWidth *= 2; //TODO: why is this needed?
                        halfScreenWidth = screenWidth / 2f;
                        screenHeight *= 2;
                    }

                    //TODO: image width and heights could be huge, much larger than screen (or smaller I guess)
                    //TODO: image aspect ratio could be very different from screen

                    var screenHeightToPictureHeightRatio = (float)screenHeight / leftBitmap.Height;

                    var imageRealisticCropWidth = screenHeightToPictureHeightRatio * leftBitmap.Width;
                    var imageLeftTrimWidth = leftBitmap.Width - imageRealisticCropWidth / 2f;

                    var finalImageWidth = imageRealisticCropWidth * 2;
                    var halfFinalImageWidth = finalImageWidth / 2f;

                    using (var tempSurface = SKSurface.Create(new SKImageInfo((int)finalImageWidth, leftBitmap.Height)))
                    {
                        var canvas = tempSurface.Canvas;
                        
                        canvas.Clear(SKColors.Transparent);

                        canvas.DrawBitmap(leftBitmap,
                            SKRect.Create(imageLeftTrimWidth, 0, imageRealisticCropWidth, leftBitmap.Height),
                            SKRect.Create(0, 0, halfFinalImageWidth, leftBitmap.Height));
                        canvas.DrawBitmap(rightBitmap,
                            SKRect.Create(imageLeftTrimWidth, 0, imageRealisticCropWidth, leftBitmap.Height),
                            SKRect.Create(halfFinalImageWidth, 0, halfFinalImageWidth, leftBitmap.Height));

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

                    photoSaver.SavePhoto(finalImageByteArray);
                    SuccessFadeTrigger = !SuccessFadeTrigger;
                }
                catch
                {
                    finalImage?.Dispose();
                    leftBitmap?.Dispose();
                    rightBitmap?.Dispose();
                    throw;
                }

                LeftImageSource = null;
                LeftByteArray = null;
                RightImageSource = null;
                RightByteArray = null;
                IsCaptureComplete = false;
                IsRightCameraVisible = false;
                IsLeftCameraVisible = true;
            });
        }
    }
}