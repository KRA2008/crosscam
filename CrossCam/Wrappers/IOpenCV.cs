using SkiaSharp;

namespace CrossCam.Wrappers
{
    public interface IOpenCv
    {
        SKBitmap CreateAlignedSecondImage(SKBitmap firstImage, SKBitmap secondImage, int downsizePercentage, int iterations, int epsilonLevel, int eccCutoff);
    }
}