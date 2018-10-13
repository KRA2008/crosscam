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
    public class CameraModuleRenderer : ViewRenderer<CameraModule, UIView>, IAVCapturePhotoCaptureDelegate, IUIGestureRecognizerDelegate
    {
        private AVCaptureSession _captureSession;
        private UIView _liveCameraStream;
        private UIGestureRecognizer _tapper;
        private AVCapturePhotoOutput _photoOutput;
        private CameraModule _cameraModule;
        private AVCaptureDevice _device;
        private bool _isInitialized;
        private AVCaptureVideoPreviewLayer _avCaptureVideoPreviewLayer;
        private UIDeviceOrientation? _previousValidOrientation;
        private nfloat _leftTrim;
        private nfloat _streamWidth;

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
                _isInitialized = true;
            }

            if (_isInitialized)
            {
                if (_cameraModule.IsVisible)
                {
                    SetupCamera();
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

            if (e.PropertyName == nameof(_cameraModule.IsFullScreenPreview))
            {
                SetupCamera();
            }
        }

        private void SetupCamera()
        {
            if (_captureSession == null)
            {
                _captureSession = new AVCaptureSession
                {
                    SessionPreset = AVCaptureSession.PresetPhoto
                };
            }

            SetPreviewSizing();
            
            SetPreviewOrientation();

            if (_photoOutput == null)
            {
                _device = AVCaptureDevice.GetDefaultDevice(AVMediaTypes.Video);
                ConfigureCameraForDevice(_device);

                _photoOutput = new AVCapturePhotoOutput
                {
                    IsHighResolutionCaptureEnabled = true
                };

                _captureSession.AddOutput(_photoOutput);
                _captureSession.AddInput(AVCaptureDeviceInput.FromDevice(_device));
            }
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
            if (photo != null && 
                error == null)
            {
                UIImageOrientation imageOrientation;
                var orientationTarget = _previousValidOrientation ?? UIDevice.CurrentDevice.Orientation;
                switch (orientationTarget)
                {
                    case UIDeviceOrientation.LandscapeRight:
                        imageOrientation = UIImageOrientation.Down;
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
                    var uiImage = UIImage.FromImage(cgImage, 1, imageOrientation);
                    _cameraModule.CapturedImage = uiImage.AsJPEG().ToArray();
                }
            }
        }

        [Export("captureOutput:didCapturePhotoForResolvedSettings:")]
        // ReSharper disable once UnusedMember.Local
        private void PhotoJustGotCaptured(AVCapturePhotoOutput photoOutput, AVCaptureResolvedPhotoSettings settings)
        {
            _cameraModule.CaptureSuccess = !_cameraModule.CaptureSuccess;
        }

        [Export("gestureRecognizer:shouldReceiveTouch:")]
        // ReSharper disable once UnusedMember.Local
        private void PreviewWasTapped(UIGestureRecognizer recognizer, UITouch touch)
        {
            var touchLocation = touch.LocationInView(touch.View);
            var taps = touch.TapCount;

            _device.LockForConfiguration(out var error);
            if (error != null) return;

            if (taps > 1)
            {
                if (_device.IsFocusModeSupported(AVCaptureFocusMode.ContinuousAutoFocus))
                {
                    _device.FocusMode = AVCaptureFocusMode.ContinuousAutoFocus;
                }
                if (_device.IsExposureModeSupported(AVCaptureExposureMode.ContinuousAutoExposure))
                {
                    _device.ExposureMode = AVCaptureExposureMode.ContinuousAutoExposure;
                }
            }
            else
            {
                var focusPoint = new CGPoint();

                if (_previousValidOrientation == UIDeviceOrientation.Portrait)
                {
                    var translatedPoint = _avCaptureVideoPreviewLayer.CaptureDevicePointOfInterestForPoint(touchLocation);
                    focusPoint.X = translatedPoint.Y;
                    focusPoint.Y = translatedPoint.X;

                    focusPoint.X = 1 - focusPoint.X;
                }
                else if (_previousValidOrientation == UIDeviceOrientation.LandscapeLeft)
                {
                    focusPoint = _avCaptureVideoPreviewLayer.CaptureDevicePointOfInterestForPoint(touchLocation);
                }
                else if (_previousValidOrientation == UIDeviceOrientation.LandscapeRight)
                {
                    var translatedPoint = _avCaptureVideoPreviewLayer.CaptureDevicePointOfInterestForPoint(touchLocation);

                    focusPoint.X = 1 - translatedPoint.X;
                    focusPoint.Y = 1 - translatedPoint.Y;
                }

                if (focusPoint.Y < 0)
                {
                    focusPoint.Y = 0;
                }

                if (focusPoint.Y > 1)
                {
                    focusPoint.Y = 1;
                }

                if (_device.FocusPointOfInterestSupported &&
                    _device.IsFocusModeSupported(AVCaptureFocusMode.AutoFocus))
                {
                    _device.FocusPointOfInterest = focusPoint;
                    _device.FocusMode = AVCaptureFocusMode.AutoFocus;
                }
                if (_device.ExposurePointOfInterestSupported &&
                    _device.IsExposureModeSupported(AVCaptureExposureMode.AutoExpose))
                {
                    _device.ExposurePointOfInterest = touchLocation;
                    _device.ExposureMode = AVCaptureExposureMode.AutoExpose;
                }
            }

            _device.UnlockForConfiguration();
        }

        private static void ConfigureCameraForDevice(AVCaptureDevice device)
        {
            if (device.IsFlashModeSupported(AVCaptureFlashMode.Off))
            {
                device.LockForConfiguration(out var error);
                if (error == null)
                {
                    device.FlashMode = AVCaptureFlashMode.Off;
                    device.UnlockForConfiguration();
                }
            }

            if (device.IsFocusModeSupported(AVCaptureFocusMode.ContinuousAutoFocus))
            {
                device.LockForConfiguration(out var error);
                if (error == null)
                {
                    device.FocusMode = AVCaptureFocusMode.ContinuousAutoFocus;
                    device.UnlockForConfiguration();
                }
            }

            if (device.IsExposureModeSupported(AVCaptureExposureMode.ContinuousAutoExposure))
            {
                device.LockForConfiguration(out var error);
                if (error == null)
                {
                    device.ExposureMode = AVCaptureExposureMode.ContinuousAutoExposure;
                    device.UnlockForConfiguration();
                }
            }

            if (device.IsWhiteBalanceModeSupported(AVCaptureWhiteBalanceMode.ContinuousAutoWhiteBalance))
            {
                device.LockForConfiguration(out var error);
                if (error == null)
                {
                    device.WhiteBalanceMode = AVCaptureWhiteBalanceMode.ContinuousAutoWhiteBalance;
                    device.UnlockForConfiguration();
                }
            }
        }

        private void SetupUserInterface()
        {
            SetupCamera();
            NativeView.Add(_liveCameraStream);
            NativeView.ClipsToBounds = true;
        }

        private void OrientationChanged(NSNotification notification)
        {
            if (_isInitialized)
            {
                switch (UIDevice.CurrentDevice.Orientation)
                {
                    case UIDeviceOrientation.Portrait:
                    case UIDeviceOrientation.LandscapeLeft:
                    case UIDeviceOrientation.LandscapeRight:
                        if (_previousValidOrientation != UIDevice.CurrentDevice.Orientation)
                        {
                            StopPreview();
                            SetupCamera();
                            switch (UIDevice.CurrentDevice.Orientation)
                            {
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

        private void SetPreviewSizing()
        {
            var sideHeight = NativeView.Bounds.Height;
            var sideWidth = NativeView.Bounds.Width;

            var orientationForSizing = UIDeviceOrientation.Portrait;
            if (UIDevice.CurrentDevice.Orientation == UIDeviceOrientation.Portrait ||
                UIDevice.CurrentDevice.Orientation == UIDeviceOrientation.LandscapeLeft ||
                UIDevice.CurrentDevice.Orientation == UIDeviceOrientation.LandscapeRight)
            {
                orientationForSizing = UIDevice.CurrentDevice.Orientation;
            }
            else if (_previousValidOrientation.HasValue)
            {
                orientationForSizing = _previousValidOrientation.Value;
            }

            if (_cameraModule.IsFullScreenPreview)
            {
                const double IPHONE_PICTURE_ASPECT_RATIO = 4 / 3d; //iPhones do 4:3 pictures
                switch (orientationForSizing)
                {
                    case UIDeviceOrientation.Portrait:
                        _cameraModule.IsPortrait = true;
                        _streamWidth = (nfloat)(sideHeight / IPHONE_PICTURE_ASPECT_RATIO);
                        break;
                    case UIDeviceOrientation.LandscapeLeft:
                    case UIDeviceOrientation.LandscapeRight:
                        _cameraModule.IsPortrait = false;
                        _streamWidth = (nfloat)(sideHeight * IPHONE_PICTURE_ASPECT_RATIO);
                        break;
                }

                _leftTrim = (sideWidth - _streamWidth) / 2f;
            }
            else
            {
                _leftTrim = 0;
                _streamWidth = sideWidth;
            }

            if (_liveCameraStream == null)
            {
                _liveCameraStream = new UIView(new CGRect(_leftTrim, 0, _streamWidth, sideHeight));
                _tapper = new UIGestureRecognizer {Delegate = this};
                _liveCameraStream.AddGestureRecognizer(_tapper);
            }
            else
            {
                _liveCameraStream.Frame = new CGRect(_leftTrim, 0, _streamWidth, sideHeight);
            }

            if (_avCaptureVideoPreviewLayer == null)
            {
                _avCaptureVideoPreviewLayer = new AVCaptureVideoPreviewLayer(_captureSession);
                _liveCameraStream.Layer.AddSublayer(_avCaptureVideoPreviewLayer);
            }

            _avCaptureVideoPreviewLayer.Frame = _liveCameraStream.Bounds;
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
                case UIDeviceOrientation.LandscapeLeft:
                    videoOrientation = AVCaptureVideoOrientation.LandscapeRight;
                    break;
            }
            
            if (videoOrientation != 0)
            {
                _avCaptureVideoPreviewLayer.Orientation = videoOrientation;
            }
        }
    }
}