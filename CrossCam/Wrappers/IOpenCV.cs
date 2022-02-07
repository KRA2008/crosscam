using CrossCam.Model;
using SkiaSharp;

namespace CrossCam.Wrappers
{
    public interface IOpenCv
    {
        bool IsOpenCvSupported();
        AlignedResult CreateAlignedSecondImageEcc(SKBitmap firstImage, SKBitmap secondImage, bool discardTransX, AlignmentSettings settings);
        AlignedResult CreateAlignedSecondImageKeypoints(SKBitmap firstImage, SKBitmap secondImage,
            bool discardTransX, AlignmentSettings settings, bool keystoneRightOnFirst);
        SKBitmap AddBarrelDistortion(SKBitmap originalImage, float strength);
    }
}