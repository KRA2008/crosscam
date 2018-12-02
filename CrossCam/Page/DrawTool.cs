using System;
using SkiaSharp;

namespace CrossCam.Page
{
    public class DrawTool
    {
        public static void DrawImagesOnCanvas(
            SKCanvas canvas, SKBitmap leftBitmap, SKBitmap rightBitmap, int borderThickness,
            int leftLeftCrop, int leftRightCrop, int rightLeftCrop, int rightRightCrop,
            int leftTopCrop, int leftBottomCrop, int rightTopCrop, int rightBottomCrop,
            float leftRotation, float rightRotation, int alignment,
            int leftHorizontalZoom, int rightHorizontalZoom,
            bool switchForParallel = false)
        {
            if (leftBitmap == null && rightBitmap == null) return;

            var canvasWidth = canvas.DeviceClipBounds.Width;
            var canvasHeight = canvas.DeviceClipBounds.Height;

            var innerBorderThickness = leftBitmap != null && rightBitmap != null ? borderThickness : 0;
            var leftBitmapWidthLessCrop = 0;
            var rightBitmapWidthLessCrop = 0;
            var bitmapHeightLessCrop = 0;
            float aspectRatio;

            if (leftBitmap != null)
            {
                aspectRatio = leftBitmap.Height / (1f * leftBitmap.Width);
                var leftVerticalZoom = aspectRatio * leftHorizontalZoom;
                leftBitmapWidthLessCrop = leftBitmap.Width - leftLeftCrop - leftRightCrop - leftHorizontalZoom;
                bitmapHeightLessCrop = leftBitmap.Height - leftTopCrop - leftBottomCrop - Math.Abs(alignment);
            }

            if (rightBitmap != null)
            {
                aspectRatio = rightBitmap.Height / (1f * rightBitmap.Width);
                var rightVerticalZoom = aspectRatio * rightHorizontalZoom;
                rightBitmapWidthLessCrop = rightBitmap.Width - rightLeftCrop - rightRightCrop;
                if (leftBitmap == null)
                {
                    bitmapHeightLessCrop = rightBitmap.Height - rightTopCrop - rightBottomCrop - Math.Abs(alignment);
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

            effectiveJoinedWidth += 4 * innerBorderThickness;
            var effectiveJoinedHeight = bitmapHeightLessCrop + 2 * innerBorderThickness;

            var widthRatio = effectiveJoinedWidth / (1f * canvasWidth);
            var heightRatio = effectiveJoinedHeight / (1f * canvasHeight);

            var scalingRatio = widthRatio > heightRatio ? widthRatio : heightRatio;

            var previewY = canvasHeight / 2f - bitmapHeightLessCrop / scalingRatio / 2f;
            var previewHeight = bitmapHeightLessCrop / scalingRatio;

            float leftPreviewX;
            float rightPreviewX;
            float leftPreviewWidth;
            float rightPreviewWidth;
            float innerLeftRotation;
            float innerRightRotation;
            if (switchForParallel)
            {
                leftPreviewX = canvasWidth / 2f + innerBorderThickness / scalingRatio;
                rightPreviewX = canvasWidth / 2f - (rightBitmapWidthLessCrop + innerBorderThickness) / scalingRatio;
                leftPreviewWidth = rightBitmapWidthLessCrop / scalingRatio;
                rightPreviewWidth = leftBitmapWidthLessCrop / scalingRatio;
                innerRightRotation = leftRotation;
                innerLeftRotation = rightRotation;
            }
            else
            {
                leftPreviewX = canvasWidth / 2f - (leftBitmapWidthLessCrop + innerBorderThickness) / scalingRatio;
                rightPreviewX = canvasWidth / 2f + innerBorderThickness / scalingRatio;
                leftPreviewWidth = leftBitmapWidthLessCrop / scalingRatio;
                rightPreviewWidth = rightBitmapWidthLessCrop / scalingRatio;
                innerRightRotation = rightRotation;
                innerLeftRotation = leftRotation;
            }

            if (leftBitmap != null)
            {
                canvas.RotateDegrees(innerLeftRotation);
                canvas.DrawBitmap(
                    leftBitmap,
                    SKRect.Create(
                        leftLeftCrop,
                        leftTopCrop + (alignment > 0 ? alignment : 0),
                        leftBitmapWidthLessCrop,
                        bitmapHeightLessCrop),
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
                        rightLeftCrop,
                        rightTopCrop - (alignment < 0 ? alignment : 0),
                        rightBitmapWidthLessCrop,
                        bitmapHeightLessCrop),
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