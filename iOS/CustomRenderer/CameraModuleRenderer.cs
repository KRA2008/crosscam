using System.ComponentModel;
using AVFoundation;
using CoreGraphics;
using CustomRenderer.iOS.CustomRenderer;
using Foundation;
using UIKit;
using Xamarin.Forms;
using Xamarin.Forms.Platform.iOS;
using CameraModule = CustomRenderer.CustomElement.CameraModule;

[assembly: ExportRenderer(typeof(CameraModule), typeof(CameraModuleRenderer))]
namespace CustomRenderer.iOS.CustomRenderer
{
    public class CameraModuleRenderer : ViewRenderer<CameraModule, UIView>
    {
        private AVCaptureSession _captureSession;
        private AVCaptureDeviceInput _captureDeviceInput;
        private UIView _liveCameraStream;
        private AVCaptureStillImageOutput _stillImageOutput;
        private CameraModule _cameraModule;
        private bool _isInitialized;

        public CameraModuleRenderer()
        {
            NSNotificationCenter.DefaultCenter.AddObserver(new NSString("UIDeviceOrientationDidChangeNotification"), FixOrientation);
        }

        protected override void OnElementChanged(ElementChangedEventArgs<CameraModule> e)
        {
            base.OnElementChanged(e);

            if (e.NewElement != null)
            {
                _cameraModule = e.NewElement;
            }
        }

        protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            base.OnElementPropertyChanged(sender, e);

            if (e.PropertyName == nameof(_cameraModule.Width) ||
                e.PropertyName == nameof(_cameraModule.Height))
            {
                NativeView.Bounds = new CGRect(0, 0, _cameraModule.Width, _cameraModule.Height);
            }
            
            if (_cameraModule.Width > 0 &&
                _cameraModule.Height > 0 &&
                !_isInitialized)
            {
                SetupUserInterface();
                AuthorizeCameraUse();
                SetupLiveCameraStream();
                _isInitialized = true;
            }

            if (_isInitialized)
            {
                if (_cameraModule.IsVisible)
                {
                    StartPreview();
                }
                else
                {
                    StopPreview();
                }
            }

            if (e.PropertyName == nameof(_cameraModule.CaptureTrigger))
            {
                if (_cameraModule.IsVisible)
                {
                    CapturePhoto();
                }
            }
        }

        private static async void AuthorizeCameraUse()
        {
            var authorizationStatus = AVCaptureDevice.GetAuthorizationStatus(AVMediaType.Video);

            if (authorizationStatus != AVAuthorizationStatus.Authorized)
            {
                await AVCaptureDevice.RequestAccessForMediaTypeAsync(AVMediaType.Video);
            }
        }

        private void SetupLiveCameraStream()
        {
            _captureSession = new AVCaptureSession();

            AVCaptureVideoOrientation videoOrientation;
            switch (UIDevice.CurrentDevice.Orientation)
            {
                case UIDeviceOrientation.LandscapeRight:
                    videoOrientation = AVCaptureVideoOrientation.LandscapeLeft;
                    break;
                default:
                    videoOrientation = AVCaptureVideoOrientation.LandscapeRight;
                    break;
            }

            var videoPreviewLayer = new AVCaptureVideoPreviewLayer(_captureSession)
            {
                Frame = _liveCameraStream.Bounds,
                Orientation = videoOrientation
            };
            _liveCameraStream.Layer.AddSublayer(videoPreviewLayer);
            
            var captureDevice = AVCaptureDevice.GetDefaultDevice(AVMediaTypes.Video);
            ConfigureCameraForDevice(captureDevice);
            _captureDeviceInput = AVCaptureDeviceInput.FromDevice(captureDevice);

            _stillImageOutput = new AVCaptureStillImageOutput
            {
                OutputSettings = new NSDictionary(),
            };

            _captureSession.AddOutput(_stillImageOutput);
            _captureSession.AddInput(_captureDeviceInput);
        }

        private void StartPreview()
        {
            _captureSession.StartRunning();
        }

        private void StopPreview()
        {
            _captureSession.StopRunning();
        }

        private async void CapturePhoto()
        {
            var videoConnection = _stillImageOutput.ConnectionFromMediaType(AVMediaType.Video);
            var sampleBuffer = await _stillImageOutput.CaptureStillImageTaskAsync(videoConnection);

            UIImageOrientation imageOrientation;
            switch (UIDevice.CurrentDevice.Orientation)
            {
                case UIDeviceOrientation.LandscapeRight:
                    imageOrientation = UIImageOrientation.Down;
                    break;
                default:
                    imageOrientation = UIImageOrientation.Up;
                    break;
            }

            var jpegImageAsNsData = AVCaptureStillImageOutput.JpegStillToNSData(sampleBuffer);
            var image = UIImage.LoadFromData(jpegImageAsNsData);
            using (var cgImage = image.CGImage)     // TODO: WHY THE HELL DO I HAVE TO DO THIS
            {
                image = UIImage.FromImage(cgImage, 1, imageOrientation);
                _cameraModule.CapturedImage = image.AsJPEG().ToArray();
            }
            image.Dispose();
        }

        private static void ConfigureCameraForDevice(AVCaptureDevice device)
        {
            var error = new NSError();
            
            device.LockForConfiguration(out error);
            device.FlashMode = AVCaptureFlashMode.Off;
            device.UnlockForConfiguration();

            if (device.IsFocusModeSupported(AVCaptureFocusMode.ContinuousAutoFocus))
            {
                device.LockForConfiguration(out error);
                device.FocusMode = AVCaptureFocusMode.ContinuousAutoFocus;
                device.UnlockForConfiguration();
            }
            else if (device.IsExposureModeSupported(AVCaptureExposureMode.ContinuousAutoExposure))
            {
                device.LockForConfiguration(out error);
                device.ExposureMode = AVCaptureExposureMode.ContinuousAutoExposure;
                device.UnlockForConfiguration();
            }
            else if (device.IsWhiteBalanceModeSupported(AVCaptureWhiteBalanceMode.ContinuousAutoWhiteBalance))
            {
                device.LockForConfiguration(out error);
                device.WhiteBalanceMode = AVCaptureWhiteBalanceMode.ContinuousAutoWhiteBalance;
                device.UnlockForConfiguration();
            }
        }

        private void SetupUserInterface()
        {
            _liveCameraStream = new UIView
            {
                Frame = new CGRect(NativeView.Bounds.Width / -2f, 0, NativeView.Bounds.Width * 2f, NativeView.Bounds.Height)
            };

            NativeView.Add(_liveCameraStream);
            NativeView.ClipsToBounds = true;
        }

        private void FixOrientation(NSNotification notification)
        {
            if (_isInitialized)
            {
                StopPreview();
                SetupLiveCameraStream();
                if (_cameraModule.IsVisible)
                {
                    StartPreview();
                }
            }
        }
    }
}