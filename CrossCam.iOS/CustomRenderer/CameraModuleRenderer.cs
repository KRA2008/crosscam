using System.ComponentModel;
using AVFoundation;
using CoreGraphics;
using CrossCam.iOS.CustomRenderer;
using Foundation;
using UIKit;
using Xamarin.Forms;
using Xamarin.Forms.Platform.iOS;
using CameraModule = CrossCam.CustomElement.CameraModule;

[assembly: ExportRenderer(typeof(CameraModule), typeof(CameraModuleRenderer))]
namespace CrossCam.iOS.CustomRenderer
{
    public class CameraModuleRenderer : ViewRenderer<CameraModule, UIView>, IAVCapturePhotoCaptureDelegate
    {
        private AVCaptureSession _captureSession;
        private AVCaptureDeviceInput _captureDeviceInput;
        private UIView _liveCameraStream;
        private AVCapturePhotoOutput _photoOutput;
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
            _captureSession = new AVCaptureSession
            {
                SessionPreset = AVCaptureSession.PresetPhoto
            };

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

            _photoOutput = new AVCapturePhotoOutput
            {
                IsHighResolutionCaptureEnabled = true
            };

            _captureSession.AddOutput(_photoOutput);
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

        private void CapturePhoto()
        {
            var photoSettings = AVCapturePhotoSettings.Create();
            photoSettings.IsHighResolutionPhotoEnabled = true;
            _photoOutput.CapturePhoto(photoSettings, this);
        }

        [Export("captureOutput:didFinishProcessingPhoto:error:")]
        // ReSharper disable once UnusedMember.Local
        private void PhotoCaptureComplete(AVCapturePhotoOutput photoOutput, AVCapturePhoto photo, NSError error)
        {
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

            using (var cgImage = photo.CGImageRepresentation)
            {
                var uiImage = UIImage.FromImage(cgImage, 1, imageOrientation);// TODO: WHY THE HELL DO I HAVE TO DO THIS
                _cameraModule.CapturedImage = uiImage.AsJPEG().ToArray();
            }
        }

        [Export("captureOutput:didCapturePhotoForResolvedSettings:")]
        // ReSharper disable once UnusedMember.Local
        private void PhotoJustGotCaptured(AVCapturePhotoOutput photoOutput, AVCaptureResolvedPhotoSettings settings)
        {
            _cameraModule.CaptureSuccess = !_cameraModule.CaptureSuccess;
        }

        private static void ConfigureCameraForDevice(AVCaptureDevice device)
        {
            var error = new NSError();
            
            if (device.IsFlashModeSupported(AVCaptureFlashMode.Off))
            {
                device.LockForConfiguration(out error);
                device.FlashMode = AVCaptureFlashMode.Off;
                device.UnlockForConfiguration();
            }

            if (device.IsFocusModeSupported(AVCaptureFocusMode.ContinuousAutoFocus))
            {
                device.LockForConfiguration(out error);
                device.FocusMode = AVCaptureFocusMode.ContinuousAutoFocus;
                device.UnlockForConfiguration();
            }

            if (device.IsExposureModeSupported(AVCaptureExposureMode.ContinuousAutoExposure))
            {
                device.LockForConfiguration(out error);
                device.ExposureMode = AVCaptureExposureMode.ContinuousAutoExposure;
                device.UnlockForConfiguration();
            }

            if (device.IsWhiteBalanceModeSupported(AVCaptureWhiteBalanceMode.ContinuousAutoWhiteBalance))
            {
                device.LockForConfiguration(out error);
                device.WhiteBalanceMode = AVCaptureWhiteBalanceMode.ContinuousAutoWhiteBalance;
                device.UnlockForConfiguration();
            }
        }

        private void SetupUserInterface()
        {
            var sideHeight = NativeView.Bounds.Height;
            var sideWidth = NativeView.Bounds.Width;
            var streamWidth = sideHeight * 4 / 3f; //iPhones do 4:3 pictures
            _liveCameraStream = new UIView
            {
                Frame = new CGRect((sideWidth - streamWidth) / 2f, 0, streamWidth, sideHeight)
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