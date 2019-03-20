﻿using System;
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
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            {
                _cameraManager = (CameraManager)MainActivity.Instance.GetSystemService(Context.CameraService);
                _useCamera2 = true;

                _orientations.Append((int)SurfaceOrientation.Rotation0, 0);
                _orientations.Append((int)SurfaceOrientation.Rotation90, 90);
                _orientations.Append((int)SurfaceOrientation.Rotation180, 180);
                _orientations.Append((int)SurfaceOrientation.Rotation270, 270);

                FindCamera2();
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
            if (!_isRunning && !_useCamera2) return;
            
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
                    rotation2 = 0;
                    verticalOffset = (moduleHeight - proportionalPreviewHeight) / 2f; //TODO: extract this and reduce duplication
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
                    rotation2 = -180;
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
                    rotation2 = -90;
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
                    rotation2 = -270;
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

#endregion

#region camera2

        private void FindCamera2()
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
                return;
            }

            var characteristics = _cameraManager.GetCameraCharacteristics(_camera2Id);

            _camera2SensorOrientation = (int)characteristics.Get(CameraCharacteristics.SensorOrientation);

            var map = (StreamConfigurationMap)characteristics.Get(CameraCharacteristics
                .ScalerStreamConfigurationMap);
            var previewSizes = map.GetOutputSizes(Class.FromType(typeof(SurfaceTexture))).ToList();
            var pictureSizes = map.GetOutputSizes((int)ImageFormatType.Jpeg).ToList();

            var previewSizesByTotal = previewSizes.OrderByDescending(s => s.Width * s.Height);
            var pictureSizesByTotal = pictureSizes.OrderByDescending(s => s.Width * s.Height);

            _picture2Size = pictureSizesByTotal.First();
            var pictureAspectRatio = _picture2Size.Width > _picture2Size.Height
                ? _picture2Size.Width / _picture2Size.Height
                : _picture2Size.Height / _picture2Size.Width;

            _preview2Size = previewSizesByTotal.First(p =>
                pictureAspectRatio == (p.Width > p.Height ? p.Width / p.Height : p.Height / p.Width));

            _stateListener = new CameraStateListener(this);
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
            if (_camera2 == null || _openingCamera2) return;

            var reader = ImageReader.NewInstance(_picture2Size.Width, _picture2Size.Height, ImageFormatType.Jpeg, 1);
            var outputSurfaces = new List<Surface>(2) { reader.Surface, new Surface(_surfaceTexture) };

            var captureBuilder = _camera2.CreateCaptureRequest(CameraTemplate.StillCapture);
            captureBuilder.AddTarget(reader.Surface);
            captureBuilder.Set(CaptureRequest.ControlMode, new Integer((int)ControlMode.Auto));

            var windowManager = MainActivity.Instance.GetSystemService(Context.WindowService).JavaCast<IWindowManager>();
            var rotation = windowManager.DefaultDisplay.Rotation;
            
            var phoneOrientation = _orientations.Get((int) rotation);
            var neededRotation = 0;
            switch (phoneOrientation)
            {
                case 0:
                    neededRotation = _camera2SensorOrientation % 360;
                    break;
                case 90:
                    neededRotation = (_camera2SensorOrientation - 90) % 360;
                    break;
                case 180:
                    neededRotation = (_camera2SensorOrientation + 180) % 360;
                    break;
                case 270:
                    neededRotation = (_camera2SensorOrientation + 90) % 360;
                    break;
            }
            //TODO: inverted portrait on S9 is wrong...
            captureBuilder.Set(CaptureRequest.JpegOrientation, new Integer(neededRotation));

            var readerListener = new ImageAvailableListener();
            readerListener.Photo += (sender, buffer) =>
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    _cameraModule.CapturedImage = buffer;
                });
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

#endregion

    }
}

