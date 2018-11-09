using SkiaSharp;

namespace CrossCam.Page
{
    public class DrawTool
    {
        public static void DrawImagesOnCanvas(
            SKImageInfo info, SKCanvas canvas, SKBitmap leftBitmap, SKBitmap rightBitmap, float border, 
            float leftLeftCrop, float leftRightCrop, float rightLeftCrop, float rightRightCrop,
            float topCrop, float bottomCrop)
        {
            float imageAspectRatio;
            float bitmapHeight;
            if (leftBitmap != null)
            {
                imageAspectRatio = leftBitmap.Height / (1f * leftBitmap.Width);
                bitmapHeight = leftBitmap.Height;
            }
            else if (rightBitmap != null)
            {
                imageAspectRatio = rightBitmap.Height / (1f * rightBitmap.Width);
                bitmapHeight = rightBitmap.Height;
            }
            else
            {
                return;
            }

            var screenWidth = info.Width;
            var screenHeight = info.Height;

            var orientationRegulatedBorder = border * screenWidth;

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
                if (imageAspectRatio > 1) // image portrait
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

            var leftX = previewX + orientationRegulatedBorder;
            var rightX = screenWidth / 2f + orientationRegulatedBorder;
            var y = previewY + orientationRegulatedBorder;
            var width = previewWidth - orientationRegulatedBorder * 2;
            var height = previewHeight - orientationRegulatedBorder * 2;

            var screenTopCrop = topCrop * height;
            var screenBottomCrop = bottomCrop * height;

            var topConvertedCrop = topCrop * bitmapHeight;
            var bottomConvertedCrop = bottomCrop * bitmapHeight;

            float screenLeftCrop;
            float screenRightCrop;
            float leftConvertedCrop;
            float rightConvertedCrop;

            if (leftBitmap != null)
            {
                screenLeftCrop = leftLeftCrop * width;
                screenRightCrop = leftRightCrop * width;
                leftConvertedCrop = leftLeftCrop * leftBitmap.Width;
                rightConvertedCrop = leftRightCrop * leftBitmap.Width;
                canvas.DrawBitmap(leftBitmap,
                    SKRect.Create(
                        leftConvertedCrop,
                        topConvertedCrop,
                        leftBitmap.Width - leftConvertedCrop - rightConvertedCrop,
                        leftBitmap.Height - topConvertedCrop - bottomConvertedCrop),
                    SKRect.Create(
                        leftX + screenLeftCrop + screenRightCrop,
                        y + screenTopCrop,
                        width - screenLeftCrop - screenRightCrop,
                        height - screenTopCrop - screenBottomCrop));
            }

            if (rightBitmap != null)
            {
                screenLeftCrop = rightLeftCrop * width;
                screenRightCrop = rightRightCrop * width;
                leftConvertedCrop = rightLeftCrop * rightBitmap.Width;
                rightConvertedCrop = rightRightCrop * rightBitmap.Width;
                canvas.DrawBitmap(rightBitmap,
                    SKRect.Create(
                        leftConvertedCrop,
                        topConvertedCrop,
                        rightBitmap.Width - leftConvertedCrop - rightConvertedCrop,
                        rightBitmap.Height - topConvertedCrop - bottomConvertedCrop),
                    SKRect.Create(
                        rightX,
                        y + screenTopCrop,
                        width - screenLeftCrop - screenRightCrop,
                        height - screenTopCrop - screenBottomCrop));
            }
        }
    }
}