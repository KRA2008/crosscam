using SkiaSharp;

namespace CrossCam.Model
{
    public class AlignedResult
    {
        public SKBitmap AlignedFirstBitmap { get; set; }
        public SKBitmap AlignedSecondBitmap { get; set; }
        public SKMatrix FirstTransformMatrix { get; set; }
        public SKMatrix SecondTransformMatrix { get; set; }
    }
}