using CrossCam.Model;
using SkiaSharp;

namespace CrossCam.Wrappers
{
    public interface IOpenCv
    {
        AlignedResult CreateAlignedSecondImage(SKBitmap firstImage, SKBitmap secondImage, int downsizePercentage, int iterations, int epsilonLevel, int eccCutoff);
    }
}