using CrossCam.Model;
using SkiaSharp;

namespace CrossCam.Wrappers
{
    public interface IOpenCv
    {
        bool IsOpenCvSupported();
        AlignedResult CreateAlignedSecondImage(SKBitmap firstImage, SKBitmap secondImage, int downsizePercentage, int iterations, int epsilonLevel, int eccCutoff, int pyramidLayers, bool discardTransX, bool allowFullAffine);
    }
}