using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Timers;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Hardware;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.Media;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using CrossCam.Droid.CustomRenderer;
using CrossCam.Droid.CustomRenderer.Camera2;
using CrossCam.Model;
using CrossCam.Page;
using CrossCam.ViewModel;
using CrossCam.Wrappers;
using Java.Lang;
using Microsoft.AppCenter.Crashes.Android;
using SkiaSharp;
using SkiaSharp.Views.Android;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;
using Boolean = Java.Lang.Boolean;
using CameraError = Android.Hardware.CameraError;
using CameraModule = CrossCam.CustomElement.CameraModule;
using Exception = System.Exception;
using Extensions = Android.Runtime.Extensions;
using Math = System.Math;
using PointF = System.Drawing.PointF;
using Rect = Android.Graphics.Rect;
using Size = Android.Util.Size;
using View = Android.Views.View;
#pragma warning disable 618
using Camera = Android.Hardware.Camera;

[assembly: ExportRenderer(typeof(CameraModule), typeof(CameraModuleRenderer))]
namespace CrossCam.Droid.CustomRenderer
{
    public sealed class CameraModuleRenderer : ViewRenderer<CameraModule, View>, TextureView.ISurfaceTextureListener, View.IOnTouchListener,
        Camera.IAutoFocusCallback, Camera.IShutterCallback, Camera.IPictureCallback, Camera.IErrorCallback
    {
        private Camera _camera1;
        private View _view;

        private Activity _activity;
        private TextureView _textureView;
        private SurfaceTexture _surfaceTexture;
        private CameraModule _cameraModule;
        private GestureDetector _gestureDetector;

        private readonly float _landscapePreviewAllottedWidth;

        private static Camera.Size _previewSize;
        private static Camera.Size _pictureSize;

        private bool _isRunning;

        private bool _wasCameraRunningBeforeMinimize;
        private bool _wasMinimizeForPermissions;

        private bool _useCamera2;
        private Surface _surface;
        private CameraManager _cameraManager;
        private CameraCaptureSession _camera2Session;
        private CaptureRequest.Builder _previewRequestBuilder;
        private CrossCamCamera2CaptureListener _previewCaptureListener;
        private CrossCamCamera2CaptureListener _finalCaptureListener;
        private CameraStateListener _stateListener;
        private ImageAvailableListener _imageAvailableListener;
        private ImageReader _finalCaptureImageReader;
        private HandlerThread _backgroundThread;
        private Handler _backgroundHandler;
        private string _camera2Id;
        private CameraDevice _camera2Device;
        private Size _preview2Size;
        private Size _picture2Size;
        private MeteringRectangle _camera2MeteringRectangle;
        private int _camera2SensorOrientation;
        private readonly SparseIntArray _orientations = new SparseIntArray();

        private bool _openingCamera2;
        private bool _camera2CameraDeviceErrorRetry;
        private int _camera1initThreadlock;
        private int _readyToCapturePreviewFrameInterlocked;
        private int _camera2FoundGoodCaptureInterlocked;
        private bool _isCamera2FocusAndExposureLocked;

        private CameraState _camera2State;
        private int _cameraRotation1;
        private int _displayRotation1;

        private enum CameraState
        {
            Preview,
            AwaitingPhotoCapture,
            AwaitingTapLock,
            PictureTaken
        }

        public CameraModuleRenderer(Context context) : base(context)
        {
            var maxSize = Math.Max(Resources.DisplayMetrics.HeightPixels, Resources.DisplayMetrics.WidthPixels);
            _landscapePreviewAllottedWidth = maxSize / 2f;

            _orientations.Append((int)SurfaceOrientation.Rotation0, 0);
            _orientations.Append((int)SurfaceOrientation.Rotation90, 90);
            _orientations.Append((int)SurfaceOrientation.Rotation180, 180);
            _orientations.Append((int)SurfaceOrientation.Rotation270, 270);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            {
                _cameraManager = (CameraManager)MainActivity.Instance.GetSystemService(Context.CameraService);
            }
        }

        private void AppWasMinimized(object obj, EventArgs args)
        {
            if ((PlatformPair.ConnectionsPermissionsTask != null &&
                 !PlatformPair.ConnectionsPermissionsTask.Task.IsCompleted) ||
                (PlatformPair.LocationPermissionsTask != null &&
                 !PlatformPair.LocationPermissionsTask.Task.IsCompleted) ||
                (PlatformPair.TurnOnLocationTask != null &&
                 !PlatformPair.TurnOnLocationTask.Task.IsCompleted))
            {
                _wasMinimizeForPermissions = true;
            }
            else
            {
                _wasMinimizeForPermissions = false;
                if (_useCamera2)
                {
                    StopCamera2();
                }
                else
                {
                    if (_isRunning)
                    {
                        StopCamera1();
                        _wasCameraRunningBeforeMinimize = true;
                    }
                    else
                    {
                        _wasCameraRunningBeforeMinimize = false;
                    }
                }
            }
        }

        private void AppWasMaximized(object obj, EventArgs args)
        {
            if (!_wasMinimizeForPermissions)
            {
                if (_useCamera2)
                {
                    OpenCamera2();
                }
                else
                {
                    if (!_isRunning &&
                        _wasCameraRunningBeforeMinimize)
                    {
                        SetupAndStartCamera1();
                    }
                }
            }
        }

        protected override void OnElementChanged(ElementChangedEventArgs<CameraModule> e)
        {
            base.OnElementChanged(e);

            try
            {
                if (e.OldElement != null)
                {
                    MainActivity.Instance.LifecycleEventListener.OrientationChanged -= HandleOrientationChangedEvent;
                    MainActivity.Instance.LifecycleEventListener.AppMaximized -= AppWasMaximized;
                    MainActivity.Instance.LifecycleEventListener.AppMinimized -= AppWasMinimized;
                    _cameraModule.PairOperator.PreviewFrameRequestReceived -= HandlePreviewFrameRequestReceived;
                    _cameraModule.PairOperator.CaptureSyncTimeElapsed -= HandleCaptureSyncTimeElapsed;
                    _cameraModule.SingleTapped -= PreviewSingleTapped;
                    _cameraModule.DoubleTapped -= PreviewDoubleTapped;

                    if (_useCamera2)
                    {
                        _previewCaptureListener.CaptureComplete -= HandlePreviewCaptureComplete;
                        _previewCaptureListener.CaptureProgressed -= HandlePreviewCaptureProgressed;

                        _imageAvailableListener.Photo -= HandleImageAvailablePhoto;
                        _imageAvailableListener.Error -= HandleImageAvailableListenerError;

                        _finalCaptureListener.CaptureComplete -= HandleFinalCaptureComplete;
                    }

                    return;
                }

                if (e.NewElement != null)
                {
                    MainActivity.Instance.LifecycleEventListener.OrientationChanged += HandleOrientationChangedEvent;
                    MainActivity.Instance.LifecycleEventListener.AppMaximized += AppWasMaximized;
                    MainActivity.Instance.LifecycleEventListener.AppMinimized += AppWasMinimized;
                    _cameraModule = e.NewElement;
                    _cameraModule.SingleTapped += PreviewSingleTapped;
                    _cameraModule.DoubleTapped += PreviewDoubleTapped;
                    _cameraModule.PairOperator.PreviewFrameRequestReceived += HandlePreviewFrameRequestReceived;
                    _cameraModule.PairOperator.CaptureSyncTimeElapsed += HandleCaptureSyncTimeElapsed;
                    
                    try
                    {
                        var settings = PersistentStorage.LoadOrDefault(PersistentStorage.SETTINGS_KEY, new Settings());
                        if (Build.VERSION.SdkInt >= BuildVersionCodes.M && !settings.IsForceCamera1Enabled) // camera2 is introduced in 21, but i need the af cancel trigger which is 23
                        {
                            var level = FindCamera2();
                            _useCamera2 = level != (int)InfoSupportedHardwareLevel.Legacy || settings.IsForceCamera2Enabled;
                        }
                    }
                    catch (Exception ex)
                    {
                        _cameraModule.Error = ex;
                        _useCamera2 = false;
                    }

                    if (_useCamera2)
                    {
                        _previewCaptureListener = new PreviewCamera2CaptureListener();
                        _previewCaptureListener.CaptureComplete += HandlePreviewCaptureComplete;
                        _previewCaptureListener.CaptureProgressed += HandlePreviewCaptureProgressed;

                        _imageAvailableListener = new ImageAvailableListener();
                        _imageAvailableListener.Photo += HandleImageAvailablePhoto;
                        _imageAvailableListener.Error += HandleImageAvailableListenerError;

                        _finalCaptureListener = new FinalCamera2CaptureListener();
                        _finalCaptureListener.CaptureComplete += HandleFinalCaptureComplete;
                    }

                    System.Diagnostics.Debug.WriteLine("### USE CAMERA2: " + _useCamera2);

                    SetupUserInterface();
                }
            }
            catch (Exception ex)
            {
                _cameraModule.Error = ex;
            }
        }

        private void HandlePreviewCaptureComplete(object sender, CrossCamCamera2CaptureListener.CameraCaptureListenerEventArgs args)
        {
            HandleCaptureResult(args.CaptureRequest, args.CaptureResult);
        }

        private void HandlePreviewCaptureProgressed(object sender, CrossCamCamera2CaptureListener.CameraCaptureListenerEventArgs args)
        {
            HandleCaptureResult(args.CaptureRequest, args.CaptureResult);
        }

        private void HandlePreviewFrameRequestReceived(object sender, EventArgs e)
        {
            _readyToCapturePreviewFrameInterlocked = 1;
            if (_useCamera2)
            {
                if (_camera2Session == null)
                {
                    OpenCamera2();
                }
            }
            else
            {
                if (!_isRunning)
                {
                    SetupAndStartCamera1();
                }
            }
        }

        private void HandleCaptureSyncTimeElapsed(object sender, ElapsedEventArgs e)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                TakePhotoButtonTapped(true);
            });
        }

        protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            base.OnElementPropertyChanged(sender, e);

            try
            {
                if (e.PropertyName == nameof(_cameraModule.Width) ||
                    e.PropertyName == nameof(_cameraModule.Height))
                {
                    SetOrientation();
                }

                if (e.PropertyName == nameof(_cameraModule.CaptureTrigger))
                {
                    if (_cameraModule.IsVisible)
                    {
                        TakePhotoButtonTapped();
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
                    TurnOnContinuousFocus();
                }

                if (e.PropertyName == nameof(_cameraModule.ChosenCamera))
                {
                    if (_useCamera2)
                    {
                        StopCamera2();
                        FindCamera2();
                        SetOrientation();
                        OpenCamera2();
                    }
                    else
                    {
                        StopCamera1();
                        SetupAndStartCamera1(null, true);
                    }
                }

                if (e.PropertyName == nameof(_cameraModule.StopPreviewTrigger))
                {
                    if (_useCamera2)
                    {
                        StopCamera2();
                    }
                    else
                    {
                        StopCamera1();
                    }
                }

                if (e.PropertyName == nameof(_cameraModule.RestartPreviewTrigger))
                {
                    System.Diagnostics.Debug.WriteLine("### Restarting preview");
                    if (_useCamera2)
                    {
                        OpenCamera2();
                    }
                    else
                    {
                        if (!_isRunning)
                        {
                            SetupAndStartCamera1();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _cameraModule.Error = ex;
            }
        }

        private void TurnOnContinuousFocus(Camera.Parameters providedParameters = null)
        {
            try
            {
                _cameraModule.IsFocusCircleLocked = false;
                if (_useCamera2)
                {
                    RestartPreview2(false);
                }
                else
                {
                    Camera.Parameters parameters;
                    if (providedParameters != null)
                    {
                        parameters = providedParameters;
                    }
                    else if (_camera1 == null)
                    {
                        return;
                    }
                    else
                    {
                        parameters = _camera1.GetParameters();
                    }

                    if (parameters.SupportedFocusModes != null &&
                        parameters.SupportedFocusModes.Contains(Camera.Parameters.FocusModeContinuousPicture))
                    {
                        parameters.FocusMode = Camera.Parameters.FocusModeContinuousPicture;
                    }

                    if (parameters.SupportedWhiteBalance != null &&
                        parameters.SupportedWhiteBalance.Contains(Camera.Parameters.WhiteBalanceAuto))
                    {
                        parameters.WhiteBalance = Camera.Parameters.WhiteBalanceAuto;
                    }

                    if (parameters.SupportedSceneModes != null &&
                        parameters.SupportedSceneModes.Contains(Camera.Parameters.SceneModeAuto))
                    {
                        parameters.SceneMode = Camera.Parameters.SceneModeAuto;
                    }

                    if (parameters.IsAutoWhiteBalanceLockSupported)
                    {
                        parameters.AutoWhiteBalanceLock = false;
                    }

                    if (parameters.IsAutoExposureLockSupported)
                    {
                        parameters.AutoExposureLock = false;
                    }

                    parameters.FocusAreas = null;
                    parameters.MeteringAreas = null;

                    if (providedParameters == null)
                    {
                        _camera1.SetParameters(parameters);
                    }
                }
            }
            catch (Exception e)
            {
                _cameraModule.Error = e;
            }
        }

        private void SetupUserInterface()
        {
            _activity = Context as Activity;
            _view = _activity.LayoutInflater.Inflate(Resource.Layout.CameraLayout, this, false);

            _textureView = _view.FindViewById<TextureView>(Resource.Id.textureView);
            _textureView.SurfaceTextureListener = this;
            _textureView.SetOnTouchListener(this);

            AddView(_view);
        }

        protected override void OnLayout(bool changed, int l, int t, int r, int b)
        {
            base.OnLayout(changed, l, t, r, b);

            var msw = MeasureSpec.MakeMeasureSpec(r - l, MeasureSpecMode.Exactly);
            var msh = MeasureSpec.MakeMeasureSpec(b - t, MeasureSpecMode.Exactly);

            _view.Measure(msw, msh);
            _view.Layout(0, 0, r - l, b - t);
        }

        public void OnSurfaceTextureUpdated(SurfaceTexture surface)
        {
            if (!_textureView.IsAvailable) return;

            Bitmap bitmap;
            try
            {
                bitmap = _textureView.Bitmap;
                //_textureView.GetBitmap(0, 0); // TODO: can downsize here, good?
            }
            catch (Error e)
            {
                Crashes.TrackError(e);
                return;
            }
            var origin = _useCamera2 ? GetOrientationCamera2() : 0;

            if (_cameraModule.PairOperator.PairStatus == PairStatus.Connected &&
                Interlocked.Exchange(ref _readyToCapturePreviewFrameInterlocked, 0) == 1 &&
                bitmap != null)
            {
                using var stream = new MemoryStream();
                bitmap.Compress(Bitmap.CompressFormat.Jpeg, 50, stream);
                _cameraModule.PairOperator.SendLatestPreviewFrame(stream.ToArray(), (byte) origin);
            }

            _cameraModule.PreviewImage = new IncomingFrame
            {
                Frame = bitmap.ToSKBitmap(),
                IsFrontFacing = _cameraModule.ChosenCamera.IsFront,
                Orientation = origin
            };
            bitmap?.Recycle();
            bitmap?.Dispose();
        }
        
        private SKEncodedOrigin GetOrientationCamera2()
        {
            var screenOrientation = (int)DeviceDisplay.MainDisplayInfo.Rotation - 1;
            var sensorOrientationInc = _camera2SensorOrientation / 90;
            if (_cameraModule.ChosenCamera.IsFront)
            {
                sensorOrientationInc += 2;
            }

            var normalizedRotationSum = (sensorOrientationInc + screenOrientation) % 4;
            var isFrontFacing = _cameraModule.ChosenCamera.IsFront;
            return normalizedRotationSum switch
            {
                0 when isFrontFacing => SKEncodedOrigin.LeftTop,
                0 => SKEncodedOrigin.RightTop,
                1 when isFrontFacing => SKEncodedOrigin.BottomLeft,
                1 => SKEncodedOrigin.TopLeft,
                2 when isFrontFacing => SKEncodedOrigin.RightBottom,
                2 => SKEncodedOrigin.LeftBottom,
                3 when isFrontFacing => SKEncodedOrigin.TopRight,
                3 => SKEncodedOrigin.BottomRight,
                _ => default
            };
        }

        public void OnSurfaceTextureAvailable(SurfaceTexture surface, int width, int height)
        {
            _textureView.LayoutParameters = new FrameLayout.LayoutParams(width, height);
            _surfaceTexture = surface;

            if (_useCamera2)
            {
                SetOrientation();
                OpenCamera2();
            }
            else
            {
                SetupAndStartCamera1(surface);
            }
        }

        public bool OnSurfaceTextureDestroyed(SurfaceTexture surface)
        {
            if (_useCamera2)
            {
                StopCamera2();
            }
            else
            {
                StopCamera1();
            }

            _surfaceTexture = null;
            return true;
        }

        public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height) { }

        private void HandleOrientationChangedEvent(object sender, EventArgs eventArgs)
        {
            SetOrientation();
        }

        private void TakePhotoButtonTapped(bool isSyncReentry = false)
        {
            if (_cameraModule.PairOperator.PairStatus == PairStatus.Connected &&
                _cameraModule.PairOperator.IsPrimary &&
                !isSyncReentry)
            {
                _cameraModule.PairOperator.BeginSyncedCapture();
                return;
            }
            if (_useCamera2)
            {
                StartRealCapture2();
            }
            else
            {
                try
                {
                    _camera1.TakePicture(this, this, this, this);
                }
                catch
                {
                    //user can try again
                }
            }
        }

        public bool OnTouch(View v, MotionEvent e)
        {
            return _gestureDetector.OnTouchEvent(e);
        }

        private void PreviewDoubleTapped(object sender, EventArgs eventArgs)
        {
            TurnOnContinuousFocus();
        }

        private void PreviewSingleTapped(object sender, PointF p)
        {
            try
            {
                _cameraModule.IsFocusCircleLocked = false;
                if (_cameraModule.IsTapToFocusEnabled)
                {
                    if (_useCamera2)
                    {
                        TapToFocus2(p);
                    }
                    else
                    {
                        var metrics = new DisplayMetrics();
                        Display.GetMetrics(metrics);

                        var tapRadius = (float)CameraPage.FOCUS_CIRCLE_WIDTH * metrics.Density / 2f;

                        var tapX = Clamp(p.X, tapRadius, _textureView.Width - tapRadius);
                        var tapY = Clamp(p.Y, tapRadius, _textureView.Height - tapRadius);

                        var tapRect = new Rect(
                            (int)(tapX - tapRadius),
                            (int)(tapY - tapRadius),
                            (int)(tapX + tapRadius),
                            (int)(tapY + tapRadius)); //TODO: this thing is going to come out rectangular, not square OH WELL

                        var parameters = _camera1.GetParameters();
                        var targetFocusRect = new RectF(
                            tapRect.Left * 2000 / _textureView.Width - 1000,
                            tapRect.Top * 2000 / _textureView.Height - 1000,
                            tapRect.Right * 2000 / _textureView.Width - 1000,
                            tapRect.Bottom * 2000 / _textureView.Height - 1000);

                        var matrix = new Matrix();

                        switch (Display.Rotation)
                        {
                            case SurfaceOrientation.Rotation0: //portrait
                                matrix.SetRotate(270);
                                break;
                            case SurfaceOrientation.Rotation180: //portrait inverted
                                matrix.SetRotate(90);
                                break;
                            case SurfaceOrientation.Rotation270: //landscape right
                                matrix.SetRotate(180);
                                break;
                            case SurfaceOrientation.Rotation90: //landscape left
                                matrix.SetRotate(0);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        matrix.MapRect(targetFocusRect);
                        var roundedRect = new Rect();
                        targetFocusRect.Round(roundedRect);

                        if (parameters.MaxNumFocusAreas > 0 &&
                            parameters.SupportedFocusModes != null &&
                            parameters.SupportedFocusModes.Contains(Camera.Parameters.FocusModeAuto))
                        {
                            parameters.FocusMode = Camera.Parameters.FocusModeAuto;
                            parameters.FocusAreas = new List<Camera.Area> { new Camera.Area(roundedRect, 1000) };
                        }

                        if (parameters.MaxNumMeteringAreas > 0)
                        {
                            parameters.MeteringAreas = new List<Camera.Area> { new Camera.Area(roundedRect, 1000) };
                        }

                        if (parameters.IsAutoExposureLockSupported)
                        {
                            //parameters.AutoExposureLock = true; //TODO: use this properly... it needs to finish adjusting exposure and then lock there, and also automatically unlock/relock with next tap.. or does it (really need to lock)?
                        }

                        _camera1.SetParameters(parameters);

                        _camera1.AutoFocus(this);
                    }
                }
            }
            catch (Exception ex)
            {
                _cameraModule.Error = ex;
            }
        }

        private static float Clamp(float x, float min, float max)
        {
            return x < min ? min : x < max ? x : max;
        }

        public void OnError(CameraError error, Camera camera)
        {
            _cameraModule.Error = new Exception(error.ToString());
        }

        private void SetOrientation()
        {
            try
            {
                if (_textureView == null || _surfaceTexture == null) return;

                var metrics = new DisplayMetrics();
                if (Display == null) return;
                Display.GetMetrics(metrics);

                var moduleWidth = (float)(_cameraModule.Width * metrics.Density);
                var moduleHeight = (float)(_cameraModule.Height * metrics.Density);

                float previewSizeWidth;
                float previewSizeHeight;
                int rotation2;

                if (_useCamera2)
                {
                    previewSizeWidth = _preview2Size.Width;
                    previewSizeHeight = _preview2Size.Height;
                }
                else
                {
                    previewSizeWidth = _previewSize.Width;
                    previewSizeHeight = _previewSize.Height;
                }

                float xAdjust2;
                float yAdjust2;
                float previewWidth2;
                float previewHeight2;
                float verticalOffset;

                float proportionalPreviewHeight;
                switch (Display.Rotation)
                {
                    case SurfaceOrientation.Rotation0:
                        proportionalPreviewHeight = previewSizeWidth * moduleWidth / previewSizeHeight;
                        _cameraRotation1 = _displayRotation1 = 90;
                        rotation2 = (_camera2SensorOrientation + 270) % 360;
                        verticalOffset = (moduleHeight - proportionalPreviewHeight) / 2f;
                        xAdjust2 = 0;
                        yAdjust2 = verticalOffset;
                        previewWidth2 = moduleWidth;
                        previewHeight2 = proportionalPreviewHeight;
                        break;
                    case SurfaceOrientation.Rotation90:
                        proportionalPreviewHeight = previewSizeHeight * moduleWidth / previewSizeWidth;
                        if (_cameraModule.ChosenCamera.IsFront)
                        {
                            _cameraRotation1 = 180;
                            _displayRotation1 = 0;
                        }
                        else
                        {
                            _cameraRotation1 = _displayRotation1 = 0;
                        }
                        rotation2 = (_camera2SensorOrientation + 180) % 360;
                        verticalOffset = (moduleHeight - proportionalPreviewHeight) / 2f;
                        xAdjust2 = 0;
                        yAdjust2 = proportionalPreviewHeight + verticalOffset;
                        previewWidth2 = proportionalPreviewHeight;
                        previewHeight2 = moduleWidth;
                        break;
                    case SurfaceOrientation.Rotation180:
                        proportionalPreviewHeight = previewSizeWidth * moduleWidth / previewSizeHeight;
                        _cameraRotation1 = _displayRotation1 = 270;
                        rotation2 = (_camera2SensorOrientation + 90) % 360;
                        verticalOffset = (moduleHeight - proportionalPreviewHeight) / 2f;
                        xAdjust2 = moduleWidth;
                        yAdjust2 = verticalOffset + proportionalPreviewHeight;
                        previewWidth2 = moduleWidth;
                        previewHeight2 = proportionalPreviewHeight;
                        break;
                    case SurfaceOrientation.Rotation270:
                    default:
                        proportionalPreviewHeight = previewSizeHeight * moduleWidth / previewSizeWidth;
                        if (_cameraModule.ChosenCamera.IsFront)
                        {
                            _cameraRotation1 = 0;
                            _displayRotation1 = 180;
                        }
                        else
                        {
                            _cameraRotation1 = _displayRotation1 = 180;
                        }
                        rotation2 = _camera2SensorOrientation;
                        verticalOffset = (moduleHeight - proportionalPreviewHeight) / 2f;
                        xAdjust2 = moduleWidth;
                        yAdjust2 = verticalOffset;
                        previewWidth2 = proportionalPreviewHeight;
                        previewHeight2 = moduleWidth;
                        break;
                }

                if (_useCamera2)
                {
                    if (_cameraModule.ChosenCamera.IsFront)
                    {
                        rotation2 += 180;
                    }
                    _textureView.PivotX = 0;
                    _textureView.PivotY = 0;
                    _textureView.Rotation = rotation2;
                }
                else
                {
                    if (_camera1 == null) return;
                    var parameters = _camera1.GetParameters();
                    parameters.SetRotation(_cameraRotation1);
                    _camera1.SetDisplayOrientation(_displayRotation1);
                    _camera1.SetParameters(parameters);
                }

                _cameraModule.PreviewBottomY = (moduleHeight - verticalOffset) / metrics.Density;

                if (_useCamera2)
                {
                    _textureView.SetX(xAdjust2);
                    _textureView.SetY(yAdjust2 - 10000);
                    _textureView.LayoutParameters = new FrameLayout.LayoutParams((int)Math.Round(previewWidth2),
                        (int)Math.Round(previewHeight2));
                }
                else
                {
                    _textureView.SetX(0);
                    _textureView.SetY(verticalOffset - 10000);
                    _textureView.LayoutParameters = new FrameLayout.LayoutParams((int)Math.Round(moduleWidth),
                        (int)Math.Round(proportionalPreviewHeight));
                }
            }
            catch (System.Exception e)
            {
                _cameraModule.Error = e;
            }
        }

        #region camera1

        private void StopCamera1()
        {
            if (_isRunning)
            {
                _camera1?.StopPreview();
                _camera1?.Release();
                _camera1 = null;
                _isRunning = false;
            }
        }

        private void StartCamera1()
        {
            if (!_isRunning)
            {
                _camera1?.StartPreview();
                _isRunning = true;
            }
        }

        private void SetupAndStartCamera1(SurfaceTexture surface = null, bool restart = false)
        {
            try
            {
                if (_surfaceTexture != null)
                {
                    if ((_camera1 == null || restart))
                    {
                        if (Interlocked.Exchange(ref _camera1initThreadlock, 1) == 1) return;

                        if (!_cameraModule.AvailableCameras.Any())
                        {
                            var backCamera = new AvailableCamera
                            {
                                CameraId = "0",
                                IsFront = false
                            };
                            _cameraModule.AvailableCameras.Add(backCamera);
                            _cameraModule.AvailableCameras.Add(new AvailableCamera
                            {
                                CameraId = "1",
                                IsFront = true
                            });
                            if (_cameraModule.ChosenCamera == null)
                            {
                                _cameraModule.ChosenCamera = backCamera;
                            }
                        }
                        _camera1 = _cameraModule.ChosenCamera.IsFront
                            ? Camera.Open((int)CameraFacing.Front)
                            : Camera.Open((int)CameraFacing.Back);
                        _camera1.SetErrorCallback(this);

                        for (var ii = 0; ii < Camera.NumberOfCameras - 1; ii++)
                        {
                            var info = new Camera.CameraInfo();
                            Camera.GetCameraInfo(ii, info);
                            if (info.CanDisableShutterSound)
                            {
                                _camera1.EnableShutterSound(false);
                            }
                        }

                        var parameters = _camera1.GetParameters();
                        if (parameters.SupportedFlashModes != null &&
                            parameters.SupportedFlashModes.Contains(Camera.Parameters.FlashModeOff))
                        {
                            parameters.FlashMode = Camera.Parameters.FlashModeOff;
                        }
                        if (parameters.IsVideoStabilizationSupported)
                        {
                            parameters.VideoStabilization = false;
                        }
                        parameters.JpegQuality = 100;
                        TurnOnContinuousFocus(parameters);

                        _pictureSize = parameters
                            .SupportedPictureSizes
                            .Where(p => p.Width > p.Height)
                            .OrderByDescending(p => p.Width * p.Height).First();
                        var pictureAspectRatio = _pictureSize.Width / (1f * _pictureSize.Height);

                        var previewSizes = parameters
                            .SupportedPreviewSizes
                            .Where(p => p.Width > p.Height)
                            .OrderByDescending(p => p.Width * p.Height)
                            .ToList();

                        var bigger = new List<Camera.Size>();
                        var smaller = new List<Camera.Size>();

                        foreach (var previewSize in previewSizes)
                        {
                            if (Math.Abs(previewSize.Width / (1f * previewSize.Height) - pictureAspectRatio) < 0.01)
                            {
                                if (previewSize.Width >= _landscapePreviewAllottedWidth)
                                {
                                    bigger.Add(previewSize);
                                }
                                else
                                {
                                    smaller.Add(previewSize);
                                }
                            }
                        }

                        if (bigger.Any())
                        {
                            _previewSize = bigger.Last();
                        }
                        else if (smaller.Any())
                        {
                            _previewSize = smaller.First();
                        }

                        if (_previewSize == null)
                        {
                            _previewSize = previewSizes.First();
                        }

                        _cameraModule.PreviewAspectRatio =
                            Math.Max(_pictureSize.Width, _pictureSize.Height) /
                            (Math.Min(_pictureSize.Width, _pictureSize.Height) * 1d);
                        parameters.SetPictureSize(_pictureSize.Width, _pictureSize.Height);
                        parameters.SetPreviewSize(_previewSize.Width, _previewSize.Height);

                        _surfaceTexture.SetDefaultBufferSize(_previewSize.Width, _previewSize.Height);

                        _camera1.SetParameters(parameters);
                        _camera1.SetPreviewTexture(_surfaceTexture);

                        _camera1initThreadlock = 0;
                    }

                    if (surface != null)
                    {
                        _surfaceTexture = surface;
                    }

                    SetOrientation();
                    StartCamera1();
                }
            }
            catch (System.Exception e)
            {
                _cameraModule.Error = e;
            }
        }

        public void OnShutter()
        {
            if (_cameraModule.PairOperator.IsPrimary ||
                _cameraModule.PairOperator.PairStatus != PairStatus.Connected)
            {
                _cameraModule.CaptureSuccess = !_cameraModule.CaptureSuccess;
            }
        }

        public void OnPictureTaken(byte[] data, Camera camera)
        {
            if (data != null)
            {
                try
                {
                    TurnOnFocusLockIfApplicable1();
                    var wasPreviewRestarted = false;
                    try
                    {
                        _camera1.StartPreview();
                        wasPreviewRestarted = true;
                    }
                    catch
                    {
                        // restarting preview failed, try again later, some devices are just weird
                    }

                    if (_cameraModule.PairOperator.PairStatus == PairStatus.Connected &&
                        !_cameraModule.PairOperator.IsPrimary)
                    {
                        _cameraModule.PairOperator.SendCapture(data);
                        StopCamera1();
                    }
                    else
                    {
                        using var stream = new SKMemoryStream(data);
                        using var codec = SKCodec.Create(stream);

                        _cameraModule.CapturedImage = new IncomingFrame
                        {
                            Frame = SKBitmap.Decode(codec),
                            Orientation = codec.EncodedOrigin,
                            IsFrontFacing = _cameraModule.ChosenCamera.IsFront
                        };
                    }

                    if (!wasPreviewRestarted)
                    {
                        try
                        {
                            _camera1.StartPreview();
                        }
                        catch
                        {
                            // i hope it worked. it keeps erroring for one user and getting in the way and although there is an error, there is no undesirable operation! so be quiet.
                        }
                    }
                }
                catch (System.Exception e)
                {
                    _cameraModule.Error = e;
                }
            }
        }

        private void TurnOnFocusLockIfApplicable1()
        {
            if (_cameraModule.IsNothingCaptured && _cameraModule.IsLockToFirstEnabled)
            {
                var parameters = _camera1.GetParameters();

                if (parameters.SupportedFocusModes != null &&
                    parameters.SupportedFocusModes.Contains(Camera.Parameters.FocusModeFixed))
                {
                    parameters.FocusMode = Camera.Parameters.FocusModeFixed;
                }
                if (parameters.IsAutoExposureLockSupported)
                {
                    parameters.AutoExposureLock = true;
                }
                if (parameters.IsAutoWhiteBalanceLockSupported)
                {
                    parameters.AutoWhiteBalanceLock = true;
                }

                _camera1.SetParameters(parameters);
            }
        }

        public void OnAutoFocus(bool success, Camera camera)
        {
            _cameraModule.IsFocusCircleLocked = success;
        }

        #endregion

        #region camera2

        private int FindCamera2()
        {
            var cameraIds = _cameraManager.GetCameraIdList();
            if (!_cameraModule.AvailableCameras.Any())
            {
                foreach (var cameraId in cameraIds)
                {
                    var cameraChars = _cameraManager.GetCameraCharacteristics(cameraId);
                    var direction = (int)cameraChars.Get(CameraCharacteristics.LensFacing);
                    _cameraModule.AvailableCameras.Add(new AvailableCamera
                    {
                        CameraId = cameraId,
                        IsFront = direction == (int)LensFacing.Front
                    });
                }
            }

            if (_cameraModule.ChosenCamera == null)
            {
                _cameraModule.ChosenCamera = _cameraModule.AvailableCameras.FirstOrDefault(c => !c.IsFront);
                _camera2Id = _cameraModule.ChosenCamera?.CameraId;
            }
            else
            {
                _camera2Id = _cameraModule.ChosenCamera.CameraId;
            }

            if (_camera2Id == null)
            {
                return (int)InfoSupportedHardwareLevel.Legacy;
            }

            var characteristics = _cameraManager.GetCameraCharacteristics(_camera2Id);
            var level = (int)characteristics.Get(CameraCharacteristics.InfoSupportedHardwareLevel);

            if (level == (int)InfoSupportedHardwareLevel.Legacy)
            {
                return level;
            }

            _camera2SensorOrientation = (int)characteristics.Get(CameraCharacteristics.SensorOrientation);

            var map = (StreamConfigurationMap)characteristics.Get(CameraCharacteristics
                .ScalerStreamConfigurationMap);

            var highResSizes = map.GetHighResolutionOutputSizes((int)ImageFormatType.Jpeg)?.Where(p => p.Width > p.Height).ToList();
            var normalSizes = map.GetOutputSizes((int)ImageFormatType.Jpeg).Where(p => p.Width > p.Height).ToList();

            var allSizes = highResSizes != null ? highResSizes.Concat(normalSizes) : normalSizes;
            _picture2Size = allSizes.OrderByDescending(s => s.Width * s.Height).First();
            var pictureAspectRatio = _picture2Size.Width / (1f * _picture2Size.Height);

            var previewSizes = map.GetOutputSizes(Class.FromType(typeof(SurfaceTexture))).Where(p => p.Width > p.Height)
                .OrderByDescending(s => s.Width * s.Height).ToList();

            var bigger = new List<Size>();
            var smaller = new List<Size>();

            foreach (var previewSize in previewSizes)
            {
                if (Math.Abs(previewSize.Width / (1f * previewSize.Height) - pictureAspectRatio) < 0.01)
                {
                    if (previewSize.Width >= _landscapePreviewAllottedWidth)
                    {
                        bigger.Add(previewSize);
                    }
                    else
                    {
                        smaller.Add(previewSize);
                    }
                }
            }

            if (bigger.Any())
            {
                _preview2Size = bigger.Last();
            }
            else if (smaller.Any())
            {
                _preview2Size = smaller.First();
            }

            if (_preview2Size == null)
            {
                return (int)InfoSupportedHardwareLevel.Legacy; // cannot find appropriate sizes with camera2. fall back to camera1.
            }

            _cameraModule.PreviewAspectRatio = 
                Math.Max(_preview2Size.Width, _preview2Size.Height) /
                (Math.Min(_preview2Size.Width, _preview2Size.Height) * 1d);
            _stateListener = new CameraStateListener(this);

            return level;
        }

        private void OpenCamera2()
        {
            try
            {
                if (_openingCamera2 || 
                    _surfaceTexture == null || 
                    _camera2Id == null)
                {
                    //System.Diagnostics.Debug.WriteLine("### opening camera averted because not initialized fully");
                    return;
                }

                if (_camera2Session != null)
                {
                    //System.Diagnostics.Debug.WriteLine("### opening camera averted because session already running");
                    return;
                }

                _openingCamera2 = true;
                _camera2FoundGoodCaptureInterlocked = 0;
                if (_surface == null)
                {
                    _surface = new Surface(_surfaceTexture);
                }

                StartBackgroundThread();
                _cameraManager.OpenCamera(_camera2Id, _stateListener, null);
            }
            catch (Exception e)
            {
                _cameraModule.Error = e;
            }
        }

        private void StopCamera2()
        {
            _camera2Session?.Close();
            _camera2Session = null;
            _camera2Device?.Close();
            _camera2Device = null;
            StopBackgroundThread();
        }

        public void CreateCamera2PreviewSession(CameraDevice camera = null)
        {
            try
            {
                if (camera == null && _camera2Device == null) return;

                if (_surfaceTexture == null) return;

                if (camera != null)
                {
                    _camera2Device = camera;
                }

                _surfaceTexture.SetDefaultBufferSize(_preview2Size.Width, _preview2Size.Height);

                _finalCaptureImageReader?.SetOnImageAvailableListener(null,null);
                _finalCaptureImageReader = ImageReader.NewInstance(_picture2Size.Width, _picture2Size.Height, ImageFormatType.Jpeg, 1);
                _finalCaptureImageReader.SetOnImageAvailableListener(_imageAvailableListener, _backgroundHandler);

                _camera2Device.CreateCaptureSession(new List<Surface> { _surface, _finalCaptureImageReader.Surface },
                    new CameraCaptureStateListener
                    {
                        OnConfigureFailedAction = session => { },
                        OnConfiguredAction = session =>
                        {
                            try
                            {
                                if (_camera2Device == null) return;

                                _camera2Session = session;
                                _previewRequestBuilder = _camera2Device.CreateCaptureRequest(CameraTemplate.Preview);
                                _previewRequestBuilder.AddTarget(_surface);

                                _camera2State = CameraState.Preview;
                                _previewRequestBuilder.SetTag(CameraState.Preview.ToString());

                                _previewRequestBuilder.Set(CaptureRequest.ControlAfMode,
                                    new Integer((int) ControlAFMode.ContinuousPicture));

                                session.SetRepeatingRequest(_previewRequestBuilder.Build(), _previewCaptureListener,
                                    _backgroundHandler);
                            }
                            catch (Exception ex)
                            {
                                _cameraModule.Error = ex;
                            }
                        }
                    },
                    null);
                _openingCamera2 = false;
            }
            catch (Exception e)
            {
                _cameraModule.Error = e;
            }
        }

        private void HandleCaptureResult(CaptureRequest request, CaptureResult result)
        {
            if (_camera2Device == null) return;

            try
            {
#if DEBUG
                //var afStateDebug = result.Get(CaptureResult.ControlAfState);
                //var aeStateDebug = result.Get(CaptureResult.ControlAeState);
                //ControlAFState afStateEnumDebug = 0;
                //if (afStateDebug != null)
                //{
                //    afStateEnumDebug = (ControlAFState)(int)afStateDebug;
                //}

                //ControlAEState aeStateEnumDebug = 0;
                //if (aeStateDebug != null)
                //{
                //    aeStateEnumDebug = (ControlAEState)(int)aeStateDebug;
                //}

                //System.Diagnostics.Debug.WriteLine("CAMERA 2 FRAME, state: " + _camera2State +
                //                                   " tag: " + request.Tag +
                //                                   " afstate: " + afStateEnumDebug +
                //                                   " aestate: " + aeStateEnumDebug);
#endif

                if (_camera2State == CameraState.Preview ||
                    _camera2State == CameraState.PictureTaken ||
                    _camera2State.ToString() != request.Tag?.ToString())
                {
                    if (_camera2State != CameraState.Preview &&
                        _camera2State != CameraState.PictureTaken)
                    {
                        //System.Diagnostics.Debug.WriteLine("### _camera2State: " + _camera2State + " requestTag: " + request.Tag);
                    }
                    return;
                }

                var afState = result.Get(CaptureResult.ControlAfState);
                var aeState = result.Get(CaptureResult.ControlAeState);
                ControlAFState afStateEnum = 0;
                if (afState != null)
                {
                    afStateEnum = (ControlAFState)(int)afState;
                }

                ControlAEState aeStateEnum = 0;
                if (aeState != null)
                {
                    aeStateEnum = (ControlAEState)(int)aeState;
                }

                if (afState != null &&
                    (afStateEnum == ControlAFState.PassiveScan ||
                     afStateEnum == ControlAFState.ActiveScan)
                    ||
                    aeState != null &&
                    aeStateEnum == ControlAEState.Searching) // the capture is still progressing
                {
                    return;
                }

                if ((afState == null ||
                    aeState == null) && // the capture wasn't clearly bad, but it's still progressing so we can't be sure it's good
                    !(result is TotalCaptureResult)) // some devices will have these be null even on total captures sometimes
                {
                    return;
                }

                if (Interlocked.Exchange(ref _camera2FoundGoodCaptureInterlocked, 1) == 0)
                {
                    switch (_camera2State)
                    {
                        case CameraState.AwaitingPhotoCapture:
                            CaptureStillPicture2();
                            break;
                        case CameraState.AwaitingTapLock:
                            {
                                if ((ControlAFMode)(int)request.Get(CaptureRequest.ControlAfMode) ==
                                    ControlAFMode.ContinuousPicture)
                                {
                                    _camera2FoundGoodCaptureInterlocked = 0;
                                    return;
                                }

                                _isCamera2FocusAndExposureLocked = true;
                                _cameraModule.IsFocusCircleLocked = true;
                                
                                _camera2Session.StopRepeating();

                                var characteristics = _cameraManager.GetCameraCharacteristics(_camera2Device.Id);
                                TurnOnAeLock(characteristics);

                                _previewRequestBuilder.Set(CaptureRequest.ControlAfTrigger, new Integer((int)ControlAFTrigger.Cancel));
                                _camera2Session.Capture(_previewRequestBuilder.Build(), _previewCaptureListener,
                                    _backgroundHandler);

                                _previewRequestBuilder.Set(CaptureRequest.ControlAfTrigger, null);
                                _previewRequestBuilder.Set(CaptureRequest.ControlAePrecaptureTrigger, null);

                                _camera2State = CameraState.Preview;
                                _previewRequestBuilder.SetTag(CameraState.Preview.ToString());
                                
                                _camera2Session.SetRepeatingRequest(_previewRequestBuilder.Build(), _previewCaptureListener,
                            _backgroundHandler);

                                break;
                            }
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
            catch (Exception e)
            {
                _cameraModule.Error = e;
            }
        }

        private void StartRealCapture2()
        {
            if (_camera2Device == null) return;

            try
            {
                //System.Diagnostics.Debug.WriteLine("### startRealCapture2, isFocusLocked: " + _isCamera2FocusAndExposureLocked);
                if (_isCamera2FocusAndExposureLocked)
                {
                    CaptureStillPicture2();
                }
                else
                {
                    _camera2State = CameraState.AwaitingPhotoCapture;
                    _previewRequestBuilder.SetTag(CameraState.AwaitingPhotoCapture.ToString());
                    
                    _camera2Session.SetRepeatingRequest(_previewRequestBuilder.Build(), _previewCaptureListener, _backgroundHandler);
                }
            }
            catch (System.Exception e)
            {
                _cameraModule.Error = e;
            }
        }

        public void Camera2Disconnected(CameraDevice camera)
        {
            camera.Close();
            _camera2Device = null;
            _openingCamera2 = false;
        }

        public void Camera2Errored(CameraDevice camera, Android.Hardware.Camera2.CameraError error)
        {
            _openingCamera2 = false;
            if (error == Android.Hardware.Camera2.CameraError.CameraDevice &&
                !_camera2CameraDeviceErrorRetry) // docs say try again: https://developer.android.com/reference/android/hardware/camera2/CameraDevice.StateCallback#ERROR_CAMERA_DEVICE
            {
                _camera2CameraDeviceErrorRetry = true;
                camera.Close();
                OpenCamera2();
                return;
            }

            if (error == Android.Hardware.Camera2.CameraError.CameraDevice &&
                _camera2CameraDeviceErrorRetry)
            {
                new AlertDialog.Builder(MainActivity.Instance)
                    .SetMessage("Your phone's camera has encountered a fatal error outside the control of CrossCam. Please restart your device. Go try out your regular camera app if you don't believe me. If restarting does not fix the issue or your camera app is working, please email me@kra2008.com.")
                    .SetTitle("Fatal Camera Error")
                    .SetPositiveButton("OK", (EventHandler<DialogClickEventArgs>)null)
                    .Create()
                    .Show();
            }
            else
            {
                _cameraModule.Error = new Exception(error.ToString());
            }

            _camera2CameraDeviceErrorRetry = false;

            camera.Close();
            _camera2Device = null;
        }

        private void CaptureStillPicture2()
        {
            try
            {
                if (_camera2Device == null || _openingCamera2) return;

                var windowManager =
                    Extensions.JavaCast<IWindowManager>(MainActivity.Instance.GetSystemService(Context.WindowService));
                var rotation = windowManager.DefaultDisplay.Rotation;

                var phoneOrientation = _orientations.Get((int)rotation);
                var neededRotation = 0;
                switch (phoneOrientation)
                {
                    case 0:
                        neededRotation = _camera2SensorOrientation;
                        break;
                    case 90:
                        neededRotation = _camera2SensorOrientation + 270;
                        break;
                    case 180:
                        neededRotation = _camera2SensorOrientation + 180;
                        break;
                    case 270:
                        neededRotation = _camera2SensorOrientation + 90;
                        break;
                }

                while (neededRotation < 0)
                {
                    neededRotation += 360;
                }

                if (neededRotation >= 360)
                {
                    neededRotation %= 360;
                }

                var captureBuilder = _camera2Device.CreateCaptureRequest(CameraTemplate.StillCapture);
                captureBuilder.AddTarget(_finalCaptureImageReader.Surface);
                captureBuilder.Set(CaptureRequest.JpegOrientation, new Integer(neededRotation));
                var characteristics = _cameraManager.GetCameraCharacteristics(_camera2Id);
                try
                {
                    if ((bool)characteristics.Get(CameraCharacteristics.ControlAeLockAvailable))
                    {
                        captureBuilder.Set(CaptureRequest.ControlAeLock, new Boolean(true));
                    }
                }
                catch (NoSuchFieldError) { }

                _camera2Session.StopRepeating();

                _camera2State = CameraState.PictureTaken;
                _previewRequestBuilder.SetTag(CameraState.PictureTaken.ToString());
                
                _camera2Session.Capture(captureBuilder.Build(), _finalCaptureListener, null);
                if (_cameraModule.PairOperator.IsPrimary ||
                    _cameraModule.PairOperator.PairStatus != PairStatus.Connected)
                {
                    _cameraModule.CaptureSuccess = !_cameraModule.CaptureSuccess;
                }
            }
            catch (Exception e)
            {
                _cameraModule.Error = e;
            }
        }

        private void HandleFinalCaptureComplete(object sender, CrossCamCamera2CaptureListener.CameraCaptureListenerEventArgs e)
        {
            RestartPreview2(true);
        }

        private void HandleImageAvailableListenerError(object sender, Exception e)
        {
            _cameraModule.Error = e;
        }

        private void HandleImageAvailablePhoto(object sender, byte[] buffer)
        {
            if (_cameraModule.PairOperator.PairStatus == PairStatus.Connected &&
              !_cameraModule.PairOperator.IsPrimary)
            {
                _cameraModule.PairOperator.SendCapture(buffer);
                StopCamera2();
            }
            else
            {
                using var stream = new SKMemoryStream(buffer);
                using var codec = SKCodec.Create(stream);

                _cameraModule.CapturedImage = new IncomingFrame
                {
                    Frame = SKBitmap.Decode(codec),
                    Orientation = codec.EncodedOrigin,
                    IsFrontFacing = _cameraModule.ChosenCamera.IsFront
                };
            }
        }

        private void TapToFocus2(PointF p)
        {
            if (_cameraManager == null || 
                _camera2Device == null || 
                Display == null || 
                _previewRequestBuilder == null) return;

            var metrics = new DisplayMetrics();
            Display.GetMetrics(metrics);

            var characteristics = _cameraManager.GetCameraCharacteristics(_camera2Device.Id);
            var arraySize = (Rect)characteristics.Get(CameraCharacteristics.SensorInfoActiveArraySize);
            var rotation = Display.Rotation;

            var screenRotation = _orientations.Get((int)rotation);
            var frontFacingModifier = _cameraModule.ChosenCamera.IsFront ? 180 : 0;
            var sensorScreenSum = (_camera2SensorOrientation + screenRotation + 360 + frontFacingModifier) % 360;
            float x;
            float y;
            if (sensorScreenSum == 0)
            {
                //landscape tipped right
                x = (1 - p.X) * arraySize.Width();
                y = (1 - p.Y) * arraySize.Height();
            }
            else if (sensorScreenSum == 90)
            {
                //portrait
                x = p.Y * arraySize.Width();
                if (_cameraModule.ChosenCamera.IsFront)
                {
                    y = p.X * arraySize.Height();
                }
                else
                {
                    y = (1 - p.X) * arraySize.Height();
                }
            }
            else if (sensorScreenSum == 180)
            {
                //landscape tipped left
                x = p.X * arraySize.Width();
                y = p.Y * arraySize.Height();
            }
            else //  i.e. if (sensorScreenSum == 270)
            {
                //upside down portrait
                x = (1 - p.Y) * arraySize.Width();
                y = p.X * arraySize.Height();
            }
            var sizingRatio = arraySize.Width() / (1f * _textureView.Height);
            var focusSquareSide = (float)CameraPage.FOCUS_CIRCLE_WIDTH * metrics.Density * sizingRatio;
            if (_cameraModule.ChosenCamera.IsFront)
            {
                x = 0;
                y = 0;
            }
            var sensorTapRect = new Rect
            {
                Left = (int)(x - focusSquareSide / 2),
                Top = (int)(y - focusSquareSide / 2),
                Right = (int)(x + focusSquareSide / 2),
                Bottom = (int)(y + focusSquareSide / 2)
            };

            while (sensorTapRect.Top < arraySize.Top)
            {
                sensorTapRect.Top += 1;
                sensorTapRect.Bottom += 1;
            }

            while (sensorTapRect.Left < arraySize.Left)
            {
                sensorTapRect.Left += 1;
                sensorTapRect.Right += 1;
            }

            while (sensorTapRect.Right > arraySize.Right)
            {
                sensorTapRect.Right -= 1;
                sensorTapRect.Left -= 1;
            }

            while (sensorTapRect.Bottom > arraySize.Bottom)
            {
                sensorTapRect.Bottom -= 1;
                sensorTapRect.Top -= 1;
            }

            _camera2MeteringRectangle = new MeteringRectangle(sensorTapRect, 999);

            var maxAfRegions = (int)characteristics.Get(CameraCharacteristics.ControlMaxRegionsAf);
            var maxAeRegions = (int)characteristics.Get(CameraCharacteristics.ControlMaxRegionsAe);

            if (maxAfRegions > 0 ||
                maxAeRegions > 0)
            {
                if (maxAfRegions > 0)
                {
                    _previewRequestBuilder.Set(CaptureRequest.ControlAfRegions, new[] { _camera2MeteringRectangle });
                    if (((int[])characteristics.Get(CameraCharacteristics.ControlAfAvailableModes)).Contains((int)ControlAFMode.Auto))
                    {
                        _previewRequestBuilder.Set(CaptureRequest.ControlAfMode, new Integer((int)ControlAFMode.Auto));
                    }
                    _previewRequestBuilder.Set(CaptureRequest.ControlAfTrigger, new Integer((int)ControlAFTrigger.Start));
                }

                if (maxAeRegions > 0)
                {
                    _previewRequestBuilder.Set(CaptureRequest.ControlAeRegions, new[] { _camera2MeteringRectangle });
                    if (((int[])characteristics.Get(CameraCharacteristics.ControlAeAvailableModes)).Contains((int)ControlAEMode.On))
                    {
                        _previewRequestBuilder.Set(CaptureRequest.ControlAeMode, new Integer((int)ControlAEMode.On));
                    }
                    _previewRequestBuilder.Set(CaptureRequest.ControlAePrecaptureTrigger, new Integer((int)ControlAEPrecaptureTrigger.Start));
                }

                TurnOffAeLock(characteristics);

                _isCamera2FocusAndExposureLocked = false;
                _camera2FoundGoodCaptureInterlocked = 0;
                
                _camera2Session.StopRepeating();
                _camera2Session.Capture(_previewRequestBuilder.Build(), _previewCaptureListener, _backgroundHandler);

                _camera2State = CameraState.AwaitingTapLock;
                _previewRequestBuilder.SetTag(CameraState.AwaitingTapLock.ToString());

                _previewRequestBuilder.Set(CaptureRequest.ControlAfTrigger, null);
                _previewRequestBuilder.Set(CaptureRequest.ControlAePrecaptureTrigger, null);
                _camera2Session.SetRepeatingRequest(_previewRequestBuilder.Build(), _previewCaptureListener, _backgroundHandler);

                _cameraModule.IsFocusCircleLocked = false;
            }
        }

        private void RestartPreview2(bool withLockIfEnabled)
        {
            if (_camera2Device == null ||
                _camera2Session == null ||
                _previewRequestBuilder == null ||
                _previewCaptureListener == null ||
                _backgroundHandler == null) return;

            try
            {
                var characteristics = _cameraManager.GetCameraCharacteristics(_camera2Id);
                _cameraModule.IsFocusCircleLocked = false;
                _camera2FoundGoodCaptureInterlocked = 0;

                _camera2State = CameraState.Preview;
                _previewRequestBuilder.SetTag(CameraState.Preview.ToString());

                if (_cameraModule.IsLockToFirstEnabled && withLockIfEnabled)
                {
                    TurnOnAeLock(characteristics);

                    _isCamera2FocusAndExposureLocked = true;
                    _camera2Session.SetRepeatingRequest(_previewRequestBuilder.Build(), _previewCaptureListener, _backgroundHandler);
                }
                else
                {
                    TurnOffAeLock(characteristics);
                    _previewRequestBuilder.Set(CaptureRequest.ControlAfMode, new Integer((int)ControlAFMode.ContinuousPicture));
                    _previewRequestBuilder.Set(CaptureRequest.ControlAfTrigger, new Integer((int)ControlAFTrigger.Cancel));
                    _previewRequestBuilder.Set(CaptureRequest.ControlAePrecaptureTrigger, new Integer((int)ControlAEPrecaptureTrigger.Cancel));

                    _previewRequestBuilder.Set(CaptureRequest.ControlAfRegions, null);
                    _previewRequestBuilder.Set(CaptureRequest.ControlAeRegions, null);

                    _isCamera2FocusAndExposureLocked = false;
                    
                    _camera2Session.StopRepeating();
                    _camera2Session.Capture(_previewRequestBuilder.Build(), _previewCaptureListener, _backgroundHandler);

                    _previewRequestBuilder.Set(CaptureRequest.ControlAfTrigger, null);
                    _previewRequestBuilder.Set(CaptureRequest.ControlAePrecaptureTrigger, null);
                    
                    _camera2Session.SetRepeatingRequest(_previewRequestBuilder.Build(), _previewCaptureListener, _backgroundHandler);
                }
            }
            catch (System.Exception e)
            {
                _cameraModule.Error = e;
            }
        }

        private void TurnOnAeLock(CameraCharacteristics characteristics)
        {
            try
            {
                if ((bool)characteristics.Get(CameraCharacteristics.ControlAeLockAvailable)) // docs say this is always present, but that's not true!!!!
                {
                    _previewRequestBuilder.Set(CaptureRequest.ControlAeLock, new Boolean(true));
                }
            }
            catch (NoSuchFieldError) { }
        }

        private void TurnOffAeLock(CameraCharacteristics characteristics)
        {
            try
            {
                if ((bool)characteristics.Get(CameraCharacteristics.ControlAeLockAvailable)) // docs say this is always present, but that's not true!!!!
                {
                    _previewRequestBuilder.Set(CaptureRequest.ControlAeLock, new Boolean(false));
                }
            }
            catch (NoSuchFieldError) { }
        }

        private void StartBackgroundThread()
        {
            _backgroundThread = new HandlerThread("CameraBackground");
            _backgroundThread.Start();
            _backgroundHandler = new Handler(_backgroundThread.Looper);
        }

        private void StopBackgroundThread()
        {
            _backgroundThread?.QuitSafely();
            _backgroundThread?.Join();
            _backgroundThread = null;
            _backgroundHandler = null;
        }
        #endregion
    }
}