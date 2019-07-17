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
    public class CameraModuleRenderer : ViewRenderer<CameraModule, UIView>, IAVCapturePhotoCaptureDelegate
    {
        private AVCaptureSession _captureSession;
        private UIView _liveCameraStream;
        private AVCapturePhotoOutput _photoOutput;
        private AVCaptureStillImageOutput _stillImageOutput;
        private CameraModule _cameraModule;
        private AVCaptureDevice _device;
        private bool _isInitialized;
        private AVCaptureVideoPreviewLayer _avCaptureVideoPreviewLayer;
        private UIDeviceOrientation? _previousValidOrientation;
        private bool _is10OrHigher;

        public CameraModuleRenderer()
        {
            NSNotificationCenter.DefaultCenter.AddObserver(new NSString("UIDeviceOrientationDidChangeNotification"), OrientationChanged);
            NSNotificationCenter.DefaultCenter.AddObserver(new NSString("UIApplicationWillResignActiveNotification"),
                n => TurnOffFlashAndSetContinuousAutoMode(_device)); // after minimizing or locking phone with first picture taken, preview of second will become super dark and locked that way - seems to be outside my control
        }

        protected override void OnElementChanged(ElementChangedEventArgs<CameraModule> e)
        {
            base.OnElementChanged(e);

            if (e.NewElement != null)
            {
                _cameraModule = e.NewElement;
                SetupCamera();
                StartPreview();
            }
        }

        protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            base.OnElementPropertyChanged(sender, e);

            try
            {
                if (e.PropertyName == nameof(_cameraModule.Width) ||
                    e.PropertyName == nameof(_cameraModule.Height))
                {
                    NativeView.Bounds = new CGRect(0, 0, _cameraModule.Width, _cameraModule.Height);
                    SetupCamera();
                    double previewHeight;
                    var orientation = UIDevice.CurrentDevice.Orientation;
                    switch (orientation)
                    {
                        case UIDeviceOrientation.LandscapeLeft:
                        case UIDeviceOrientation.LandscapeRight:
                            previewHeight = 0.75 * _cameraModule.Width;
                            _cameraModule.PreviewBottomY = (previewHeight + _cameraModule.Height) / 2;
                            break;
                        case UIDeviceOrientation.Portrait:
                            previewHeight = 1.33 * _cameraModule.Width;
                            _cameraModule.PreviewBottomY = (previewHeight + _cameraModule.Height) / 2;
                            break;
                    }
                }

                if (_cameraModule.Width > 0 &&
                    _cameraModule.Height > 0 &&
                    !_isInitialized)
                {
                    SetupUserInterface();
                    _isInitialized = true;
                }

                if (e.PropertyName == nameof(_cameraModule.CaptureTrigger))
                {
                    if (_cameraModule.IsVisible)
                    {
                        CapturePhoto();
                    }
                }

                if (e.PropertyName == nameof(_cameraModule.IsNothingCaptured) &&
                    _cameraModule.IsNothingCaptured ||
                    e.PropertyName == nameof(_cameraModule.IsTapToFocusEnabled) &&
                    !_cameraModule.IsTapToFocusEnabled ||
                    e.PropertyName == nameof(_cameraModule.SwitchToContinuousFocusTrigger) &&
                    _cameraModule.IsTapToFocusEnabled ||
                    e.PropertyName == nameof(_cameraModule.IsLockToFirstEnabled) &&
                    !_cameraModule.IsLockToFirstEnabled)
                {
                    TurnOffFlashAndSetContinuousAutoMode(_device);
                }
            }
            catch (Exception ex)
            {
                _cameraModule.ErrorMessage = ex.ToString();
            }
        }

        private void LockPictureSpecificSettingsIfApplicable()
        {
            if (_cameraModule.IsNothingCaptured && _cameraModule.IsLockToFirstEnabled)
            {
                _cameraModule.IsFocusCircleVisible = false;

                _device.LockForConfiguration(out var error);
                if (error != null) return;

                if (_device.IsFocusModeSupported(AVCaptureFocusMode.Locked))
                {
                    _device.FocusMode = AVCaptureFocusMode.Locked;
                }
                if (_device.IsExposureModeSupported(AVCaptureExposureMode.Locked))
                {
                    _device.ExposureMode = AVCaptureExposureMode.Locked;
                }
                if (_device.IsWhiteBalanceModeSupported(AVCaptureWhiteBalanceMode.Locked))
                {
                    _device.WhiteBalanceMode = AVCaptureWhiteBalanceMode.Locked;
                }

                _device.UnlockForConfiguration();
            }
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
                
                _is10OrHigher = UIDevice.CurrentDevice.CheckSystemVersion(10, 0);
                if (_is10OrHigher && _photoOutput == null)
                {
                    _device = AVCaptureDevice.GetDefaultDevice(AVMediaTypes.Video);
                    TurnOffFlashAndSetContinuousAutoMode(_device);

                    _photoOutput = new AVCapturePhotoOutput
                    {
                        IsHighResolutionCaptureEnabled = true
                    };

                    _captureSession.AddOutput(_photoOutput);
                    _captureSession.AddInput(AVCaptureDeviceInput.FromDevice(_device));
                }
                else if (!_is10OrHigher && _stillImageOutput == null)
                {
                    _device = AVCaptureDevice.GetDefaultDevice(AVMediaTypes.Video);
                    TurnOffFlashAndSetContinuousAutoMode(_device);

                    _stillImageOutput = new AVCaptureStillImageOutput
                    {
                        OutputSettings = new NSDictionary(),
                        HighResolutionStillImageOutputEnabled = true
                    };

                    _captureSession.AddOutput(_stillImageOutput);
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

        private async void CapturePhoto()
        {
            try
            {
                if (_is10OrHigher)
                {
                    var photoSettings = AVCapturePhotoSettings.Create();
                    photoSettings.IsHighResolutionPhotoEnabled = true;
                    _photoOutput.CapturePhoto(photoSettings, this);
                }
                else
                {

                    var videoConnection = _stillImageOutput.ConnectionFromMediaType(AVMediaType.Video);
                    var sampleBuffer = await _stillImageOutput.CaptureStillImageTaskAsync(videoConnection);

                    _cameraModule.CaptureSuccess = !_cameraModule.CaptureSuccess;

                    LockPictureSpecificSettingsIfApplicable();

                    var jpegImageAsNsData = AVCaptureStillImageOutput.JpegStillToNSData(sampleBuffer);
                    using (var image = UIImage.LoadFromData(jpegImageAsNsData))
                    using (var cgImage = image.CGImage)
                    using (var rotatedImage = UIImage.FromImage(cgImage, 1, GetOrientationForCorrection()))
                    {
                        _cameraModule.CapturedImage = rotatedImage.AsJPEG().ToArray();
                    }
                }
            }
            catch (Exception e)
            {
                _cameraModule.ErrorMessage = e.ToString();
            }
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
                    LockPictureSpecificSettingsIfApplicable();

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
                    LockPictureSpecificSettingsIfApplicable();

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
                    imageOrientation = UIImageOrientation.Down;
                    break;
                case UIDeviceOrientation.Portrait:
                    imageOrientation = UIImageOrientation.Right;
                    break;
                default:
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
        
        private void PreviewWasTapped(UIGestureRecognizer recognizer)
        {
            var touchLocation = recognizer.LocationInView(recognizer.View);

            if (_cameraModule.IsTapToFocusEnabled)
            {
                var translatedPoint = _avCaptureVideoPreviewLayer.CaptureDevicePointOfInterestForPoint(touchLocation);

                if (translatedPoint.X < 0)
                {
                    translatedPoint.X = 0;
                }

                if (translatedPoint.X > 1)
                {
                    translatedPoint.X = 1;
                }

                if (translatedPoint.Y < 0)
                {
                    translatedPoint.Y = 0;
                }

                if (translatedPoint.Y > 1)
                {
                    translatedPoint.Y = 1;
                }
                
                double focusCircleX = 0;
                double focusCircleY = 0;
                if (_previousValidOrientation == UIDeviceOrientation.Portrait)
                {
                    var previewHeight = _cameraModule.Width * (4f / 3f);
                    var verticalOffset = (_cameraModule.Height - previewHeight) / 2;
                    focusCircleX = (1 - translatedPoint.Y) * _cameraModule.Width;
                    focusCircleY = translatedPoint.X * previewHeight + verticalOffset;
                }
                else if (_previousValidOrientation == UIDeviceOrientation.LandscapeLeft)
                {
                    var previewHeight = _cameraModule.Width * (3f / 4f);
                    var verticalOffset = (_cameraModule.Height - previewHeight) / 2;
                    focusCircleX = translatedPoint.X * _cameraModule.Width;
                    focusCircleY = translatedPoint.Y * previewHeight + verticalOffset;
                }
                else if (_previousValidOrientation == UIDeviceOrientation.LandscapeRight)
                {
                    var previewHeight = _cameraModule.Width * (3f / 4f);
                    var verticalOffset = (_cameraModule.Height - previewHeight) / 2;
                    focusCircleX = (1 - translatedPoint.X) * _cameraModule.Width;
                    focusCircleY = (1 - translatedPoint.Y) * previewHeight + verticalOffset;
                }

                _device.LockForConfiguration(out var error);
                if (error != null) return;

                if (_device.FocusPointOfInterestSupported &&
                    _device.IsFocusModeSupported(AVCaptureFocusMode.AutoFocus))
                {
                    _device.FocusPointOfInterest = translatedPoint;
                    _device.FocusMode = AVCaptureFocusMode.AutoFocus;
                }
                if (_device.ExposurePointOfInterestSupported &&
                    _device.IsExposureModeSupported(AVCaptureExposureMode.AutoExpose))
                {
                    _device.ExposurePointOfInterest = translatedPoint;
                    _device.ExposureMode = AVCaptureExposureMode.AutoExpose;
                }
                if (_device.IsWhiteBalanceModeSupported(AVCaptureWhiteBalanceMode.AutoWhiteBalance))
                {
                    _device.WhiteBalanceMode = AVCaptureWhiteBalanceMode.AutoWhiteBalance; //not sure about this
                }

                _cameraModule.FocusCircleX = focusCircleX;
                _cameraModule.FocusCircleY = focusCircleY;
                _cameraModule.IsFocusCircleVisible = true;

                _device.UnlockForConfiguration();
            }
        }

        private void PreviewWasSwiped(UISwipeGestureRecognizer swipeGesture)
        {
            _cameraModule.WasSwipedTrigger = !_cameraModule.WasSwipedTrigger;
        }

        private void TurnOffFlashAndSetContinuousAutoMode(AVCaptureDevice device)
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

            _cameraModule.IsFocusCircleVisible = false;

            device.UnlockForConfiguration();
        }

        private void SetupUserInterface()
        {
            SetupCamera();
            StartPreview();
            NativeView.Add(_liveCameraStream);
            NativeView.ClipsToBounds = true;
        }

        private void OrientationChanged(NSNotification notification)
        {
            if (_isInitialized)
            {
                SetupCamera();
                switch (UIDevice.CurrentDevice.Orientation)
                {
                    case UIDeviceOrientation.Portrait:
                    case UIDeviceOrientation.LandscapeLeft:
                    case UIDeviceOrientation.LandscapeRight:
                        if (_previousValidOrientation != UIDevice.CurrentDevice.Orientation)
                        {
                            switch (UIDevice.CurrentDevice.Orientation)
                            {
                                case UIDeviceOrientation.Portrait:
                                    _cameraModule.IsPortrait = true;
                                    _cameraModule.IsViewInverted = false;
                                    break;
                                case UIDeviceOrientation.LandscapeLeft:
                                    _cameraModule.IsPortrait = false;
                                    _cameraModule.IsViewInverted = false;
                                    break;
                                case UIDeviceOrientation.LandscapeRight:
                                    _cameraModule.IsPortrait = false;
                                    _cameraModule.IsViewInverted = true;
                                    break;
                            }
                            
                            _previousValidOrientation = UIDevice.CurrentDevice.Orientation;
                        }

                        break;
                    default:
                        if (!_previousValidOrientation.HasValue)
                        {
                            _cameraModule.IsPortrait = true;
                            _cameraModule.IsViewInverted = false;
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

            if (_liveCameraStream == null)
            {
                _liveCameraStream = new UIView(new CGRect(0, 0, sideWidth, sideHeight));

                var singleTapGesture = new UITapGestureRecognizer
                {
                    NumberOfTapsRequired = 1
                };
                singleTapGesture.AddTarget(() => PreviewWasTapped(singleTapGesture));
                _liveCameraStream.AddGestureRecognizer(singleTapGesture);

                var doubleTapGesture = new UITapGestureRecognizer
                {
                    NumberOfTapsRequired = 2
                };
                doubleTapGesture.AddTarget(() => TurnOffFlashAndSetContinuousAutoMode(_device));
                _liveCameraStream.AddGestureRecognizer(doubleTapGesture);

                var swipeGesture = new UISwipeGestureRecognizer
                {
                    Direction = UISwipeGestureRecognizerDirection.Left | UISwipeGestureRecognizerDirection.Right
                };
                swipeGesture.AddTarget(() => PreviewWasSwiped(swipeGesture));
                _liveCameraStream.AddGestureRecognizer(swipeGesture);
            }
            else
            {
                _liveCameraStream.Frame = new CGRect(0, 0, sideWidth, sideHeight);
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