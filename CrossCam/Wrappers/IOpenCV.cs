using CrossCam.Model;
using SkiaSharp;

namespace CrossCam.Wrappers
{
    public interface IOpenCv
    {
        bool IsOpenCvSupported();
        AlignedResult CreateAlignedSecondImageEcc(SKBitmap firstImage, SKBitmap secondImage, 
            int downsizePercentage, int iterations, int epsilonLevel, int eccCutoff, int pyramidLayers, bool discardTransX);
        AlignedResult CreateAlignedSecondImageKeypoints(SKBitmap firstImage, SKBitmap secondImage,
            bool discardTransX, Settings settings);
    }
}