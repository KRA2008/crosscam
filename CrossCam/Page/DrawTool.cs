using SkiaSharp;

namespace CrossCam.Page
{
    public class DrawTool
    {
        public static void DrawImagesOnCanvas(
            SKCanvas canvas, SKBitmap leftBitmap, SKBitmap rightBitmap, int borderThickness,
            int leftLeftCrop, int leftRightCrop, int rightLeftCrop, int rightRightCrop,
            int topCrop, int bottomCrop, bool switchForParallel = false)
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

            float leftPreviewX;
            float rightPreviewX;
            float leftPreviewWidth;
            float rightPreviewWidth;
            if (switchForParallel)
            {
                leftPreviewX = canvasWidth / 2f + borderThickness / scalingRatio;
                rightPreviewX = canvasWidth / 2f - (rightBitmapWidthLessCrop + borderThickness) / scalingRatio;
                leftPreviewWidth = rightBitmapWidthLessCrop / scalingRatio;
                rightPreviewWidth = leftBitmapWidthLessCrop / scalingRatio;
            }
            else
            {
                leftPreviewX = canvasWidth / 2f - (leftBitmapWidthLessCrop + borderThickness) / scalingRatio;
                rightPreviewX = canvasWidth / 2f + borderThickness / scalingRatio;
                leftPreviewWidth = leftBitmapWidthLessCrop / scalingRatio;
                rightPreviewWidth = rightBitmapWidthLessCrop / scalingRatio;
            }

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
                        leftPreviewX,
                        previewY,
                        leftPreviewWidth,
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
                        rightPreviewX,
                        previewY,
                        rightPreviewWidth,
                        previewHeight));
            }
        }
    }
}