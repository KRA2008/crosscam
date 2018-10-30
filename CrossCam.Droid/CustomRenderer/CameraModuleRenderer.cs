using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Hardware;
using Android.Util;
using Android.Views;
using Android.Widget;
using CrossCam.Droid.CustomRenderer;
using Java.Lang;
using SkiaSharp;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;
using CameraModule = CrossCam.CustomElement.CameraModule;
using Exception = Java.Lang.Exception;
using Math = System.Math;
using View = Android.Views.View;
#pragma warning disable 618
using Camera = Android.Hardware.Camera;

[assembly: ExportRenderer(typeof(CameraModule), typeof(CameraModuleRenderer))]
namespace CrossCam.Droid.CustomRenderer
{
    public class CameraModuleRenderer : ViewRenderer<CameraModule, View>, TextureView.ISurfaceTextureListener, Camera.IShutterCallback, Camera.IPictureCallback, View.IOnTouchListener
    {
        private Camera _camera;
        private View _view;

        private Activity _activity;
        private CameraFacing _cameraType;
        private TextureView _textureView;
        private SurfaceTexture _surfaceTexture;
        private CameraModule _cameraModule;

        private static Camera.Size _previewSize;
        private static Camera.Size _pictureSize;

        private bool _isSurfaceAvailable;
        private bool _isRunning;

        private bool _wasCameraRunningBeforeMinimize;

        public CameraModuleRenderer(Context context) : base(context)
        {
            MainActivity.Instance.LifecycleEventListener.OrientationChanged += (sender, args) =>
            {
                OrientationChanged();
            };
            MainActivity.Instance.LifecycleEventListener.AppMaximized += AppWasMaximized;
            MainActivity.Instance.LifecycleEventListener.AppMinimized += AppWasMinimized;
        }

        private void AppWasMinimized(object obj, EventArgs args)
        {
            if (_isRunning)
            {
                StopCamera();
                _wasCameraRunningBeforeMinimize = true;
            }
            else
            {
                _wasCameraRunningBeforeMinimize = false;
            }
        }

        private void AppWasMaximized(object obj, EventArgs args)
        {
            if (!_isRunning &&
                _wasCameraRunningBeforeMinimize)
            {
                SetupAndStartCamera();
            }
        }

        protected override void OnElementChanged(ElementChangedEventArgs<CameraModule> e)
        {
            base.OnElementChanged(e);

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
        }

        protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            base.OnElementPropertyChanged(sender, e);

            if (e.PropertyName == nameof(_cameraModule.IsVisible))
            {
                if (_cameraModule.IsVisible)
                {
                    if (_isSurfaceAvailable)
                    {
                        SetupAndStartCamera();
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

            if (e.PropertyName == nameof(_cameraModule.IsFullScreenPreview))
            {
                if (_isSurfaceAvailable && _isRunning)
                {
                    SetupAndStartCamera();
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
            var parameters = _camera.GetParameters();
            if (parameters.SupportedFocusModes.Contains(Camera.Parameters.FocusModeContinuousPicture))
            {
                parameters.FocusMode = Camera.Parameters.FocusModeContinuousPicture;
            }
            _camera.SetParameters(parameters);
        }

        private void SetupUserInterface()
        {
            _activity = Context as Activity;
            _view = _activity.LayoutInflater.Inflate(Resource.Layout.CameraLayout, this, false);
            _cameraType = CameraFacing.Back;

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

            _isSurfaceAvailable = true;
            SetupAndStartCamera(surface);
        }

        public bool OnSurfaceTextureDestroyed(SurfaceTexture surface)
        {
            _isSurfaceAvailable = false;
            StopCamera();
            return true;
        }

        private void StopCamera()
        {
            if (_isRunning)
            {
                _camera?.StopPreview();
                _camera?.Release();
                _camera = null;
                _isRunning = false;
            }
        }

        private void StartCamera()
        {
            if (!_isRunning)
            {
                _camera?.StartPreview();
                _isRunning = true;
            }
        }

        public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height)
        {
        }

        private void SetupAndStartCamera(SurfaceTexture surface = null)
        {
            try
            {
                if (_isSurfaceAvailable)
                {
                    if (_camera == null)
                    {
                        _camera = Camera.Open((int) _cameraType);

                        for (var ii = 0; ii < Camera.NumberOfCameras - 1; ii++)
                        {
                            var info = new Camera.CameraInfo();
                            Camera.GetCameraInfo(ii, info);
                            if (info.CanDisableShutterSound)
                            {
                                _camera.EnableShutterSound(false);
                            }
                        }

                        var parameters = _camera.GetParameters();
                        parameters.FlashMode = Camera.Parameters.FlashModeOff;
                        parameters.VideoStabilization = false;
                        parameters.JpegQuality = 100;
                        if (parameters.SupportedFocusModes.Contains(Camera.Parameters.FocusModeContinuousPicture))
                        {
                            parameters.FocusMode = Camera.Parameters.FocusModeContinuousPicture;
                        }

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
                                if (Math.Abs((double) pictureSize.Width / pictureSize.Height -
                                             (double) previewSize.Width / previewSize.Height) < 0.0001)
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

                        _camera.SetParameters(parameters);
                        _camera.SetPreviewTexture(_surfaceTexture);
                    }

                    if (surface != null)
                    {
                        _surfaceTexture = surface;
                    }

                    SetOrientation();
                    StartCamera();
                }
            }
            catch (Exception e)
            {
                _cameraModule.ErrorMessage = e.ToString();
            }
        }

        private void OrientationChanged()
        {
            if (_isSurfaceAvailable && _isRunning)
            {
                SetOrientation();
            }
        }

        private void SetOrientation()
        {
            var metrics = new DisplayMetrics();
            Display.GetMetrics(metrics);
            
            var moduleWidth = _cameraModule.Width * metrics.Density;
            var moduleHeight = _cameraModule.Height * metrics.Density;

            if (_cameraModule.IsFullScreenPreview)
            {
                double proportionalPreviewWidth;
                switch (Display.Rotation)
                {
                    case SurfaceOrientation.Rotation0: //portraits
                    case SurfaceOrientation.Rotation180:
                        _cameraModule.IsPortrait = true;
                        proportionalPreviewWidth = _previewSize.Height * moduleHeight / _previewSize.Width;
                        break;
                    default: //landscapes
                        _cameraModule.IsPortrait = false;
                        proportionalPreviewWidth = _previewSize.Width * moduleHeight / _previewSize.Height;
                        break;
                }
                var leftTrim = (proportionalPreviewWidth - moduleWidth) / 2f;

                _textureView.SetX((float)(-1f * leftTrim));
                _textureView.SetY(0);
                _textureView.LayoutParameters = new FrameLayout.LayoutParams((int)Math.Round(proportionalPreviewWidth),
                    (int)Math.Round(moduleHeight));
            }
            else
            {
                double proportionalPreviewHeight;
                switch (Display.Rotation)
                {
                    case SurfaceOrientation.Rotation0: //portraits
                    case SurfaceOrientation.Rotation180:
                        _cameraModule.IsPortrait = true;
                        proportionalPreviewHeight = _previewSize.Width * moduleWidth / _previewSize.Height;
                        break;
                    default: //landscapes
                        _cameraModule.IsPortrait = false;
                        proportionalPreviewHeight = _previewSize.Height * moduleWidth / _previewSize.Width;
                        break;
                }

                var verticalOffset = (moduleHeight - proportionalPreviewHeight) / 2f;

                _textureView.SetX(0);
                _textureView.SetY((float)verticalOffset);

                _textureView.LayoutParameters = new FrameLayout.LayoutParams((int) Math.Round(moduleWidth),
                    (int)Math.Round(proportionalPreviewHeight));
            }

            var parameters = _camera.GetParameters();

            var display = _activity.WindowManager.DefaultDisplay;
            if (display.Rotation == SurfaceOrientation.Rotation0) // portrait
            {
                _camera.SetDisplayOrientation(90);
                parameters.SetRotation(90);
            }
            else if (display.Rotation == SurfaceOrientation.Rotation90)
            {
                _camera.SetDisplayOrientation(0);
                parameters.SetRotation(0);
            }
            else if (display.Rotation == SurfaceOrientation.Rotation180) // portrait
            {
                _camera.SetDisplayOrientation(270);
                parameters.SetRotation(270);
            }
            else if (display.Rotation == SurfaceOrientation.Rotation270)
            {
                _camera.SetDisplayOrientation(180);
                parameters.SetRotation(180);
            }

            _camera.SetParameters(parameters);
        }

        private void TakePhotoButtonTapped()
        {
            _camera.TakePicture(this, this, this, this);
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
                    _camera.StartPreview();

                    SKCodecOrigin origin;

                    using (var stream = new MemoryStream(data))
                    using (var skData = SKData.Create(stream))
                    using (var codec = SKCodec.Create(skData))
                    {
                        origin = codec.Origin;
                    }

                    if (origin != SKCodecOrigin.BottomRight &&
                        origin != SKCodecOrigin.RightTop)
                    {
                        _cameraModule.CapturedImage = data;
                        return;
                    }

                    var correctedWidth = 0;
                    var correctedHeight = 0;
                    var correctDy = 0;
                    float correctRotateDegrees = 0;

                    var originalBitmap = SKBitmap.Decode(data);

                    switch (origin)
                    {
                        case SKCodecOrigin.BottomRight:
                            correctedWidth = originalBitmap.Width;
                            correctedHeight = originalBitmap.Height;
                            correctDy = correctedHeight;
                            correctRotateDegrees = 180;
                            break;
                        case SKCodecOrigin.RightTop:
                            correctedWidth = originalBitmap.Height;
                            correctedHeight = originalBitmap.Width;
                            correctDy = 0;
                            correctRotateDegrees = 90;
                            break;
                    }

                    SKImage correctedImage;
                    using (var tempSurface = SKSurface.Create(new SKImageInfo(correctedWidth, correctedHeight)))
                    {
                        var canvas = tempSurface.Canvas;

                        canvas.Clear(SKColors.Transparent);

                        canvas.Translate(correctedWidth, correctDy);
                        canvas.RotateDegrees(correctRotateDegrees);
                        canvas.DrawBitmap(originalBitmap, 0, 0);
                        originalBitmap.Dispose();

                        correctedImage = tempSurface.Snapshot();
                    }

                    byte[] correctedBytes;
                    using (var encoded = correctedImage.Encode(SKEncodedImageFormat.Jpeg, 100))
                    {
                        correctedBytes = encoded.ToArray();
                        correctedImage.Dispose();
                    }

                    _cameraModule.CapturedImage = correctedBytes;
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
            if (_camera != null)
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
                        var parameters = _camera.GetParameters();
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
                        _camera.SetParameters(parameters);
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
    }
}

