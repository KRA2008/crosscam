using SkiaSharp;

namespace CrossCam.Page
{
    public class DrawTool
    {
        public static void DrawImagesOnCanvas(
            SKCanvas canvas, SKBitmap leftBitmap, SKBitmap rightBitmap, int borderThickness,
            int outsideCrop, int insideCrop, int topCrop, int bottomCrop,
            float leftRotation, float rightRotation,
            int horizontalZoom,
            bool switchForParallel = false)
        {
            if (leftBitmap == null && rightBitmap == null) return;

            var canvasWidth = canvas.DeviceClipBounds.Width;
            var canvasHeight = canvas.DeviceClipBounds.Height;

            var leftBitmapWidthLessCropZoom = 0;
            var rightBitmapWidthLessCropZoom = 0;
            var bitmapHeightLessCropZoom = 0;
            var verticalZoom = 0;

            if (leftBitmap != null)
            {
                var aspectRatio = leftBitmap.Height / (1f * leftBitmap.Width);
                verticalZoom = (int)(aspectRatio * horizontalZoom);
                leftBitmapWidthLessCropZoom = leftBitmap.Width  - outsideCrop - insideCrop - 2 * horizontalZoom;
                bitmapHeightLessCropZoom = leftBitmap.Height - topCrop - bottomCrop - 2 * verticalZoom;
            }

            if (rightBitmap != null)
            {
                var aspectRatio = rightBitmap.Height / (1f * rightBitmap.Width);
                rightBitmapWidthLessCropZoom = rightBitmap.Width - insideCrop - outsideCrop - 2 * horizontalZoom;
                if (leftBitmap == null)
                {
                    verticalZoom = (int)(aspectRatio * horizontalZoom);
                    bitmapHeightLessCropZoom = rightBitmap.Height - topCrop - bottomCrop - 2 * verticalZoom;
                }
            }

            int effectiveJoinedWidth;
            if (leftBitmapWidthLessCropZoom > rightBitmapWidthLessCropZoom)
            {
                effectiveJoinedWidth = leftBitmapWidthLessCropZoom * 2;
            }
            else
            {
                effectiveJoinedWidth = rightBitmapWidthLessCropZoom * 2;
            }

            effectiveJoinedWidth += 4 * borderThickness;
            var effectiveJoinedHeight = bitmapHeightLessCropZoom + 2 * borderThickness;

            var widthRatio = effectiveJoinedWidth / (1f * canvasWidth);
            var heightRatio = effectiveJoinedHeight / (1f * canvasHeight);

            var scalingRatio = widthRatio > heightRatio ? widthRatio : heightRatio;

            var previewY = canvasHeight / 2f - bitmapHeightLessCropZoom / scalingRatio / 2f;
            var previewHeight = bitmapHeightLessCropZoom / scalingRatio;

            float leftPreviewX;
            float rightPreviewX;
            float leftPreviewWidth;
            float rightPreviewWidth;
            float innerLeftRotation;
            float innerRightRotation;
            if (switchForParallel)
            {
                leftPreviewX = canvasWidth / 2f + borderThickness / scalingRatio;
                rightPreviewX = canvasWidth / 2f - (rightBitmapWidthLessCropZoom + borderThickness) / scalingRatio;
                leftPreviewWidth = rightBitmapWidthLessCropZoom / scalingRatio;
                rightPreviewWidth = leftBitmapWidthLessCropZoom / scalingRatio;
                innerRightRotation = leftRotation;
                innerLeftRotation = rightRotation;
            }
            else
            {
                leftPreviewX = canvasWidth / 2f - (leftBitmapWidthLessCropZoom + borderThickness) / scalingRatio;
                rightPreviewX = canvasWidth / 2f + borderThickness / scalingRatio;
                leftPreviewWidth = leftBitmapWidthLessCropZoom / scalingRatio;
                rightPreviewWidth = rightBitmapWidthLessCropZoom / scalingRatio;
                innerRightRotation = rightRotation;
                innerLeftRotation = leftRotation;
            }

            if (leftBitmap != null)
            {
                canvas.RotateDegrees(innerLeftRotation);
                canvas.DrawBitmap(
                    leftBitmap,
                    SKRect.Create(
                        outsideCrop + horizontalZoom,
                        topCrop + verticalZoom,
                        leftBitmapWidthLessCropZoom,
                        bitmapHeightLessCropZoom),
                    SKRect.Create(
                        leftPreviewX,
                        previewY,
                        leftPreviewWidth,
                        previewHeight));
                canvas.RotateDegrees(-1 * innerLeftRotation);
            }

            if (rightBitmap != null)
            {
                canvas.RotateDegrees(innerRightRotation);
                canvas.DrawBitmap(
                    rightBitmap,
                    SKRect.Create(
                        insideCrop + horizontalZoom,
                        topCrop + verticalZoom,
                        rightBitmapWidthLessCropZoom,
                        bitmapHeightLessCropZoom),
                    SKRect.Create(
                        rightPreviewX,
                        previewY,
                        rightPreviewWidth,
                        previewHeight));
                canvas.RotateDegrees(-1 * innerRightRotation);
            }
        }
    }
}