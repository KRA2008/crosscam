using SkiaSharp;

namespace CrossCam.Model
{
    public class PreviewFrame
    {
        public SKBitmap Frame { get; set; }
        public SKEncodedOrigin Orientation { get; set; }
    }
}