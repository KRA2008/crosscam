using SkiaSharp;

namespace CrossCam.Page
{
    public class DrawTool
    {
        public static void DrawImageOnCanvas(SKImageInfo info, SKCanvas canvas, SKBitmap bitmap, bool isLeft, int border, int leftCrop, int topCrop, int rightCrop, int bottomCrop)
        {
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

            var x = (isLeft ? previewX : screenWidth / 2f) + border;
            var y = previewY + border;
            var width = previewWidth - border * 2;
            var height = previewHeight - border * 2;

            var xCropRatio = bitmap.Width / width;
            var leftConvertedCrop = xCropRatio * leftCrop;
            var rightConvertedCrop = xCropRatio * rightCrop;
            var yCropRatio = bitmap.Height / height;
            var topConvertedCrop = yCropRatio * topCrop;
            var bottomConvertedCrop = yCropRatio * bottomCrop;

            canvas.DrawBitmap(bitmap,
                SKRect.Create(
                    leftConvertedCrop, 
                    topConvertedCrop, 
                    bitmap.Width - leftConvertedCrop - rightConvertedCrop, 
                    bitmap.Height - topConvertedCrop - bottomConvertedCrop),
                SKRect.Create(
                    x + (isLeft ? leftCrop+rightCrop : 0), 
                    y + topCrop, 
                    width - leftCrop - rightCrop, 
                    height - topCrop - bottomCrop));
        }
    }
}