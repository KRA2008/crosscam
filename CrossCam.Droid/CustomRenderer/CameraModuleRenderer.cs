using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Hardware;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.Media;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using CrossCam.Droid.CustomRenderer;
using CrossCam.Droid.CustomRenderer.Camera2;
using CrossCam.Model;
using CrossCam.Page;
using CrossCam.Wrappers;
using Java.Lang;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;
using Boolean = Java.Lang.Boolean;
using CameraError = Android.Hardware.CameraError;
using CameraModule = CrossCam.CustomElement.CameraModule;
using Exception = Java.Lang.Exception;
using Math = System.Math;
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

        private readonly bool _useCamera2;
        private Surface _surface;
        private readonly CameraManager _cameraManager;
        private CameraCaptureSession _camera2Session;
        private CaptureRequest.Builder _previewRequestBuilder;
        private CameraCaptureListener _captureListener;
        private CameraStateListener _stateListener;
        private ImageReader _imageReader;
        private CameraState _cameraState;
        private HandlerThread _backgroundThread;
        private Handler _backgroundHandler;
        private string _camera2Id;
        private CameraDevice _camera2Device;
        private bool _openingCamera2;
        private Size _preview2Size;
        private Size _picture2Size;
        private MeteringRectangle _camera2MeteringRectangle;
        private bool _isCamera2Locked;
        private int _camera2SensorOrientation;
        private readonly SparseIntArray _orientations = new SparseIntArray();

        public CameraModuleRenderer(Context context) : base(context)
        {
            MainActivity.Instance.LifecycleEventListener.OrientationChanged += (sender, args) =>
            {
                OrientationChanged();
            };
            MainActivity.Instance.LifecycleEventListener.AppMaximized += AppWasMaximized;
            MainActivity.Instance.LifecycleEventListener.AppMinimized += AppWasMinimized;
            
            var settings = PersistentStorage.LoadOrDefault(PersistentStorage.SETTINGS_KEY, new Settings());
            var forceCamera1 = settings.IsForceCamera1Enabled;

            _landscapePreviewAllottedWidth = Resources.DisplayMetrics.HeightPixels / 2f; // when in landscape (the larger of the two), preview width will be half the height of the screen
                                                                                         // ("height" of a screen is the larger of the two dimensions, which is opposite of camera/preview sizes)

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop && !forceCamera1)
            {
                _cameraManager = (CameraManager)MainActivity.Instance.GetSystemService(Context.CameraService);

                _orientations.Append((int)SurfaceOrientation.Rotation0, 0);
                _orientations.Append((int)SurfaceOrientation.Rotation90, 90);
                _orientations.Append((int)SurfaceOrientation.Rotation180, 180);
                _orientations.Append((int)SurfaceOrientation.Rotation270, 270);

                try
                {
                    var level = FindCamera2();
                    _useCamera2 = level != (int)InfoSupportedHardwareLevel.Legacy;
                }
                catch (Exception e)
                {
                    _cameraModule.ErrorMessage = e.ToString();
                    _useCamera2 = false;
                }
            }
        }

        private void AppWasMinimized(object obj, EventArgs args)
        {
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

        private void AppWasMaximized(object obj, EventArgs args)
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

        protected override void OnElementChanged(ElementChangedEventArgs<CameraModule> e)
        {
            base.OnElementChanged(e);

            try
            {
                if (e.OldElement != null || Element == null)
                {
                    return;
                }

                if (e.NewElement != null)
                {
                    _cameraModule = e.NewElement;
                }

                
                SetupUserInterface();
                AddView(_view);
                if (_useCamera2)
                {
                    OpenCamera2();
                }
            }
            catch (Exception ex)
            {
                _cameraModule.ErrorMessage = ex.ToString();
            }
        }

        protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            base.OnElementPropertyChanged(sender, e);

            try
            {
                if (e.PropertyName == nameof(_cameraModule.IsVisible))
                {
                    if (_cameraModule.IsVisible)
                    {
                        if (_surfaceTexture != null)
                        {
                            if (!_useCamera2)
                            {
                                SetupAndStartCamera1();
                            }
                        }
                    }
                }

                if (e.PropertyName == nameof(_cameraModule.Width) ||
                    e.PropertyName == nameof(_cameraModule.Height))
                {
                    OrientationChanged();
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
            }
            catch (Exception ex)
            {
                _cameraModule.ErrorMessage = ex.ToString();
            }
        }

        private void TurnOnContinuousFocus(Camera.Parameters providedParameters = null)
        {
            try
            {
                _cameraModule.IsFocusCircleVisible = false;
                if (_useCamera2)
                {
                    RestartPreview2(false);
                }
                else
                {
                    var parameters = providedParameters ?? _camera1.GetParameters();

                    if (parameters.SupportedFocusModes.Contains(Camera.Parameters.FocusModeContinuousPicture))
                    {
                        parameters.FocusMode = Camera.Parameters.FocusModeContinuousPicture;
                    }

                    if (parameters.SupportedWhiteBalance.Contains(Camera.Parameters.WhiteBalanceAuto))
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
                _cameraModule.ErrorMessage = e.ToString();
            }
        }

        private void SetupUserInterface()
        {
            _activity = Context as Activity;
            _view = _activity.LayoutInflater.Inflate(Resource.Layout.CameraLayout, this, false);

            _textureView = _view.FindViewById<TextureView>(Resource.Id.textureView);
            _textureView.SurfaceTextureListener = this;
            _textureView.SetOnTouchListener(this);
            _gestureDetector = new GestureDetector(MainActivity.Instance, new CameraPreviewGestureListener(this));
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

        public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height) {}

        private void OrientationChanged()
        {
            if (_surfaceTexture != null)
            {
                SetOrientation();
            }
        }

        private void TakePhotoButtonTapped()
        {
            try
            {
                if (_useCamera2)
                {
                    StartRealCapture2();
                }
                else
                {
                    _camera1.TakePicture(this, this, this, this);
                }
            }
            catch
            {
                //user can try again
            }
        }

        public bool OnTouch(View v, MotionEvent e)
        {
            return _gestureDetector.OnTouchEvent(e);
        }

        private void PreviewDoubleTapped()
        {
            TurnOnContinuousFocus();
        }

        private void PreviewSingleTapped(MotionEvent e)
        {
            try
            {
                if (_cameraModule.IsTapToFocusEnabled)
                {
                    if (_useCamera2)
                    {
                        TapToFocus2(e);
                    }
                    else
                    {
                        var metrics = new DisplayMetrics();
                        Display.GetMetrics(metrics);

                        var tapRadius = (float)CameraPage.FOCUS_CIRCLE_WIDTH * metrics.Density / 2f;

                        var tapX = Clamp(e.GetX(), tapRadius, _textureView.Width - tapRadius);
                        var tapY = Clamp(e.GetY(), tapRadius, _textureView.Height - tapRadius);

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

                        double focusCircleX = e.GetX() + _textureView.GetX();
                        double focusCircleY = e.GetY() + _textureView.GetY();

                        _cameraModule.FocusCircleX = focusCircleX / metrics.Density;
                        _cameraModule.FocusCircleY = focusCircleY / metrics.Density;
                        _cameraModule.IsFocusCircleVisible = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _cameraModule.ErrorMessage = ex.ToString();
            }
        }
        
        private static float Clamp(float x, float min, float max)
        {
            return x < min ? min : x < max ? x : max;
        }

        public void OnError(CameraError error, Camera camera)
        {
            _cameraModule.ErrorMessage = error.ToString();
        }
        
        private void SetOrientation()
        {
            try
            {
                var metrics = new DisplayMetrics();
                Display.GetMetrics(metrics);

                var moduleWidth = (float) (_cameraModule.Width * metrics.Density);
                var moduleHeight = (float) (_cameraModule.Height * metrics.Density);

                float previewSizeWidth;
                float previewSizeHeight;
                int rotation1;
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
                        _cameraModule.IsViewInverted = false;
                        _cameraModule.IsPortrait = true;
                        proportionalPreviewHeight = previewSizeWidth * moduleWidth / previewSizeHeight;
                        rotation1 = 90;
                        rotation2 = _camera2SensorOrientation - 90;
                        verticalOffset = (moduleHeight - proportionalPreviewHeight) / 2f;
                        xAdjust2 = 0;
                        yAdjust2 = verticalOffset;
                        previewWidth2 = moduleWidth;
                        previewHeight2 = proportionalPreviewHeight;
                        break;
                    case SurfaceOrientation.Rotation180:
                        _cameraModule.IsViewInverted = true;
                        _cameraModule.IsPortrait = true;
                        proportionalPreviewHeight = previewSizeWidth * moduleWidth / previewSizeHeight;
                        rotation1 = 270;
                        rotation2 = _camera2SensorOrientation - 270;
                        verticalOffset = (moduleHeight - proportionalPreviewHeight) / 2f;
                        xAdjust2 = moduleWidth;
                        yAdjust2 = verticalOffset + proportionalPreviewHeight;
                        previewWidth2 = moduleWidth;
                        previewHeight2 = proportionalPreviewHeight;
                        break;
                    case SurfaceOrientation.Rotation90:
                        _cameraModule.IsViewInverted = false;
                        _cameraModule.IsPortrait = false;
                        proportionalPreviewHeight = previewSizeHeight * moduleWidth / previewSizeWidth;
                        rotation1 = 0;
                        rotation2 = _camera2SensorOrientation - 180;
                        verticalOffset = (moduleHeight - proportionalPreviewHeight) / 2f;
                        xAdjust2 = 0;
                        yAdjust2 = proportionalPreviewHeight + verticalOffset;
                        previewWidth2 = proportionalPreviewHeight;
                        previewHeight2 = moduleWidth;
                        break;
                    default:
                        _cameraModule.IsPortrait = false;
                        _cameraModule.IsViewInverted = true;
                        proportionalPreviewHeight = previewSizeHeight * moduleWidth / previewSizeWidth;
                        rotation1 = 180;
                        rotation2 = _camera2SensorOrientation - 360;
                        verticalOffset = (moduleHeight - proportionalPreviewHeight) / 2f;
                        xAdjust2 = moduleWidth;
                        yAdjust2 = verticalOffset;
                        previewWidth2 = proportionalPreviewHeight;
                        previewHeight2 = moduleWidth;
                        break;
                }

                if (_useCamera2)
                {
                    _textureView.PivotX = 0;
                    _textureView.PivotY = 0;
                    _textureView.Rotation = rotation2;
                }
                else
                {
                    var parameters = _camera1.GetParameters();
                    parameters.SetRotation(rotation1);
                    _camera1.SetDisplayOrientation(rotation1);
                    _camera1.SetParameters(parameters);
                }

                _cameraModule.PreviewBottomY = (moduleHeight - verticalOffset) / metrics.Density;

                if (_useCamera2)
                {
                    _textureView.SetX(xAdjust2);
                    _textureView.SetY(yAdjust2);
                    _textureView.LayoutParameters = new FrameLayout.LayoutParams((int) Math.Round(previewWidth2),
                        (int) Math.Round(previewHeight2));
                }
                else
                {
                    _textureView.SetX(0);
                    _textureView.SetY(verticalOffset);
                    _textureView.LayoutParameters = new FrameLayout.LayoutParams((int) Math.Round(moduleWidth),
                        (int) Math.Round(proportionalPreviewHeight));
                }
            }
            catch (Exception e)
            {
                _cameraModule.ErrorMessage = e.ToString();
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

        private void SetupAndStartCamera1(SurfaceTexture surface = null)
        {
            try
            {
                if (_surfaceTexture != null)
                {
                    if (_camera1 == null)
                    {
                        _camera1 = Camera.Open((int)CameraFacing.Back);
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
                        parameters.FlashMode = Camera.Parameters.FlashModeOff;
                        parameters.VideoStabilization = false;
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

                        parameters.SetPictureSize(_pictureSize.Width, _pictureSize.Height);
                        parameters.SetPreviewSize(_previewSize.Width, _previewSize.Height);

                        _surfaceTexture.SetDefaultBufferSize(_previewSize.Width, _previewSize.Height);

                        _camera1.SetParameters(parameters);
                        _camera1.SetPreviewTexture(_surfaceTexture);
                    }

                    if (surface != null)
                    {
                        _surfaceTexture = surface;
                    }

                    SetOrientation();
                    StartCamera1();
                }
            }
            catch (Exception e)
            {
                _cameraModule.ErrorMessage = e.ToString();
            }
        }

        public void OnShutter()
        {
            _cameraModule.CaptureSuccess = !_cameraModule.CaptureSuccess;
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

                    _cameraModule.CapturedImage = data;

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
                catch (Exception e)
                {
                    _cameraModule.ErrorMessage = e.ToString();
                }
            }
        }

        private void TurnOnFocusLockIfApplicable1()
        {
            if (_cameraModule.IsNothingCaptured && _cameraModule.IsLockToFirstEnabled)
            {
                var parameters = _camera1.GetParameters();

                if (parameters.SupportedFocusModes.Contains(Camera.Parameters.FocusModeFixed))
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
        }

        #endregion

        #region camera2

        private int FindCamera2()
        {
            var cameraIds = _cameraManager.GetCameraIdList();
            foreach (var cameraId in cameraIds)
            {
                var cameraChars = _cameraManager.GetCameraCharacteristics(cameraId);
                var direction = (int)cameraChars.Get(CameraCharacteristics.LensFacing);
                if (direction == (int)LensFacing.Back)
                {
                    _camera2Id = cameraId;
                    break;
                }
            }

            if (_camera2Id == null)
            {
                return (int)InfoSupportedHardwareLevel.Legacy;
            }

            var characteristics = _cameraManager.GetCameraCharacteristics(_camera2Id);
            var level = (int) characteristics.Get(CameraCharacteristics.InfoSupportedHardwareLevel);

            if (level == (int) InfoSupportedHardwareLevel.Legacy)
            {
                return level;
            }

            _camera2SensorOrientation = (int)characteristics.Get(CameraCharacteristics.SensorOrientation);

            var map = (StreamConfigurationMap)characteristics.Get(CameraCharacteristics
                .ScalerStreamConfigurationMap);

            _picture2Size = map.GetOutputSizes((int) ImageFormatType.Jpeg).Where(p => p.Width > p.Height)
                .OrderByDescending(s => s.Width * s.Height).First();
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
                return (int) InfoSupportedHardwareLevel.Legacy; // cannot find appropriate sizes with camera2. fall back to camera1.
            }

            _stateListener = new CameraStateListener(this);

            return level;
        }

        private void OpenCamera2()
        {
            try
            {
                if (_openingCamera2 || _surfaceTexture == null || _camera2Id == null)
                {
                    return;
                }

                _openingCamera2 = true;
                _surface = new Surface(_surfaceTexture);
                StartBackgroundThread();
                _cameraManager.OpenCamera(_camera2Id, _stateListener, null);
            }
            catch (Exception e)
            {
                _cameraModule.ErrorMessage = e.ToString();
            }
        }

        private void StopCamera2()
        {
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

                _imageReader =
                    ImageReader.NewInstance(_picture2Size.Width, _picture2Size.Height, ImageFormatType.Jpeg, 1);

                _captureListener = new CameraCaptureListener();
                _captureListener.CaptureComplete += (sender, args) => { HandleCaptureResult(args.CaptureResult); };
                _captureListener.CaptureProgressed += (sender, args) => { HandleCaptureResult(args.CaptureResult); };
                
                _camera2Device.CreateCaptureSession(new List<Surface> { _surface, _imageReader.Surface },
                    new CameraCaptureStateListener
                    {
                        OnConfigureFailedAction = session => { },
                        OnConfiguredAction = session =>
                        {
                            _camera2Session = session;

                            _previewRequestBuilder = _camera2Device.CreateCaptureRequest(CameraTemplate.Preview);
                            _previewRequestBuilder.AddTarget(_surface);

                            _previewRequestBuilder.Set(CaptureRequest.ControlAfMode,
                                new Integer((int) ControlAFMode.ContinuousPicture));

                            session.SetRepeatingRequest(_previewRequestBuilder.Build(), _captureListener, _backgroundHandler);
                        }
                    },
                    null);
                _openingCamera2 = false;
            }
            catch (Exception e)
            {
                _cameraModule.ErrorMessage = e.ToString();
            }
        }

        private void HandleCaptureResult(CaptureResult result)
        {
            if (_cameraState == CameraState.Preview ||
                _cameraState == CameraState.PictureTaken)
            {
                return;
            }

            var afState = result.Get(CaptureResult.ControlAfState);
            var aeState = result.Get(CaptureResult.ControlAeState);
            ControlAFState afStateEnum = 0;
            if (afState != null)
            {
                afStateEnum = (ControlAFState) (int) afState;
            }
            ControlAEState aeStateEnum = 0;
            if (aeState != null)
            {
                aeStateEnum = (ControlAEState) (int) aeState;
            }
            
            System.Diagnostics.Debug.WriteLine("### CAMERA STATE: " + _cameraState);
            System.Diagnostics.Debug.WriteLine("### AF STATE: " + afStateEnum);
            System.Diagnostics.Debug.WriteLine("### AE STATE: " + aeStateEnum);

            switch (_cameraState)
            {
                case CameraState.Preview:
                    break;
                case CameraState.WaitingLock:
                    if (afState == null ||
                        afStateEnum == ControlAFState.FocusedLocked ||
                        afStateEnum == ControlAFState.NotFocusedLocked)
                    {
                        if (aeState == null)
                        {
                            _cameraState = CameraState.PictureTaken;
                            CaptureStillPicture2();
                            break;
                        }
                        aeStateEnum = (ControlAEState) (int) aeState;
                        if (aeStateEnum == ControlAEState.Converged)
                        {
                            _cameraState = CameraState.PictureTaken;
                            CaptureStillPicture2();
                        }
                        else
                        {
                            RunPrecaptureSequence();
                        }
                    }
                    break;
                case CameraState.WaitingTapLock:
                    if (afState == null ||
                        afStateEnum == ControlAFState.FocusedLocked ||
                        afStateEnum == ControlAFState.NotFocusedLocked)
                    {
                        if (aeState == null)
                        {
                            _cameraState = CameraState.Preview;
                            break;
                        }
                        aeStateEnum = (ControlAEState)(int)aeState;
                        if (aeStateEnum == ControlAEState.Converged)
                        {
                            LockAe();
                            _cameraState = CameraState.Preview;
                        }
                    }
                    break;
                case CameraState.WaitingPrecapture:
                    if (aeState == null ||
                        aeStateEnum == ControlAEState.Precapture ||
                        aeStateEnum == ControlAEState.FlashRequired ||
                        aeStateEnum == ControlAEState.Locked)
                    {
                        _cameraState = CameraState.WaitingNonPrecapture;
                    }
                    break;
                case CameraState.WaitingNonPrecapture:
                    if (aeState == null ||
                        aeStateEnum != ControlAEState.Precapture)
                    {
                        _cameraState = CameraState.PictureTaken;
                        CaptureStillPicture2();
                    }
                    break;
                case CameraState.PictureTaken:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void RunPrecaptureSequence()
        {
            try
            {
                _previewRequestBuilder.Set(CaptureRequest.ControlAfTrigger, null);
                _previewRequestBuilder.Set(CaptureRequest.ControlAePrecaptureTrigger, new Integer((int) ControlAEPrecaptureTrigger.Start));
                _cameraState = CameraState.WaitingPrecapture;
                _camera2Session.Capture(_previewRequestBuilder.Build(), _captureListener, _backgroundHandler);
            }
            catch (Exception e)
            {
                _cameraModule.ErrorMessage = e.ToString();
            }
        }

        private void LockAe()
        {
            try
            {
                var characteristics = _cameraManager.GetCameraCharacteristics(_camera2Device.Id);
                if ((bool)characteristics.Get(CameraCharacteristics.ControlAeLockAvailable))
                {
                    _previewRequestBuilder.Set(CaptureRequest.ControlAeLock, new Boolean(true));
                }
                _previewRequestBuilder.Set(CaptureRequest.ControlAfTrigger, null);
                _previewRequestBuilder.Set(CaptureRequest.ControlAePrecaptureTrigger, null);
                _cameraState = CameraState.Preview;

                _camera2Session.StopRepeating();
                _camera2Session.SetRepeatingRequest(_previewRequestBuilder.Build(), _captureListener, _backgroundHandler);

                _isCamera2Locked = true;
            }
            catch (Exception e)
            {
                _cameraModule.ErrorMessage = e.ToString();
            }
        }

        private void StartRealCapture2()
        {
            try
            {
                if (_isCamera2Locked)
                {
                    _cameraState = CameraState.PictureTaken;
                    CaptureStillPicture2();
                }
                else
                {
                    _previewRequestBuilder.Set(CaptureRequest.ControlAePrecaptureTrigger, null);
                    _previewRequestBuilder.Set(CaptureRequest.ControlAfTrigger, new Integer((int)ControlAFTrigger.Start));
                    _cameraState = CameraState.WaitingLock;
                    _camera2Session.Capture(_previewRequestBuilder.Build(), _captureListener, _backgroundHandler);
                }
            }
            catch (Exception e)
            {
                _cameraModule.ErrorMessage = e.ToString();
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
            _cameraModule.ErrorMessage = error.ToString();

            camera.Close();
            _camera2Device = null;
            _openingCamera2 = false;
        }

        private void CaptureStillPicture2()
        {
            try
            {
                if (_camera2Device == null || _openingCamera2) return;

                var windowManager = MainActivity.Instance.GetSystemService(Context.WindowService)
                    .JavaCast<IWindowManager>();
                var rotation = windowManager.DefaultDisplay.Rotation;

                var phoneOrientation = _orientations.Get((int) rotation);
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
                    neededRotation = neededRotation % 360;
                }


                var captureBuilder = _camera2Device.CreateCaptureRequest(CameraTemplate.StillCapture);
                captureBuilder.AddTarget(_imageReader.Surface);
                captureBuilder.Set(CaptureRequest.JpegOrientation, new Integer(neededRotation));

                var readerListener = new ImageAvailableListener();
                readerListener.Photo += (sender, buffer) =>
                {
                    Device.BeginInvokeOnMainThread(() => { _cameraModule.CapturedImage = buffer; });
                };
                readerListener.Error += (sender, exception) => { _cameraModule.ErrorMessage = exception.ToString(); };
                _imageReader.SetOnImageAvailableListener(readerListener, _backgroundHandler);

                var captureListener = new CameraCaptureListener();
                captureListener.CaptureComplete += (sender, e) =>
                {
                    RestartPreview2(true);
                };

                _camera2Session.StopRepeating();
                _camera2Session.AbortCaptures();
                _camera2Session.Capture(captureBuilder.Build(), captureListener, null);
                _cameraModule.CaptureSuccess = !_cameraModule.CaptureSuccess;
            }
            catch (Exception e)
            {
                _cameraModule.ErrorMessage = e.ToString();
            }
        }
        
        private void TapToFocus2(MotionEvent e)
        {
            var metrics = new DisplayMetrics();
            Display.GetMetrics(metrics);

            double focusCircleX = 0;
            double focusCircleY = 0;

            var characteristics = _cameraManager.GetCameraCharacteristics(_camera2Device.Id);
            var arraySize = (Rect)characteristics.Get(CameraCharacteristics.SensorInfoActiveArraySize);
            var rotation = Display.Rotation;

            Rect sensorTapRect = null;
            var screenRotation = _orientations.Get((int)rotation);
            if ((_camera2SensorOrientation + screenRotation + 360) % 360 == 90)
            {
                //portrait
                var displayHorizontalProportion = e.GetX() / _textureView.Width;
                var displayVerticalProportion = e.GetY() / _textureView.Height;
                var sizingRatio = arraySize.Width() / (1f * _textureView.Height);
                var focusSquareSide = (float)CameraPage.FOCUS_CIRCLE_WIDTH * metrics.Density * sizingRatio;
                var x = displayVerticalProportion * arraySize.Width();
                var y = (1 - displayHorizontalProportion) * arraySize.Height();
                sensorTapRect = new Rect
                {
                    Left = (int)(x - focusSquareSide / 2),
                    Top = (int)(y - focusSquareSide / 2),
                    Right = (int)(x + focusSquareSide / 2),
                    Bottom = (int)(y + focusSquareSide / 2)
                };
                focusCircleX = e.GetX() + _textureView.GetX();
                focusCircleY = e.GetY() + _textureView.GetY();
            }
            else if ((_camera2SensorOrientation + screenRotation + 360) % 360 == 0)
            {
                //landscape tipped right
                var displayVerticalProportion = e.GetX() / _textureView.Width;
                var displayHorizontalProportion = (_textureView.Height - e.GetY()) / _textureView.Height;
                var sizingRatio = arraySize.Width() / (1f * _textureView.Height);
                var focusSquareSide = (float)CameraPage.FOCUS_CIRCLE_WIDTH * metrics.Density * sizingRatio;
                var x = (1 - displayHorizontalProportion) * arraySize.Width();
                var y = (1 - displayVerticalProportion) * arraySize.Height();
                sensorTapRect = new Rect
                {
                    Left = (int)(x - focusSquareSide / 2),
                    Top = (int)(y - focusSquareSide / 2),
                    Right = (int)(x + focusSquareSide / 2),
                    Bottom = (int)(y + focusSquareSide / 2)
                };
                focusCircleX = _textureView.Height - e.GetY();
                focusCircleY = e.GetX() + _textureView.GetY();
            }
            else if ((_camera2SensorOrientation + screenRotation + 360) % 360 == 180)
            {
                //landscape tipped left
                var displayVerticalProportion = (_textureView.Width - e.GetX()) / _textureView.Width;
                var displayHorizontalProportion = e.GetY() / _textureView.Height;
                var sizingRatio = arraySize.Width() / (1f * _textureView.Height);
                var focusSquareSide = (float)CameraPage.FOCUS_CIRCLE_WIDTH * metrics.Density * sizingRatio;
                var x = displayHorizontalProportion * arraySize.Width();
                var y = displayVerticalProportion * arraySize.Height();
                sensorTapRect = new Rect
                {
                    Left = (int)(x - focusSquareSide / 2),
                    Top = (int)(y - focusSquareSide / 2),
                    Right = (int)(x + focusSquareSide / 2),
                    Bottom = (int)(y + focusSquareSide / 2)
                };
                focusCircleX = e.GetY();
                focusCircleY = _textureView.GetY() - e.GetX();
            }
            else if ((_camera2SensorOrientation + screenRotation + 360) % 360 == 270)
            {
                //upside down portrait
                var displayHorizontalProportion = (_textureView.Width - e.GetX()) / _textureView.Width;
                var displayVerticalProportion = (_textureView.Height - e.GetY()) / _textureView.Height;
                var sizingRatio = arraySize.Width() / (1f * _textureView.Height);
                var focusSquareSide = (float)CameraPage.FOCUS_CIRCLE_WIDTH * metrics.Density * sizingRatio;
                var x = (1 - displayVerticalProportion) * arraySize.Width();
                var y = displayHorizontalProportion * arraySize.Height();
                sensorTapRect = new Rect
                {
                    Left = (int)(x - focusSquareSide / 2),
                    Top = (int)(y - focusSquareSide / 2),
                    Right = (int)(x + focusSquareSide / 2),
                    Bottom = (int)(y + focusSquareSide / 2)
                };
                focusCircleX = _textureView.Width - e.GetX();
                focusCircleY = _textureView.GetY() - e.GetY();
            }

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
                }

                if ((bool)characteristics.Get(CameraCharacteristics.ControlAeLockAvailable))
                {
                    _previewRequestBuilder.Set(CaptureRequest.ControlAeLock, new Boolean(false));
                }

                _cameraState = CameraState.WaitingTapLock;
                _camera2Session.StopRepeating();
                _camera2Session.Capture(_previewRequestBuilder.Build(), _captureListener, _backgroundHandler);

                _previewRequestBuilder.Set(CaptureRequest.ControlAfTrigger, null);
                _previewRequestBuilder.Set(CaptureRequest.ControlAePrecaptureTrigger, null);
                _camera2Session.SetRepeatingRequest(_previewRequestBuilder.Build(), _captureListener,
                    _backgroundHandler);

                _cameraModule.FocusCircleX = focusCircleX / metrics.Density;
                _cameraModule.FocusCircleY = focusCircleY / metrics.Density;
                _cameraModule.IsFocusCircleVisible = true;
            }
        }

        private void RestartPreview2(bool withLockIfEnabled)
        {
            try
            {
                var characteristics = _cameraManager.GetCameraCharacteristics(_camera2Id);
                _cameraState = CameraState.Preview;

                if (_cameraModule.IsLockToFirstEnabled && withLockIfEnabled)
                {
                    if ((bool)characteristics.Get(CameraCharacteristics.ControlAeLockAvailable))
                    {
                        _previewRequestBuilder.Set(CaptureRequest.ControlAeLock, new Boolean(true));
                    }

                    _camera2Session.StopRepeating();
                    _camera2Session.SetRepeatingRequest(_previewRequestBuilder.Build(), _captureListener, _backgroundHandler);

                    _isCamera2Locked = true;
                }
                else
                {
                    if ((bool)characteristics.Get(CameraCharacteristics.ControlAeLockAvailable))
                    {
                        _previewRequestBuilder.Set(CaptureRequest.ControlAeLock, new Boolean(false));
                    }
                    _previewRequestBuilder.Set(CaptureRequest.ControlAfTrigger, new Integer((int)ControlAFTrigger.Cancel));
                    _previewRequestBuilder.Set(CaptureRequest.ControlAePrecaptureTrigger, new Integer((int)ControlAEPrecaptureTrigger.Cancel));
                    _previewRequestBuilder.Set(CaptureRequest.ControlAfMode, new Integer((int)ControlAFMode.ContinuousPicture));

                    _camera2Session.StopRepeating();
                    _camera2Session.Capture(_previewRequestBuilder.Build(), _captureListener, _backgroundHandler);

                    _previewRequestBuilder.Set(CaptureRequest.ControlAfTrigger, null);
                    _previewRequestBuilder.Set(CaptureRequest.ControlAePrecaptureTrigger, null);

                    _camera2Session.SetRepeatingRequest(_previewRequestBuilder.Build(), _captureListener, _backgroundHandler);

                    _isCamera2Locked = false;
                }
            }
            catch (Exception e)
            {
                _cameraModule.ErrorMessage = e.ToString();
            }
        }

        private void StartBackgroundThread()
        {
            _backgroundThread = new HandlerThread("CameraBackground");
            _backgroundThread.Start();
            _backgroundHandler = new Handler(_backgroundThread.Looper);
        }
        
        private void StopBackgroundThread()
        {
            _backgroundThread.QuitSafely();
            _backgroundThread.Join();
            _backgroundThread = null;
            _backgroundHandler = null;
        }

        private enum CameraState
        {
            Preview,
            WaitingLock,
            WaitingTapLock,
            WaitingPrecapture,
            WaitingNonPrecapture,
            PictureTaken
        }

        #endregion

        private sealed class CameraPreviewGestureListener : GestureDetector.SimpleOnGestureListener
        {
            private readonly CameraModuleRenderer _renderer;

            public CameraPreviewGestureListener(CameraModuleRenderer renderer)
            {
                _renderer = renderer;
            }

            public override bool OnDown(MotionEvent e)
            {
                return true;
            }

            public override bool OnSingleTapUp(MotionEvent e)
            {
                _renderer.PreviewSingleTapped(e);
                return true;
            }

            public override bool OnDoubleTap(MotionEvent e)
            {
                _renderer.PreviewDoubleTapped();
                return true;
            }

            public override bool OnFling(MotionEvent e1, MotionEvent e2, float velocityX, float velocityY)
            {
                if (Math.Abs(velocityX) > Math.Abs(velocityY))
                {
                    _renderer._cameraModule.WasSwipedTrigger = !_renderer._cameraModule.WasSwipedTrigger;
                }
                return true;
            }
        }
    }
}

