using SkiaSharp;

namespace CrossCam.Model
{
    public class IncomingFrame
    {
        public SKBitmap Frame { get; set; }
        public SKEncodedOrigin Orientation { get; set; }
        public bool IsFrontFacing { get; set; }
    }
}