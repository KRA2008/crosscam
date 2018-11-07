using System.IO;
using SkiaSharp;

namespace CrossCam.ViewModel
{
    public class DrawTool
    {
        public static void DrawImageOnCanvas(SKImageInfo info, SKCanvas canvas, byte[] byteArray, bool isLeft, int border,
            int leftImageLeftCrop, int leftImageRightCrop, int rightImageLeftCrop, int rightImageRightCrop)
        {
            var bitmap = GetBitmapAndCorrectOrientation(byteArray);
            var imageAspectRatio = bitmap.Height / (1f * bitmap.Width);
            var screenWidth = info.Width;
            var screenHeight = info.Height;
            float previewHeight;
            float previewWidth;
            float previewX;
            float previewY;
            if (screenHeight > screenWidth) // screen portrait
            {
                previewWidth = screenWidth / 2f;
                previewHeight = imageAspectRatio * previewWidth;
                previewX = 0;
                previewY = (screenHeight - previewHeight) / 2f;
            }
            else // screen landscape
            {
                if (bitmap.Height > bitmap.Width) // image portrait
                {
                    previewHeight = screenHeight;
                    previewWidth = previewHeight / imageAspectRatio;
                    previewX = screenWidth / 2f - previewWidth;
                    previewY = 0;
                }
                else // image landscape
                {
                    previewWidth = screenWidth / 2f;
                    previewHeight = previewWidth * imageAspectRatio;
                    previewX = 0;
                    previewY = (screenHeight - previewHeight) / 2f;
                }
            }

            canvas.DrawBitmap(bitmap,
                SKRect.Create(0, 0, bitmap.Width, bitmap.Height),
                SKRect.Create(
                    (isLeft ? previewX : screenWidth / 2f) + border,
                    previewY + border,
                    previewWidth - border * 2,
                    previewHeight - border * 2));
            bitmap.Dispose();
        }

        private static SKBitmap GetBitmapAndCorrectOrientation(byte[] byteArray)
        {
            SKCodecOrigin origin;

            using (var stream = new MemoryStream(byteArray))
            using (var data = SKData.Create(stream))
            using (var codec = SKCodec.Create(data))
            {
                origin = codec.Origin;
            }

            switch (origin)
            {
                case SKCodecOrigin.BottomRight:
                    return BitmapRotate180(SKBitmap.Decode(byteArray));
                case SKCodecOrigin.RightTop:
                    return BitmapRotate90(SKBitmap.Decode(byteArray));
                default:
                    return SKBitmap.Decode(byteArray);
            }
        }

        private static SKBitmap BitmapRotate90(SKBitmap originalBitmap)
        {
            var rotated = new SKBitmap(originalBitmap.Height, originalBitmap.Width);

            using (var surface = new SKCanvas(rotated))
            {
                surface.Translate(rotated.Width, 0);
                surface.RotateDegrees(90);
                surface.DrawBitmap(originalBitmap, 0, 0);
            }

            return rotated;
        }

        private static SKBitmap BitmapRotate180(SKBitmap originalBitmap)
        {
            var rotated = new SKBitmap(originalBitmap.Width, originalBitmap.Height);

            using (var surface = new SKCanvas(rotated))
            {
                surface.Translate(rotated.Width, rotated.Height);
                surface.RotateDegrees(180);
                surface.DrawBitmap(originalBitmap, 0, 0);
            }

            return rotated;
        }
    }
}