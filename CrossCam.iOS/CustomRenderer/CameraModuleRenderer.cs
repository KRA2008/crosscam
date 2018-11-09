using System;
using System.ComponentModel;
using AVFoundation;
using CoreGraphics;
using CoreMedia;
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

            if (e.PropertyName == nameof(_cameraModule.IsTapToFocusEnabled) &&
                !_cameraModule.IsTapToFocusEnabled)
            {
                TurnOnContinuousFocus();
            }

            if (e.PropertyName == nameof(_cameraModule.SwitchToContinuousFocusTrigger) &&
                _cameraModule.IsTapToFocusEnabled)
            {
                TurnOnContinuousFocus();
            }
        }

        private void TurnOnContinuousFocus()
        {
            _device.LockForConfiguration(out var error);
            if (error != null) return;

            if (_device.IsFocusModeSupported(AVCaptureFocusMode.ContinuousAutoFocus))
            {
                _device.FocusMode = AVCaptureFocusMode.ContinuousAutoFocus;
            }
            if (_device.IsExposureModeSupported(AVCaptureExposureMode.ContinuousAutoExposure))
            {
                _device.ExposureMode = AVCaptureExposureMode.ContinuousAutoExposure;
            }

            _device.UnlockForConfiguration();
        }

        private void SetupCamera()
        {
            try
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
            catch (Exception e)
            {
                _cameraModule.ErrorMessage = e.ToString();
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

        [Export("captureOutput:didFinishProcessingPhotoSampleBuffer:previewPhotoSampleBuffer:resolvedSettings:bracketSettings:error:")]
        // ReSharper disable once UnusedMember.Local
        private void PhotoCaptureComplete(AVCapturePhotoOutput captureOutput, CMSampleBuffer finishedPhotoBuffer, CMSampleBuffer previewPhotoBuffer, AVCaptureResolvedPhotoSettings resolvedSettings, AVCaptureBracketedStillImageSettings bracketSettings, NSError error)
        {
            try
            {
                if (error != null)
                {
                    _cameraModule.ErrorMessage = error.ToString();
                }
                else if (finishedPhotoBuffer != null)
                {
                    using (var image = AVCapturePhotoOutput.GetJpegPhotoDataRepresentation(finishedPhotoBuffer, previewPhotoBuffer))
                    using (var imgDataProvider = new CGDataProvider(image))
                    using (var cgImage = CGImage.FromJPEG(imgDataProvider, null, false, CGColorRenderingIntent.Default))
                    using (var uiImage = UIImage.FromImage(cgImage, 1, GetOrientationForCorrection()))
                    {
                        _cameraModule.CapturedImage = uiImage.AsJPEG().ToArray();
                    }
                }
            }
            catch (Exception e)
            {
                _cameraModule.ErrorMessage = e.ToString();
            }
        }

        [Export("captureOutput:didFinishProcessingPhoto:error:")]
        // ReSharper disable once UnusedMember.Local
        private void PhotoCaptureComplete(AVCapturePhotoOutput captureOutput, AVCapturePhoto photo, NSError error)
        {
            try
            {
                if (error != null)
                {
                    _cameraModule.ErrorMessage = error.ToString();
                }
                else if (photo != null)
                {
                    using (var cgImage = photo.CGImageRepresentation)
                    using (var uiImage = UIImage.FromImage(cgImage, 1, GetOrientationForCorrection()))
                    {
                        _cameraModule.CapturedImage = uiImage.AsJPEG().ToArray();
                    }
                }
            }
            catch (Exception e)
            {
                _cameraModule.ErrorMessage = e.ToString();
            }
        }

        private UIImageOrientation GetOrientationForCorrection()
        {
            UIImageOrientation imageOrientation;
            var orientationTarget = _previousValidOrientation ?? UIDevice.CurrentDevice.Orientation;
            switch (orientationTarget)
            {
                case UIDeviceOrientation.LandscapeRight:
                    _cameraModule.WasCapturePortrait = false;
                    imageOrientation = UIImageOrientation.Down;
                    break;
                case UIDeviceOrientation.Portrait:
                    _cameraModule.WasCapturePortrait = true;
                    imageOrientation = UIImageOrientation.Right;
                    break;
                default:
                    _cameraModule.WasCapturePortrait = false;
                    imageOrientation = UIImageOrientation.Up;
                    break;
            }

            return imageOrientation;
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

            if (taps > 1)
            {
                TurnOnContinuousFocus();
            }
            else
            {
                if (_cameraModule.IsTapToFocusEnabled)
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

                    _device.LockForConfiguration(out var error);
                    if (error != null) return;

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

                    _device.UnlockForConfiguration();
                }
            }
        }

        private static void ConfigureCameraForDevice(AVCaptureDevice device)
        {
            device.LockForConfiguration(out var error);
            if (error != null) return;

            if (device.IsFlashModeSupported(AVCaptureFlashMode.Off))
            {
                device.FlashMode = AVCaptureFlashMode.Off;
            }

            if (device.IsFocusModeSupported(AVCaptureFocusMode.ContinuousAutoFocus))
            {
                device.FocusMode = AVCaptureFocusMode.ContinuousAutoFocus;
            }

            if (device.IsExposureModeSupported(AVCaptureExposureMode.ContinuousAutoExposure))
            {
                device.ExposureMode = AVCaptureExposureMode.ContinuousAutoExposure;
            }

            if (device.IsWhiteBalanceModeSupported(AVCaptureWhiteBalanceMode.ContinuousAutoWhiteBalance))
            {
                device.WhiteBalanceMode = AVCaptureWhiteBalanceMode.ContinuousAutoWhiteBalance;
            }

            device.UnlockForConfiguration();
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

            _leftTrim = 0;
            _streamWidth = sideWidth;

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