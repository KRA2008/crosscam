using CrossCam.Model;
using SkiaSharp;

namespace CrossCam.Wrappers
{
    public interface IOpenCv
    {
        bool IsOpenCvSupported();

        AlignedResult CreateAlignedSecondImageEcc(SKBitmap firstImage, SKBitmap secondImage,
            AlignmentSettings settings);
        AlignedResult CreateAlignedSecondImageKeypoints(SKBitmap firstImage, SKBitmap secondImage,
            AlignmentSettings settings, bool keystoneRightOnFirst);
        SKImage AddBarrelDistortion(SKImage originalImage, float downsize, float strength, float cxProportion);
        byte[] GetBytes(SKBitmap bitmap, double downsize, SKFilterQuality filterQuality = SKFilterQuality.High);
    }
}