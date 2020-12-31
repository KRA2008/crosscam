using SkiaSharp;

namespace CrossCam.Model
{
    public class AlignedResult
    {
        public SKBitmap AlignedBitmap { get; set; }
        public SKBitmap DrawnMatches { get; set; }
        public SKMatrix TransformMatrix { get; set; }
        public SKBitmap Rectified1 { get; set; }
        public SKBitmap Rectified2 { get; set; }
    }
}