using SkiaSharp;

namespace CrossCam.Wrappers
{
    public interface IOpenCv
    {
        byte[] CreateAlignedSecondImage(SKBitmap firstImage, SKBitmap secondImage);
    }
}