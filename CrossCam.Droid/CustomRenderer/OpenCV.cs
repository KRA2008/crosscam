using CrossCam.Droid.CustomRenderer;
using CrossCam.Wrappers;
using SkiaSharp;
using Xamarin.Forms;

[assembly: Dependency(typeof(OpenCv))]
namespace CrossCam.Droid.CustomRenderer
{
    public class OpenCv : IOpenCv
    {
        public byte[] CreateAlignedSecondImage(SKBitmap firstImage, SKBitmap secondImage)
        {
            throw new System.NotImplementedException();
        }
    }
}