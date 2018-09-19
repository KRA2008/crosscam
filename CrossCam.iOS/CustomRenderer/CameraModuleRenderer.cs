using System;
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
        private UIView _liveCameraStream;
        private AVCapturePhotoOutput _photoOutput;
        private CameraModule _cameraModule;
        private bool _isInitialized;
        private AVCaptureVideoPreviewLayer _avCaptureVideoPreviewLayer;
        private UIDeviceOrientation? _previousValidOrientation;

        public CameraModuleRenderer()
        {
            NSNotificationCenter.DefaultCenter.AddObserver(new NSString("UIDeviceOrientationDidChangeNotification"), OrientationChanged);
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

        private void SetupLiveCameraStream()
        {
            _captureSession = new AVCaptureSession
            {
                SessionPreset = AVCaptureSession.PresetPhoto
            };

            _avCaptureVideoPreviewLayer = new AVCaptureVideoPreviewLayer(_captureSession)
            {
                Frame = _liveCameraStream.Bounds
            };
            _liveCameraStream.Layer.AddSublayer(_avCaptureVideoPreviewLayer);

            SetPreviewFrame();
            SetPreviewOrientation();
            
            var captureDevice = AVCaptureDevice.GetDefaultDevice(AVMediaTypes.Video);
            ConfigureCameraForDevice(captureDevice);
            
            _photoOutput = new AVCapturePhotoOutput
            {
                IsHighResolutionCaptureEnabled = true
            };

            _captureSession.AddOutput(_photoOutput);
            _captureSession.AddInput(AVCaptureDeviceInput.FromDevice(captureDevice));

            _isInitialized = true;
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
                case UIDeviceOrientation.PortraitUpsideDown:
                    imageOrientation = UIImageOrientation.Left;
                    break;
                case UIDeviceOrientation.Portrait:
                    imageOrientation = UIImageOrientation.Right;
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
            SetPreviewFrame();
            NativeView.Add(_liveCameraStream);
            NativeView.ClipsToBounds = true;
        }

        private void OrientationChanged(NSNotification notification)
        {
            if (_isInitialized)
            {
                switch (UIDevice.CurrentDevice.Orientation)
                {
                    case UIDeviceOrientation.PortraitUpsideDown:
                    case UIDeviceOrientation.Portrait:
                    case UIDeviceOrientation.LandscapeLeft:
                    case UIDeviceOrientation.LandscapeRight:
                        if (_previousValidOrientation != UIDevice.CurrentDevice.Orientation)
                        {
                            StopPreview();
                            SetPreviewOrientation();
                            SetPreviewFrame();
                            switch (UIDevice.CurrentDevice.Orientation)
                            {
                                case UIDeviceOrientation.PortraitUpsideDown:
                                case UIDeviceOrientation.Portrait:
                                    _cameraModule.IsPortrait = true;
                                    break;
                                case UIDeviceOrientation.LandscapeLeft:
                                case UIDeviceOrientation.LandscapeRight:
                                    _cameraModule.IsPortrait = false;
                                    break;
                            }

                            if (_cameraModule.IsVisible)
                            {
                                StartPreview();
                            }
                            _previousValidOrientation = UIDevice.CurrentDevice.Orientation;
                        }

                        break;
                    default:
                        if (!_previousValidOrientation.HasValue)
                        {
                            StopPreview();
                            _cameraModule.IsPortrait = true;
                            if (_cameraModule.IsVisible)
                            {
                                StartPreview();
                            }

                            _previousValidOrientation = UIDeviceOrientation.Portrait;
                        }
                        break;
                }
            }
        }

        private void SetPreviewOrientation()
        {
            AVCaptureVideoOrientation videoOrientation = 0;
            switch (UIDevice.CurrentDevice.Orientation)
            {
                case UIDeviceOrientation.Portrait:
                    videoOrientation = AVCaptureVideoOrientation.Portrait;
                    break;
                case UIDeviceOrientation.LandscapeRight:
                    videoOrientation = AVCaptureVideoOrientation.LandscapeLeft;
                    break;
                case UIDeviceOrientation.PortraitUpsideDown:
                    videoOrientation = AVCaptureVideoOrientation.PortraitUpsideDown;
                    break;
                case UIDeviceOrientation.LandscapeLeft:
                    videoOrientation = AVCaptureVideoOrientation.LandscapeRight;
                    break;
            }

            if (videoOrientation != 0)
            {
                _avCaptureVideoPreviewLayer.Orientation = videoOrientation;
            }
        }

        private void SetPreviewFrame()
        {
            var sideHeight = NativeView.Bounds.Height;
            var sideWidth = NativeView.Bounds.Width;

            const double IPHONE_PICTURE_ASPECT_RATIO = 4 / 3d; //iPhones do 4:3 pictures
            nfloat streamWidth = 0;
            switch (UIDevice.CurrentDevice.Orientation)
            {
                case UIDeviceOrientation.PortraitUpsideDown:
                case UIDeviceOrientation.Portrait:
                    _cameraModule.IsPortrait = true;
                    streamWidth = (nfloat) (sideHeight / IPHONE_PICTURE_ASPECT_RATIO);
                    break;
                case UIDeviceOrientation.LandscapeLeft:
                case UIDeviceOrientation.LandscapeRight:
                    _cameraModule.IsPortrait = false;
                    streamWidth = (nfloat) (sideHeight * IPHONE_PICTURE_ASPECT_RATIO); 
                    break;
            }

            if (_liveCameraStream == null)
            {
                _liveCameraStream = new UIView()
                {
                    ContentMode = UIViewContentMode.Redraw
                };
            }

            var leftTrim = (sideWidth - streamWidth) / 2f;

            _liveCameraStream.Frame = new CGRect(leftTrim, 0, streamWidth, sideHeight);
            if (_avCaptureVideoPreviewLayer != null)
            {
                _avCaptureVideoPreviewLayer.Frame = _liveCameraStream.Bounds;
            }
        }
    }
}