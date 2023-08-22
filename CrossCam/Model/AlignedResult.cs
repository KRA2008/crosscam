using SkiaSharp;

namespace CrossCam.Model
{
    public class AlignedResult
    {
        public int Confidence { get; set; }
        public SKMatrix TransformMatrix1 { get; set; }
        public SKMatrix TransformMatrix2 { get; set; }
        public int CleanMatchesCount { get; set; }
        public SKBitmap DrawnCleanMatches { get; set; }
        public int DirtyMatchesCount { get; set; }
        public SKBitmap DrawnDirtyMatches { get; set; }
        public SKBitmap Warped1 { get; set; }
        public SKBitmap Warped2 { get; set; }
        public string MethodName { get; set; }

        public AlignedResult()
        {
            TransformMatrix1 = SKMatrix.CreateIdentity();
            TransformMatrix2 = SKMatrix.CreateIdentity();
            Confidence = -1;
        }
    }
}