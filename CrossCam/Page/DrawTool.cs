using SkiaSharp;

namespace CrossCam.Page
{
    public class DrawTool
    {
        public static void DrawImagesOnCanvas(
            SKCanvas canvas, SKBitmap leftBitmap, SKBitmap rightBitmap, int borderThickness,
            int leftLeftCrop, int leftRightCrop, int rightLeftCrop, int rightRightCrop,
            int topCrop, int bottomCrop)
        {
            if (leftBitmap == null && rightBitmap == null) return;

            var canvasWidth = canvas.DeviceClipBounds.Width;
            var canvasHeight = canvas.DeviceClipBounds.Height;

            var leftBitmapWidthLessCrop = 0;
            var rightBitmapWidthLessCrop = 0;
            var bitmapHeightLessCrop = 0;

            if (leftBitmap != null)
            {
                leftBitmapWidthLessCrop = leftBitmap.Width  - leftLeftCrop - leftRightCrop;
                bitmapHeightLessCrop = leftBitmap.Height - topCrop - bottomCrop;
            }

            if (rightBitmap != null)
            {
                rightBitmapWidthLessCrop = rightBitmap.Width - rightLeftCrop - rightRightCrop;
                if (leftBitmap == null)
                {
                    bitmapHeightLessCrop = rightBitmap.Height - topCrop - bottomCrop;
                }
            }

            int effectiveJoinedWidth;
            if (leftBitmapWidthLessCrop > rightBitmapWidthLessCrop)
            {
                effectiveJoinedWidth = leftBitmapWidthLessCrop * 2;
            }
            else
            {
                effectiveJoinedWidth = rightBitmapWidthLessCrop * 2;
            }

            effectiveJoinedWidth += 4 * borderThickness;
            var effectiveJoinedHeight = bitmapHeightLessCrop + 2 * borderThickness;

            var widthRatio = effectiveJoinedWidth / (1f * canvasWidth);
            var heightRatio = effectiveJoinedHeight / (1f * canvasHeight);

            var scalingRatio = widthRatio > heightRatio ? widthRatio : heightRatio;

            var previewY = canvasHeight / 2f - bitmapHeightLessCrop / scalingRatio / 2f;
            var previewHeight = bitmapHeightLessCrop / scalingRatio;
            if (leftBitmap != null)
            {
                canvas.DrawBitmap(
                    leftBitmap,
                    SKRect.Create(
                        leftLeftCrop,
                        topCrop,
                        leftBitmapWidthLessCrop,
                        bitmapHeightLessCrop),
                    SKRect.Create(
                        canvasWidth / 2f - (leftBitmapWidthLessCrop + borderThickness) / scalingRatio,
                        previewY,
                        leftBitmapWidthLessCrop / scalingRatio,
                        previewHeight));
            }

            if (rightBitmap != null)
            {
                canvas.DrawBitmap(
                    rightBitmap,
                    SKRect.Create(
                        rightLeftCrop,
                        topCrop,
                        rightBitmapWidthLessCrop,
                        bitmapHeightLessCrop),
                    SKRect.Create(
                        canvasWidth / 2f + borderThickness / scalingRatio,
                        previewY,
                        rightBitmapWidthLessCrop / scalingRatio,
                        previewHeight));
            }
        }
    }
}