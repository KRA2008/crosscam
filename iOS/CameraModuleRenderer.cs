using System.ComponentModel;
using CoreGraphics;
using Xamarin.Forms;
using Xamarin.Forms.Platform.iOS;
using AVFoundation;
using CustomRenderer;
using CustomRenderer.iOS;
using Foundation;
using UIKit;

[assembly: ExportRenderer(typeof(CameraModule), typeof(CameraModuleRenderer))]
namespace CustomRenderer.iOS
{
    public class CameraModuleRenderer : ViewRenderer<CameraModule, UIView>
    {
        private AVCaptureSession _captureSession;
        private AVCaptureDeviceInput _captureDeviceInput;
        private UIView _liveCameraStream;
        private AVCaptureStillImageOutput _stillImageOutput;
        private UIButton _takePhotoButton;
        private CameraModule _cameraModule;
        private bool _isInitialized;

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
                SetupEventHandlers();
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

            var videoPreviewLayer = new AVCaptureVideoPreviewLayer(_captureSession)
            {
                Frame = _liveCameraStream.Bounds
            };
            _liveCameraStream.Layer.AddSublayer(videoPreviewLayer);
            
            var captureDevice = AVCaptureDevice.GetDefaultDevice(AVMediaTypes.Video);
            ConfigureCameraForDevice(captureDevice);
            _captureDeviceInput = AVCaptureDeviceInput.FromDevice(captureDevice);

            _stillImageOutput = new AVCaptureStillImageOutput
            {
                OutputSettings = new NSDictionary()
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
            
            var jpegImageAsNsData = AVCaptureStillImageOutput.JpegStillToNSData(sampleBuffer);
            _cameraModule.CapturedImage = jpegImageAsNsData.ToArray();
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
            var view = NativeView;
            var centerButtonX = view.Bounds.GetMidX() - 35f;
            var bottomButtonY = view.Bounds.Bottom - 85;
            const int BUTTON_WIDTH = 70;
            const int BUTTON_HEIGHT = 70;

            _liveCameraStream = new UIView
            {
                Frame = new CGRect(view.Bounds.Width/-2f, 0, view.Bounds.Width*2f, view.Bounds.Height)
            };

            _takePhotoButton = new UIButton
            {
                Frame = new CGRect(centerButtonX, bottomButtonY, BUTTON_WIDTH, BUTTON_HEIGHT)
            };
            _takePhotoButton.SetBackgroundImage(UIImage.FromFile("TakePhotoButton.png"), UIControlState.Normal);

            view.Add(_liveCameraStream);
            view.Add(_takePhotoButton);
            view.ClipsToBounds = true;
        }

        private void SetupEventHandlers()
        {
            _takePhotoButton.TouchUpInside += (sender, e) => {
                CapturePhoto();
            };
        }
    }
}