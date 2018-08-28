using System.IO;
using CustomRenderer.CustomElement;
using FreshMvvm;
using SkiaSharp;
using Xamarin.Forms;

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
                try
                {
                    leftBitmap = SKBitmap.Decode(LeftByteArray);
                    rightBitmap = SKBitmap.Decode(RightByteArray);
                    
                    var width = leftBitmap.Width;
                    var halfWidth = width / 2f;
                    var height = leftBitmap.Height;
                    var quarterInterval = width / 4f;

                    SKImage finalImage;
                    using (var tempSurface = SKSurface.Create(new SKImageInfo(width, height)))
                    {
                        var canvas = tempSurface.Canvas;
                        
                        canvas.Clear(SKColors.Transparent);

                        canvas.DrawBitmap(leftBitmap,
                            SKRect.Create(quarterInterval, 0, halfWidth, height),
                            SKRect.Create(0, 0, halfWidth, height));
                        canvas.DrawBitmap(rightBitmap,
                            SKRect.Create(quarterInterval, 0, halfWidth, height),
                            SKRect.Create(halfWidth, 0, halfWidth, height));

                        finalImage = tempSurface.Snapshot();
                    }
                    
                    using (var encoded = finalImage.Encode(SKEncodedImageFormat.Jpeg, 100))
                    {
                        photoSaver.SavePhoto(encoded.AsStream());
                        SuccessFadeTrigger = !SuccessFadeTrigger;
                    }
                }
                finally
                {
                    leftBitmap?.Dispose();
                    rightBitmap?.Dispose();
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