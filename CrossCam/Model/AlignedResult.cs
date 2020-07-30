using SkiaSharp;

namespace CrossCam.Model
{
    public class AlignedResult
    {
        public SKBitmap AlignedBitmap { get; set; }
        public SKMatrix TransformMatrix { get; set; }
    }
}