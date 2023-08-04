using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Timers;
using AVFoundation;
using CoreFoundation;
using CoreGraphics;
using CoreMedia;
using CoreVideo;
using CrossCam.iOS.CustomRenderer;
using CrossCam.Model;
using CrossCam.ViewModel;
using Foundation;
using SkiaSharp;
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
        private AVCaptureVideoDataOutput _previewFrameOutput;
        private AVCaptureDeviceInput _deviceInput;
        private PreviewFrameDelegate _previewFrameDelegate;
        private CameraModule _cameraModule;
        private AVCaptureDevice _device;
        private AVCaptureVideoPreviewLayer _avCaptureVideoPreviewLayer;
        private static UIDeviceOrientation? _previousValidOrientation;
        private bool _is10OrHigher;
        private IEnumerable<AVCaptureDevice> _devices;
        private const string ADJUSTING_FOCUS = "adjustingFocus";
        private readonly List<string> _setupProperties = new List<string>
        {
            "Height",
            "Width",
            "Renderer"
        };

        protected override void OnElementChanged(ElementChangedEventArgs<CameraModule> e)
        {
            base.OnElementChanged(e);

            if (e.OldElement != null)
            {
                _cameraModule.PairOperator.CaptureSyncTimeElapsed -= HandleCaptureSyncTimeElapsed;
                _cameraModule.SingleTapped -= HandleSingleTap;
                _cameraModule.DoubleTapped -= HandleDoubleTap;
                NSNotificationCenter.DefaultCenter.RemoveObservers(new[]
                {
                    new NSString("UIDeviceOrientationDidChangeNotification"),
                    new NSString("UIApplicationWillResignActiveNotification")
                });
            }

            if (e.NewElement != null)
            {
                _cameraModule = e.NewElement;
                _cameraModule.PairOperator.CaptureSyncTimeElapsed += HandleCaptureSyncTimeElapsed;
                _cameraModule.SingleTapped += HandleSingleTap;
                _cameraModule.DoubleTapped += HandleDoubleTap;
                NSNotificationCenter.DefaultCenter.AddObserver(new NSString("UIDeviceOrientationDidChangeNotification"), OrientationChanged);
                NSNotificationCenter.DefaultCenter.AddObserver(new NSString("UIApplicationWillResignActiveNotification"),
                    n => TurnOffFlashAndSetContinuousAutoMode(_device)); // after minimizing or locking phone with first picture taken, preview of second will become super dark and locked that way - seems to be outside my control
                UIApplication.Notifications.ObserveDidEnterBackground(StopPreview);
                UIApplication.Notifications.ObserveWillEnterForeground(StartPreview);
            }
        }

        private void HandleCaptureSyncTimeElapsed(object sender, ElapsedEventArgs e)
        {
            Device.BeginInvokeOnMainThread(CapturePhoto);
        }

        private void HandleDoubleTap(object sender, EventArgs e)
        {
            TurnOffFlashAndSetContinuousAutoMode(_device);
        }

        private void HandleSingleTap(object sender, PointF point)
        {
            PreviewWasTapped(point);
        }

#if !__SIMULATOR__
        protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            base.OnElementPropertyChanged(sender, e);

            try
            {
                if (_setupProperties.Contains(e.PropertyName))
                {
                    _setupProperties.Remove(e.PropertyName);
                    if (!_setupProperties.Any())
                    {
                        FullInit();
                        SetPreviewBottomY();
                    }
                }

                if (e.PropertyName == nameof(_cameraModule.Width) ||
                    e.PropertyName == nameof(_cameraModule.Height))
                {
                    SetPreviewBottomY();
                }

                if (e.PropertyName == nameof(_cameraModule.CaptureTrigger))
                {
                    if (_cameraModule.IsVisible)
                    {
                        CapturePhoto();
                    }
                }

                if (e.PropertyName == nameof(_cameraModule.AreBothSidesCaptured))
                {
                    if (_cameraModule.AreBothSidesCaptured)
                    {
                        StopPreview();
                    }
                    else
                    {
                        StartPreview();
                    }
                }

                if (e.PropertyName == nameof(_cameraModule.StopPreviewTrigger))
                {
                    StopPreview();
                }

                if (e.PropertyName == nameof(_cameraModule.RestartPreviewTrigger))
                {
                    StartPreview();
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

                if (e.PropertyName == nameof(_cameraModule.ChosenCamera))
                {
                    ChosenCameraChanged();
                }
            }
            catch (Exception ex)
            {
                _cameraModule.Error = ex;
            }
        }

        private void SetPreviewBottomY()
        {
            //Debug.WriteLine("### _cameraModule: " + _cameraModule.Width + " " + _cameraModule.Height);
            double previewHeight;
            var orientation = UIDevice.CurrentDevice.Orientation;
            _cameraModule.PreviewAspectRatio = 4 / 3d;
            switch (orientation)
            {
                case UIDeviceOrientation.LandscapeLeft:
                case UIDeviceOrientation.LandscapeRight:
                    previewHeight = 0.75 * _cameraModule.Width;
                    _cameraModule.PreviewBottomY = (previewHeight + _cameraModule.Height) / 2;
                    break;
                default:
                    previewHeight = 1.33 * _cameraModule.Width;
                    _cameraModule.PreviewBottomY = (previewHeight + _cameraModule.Height) / 2;
                    break;
            }
            NativeView.Bounds = new CGRect(0, -10000, _cameraModule.Width, previewHeight);
        }
#endif

        private void ChosenCameraChanged()
        {
            if (_device != null &&
                _cameraModule.ChosenCamera?.CameraId != null)
            {
                var chosenDevice = AVCaptureDevice.DeviceWithUniqueID(_cameraModule.ChosenCamera.CameraId);
                if (chosenDevice?.UniqueID != _device.UniqueID)
                {
                    _device = chosenDevice;
                    _device?.AddObserver(this, ADJUSTING_FOCUS, NSKeyValueObservingOptions.OldNew, IntPtr.Zero);

                    TurnOffFlashAndSetContinuousAutoMode(_device);

                    if (_deviceInput != null)
                    {
                        _captureSession.RemoveInput(_deviceInput);
                    }
                    _deviceInput = AVCaptureDeviceInput.FromDevice(_device);
                    _captureSession.AddInput(_deviceInput);
                }
            }
        }

        private void LockPictureSpecificSettingsIfApplicable()
        {
            if (_cameraModule.IsNothingCaptured && _cameraModule.IsLockToFirstEnabled)
            {

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
                _captureSession = new AVCaptureSession
                {
                    SessionPreset = AVCaptureSession.PresetPhoto
                };
                if (!_cameraModule.AvailableCameras.Any())
                {
                    var deviceTypes = new List<AVCaptureDeviceType>
                    {
                        AVCaptureDeviceType.BuiltInWideAngleCamera,
                        AVCaptureDeviceType.BuiltInTelephotoCamera
                    };
                    if (UIDevice.CurrentDevice.CheckSystemVersion(13, 0))
                    {
                        deviceTypes.Add(AVCaptureDeviceType.BuiltInUltraWideCamera);
                    }
                    var session = AVCaptureDeviceDiscoverySession.Create(
                        deviceTypes.ToArray(), AVMediaType.Video, AVCaptureDevicePosition.Unspecified);
                    _devices = session.Devices;
                    foreach (var avCaptureDevice in _devices)
                    {
                        _cameraModule.AvailableCameras.Add(new AvailableCamera
                        {
                            DisplayName = avCaptureDevice.LocalizedName,
                            CameraId = avCaptureDevice.UniqueID,
                            IsFront = avCaptureDevice.Position == AVCaptureDevicePosition.Front
                        });
                    }
                }

                SetPreviewOrientation();

                _device = AVCaptureDevice.GetDefaultDevice(AVMediaTypes.Video);
                _cameraModule.ChosenCamera = _cameraModule.AvailableCameras.First(c => c.CameraId == _device.UniqueID);

                _device?.AddObserver(this, ADJUSTING_FOCUS, NSKeyValueObservingOptions.OldNew, IntPtr.Zero);

                SetPreviewSizing();

                TurnOffFlashAndSetContinuousAutoMode(_device);

                _is10OrHigher = UIDevice.CurrentDevice.CheckSystemVersion(10, 0);

                if (_is10OrHigher)
                {
                    _photoOutput = new AVCapturePhotoOutput
                    {
                        IsHighResolutionCaptureEnabled = true
                    };

                    _captureSession.AddOutput(_photoOutput);
                }
                else if (!_is10OrHigher)
                {
                    _stillImageOutput = new AVCaptureStillImageOutput
                    {
                        OutputSettings = new NSDictionary(),
                        HighResolutionStillImageOutputEnabled = true
                    };

                    _captureSession.AddOutput(_stillImageOutput);
                }
                
                var settings = new AVVideoSettingsUncompressed
                {
                    PixelFormatType = CVPixelFormatType.CV32BGRA
                };

                _previewFrameOutput = new AVCaptureVideoDataOutput
                {
                    AlwaysDiscardsLateVideoFrames = true,
                    MinFrameDuration = new CMTime(1, 30),
                    UncompressedVideoSetting = settings
                };
                //if (UIDevice.CurrentDevice.CheckSystemVersion(13, 0)) //TODO: what is this?
                //{
                //    _previewFrameOutput.DeliversPreviewSizedOutputBuffers = true;
                //    _previewFrameOutput.AutomaticallyConfiguresOutputBufferDimensions = false;
                //}
                _previewFrameDelegate = new PreviewFrameDelegate(_cameraModule, this);
                var queue = new DispatchQueue("PreviewFrameQueue");
                _previewFrameOutput.WeakVideoSettings = settings.Dictionary;
                _previewFrameOutput.SetSampleBufferDelegate(_previewFrameDelegate, queue);

                _captureSession.AddOutput(_previewFrameOutput);

                _deviceInput = AVCaptureDeviceInput.FromDevice(_device);
                _captureSession.AddInput(_deviceInput);
            }
            catch (Exception e)
            {
                _cameraModule.Error = e;
            }
        }

        public override void ObserveValue(NSString keyPath, NSObject ofObject, NSDictionary change, IntPtr context)
        {
            _cameraModule.IsFocusCircleLocked = !_device.AdjustingFocus;
        }

        private void StartPreview(object o = null, NSNotificationEventArgs args = null)
        {
            if (_captureSession?.Running == false)
            {
                _captureSession.StartRunning();
            }
        }

        private void StopPreview(object o = null, NSNotificationEventArgs args = null)
        {
            if (_captureSession?.Running == true)
            {
                _captureSession.StopRunning();
            }
        }

        private async void CapturePhoto()
        {
            try
            {
                if (_is10OrHigher)
                {
                    using var photoSettings = AVCapturePhotoSettings.Create();
                    photoSettings.IsHighResolutionPhotoEnabled = true;
                    _photoOutput.CapturePhoto(photoSettings, this);
                }
                else
                {
                    using var videoConnection = _stillImageOutput.ConnectionFromMediaType(AVMediaType.Video);
                    using var sampleBuffer = await _stillImageOutput.CaptureStillImageTaskAsync(videoConnection);

                    if (_cameraModule.PairOperator.IsPrimary ||
                        _cameraModule.PairOperator.PairStatus != PairStatus.Connected)
                    {
                        _cameraModule.CaptureSuccess = !_cameraModule.CaptureSuccess;
                    }

                    if (!(_cameraModule.PairOperator.PairStatus == PairStatus.Connected &&
                        !_cameraModule.PairOperator.IsPrimary))
                    {
                        LockPictureSpecificSettingsIfApplicable();
                    }

                    using var jpegImageAsNsData = AVCaptureStillImageOutput.JpegStillToNSData(sampleBuffer);
                    using var image = UIImage.LoadFromData(jpegImageAsNsData);
                    HandleCapturedPhoto(image.CGImage);
                }
            }
            catch (Exception e)
            {
                _cameraModule.Error = e;
            }
        }

        private void HandleCapturedPhoto(CGImage cgImage)
        {
            using var uiImage = UIImage.FromImage(cgImage, 1, GetOrientationForCorrection());
            var imageBytes = uiImage.AsJPEG().ToArray();

            if (_cameraModule.PairOperator.PairStatus == PairStatus.Connected &&
                !_cameraModule.PairOperator.IsPrimary)
            {
                _cameraModule.PairOperator.SendCapture(imageBytes);
                StopPreview();
            }
            else
            {
                if (_cameraModule.PairOperator.PairStatus == PairStatus.Connected)
                {
                    StopPreview();
                }
                using var stream = new SKMemoryStream(imageBytes);
                using var skData = SKData.Create(stream);
                using var codec = SKCodec.Create(skData);
                _cameraModule.CapturedImage = new IncomingFrame
                {
                    Frame = SKBitmap.Decode(skData),
                    IsFrontFacing = _cameraModule.ChosenCamera.IsFront,
                    Orientation = codec.EncodedOrigin
                };
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
                    _cameraModule.Error = new Exception(error.ToString());
                }
                else if (finishedPhotoBuffer != null)
                {
                    if (!(_cameraModule.PairOperator.PairStatus == PairStatus.Connected &&
                          !_cameraModule.PairOperator.IsPrimary))
                    {
                        LockPictureSpecificSettingsIfApplicable();
                    }

                    using var image = AVCapturePhotoOutput.GetJpegPhotoDataRepresentation(finishedPhotoBuffer, previewPhotoBuffer);
                    using var imgDataProvider = new CGDataProvider(image);
                    HandleCapturedPhoto(CGImage.FromJPEG(imgDataProvider, null, false, CGColorRenderingIntent.Default));
                }
            }
            catch (Exception e)
            {
                _cameraModule.Error = e;
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
                    _cameraModule.Error = new Exception(error.ToString());
                }
                else if (photo != null)
                {
                    if (!(_cameraModule.PairOperator.PairStatus == PairStatus.Connected &&
                          !_cameraModule.PairOperator.IsPrimary))
                    {
                        LockPictureSpecificSettingsIfApplicable();
                    }

                    HandleCapturedPhoto(photo.CGImageRepresentation);
                }
            }
            catch (Exception e)
            {
                _cameraModule.Error = e;
            }
        }

        private static UIImageOrientation GetOrientationForCorrection()
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
            if (_cameraModule.PairOperator.IsPrimary ||
                _cameraModule.PairOperator.PairStatus != PairStatus.Connected)
            {
                _cameraModule.CaptureSuccess = !_cameraModule.CaptureSuccess;
            }
        }
        
        private void PreviewWasTapped(PointF point)
        {
            _cameraModule.IsFocusCircleLocked = false;
            Debug.WriteLine("### single tap pointf: " + point.X + ", " + point.Y);

            CGPoint touchPoint;

            switch (UIDevice.CurrentDevice.Orientation)
            {
                case UIDeviceOrientation.LandscapeRight:
                    touchPoint = new CGPoint(
                        point.Y * _avCaptureVideoPreviewLayer.Frame.Width,
                        (1-point.X) * _avCaptureVideoPreviewLayer.Frame.Height);
                    break;
                case UIDeviceOrientation.LandscapeLeft:
                    touchPoint = new CGPoint(
                        (1-point.Y) * _avCaptureVideoPreviewLayer.Frame.Width,
                        point.X * _avCaptureVideoPreviewLayer.Frame.Height);
                    break;
                case UIDeviceOrientation.Portrait:
                default:
                    touchPoint = new CGPoint(
                        point.X * _avCaptureVideoPreviewLayer.Frame.Width,
                        point.Y * _avCaptureVideoPreviewLayer.Frame.Height);
                    break;
            }

            Debug.WriteLine("### single tap cgpoint: " + point.X + ", " + point.Y);

            if (_cameraModule.IsTapToFocusEnabled)
            {
                var translatedPoint = _avCaptureVideoPreviewLayer.CaptureDevicePointOfInterestForPoint(touchPoint);

                Debug.WriteLine("### translated tap location: " + translatedPoint.X + ", " + translatedPoint.Y);
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

                _device.UnlockForConfiguration();
            }
        }

        private void PreviewWasSwiped(UISwipeGestureRecognizer swipeGesture)
        {
            _cameraModule.WasSwipedTrigger = !_cameraModule.WasSwipedTrigger;
        }

        private void TurnOffFlashAndSetContinuousAutoMode(AVCaptureDevice device)
        {
#if !__SIMULATOR__
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
#endif
        }

        private void FullInit()
        {
            SetupCamera();
            StartPreview();
            if (NativeView != null &&
                _liveCameraStream != null)
            {
                NativeView.Add(_liveCameraStream);
                NativeView.ClipsToBounds = true;
            }
        }

        private void OrientationChanged(NSNotification notification)
        {
            switch (UIDevice.CurrentDevice.Orientation)
            {
                case UIDeviceOrientation.Portrait:
                case UIDeviceOrientation.LandscapeLeft:
                case UIDeviceOrientation.LandscapeRight:
                    _previousValidOrientation = UIDevice.CurrentDevice.Orientation;
                    break;
                default:
                    _previousValidOrientation ??= UIDeviceOrientation.Portrait;
                    break;
            }
        }

        private void SetPreviewSizing()
        {
            var sideWidth = NativeView.Bounds.Width;
            var sideHeight = NativeView.Bounds.Height;
            Debug.WriteLine("### native view: " + sideWidth + " " + sideHeight);
            
            _liveCameraStream = new UIView(new CGRect(0, 0, sideWidth, sideHeight));

            _avCaptureVideoPreviewLayer = new AVCaptureVideoPreviewLayer(_captureSession);
            _liveCameraStream.Layer.AddSublayer(_avCaptureVideoPreviewLayer);

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
            
            if (videoOrientation != 0 &&
                _avCaptureVideoPreviewLayer != null)
            {
                _avCaptureVideoPreviewLayer.Orientation = videoOrientation;
            }
        }

        private class PreviewFrameDelegate : AVCaptureVideoDataOutputSampleBufferDelegate
        {
            private readonly CameraModule _camera;
            private int _readyToCapturePreviewFrameInterlocked;

            public PreviewFrameDelegate(CameraModule camera, CameraModuleRenderer renderer)
            {
                _camera = camera;
                _camera.PairOperator.PreviewFrameRequestReceived += (sender, args) =>
                {
                    if (!renderer._captureSession.Running)
                    {
                        renderer.StartPreview();
                    }
                    _readyToCapturePreviewFrameInterlocked = 1;
                };
            }

            public override void DidOutputSampleBuffer(AVCaptureOutput captureOutput, CMSampleBuffer sampleBuffer, AVCaptureConnection connection)
            {
                try
                {
                    using var image = GetImageFromSampleBuffer(sampleBuffer);
                    var bytes = image.AsJPEG(0).ToArray();
                    using var stream = new SKMemoryStream(image.AsJPEG(0).ToArray());
                    using var skData = SKData.Create(stream);
                    using var codec = SKCodec.Create(skData);
                    _camera.PreviewImage = new IncomingFrame
                    {
                        Frame = SKBitmap.Decode(skData),
                        IsFrontFacing = _camera.ChosenCamera.IsFront,
                        Orientation = codec.EncodedOrigin
                    };

                    if (_camera.PairOperator.PairStatus == PairStatus.Connected)
                    {
                        if (Interlocked.Exchange(ref _readyToCapturePreviewFrameInterlocked, 0) == 1)
                        {
                            _camera.PairOperator.SendLatestPreviewFrame(bytes);
                        }
                    }

                }
                catch (Exception e)
                {
                    Debug.WriteLine("Error sampling buffer: {0}", e.Message);
                }
                finally
                {
                    sampleBuffer.Dispose();
                }
            }

            private static UIImage GetImageFromSampleBuffer(CMSampleBuffer sampleBuffer)
            {
                // Get a pixel buffer from the sample buffer
                using var pixelBuffer = sampleBuffer.GetImageBuffer() as CVPixelBuffer;
                // Lock the base address
                pixelBuffer.Lock(CVPixelBufferLock.None);

                // Prepare to decode buffer
                const CGBitmapFlags FLAGS = CGBitmapFlags.PremultipliedFirst | CGBitmapFlags.ByteOrder32Little;

                // Decode buffer - Create a new colorspace
                using var cs = CGColorSpace.CreateDeviceRGB();
                // Create new context from buffer
                using var context = new CGBitmapContext(pixelBuffer.BaseAddress,
                    pixelBuffer.Width,
                    pixelBuffer.Height,
                    8,
                    pixelBuffer.BytesPerRow,
                    cs,
                    (CGImageAlphaInfo)FLAGS);
                // Get the image from the context
                using var cgImage = context.ToImage();
                // Unlock and return image
                pixelBuffer.Unlock(CVPixelBufferLock.None);
                return UIImage.FromImage(cgImage, new nfloat(0.1), GetOrientationForCorrection());
            }
        }
    }
}