using CrossCam.Droid.CustomRenderer;
using CrossCam.Wrappers;
using SkiaSharp;
using Xamarin.Forms;

[assembly: Dependency(typeof(OpenCv))]
namespace CrossCam.Droid.CustomRenderer
{
    public class OpenCv : IOpenCv
    {
        public SKBitmap CreateAlignedSecondImage(SKBitmap firstImage, SKBitmap secondImage, int downsizePercentage, int iterations,
            int epsilonLevel)
        {
            throw new System.NotImplementedException();
        }
    }
}