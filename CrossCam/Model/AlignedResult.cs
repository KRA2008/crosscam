using SkiaSharp;

namespace CrossCam.Model
{
    public class AlignedResult
    {
        public SKBitmap AlignedBitmap { get; set; }
        public int CleanMatchesCount { get; set; }
        public SKBitmap DrawnCleanMatches { get; set; }
        public int DirtyMatchesCount { get; set; }
        public SKBitmap DrawnDirtyMatches { get; set; }
        public SKMatrix TransformMatrix { get; set; }
    }
}