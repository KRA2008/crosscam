using SkiaSharp;

namespace CrossCam.Page
{
    public class DrawTool
    {
        public static void DrawImageOnCanvas(SKImageInfo info, SKCanvas canvas, SKBitmap bitmap, bool isLeft, float border, float leftCrop, float topCrop, float rightCrop, float bottomCrop)
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

            var orientationRegulatedBorder = border * screenWidth;
            var x = (isLeft ? previewX : screenWidth / 2f) + orientationRegulatedBorder;
            var y = previewY + orientationRegulatedBorder;
            var width = previewWidth - orientationRegulatedBorder * 2;
            var height = previewHeight - orientationRegulatedBorder * 2;

            var screenLeftCrop = leftCrop * width;
            var screenRightCrop = rightCrop * width;
            var screenTopCrop = topCrop * height;
            var screenBottomCrop = bottomCrop * height;
            
            var leftConvertedCrop = leftCrop * bitmap.Width;
            var rightConvertedCrop = rightCrop * bitmap.Width;
            var topConvertedCrop = topCrop * bitmap.Height;
            var bottomConvertedCrop = bottomCrop * bitmap.Height;

            canvas.DrawBitmap(bitmap,
                SKRect.Create(
                    leftConvertedCrop,
                    topConvertedCrop, 
                    bitmap.Width - leftConvertedCrop - rightConvertedCrop, 
                    bitmap.Height - topConvertedCrop - bottomConvertedCrop),
                SKRect.Create(
                    x + (isLeft ? screenLeftCrop + screenRightCrop : 0), 
                    y + screenTopCrop, 
                    width - screenLeftCrop - screenRightCrop, 
                    height - screenTopCrop - screenBottomCrop));
        }
    }
}