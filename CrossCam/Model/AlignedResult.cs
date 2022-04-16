using SkiaSharp;

namespace CrossCam.Model
{
    public class AlignedResult
    {
        public SKMatrix TransformMatrix1 { get; set; }
        public SKMatrix TransformMatrix2 { get; set; }
        public int CleanMatchesCount { get; set; }
        public SKBitmap DrawnCleanMatches { get; set; }
        public int DirtyMatchesCount { get; set; }
        public SKBitmap DrawnDirtyMatches { get; set; }
    }
}