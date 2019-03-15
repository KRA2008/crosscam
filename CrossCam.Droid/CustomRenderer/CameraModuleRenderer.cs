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
using Java.Lang;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;
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
    public class CameraModuleRenderer : ViewRenderer<CameraModule, View>, TextureView.ISurfaceTextureListener, Camera.IShutterCallback, Camera.IPictureCallback, View.IOnTouchListener, Camera.IErrorCallback
    {
        private Camera _camera1;
        private View _view;

        private Activity _activity;
        private TextureView _textureView;
        private SurfaceTexture _surfaceTexture;
        private CameraModule _cameraModule;

        private static Camera.Size _previewSize;
        private static Camera.Size _pictureSize;

        private bool _isRunning;

        private bool _wasCameraRunningBeforeMinimize;

        private readonly bool _useCamera2;
        private readonly CameraManager _cameraManager;
        private CameraStateListener _stateListener;
        private CameraDevice _camera2;
        private CaptureRequest.Builder _previewBuilder;
        private CameraCaptureSession _previewSession;
        private bool _openingCamera2;
        private Size _preview2Size;

        public CameraModuleRenderer(Context context) : base(context)
        {
            MainActivity.Instance.LifecycleEventListener.OrientationChanged += (sender, args) =>
            {
                OrientationChanged();
            };
            MainActivity.Instance.LifecycleEventListener.AppMaximized += AppWasMaximized;
            MainActivity.Instance.LifecycleEventListener.AppMinimized += AppWasMinimized;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            {
                _cameraManager = (CameraManager)MainActivity.Instance.GetSystemService(Context.CameraService);
                _useCamera2 = true;
            }
        }

        private void AppWasMinimized(object obj, EventArgs args)
        {
            if (_useCamera2)
            {

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
                            if (_useCamera2)
                            {
                                OpenCamera2();
                            }
                            else
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
                    _cameraModule.IsNothingCaptured)
                {
                    TurnOnContinuousFocus();
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
            catch (Exception ex)
            {
                _cameraModule.ErrorMessage = ex.ToString();
            }
        }

        private void TurnOnFocusLockIfNothingCaptured()
        {
            if (_cameraModule.IsNothingCaptured)
            {
                if (_useCamera2)
                {

                }
                else
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
        }

        private void TurnOnContinuousFocus(Camera.Parameters providedParameters = null)
        {
            if (_useCamera2)
            {

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
                SetOrientation2();
                StartPreview2();
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
                
            }
            else
            {
                StopCamera1();
            }

            _surfaceTexture = null;
            return true;
        }

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

        public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height)
        {
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

                        var landscapePictureDescendingSizes = parameters
                            .SupportedPictureSizes
                            .Where(p => p.Width > p.Height)
                            .OrderByDescending(p => p.Width * p.Height)
                            .ToList();
                        var landscapePreviewDescendingSizes = parameters
                            .SupportedPreviewSizes
                            .Where(p => p.Width > p.Height)
                            .OrderByDescending(p => p.Width * p.Height)
                            .ToList();

                        foreach (var pictureSize in landscapePictureDescendingSizes)
                        {
                            foreach (var previewSize in landscapePreviewDescendingSizes)
                            {
                                if (Math.Abs((double)pictureSize.Width / pictureSize.Height -
                                             (double)previewSize.Width / previewSize.Height) < 0.0001)
                                {
                                    _pictureSize = pictureSize;
                                    _previewSize = previewSize;
                                    break;
                                }
                            }

                            if (_pictureSize != null)
                            {
                                break;
                            }
                        }

                        if (_pictureSize == null ||
                            _previewSize == null)
                        {
                            _pictureSize = landscapePictureDescendingSizes.First();
                            _previewSize = landscapePreviewDescendingSizes.First();
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

                    SetOrientation1();
                    StartCamera1();
                }
            }
            catch (Exception e)
            {
                _cameraModule.ErrorMessage = e.ToString();
            }
        }

        private void OrientationChanged()
        {
            if (_surfaceTexture != null)
            {
                if (_isRunning && !_useCamera2)
                {
                    SetOrientation1();
                    return;
                }

                if (_useCamera2)
                {
                    SetOrientation2();
                }
            }
        }

        private void SetOrientation1()
        {
            var metrics = new DisplayMetrics();
            Display.GetMetrics(metrics);
            
            var moduleWidth = _cameraModule.Width * metrics.Density;
            var moduleHeight = _cameraModule.Height * metrics.Density;

            var parameters = _camera1.GetParameters();
            double proportionalPreviewHeight;
            switch (Display.Rotation)
            {
                case SurfaceOrientation.Rotation0:
                    _cameraModule.IsViewInverted = false;
                    _cameraModule.IsPortrait = true;
                    proportionalPreviewHeight = _previewSize.Width * moduleWidth / _previewSize.Height;
                    _camera1.SetDisplayOrientation(90);
                    parameters.SetRotation(90);
                    break;
                case SurfaceOrientation.Rotation180:
                    _cameraModule.IsViewInverted = true;
                    _cameraModule.IsPortrait = true;
                    proportionalPreviewHeight = _previewSize.Width * moduleWidth / _previewSize.Height;
                    _camera1.SetDisplayOrientation(270);
                    parameters.SetRotation(270);
                    break;
                case SurfaceOrientation.Rotation90:
                    _cameraModule.IsViewInverted = false;
                    _cameraModule.IsPortrait = false;
                    proportionalPreviewHeight = _previewSize.Height * moduleWidth / _previewSize.Width;
                    _camera1.SetDisplayOrientation(0);
                    parameters.SetRotation(0);
                    break;
                default:
                    _cameraModule.IsPortrait = false;
                    _cameraModule.IsViewInverted = true;
                    proportionalPreviewHeight = _previewSize.Height * moduleWidth / _previewSize.Width;
                    _camera1.SetDisplayOrientation(180);
                    parameters.SetRotation(180);
                    break;
            }
            _camera1.SetParameters(parameters);

            var verticalOffset = (moduleHeight - proportionalPreviewHeight) / 2f;

            _textureView.SetX(0);
            _textureView.SetY((float)verticalOffset);

            _cameraModule.PreviewBottomY = (moduleHeight - verticalOffset)/metrics.Density;

            _textureView.LayoutParameters = new FrameLayout.LayoutParams((int) Math.Round(moduleWidth),
                (int)Math.Round(proportionalPreviewHeight));
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
                    TurnOnFocusLockIfNothingCaptured();
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
                        _camera1.StartPreview();
                    }
                }
                catch (Exception e)
                {
                    _cameraModule.ErrorMessage = e.ToString();
                }
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

            return new Rect((int)(rectF.Left+0.5), (int)(rectF.Top + 0.5), (int)(rectF.Right + 0.5), (int)(rectF.Bottom + 0.5));
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

        private void OpenCamera2()
        {
            if (_openingCamera2)
            {
                return;
            }

            _openingCamera2 = true;

            var cameraIds = _cameraManager.GetCameraIdList();
            var backFacingCameraId = "";
            foreach (var cameraId in cameraIds)
            {
                var cameraChars = _cameraManager.GetCameraCharacteristics(cameraId);
                var direction = (int)cameraChars.Get(CameraCharacteristics.LensFacing);
                if (direction == (int)LensFacing.Back)
                {
                    backFacingCameraId = cameraId;
                    break;
                }
            }

            var characteristics = _cameraManager.GetCameraCharacteristics(backFacingCameraId);
            var map = (StreamConfigurationMap)characteristics.Get(CameraCharacteristics.ScalerStreamConfigurationMap);

            var previewSizes = map.GetOutputSizes(Class.FromType(typeof(SurfaceTexture)));
            _preview2Size = previewSizes[0];

            _stateListener = new CameraStateListener(this);

            _cameraManager.OpenCamera(backFacingCameraId, _stateListener, null);
        }

        public void StartPreview2(CameraDevice camera = null)
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

            _camera2.CreateCaptureSession(new List<Surface> { surface },
                new CameraCaptureStateListener
                {
                    OnConfigureFailedAction = session =>
                    {
                    },
                    OnConfiguredAction = session =>
                    {
                        _previewSession = session;
                        SetRefreshingPreview2();
                    }
                },
                null);
            _openingCamera2 = false;
        }

        public void Camera2Disconnected(CameraDevice camera)
        {
            camera.Close();
            _openingCamera2 = false;
        }

        public void Camera2Errored(CameraDevice camera, Android.Hardware.Camera2.CameraError error)
        {
            _cameraModule.ErrorMessage = error.ToString();

            camera.Close();
            _openingCamera2 = false;
        }

        private void SetOrientation2()
        {
            if (_surfaceTexture == null || _preview2Size == null) return;

            var metrics = new DisplayMetrics();
            Display.GetMetrics(metrics);
            
            var matrix = new Matrix();
            var viewWidth = (float) _cameraModule.Width * metrics.Density;
            var viewHeight = (float) _cameraModule.Height * metrics.Density;

            float previewDisplayHeight;
            float rotation;
            float previewDisplayYOffset;
            RectF previewDisplayRect;
            RectF viewRect;
            switch (Display.Rotation)
            {
                case SurfaceOrientation.Rotation0:
                    _cameraModule.IsViewInverted = false;
                    _cameraModule.IsPortrait = true;
                    previewDisplayHeight = _preview2Size.Width * viewWidth / _preview2Size.Height;
                    previewDisplayYOffset = (viewHeight - previewDisplayHeight) / 2;
                    viewRect = new RectF(0, 0, viewWidth, viewHeight);
                    previewDisplayRect = new RectF(0, previewDisplayYOffset, viewWidth,
                        previewDisplayHeight + previewDisplayYOffset);
                    rotation = 0;
                    break;
                case SurfaceOrientation.Rotation180:
                    _cameraModule.IsViewInverted = true;
                    _cameraModule.IsPortrait = true;
                    previewDisplayHeight = _preview2Size.Width * viewWidth / _preview2Size.Height;
                    previewDisplayYOffset = (viewHeight - previewDisplayHeight) / 2;
                    viewRect = new RectF(0, 0, viewWidth, viewHeight);
                    previewDisplayRect = new RectF(0, previewDisplayYOffset, viewWidth,
                        previewDisplayHeight + previewDisplayYOffset);
                    rotation = 180;
                    break;
                case SurfaceOrientation.Rotation90:
                    _cameraModule.IsViewInverted = false;
                    _cameraModule.IsPortrait = false;
                    previewDisplayHeight = _preview2Size.Height * viewWidth / _preview2Size.Width;
                    previewDisplayYOffset = (viewHeight - previewDisplayHeight) / 2;
                    viewRect = new RectF(0, 0, viewWidth, viewHeight);
                    previewDisplayRect = new RectF(0, previewDisplayYOffset, viewWidth,
                        previewDisplayHeight + previewDisplayYOffset);
                    rotation = 0;
                    break;
                default:
                    _cameraModule.IsPortrait = false;
                    _cameraModule.IsViewInverted = true;
                    previewDisplayHeight = _preview2Size.Height * viewWidth / _preview2Size.Width;
                    previewDisplayYOffset = (viewHeight - previewDisplayHeight) / 2;
                    viewRect = new RectF(0, 0, viewWidth, viewHeight);
                    previewDisplayRect = new RectF(0, previewDisplayYOffset, viewWidth,
                        previewDisplayHeight + previewDisplayYOffset);
                    rotation = 90;
                    break;
            }

            _cameraModule.PreviewBottomY = (previewDisplayHeight + previewDisplayYOffset) / metrics.Density;

            matrix.SetRectToRect(viewRect, previewDisplayRect, Matrix.ScaleToFit.Fill);

            matrix.PostRotate(rotation, viewRect.CenterX(), viewRect.CenterY());

            _textureView.LayoutParameters = new FrameLayout.LayoutParams((int)viewWidth, (int)viewHeight);

            _textureView.SetTransform(matrix);
        }

        private void SetRefreshingPreview2()
        {
            _previewBuilder.Set(CaptureRequest.ControlMode, new Integer((int)ControlMode.Auto));
            var thread = new HandlerThread("CameraPreview");
            thread.Start();
            var backgroundHandler = new Handler(thread.Looper);

            _previewSession.SetRepeatingRequest(_previewBuilder.Build(), null, backgroundHandler);
        }

        private void TakePhoto2()
        {
            var characteristics = _cameraManager.GetCameraCharacteristics(_camera2.Id);

            var sizes = ((StreamConfigurationMap)characteristics.Get(CameraCharacteristics.ScalerStreamConfigurationMap)).GetOutputSizes((int)ImageFormatType.Jpeg);

            var reader = ImageReader.NewInstance(sizes[0].Width, sizes[0].Height, ImageFormatType.Jpeg, 1);//TODO: get the optimum size, don't just use the first one
            var outputSurfaces = new List<Surface>(2) { reader.Surface, new Surface(_surfaceTexture) };

            var captureBuilder = _camera2.CreateCaptureRequest(CameraTemplate.StillCapture);
            captureBuilder.AddTarget(reader.Surface);
            captureBuilder.Set(CaptureRequest.ControlMode, new Integer((int)ControlMode.Auto));

            var windowManager = MainActivity.Instance.GetSystemService(Context.WindowService).JavaCast<IWindowManager>();
            var rotation = windowManager.DefaultDisplay.Rotation;

            var orientations = new SparseIntArray();
            orientations.Append((int)SurfaceOrientation.Rotation0, 0);
            orientations.Append((int)SurfaceOrientation.Rotation90, 90);
            orientations.Append((int)SurfaceOrientation.Rotation180, 180);
            orientations.Append((int)SurfaceOrientation.Rotation270, 270);
            captureBuilder.Set(CaptureRequest.JpegOrientation, new Integer(orientations.Get((int)rotation))); //TODO: use the other orientation stuff?

            var readerListener = new ImageAvailableListener();
            readerListener.Photo += (sender, buffer) =>
            {
                _cameraModule.CapturedImage = buffer;
            };
            readerListener.Error += (sender, exception) =>
            {
                _cameraModule.ErrorMessage = exception.ToString();
            };

            var thread = new HandlerThread("CameraPicture");
            thread.Start();
            var backgroundHandler = new Handler(thread.Looper);
            reader.SetOnImageAvailableListener(readerListener, backgroundHandler);


            var captureListener = new CameraCaptureListener();

            captureListener.PhotoComplete += (sender, e) =>
            {
                StartPreview2(_camera2);
            };

            _camera2.CreateCaptureSession(outputSurfaces, new CameraCaptureStateListener
            {
                OnConfiguredAction = session =>
                {
                    try
                    {
                        _previewSession = session;
                        session.Capture(captureBuilder.Build(), captureListener, backgroundHandler);
                    }
                    catch (CameraAccessException ex)
                    {
                        Log.WriteLine(LogPriority.Info, "Capture Session error: ", ex.ToString());
                    }
                }
            }, backgroundHandler);
        }
    }
}

