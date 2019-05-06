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
    public sealed class CameraModuleRenderer : ViewRenderer<CameraModule, View>, TextureView.ISurfaceTextureListener, Camera.IShutterCallback, Camera.IPictureCallback, View.IOnTouchListener, Camera.IErrorCallback
    {
        private Camera _camera1;
        private View _view;

        private Activity _activity;
        private TextureView _textureView;
        private SurfaceTexture _surfaceTexture;
        private CameraModule _cameraModule;

        private readonly float _landscapePreviewAllottedWidth;

        private static Camera.Size _previewSize;
        private static Camera.Size _pictureSize;

        private bool _isRunning;

        private bool _wasCameraRunningBeforeMinimize;

        private readonly bool _useCamera2;
        private readonly CameraManager _cameraManager;
        private CameraStateListener _stateListener;
        private string _camera2Id;
        private CameraDevice _camera2;
        private CaptureRequest.Builder _previewBuilder;
        private CameraCaptureSession _previewSession;
        private bool _openingCamera2;
        private Size _preview2Size;
        private Size _picture2Size;
        private int _camera2SensorOrientation;
        readonly SparseIntArray _orientations = new SparseIntArray();

        public CameraModuleRenderer(Context context) : base(context)
        {
            MainActivity.Instance.LifecycleEventListener.OrientationChanged += (sender, args) =>
            {
                OrientationChanged();
            };
            MainActivity.Instance.LifecycleEventListener.AppMaximized += AppWasMaximized;
            MainActivity.Instance.LifecycleEventListener.AppMinimized += AppWasMinimized;

            //TODO: remove when i figure out why distortion is happening on a couple devices with camera2
            var settings = PersistentStorage.LoadOrDefault(PersistentStorage.SETTINGS_KEY, new Settings());
            var forceCamera1 = settings.IsForceCamera1Enabled;

            _landscapePreviewAllottedWidth = Resources.DisplayMetrics.HeightPixels / 2f; // when in landscape (the larger of the two), preview width will be half the height of the screen
            // ("height" of a screen is the larger of the two dimensions, which is opposite of camera/preview sizes)

            if (Build.VERSION.SdkInt >= BuildVersionCodes.M && !forceCamera1)
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
                StartCamera2();
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
                    StartCamera2();
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
            _cameraModule.IsFocusCircleVisible = false;
            if (_useCamera2)
            {
                SetRefreshingPreview2(false);
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

                if (providedParameters == null)
                {
                    _camera1.SetParameters(parameters);
                }
            }
        }

        private void SetupUserInterface()
        {
            _activity = Context as Activity;
            _view = _activity.LayoutInflater.Inflate(Resource.Layout.CameraLayout, this, false);

            _textureView = _view.FindViewById<TextureView>(Resource.Id.textureView);
            _textureView.SurfaceTextureListener = this;
            _textureView.SetOnTouchListener(this);
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
                StartCamera2();
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
                    TakePhoto2();
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

        private const int MAX_DOUBLE_TAP_DURATION = 200;
        private long _tapStartTime;

        public bool OnTouch(View v, MotionEvent e)
        {
            if (_camera1 != null)
            {
                if (JavaSystem.CurrentTimeMillis() - _tapStartTime <= MAX_DOUBLE_TAP_DURATION)
                { //double tap
                    TurnOnContinuousFocus();
                }
                else
                { //single tap
                    _tapStartTime = JavaSystem.CurrentTimeMillis();

                    if (_cameraModule.IsTapToFocusEnabled)
                    {
                        if (_useCamera2)
                        {

                        }
                        else
                        {
                            var parameters = _camera1.GetParameters();
                            var focusRect = CalculateTapArea(e.GetX(), e.GetY(), 1f);
                            var meteringRect = CalculateTapArea(e.GetX(), e.GetY(), 1.5f);

                            if (parameters.MaxNumFocusAreas > 0 &&
                                parameters.SupportedFocusModes.Contains(Camera.Parameters.FocusModeAuto))
                            {
                                parameters.FocusMode = Camera.Parameters.FocusModeAuto;
                                parameters.FocusAreas = new List<Camera.Area> { new Camera.Area(focusRect, 1000) };
                            }

                            if (parameters.MaxNumMeteringAreas > 0)
                            {
                                parameters.MeteringAreas = new List<Camera.Area> { new Camera.Area(meteringRect, 1000) };
                            }

                            var metrics = new DisplayMetrics();
                            Display.GetMetrics(metrics);
                            _cameraModule.FocusCircleX = (focusRect.CenterX() + _textureView.GetX()) / metrics.Density;
                            _cameraModule.FocusCircleY = (focusRect.CenterY() + _textureView.GetY()) / metrics.Density;
                            _cameraModule.IsFocusCircleVisible = true;

                            _camera1.SetParameters(parameters);
                        }
                    }
                }
            }

            return false;
        }

        private Rect CalculateTapArea(float x, float y, float coefficient)
        {
            var areaSize = Float.ValueOf(100 * coefficient).IntValue();

            var left = Clamp((int)x - areaSize / 2, 0, _textureView.Width - areaSize);
            var top = Clamp((int)y - areaSize / 2, 0, _textureView.Height - areaSize);

            var rectF = new RectF(left, top, left + areaSize, top + areaSize);
            Matrix.MapRect(rectF);

            return new Rect((int)(rectF.Left + 0.5), (int)(rectF.Top + 0.5), (int)(rectF.Right + 0.5), (int)(rectF.Bottom + 0.5));
        }

        private static int Clamp(int x, int min, int max)
        {
            if (x > max)
            {
                return max;
            }
            return x < min ? min : x;
        }

        public void OnError(CameraError error, Camera camera)
        {
            _cameraModule.ErrorMessage = error.ToString();
        }


        private void SetOrientation()
        {
            var metrics = new DisplayMetrics();
            Display.GetMetrics(metrics);

            var moduleWidth = (float)(_cameraModule.Width * metrics.Density);
            var moduleHeight = (float)(_cameraModule.Height * metrics.Density);

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
                //var matrix = new Matrix();
                //_textureView.SetTransform(matrix);

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
                _textureView.LayoutParameters = new FrameLayout.LayoutParams((int)Math.Round(previewWidth2),
                    (int)Math.Round(previewHeight2));
            }
            else
            {
                _textureView.SetX(0);
                _textureView.SetY(verticalOffset);
                _textureView.LayoutParameters = new FrameLayout.LayoutParams((int)Math.Round(moduleWidth),
                    (int)Math.Round(proportionalPreviewHeight));
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
                            if (Math.Abs(previewSize.Width / (1f * previewSize.Height) - pictureAspectRatio) < 0.001)
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

                        if (_pictureSize == null ||
                            _previewSize == null)
                        {
                            _pictureSize = parameters.SupportedPictureSizes.First();
                            _previewSize = previewSizes.First();
                        }

                        parameters.SetPictureSize(_pictureSize.Width, _pictureSize.Height);
                        parameters.SetPreviewSize(_previewSize.Width, _previewSize.Height);

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
                if (Math.Abs(previewSize.Width / (1f * previewSize.Height) - pictureAspectRatio) < 0.001)
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

            if (_picture2Size == null ||
                _preview2Size == null)
            {
                return (int) InfoSupportedHardwareLevel.Legacy; // cannot find appropriate sizes with camera2. fall back to camera1.
            }

            _stateListener = new CameraStateListener(this);

            return level;
        }

        private void StartCamera2()
        {
            try
            {
                if (_openingCamera2 || _surfaceTexture == null || _camera2Id == null)
                {
                    return;
                }

                _openingCamera2 = true;
                _cameraManager.OpenCamera(_camera2Id, _stateListener, null);
            }
            catch (Exception e)
            {
                _cameraModule.ErrorMessage = e.ToString();
            }
        }

        private void StopCamera2()
        {
            _camera2?.Close();
            _camera2 = null;
        }

        public void StartPreview2(CameraDevice camera = null, bool applyLocks = false)
        {
            try
            {
                if (camera == null && _camera2 == null) return;

                if (_surfaceTexture == null) return;

                if (camera != null)
                {
                    _camera2 = camera;
                }

                _previewBuilder = _camera2.CreateCaptureRequest(CameraTemplate.Preview);
                var surface = new Surface(_surfaceTexture);
                _previewBuilder.AddTarget(surface);

                _camera2.CreateCaptureSession(new List<Surface> {surface},
                    new CameraCaptureStateListener
                    {
                        OnConfigureFailedAction = session => { },
                        OnConfiguredAction = session =>
                        {
                            _previewSession = session;
                            SetRefreshingPreview2(applyLocks);
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

        public void Camera2Disconnected(CameraDevice camera)
        {
            camera.Close();
            _camera2 = null;
            _openingCamera2 = false;
        }

        public void Camera2Errored(CameraDevice camera, Android.Hardware.Camera2.CameraError error)
        {
            _cameraModule.ErrorMessage = error.ToString();

            camera.Close();
            _camera2 = null;
            _openingCamera2 = false;
        }

        private void SetRefreshingPreview2(bool applyLocks)
        {
            try
            {
                _previewBuilder.Set(CaptureRequest.ControlMode, new Integer((int) ControlMode.Auto));

                if (applyLocks && _cameraModule.IsLockToFirstEnabled)
                {
                    _previewBuilder.Set(CaptureRequest.ControlAwbLock, new Boolean(true));
                    _previewBuilder.Set(CaptureRequest.BlackLevelLock, new Boolean(true));
                    _previewBuilder.Set(CaptureRequest.ControlAeLock, new Boolean(true));
                    _previewBuilder.Set(CaptureRequest.ControlAfTrigger, new Integer((int) ControlAFTrigger.Start));
                }
                else
                {
                    _previewBuilder.Set(CaptureRequest.ControlAwbLock, new Boolean(false));
                    _previewBuilder.Set(CaptureRequest.BlackLevelLock, new Boolean(false));
                    _previewBuilder.Set(CaptureRequest.ControlAeLock, new Boolean(false));
                    _previewBuilder.Set(CaptureRequest.ControlAfTrigger, new Integer((int) ControlAFTrigger.Cancel));
                }

                var thread = new HandlerThread("CameraPreview");
                thread.Start();
                var backgroundHandler = new Handler(thread.Looper);

                _previewSession.SetRepeatingRequest(_previewBuilder.Build(), null, backgroundHandler);
            }
            catch (Exception e)
            {
                _cameraModule.ErrorMessage = e.ToString();
            }
        }

        private void TakePhoto2()
        {
            try
            {
                if (_camera2 == null || _openingCamera2) return;

                var reader =
                    ImageReader.NewInstance(_picture2Size.Width, _picture2Size.Height, ImageFormatType.Jpeg, 1);
                var outputSurfaces = new List<Surface>(2) {reader.Surface, new Surface(_surfaceTexture)};

                var captureBuilder = _camera2.CreateCaptureRequest(CameraTemplate.StillCapture);
                captureBuilder.AddTarget(reader.Surface);
                captureBuilder.Set(CaptureRequest.ControlMode, new Integer((int) ControlMode.Auto));

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

                captureBuilder.Set(CaptureRequest.JpegOrientation, new Integer(neededRotation));

                var readerListener = new ImageAvailableListener();
                readerListener.Photo += (sender, buffer) =>
                {
                    Device.BeginInvokeOnMainThread(() => { _cameraModule.CapturedImage = buffer; });
                };
                readerListener.Error += (sender, exception) => { _cameraModule.ErrorMessage = exception.ToString(); };

                var thread = new HandlerThread("CameraPicture");
                thread.Start();
                var backgroundHandler = new Handler(thread.Looper);
                reader.SetOnImageAvailableListener(readerListener, backgroundHandler);

                var captureListener = new CameraCaptureListener();

                captureListener.PhotoComplete += (sender, e) => { StartPreview2(_camera2, true); };

                _camera2.CreateCaptureSession(outputSurfaces, new CameraCaptureStateListener
                {
                    OnConfiguredAction = session =>
                    {
                        try
                        {
                            _previewSession = session;
                            session.Capture(captureBuilder.Build(), captureListener, backgroundHandler);
                            _cameraModule.CaptureSuccess = !_cameraModule.CaptureSuccess;
                        }
                        catch (CameraAccessException ex)
                        {
                            Log.WriteLine(LogPriority.Info, "Capture Session error: ", ex.ToString());
                        }
                    }
                }, backgroundHandler);
            }
            catch (Exception e)
            {
                _cameraModule.ErrorMessage = e.ToString();
            }
        }

#endregion

    }
}

