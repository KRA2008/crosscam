using System;
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
using SkiaSharp;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;
using CameraModule = CrossCam.CustomElement.CameraModule;
#pragma warning disable 618
using Camera = Android.Hardware.Camera;

[assembly: ExportRenderer(typeof(CameraModule), typeof(CameraModuleRenderer))]
namespace CrossCam.Droid.CustomRenderer
{
    public class CameraModuleRenderer : ViewRenderer<CameraModule, Android.Views.View>, TextureView.ISurfaceTextureListener, Camera.IShutterCallback, Camera.IPictureCallback
    {
        private Camera _camera;
        private Android.Views.View _view;

        private Activity _activity;
        private CameraFacing _cameraType;
        private TextureView _textureView;
        private SurfaceTexture _surfaceTexture;
        private CameraModule _cameraModule;

        private static Camera.Size _previewSize;
        private static Camera.Size _pictureSize;

        private bool _isSurfaceAvailable;
        private bool _isRunning;

        public CameraModuleRenderer(Context context) : base(context)
        {
            MainActivity.Instance.OrientationHelper.OrientationChanged += (sender, args) =>
            {
                OrientationChanged();
            };
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
                if (!_cameraModule.IsVisible)
                {
                    StopCamera();
                }
                else
                {
                    if (_isSurfaceAvailable)
                    {
                        SetupCamera();
                        StartCamera();
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
                    SetupCamera();
                }
            }
        }

        private void SetupUserInterface()
        {
            _activity = Context as Activity;
            _view = _activity.LayoutInflater.Inflate(Resource.Layout.CameraLayout, this, false);
            _cameraType = CameraFacing.Back;

            _textureView = _view.FindViewById<TextureView>(Resource.Id.textureView);
            _textureView.SurfaceTextureListener = this;
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

            SetupCamera(surface);
            _isSurfaceAvailable = true;
            StartCamera();
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
                if (_camera != null)
                {
                    _camera.StopPreview();
                    _camera.Release();
                    _camera = null;
                }

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

        private void SetupCamera(SurfaceTexture surface = null)
        {
            if (_camera == null)
            {
                _camera = Camera.Open((int)_cameraType);

                var parameters = _camera.GetParameters();
                parameters.FlashMode = Camera.Parameters.FlashModeOff;
                parameters.VideoStabilization = false;
                parameters.JpegQuality = 100;

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
                        if (Math.Abs((double)pictureSize.Width / pictureSize.Height - (double)previewSize.Width / previewSize.Height) < 0.0001)
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
            parameters.JpegQuality = 100;

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
        }
    }
}

