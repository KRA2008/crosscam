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
            int zoom,
            bool switchForParallel = false)
        {
            if (leftBitmap == null && rightBitmap == null) return;

            var canvasWidth = canvas.DeviceClipBounds.Width;
            var canvasHeight = canvas.DeviceClipBounds.Height;

            var innerBorderThickness = leftBitmap != null && rightBitmap != null ? borderThickness : 0;
            var leftBitmapWidthLessCrop = 0;
            var rightBitmapWidthLessCrop = 0;
            var bitmapHeightLessCrop = 0;

            if (leftBitmap != null)
            {
                leftBitmapWidthLessCrop = leftBitmap.Width - leftLeftCrop - leftRightCrop;
                bitmapHeightLessCrop = leftBitmap.Height - leftTopCrop - leftBottomCrop - Math.Abs(alignment);
            }

            if (rightBitmap != null)
            {
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
                var aspectRatio = leftBitmap.Height / (1f * leftBitmap.Width);
                var leftHorizontalZoom = zoom > 0 ? zoom : 0;
                var leftVerticalZoom = aspectRatio * leftHorizontalZoom;

                canvas.RotateDegrees(innerLeftRotation, leftPreviewWidth / 2f + leftPreviewX, previewHeight / 2f + previewY);
                canvas.DrawBitmap(
                    leftBitmap,
                    SKRect.Create(
                        leftLeftCrop + leftHorizontalZoom / 2f,
                        leftTopCrop + leftVerticalZoom / 2f + (alignment > 0 ? alignment : 0),
                        leftBitmapWidthLessCrop - leftHorizontalZoom,
                        bitmapHeightLessCrop - leftVerticalZoom),
                    SKRect.Create(
                        leftPreviewX,
                        previewY,
                        leftPreviewWidth,
                        previewHeight));
                canvas.RotateDegrees(-1 * innerLeftRotation, leftPreviewWidth / 2f + leftPreviewX, previewHeight / 2f + previewY);
            }

            if (rightBitmap != null)
            {
                var aspectRatio = rightBitmap.Height / (1f * rightBitmap.Width);
                var rightHorizontalZoom = zoom < 0 ? Math.Abs(zoom) : 0;
                var rightVerticalZoom = aspectRatio * rightHorizontalZoom;

                canvas.RotateDegrees(innerRightRotation, rightPreviewWidth / 2f + rightPreviewX, previewHeight / 2f + previewY);
                canvas.DrawBitmap(
                    rightBitmap,
                    SKRect.Create(
                        rightLeftCrop + rightVerticalZoom / 2f,
                        rightTopCrop + rightVerticalZoom / 2f - (alignment < 0 ? alignment : 0),
                        rightBitmapWidthLessCrop - rightHorizontalZoom,
                        bitmapHeightLessCrop - rightHorizontalZoom),
                    SKRect.Create(
                        rightPreviewX,
                        previewY,
                        rightPreviewWidth,
                        previewHeight));
                canvas.RotateDegrees(-1 * innerLeftRotation, rightPreviewWidth / 2f + rightPreviewX, previewHeight / 2f + previewY);
            }
        }

        public static int CalculateCanvasWidth(SKBitmap leftBitmap, SKBitmap rightBitmap, 
            int leftLeftCrop, int leftRightCrop, int rightLeftCrop, int rightRightCrop,
            int borderThickness, bool addBorder)
        {
            return leftBitmap.Width + rightBitmap.Width -
                - leftLeftCrop - leftRightCrop - rightLeftCrop - rightRightCrop +
                4 * (addBorder ? borderThickness : 0);
        }

        public static int CalculateCanvasHeight(SKBitmap leftBitmap, SKBitmap rightBitmap, 
            int leftTopCrop, int leftBottomCrop, int rightTopCrop, int rightBottomCrop,
            int borderThickness, bool addBorder)
        {
            return 0;
            //leftBitmap.Height - LeftTopCrop - LeftBottomCrop +
            //    2 * (Settings.AddBorder ? Settings.BorderThickness : 0);
        }
    }
}