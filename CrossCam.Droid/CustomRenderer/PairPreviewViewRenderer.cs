using Android.Graphics;
using Android.Views;
using Android.Widget;
using CrossCam.CustomElement;
using CrossCam.Droid.CustomRenderer;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;
using View = Android.Views.View;

[assembly: ExportRenderer(typeof(PairPreviewView), typeof(PairPreviewViewRenderer))]
namespace CrossCam.Droid.CustomRenderer
{
#pragma warning disable 618
    public class PairPreviewViewRenderer : ViewRenderer<PairPreviewView, View>, TextureView.ISurfaceTextureListener
#pragma warning restore 618
    {
        private PairPreviewView _previewView;
        private SurfaceTexture _surfaceTexture;
        private TextureView _textureView;

        protected override void OnElementChanged(ElementChangedEventArgs<PairPreviewView> e)
        {
            base.OnElementChanged(e);
            if (e.OldElement != null || Element == null)
            {
                return;
            }

            if (e.NewElement != null)
            {
                _previewView = e.NewElement;
                _previewView.BluetoothOperator.PreviewFrameReady += (sender2, args) =>
                {
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        DecodeAndApplyFrame(args.Frame);
                    });
                };
            }
        }

        public void OnSurfaceTextureAvailable(SurfaceTexture surface, int width, int height)
        {
            _textureView.LayoutParameters = new FrameLayout.LayoutParams(width, height);
            _surfaceTexture = surface;

            //_surfaceTexture.SetDefaultBufferSize(_previewSize.Width, _previewSize.Height);
        }

        public bool OnSurfaceTextureDestroyed(SurfaceTexture surface)
        {
            return true;
        }

        public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height)
        {
        }

        public void OnSurfaceTextureUpdated(SurfaceTexture surface)
        {
        }

        private void DecodeAndApplyFrame(byte[] frame)
        {
            //_textureView.Bitmap
        }
    }
}