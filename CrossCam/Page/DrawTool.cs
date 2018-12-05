using System;
using SkiaSharp;

namespace CrossCam.Page
{
    public class DrawTool
    {
        public static void DrawImagesOnCanvas(
            SKCanvas canvas, SKBitmap leftBitmap, SKBitmap rightBitmap,
            int borderThickness, bool addBorder,
            int leftLeftCrop, int leftRightCrop, int rightLeftCrop, int rightRightCrop,
            int leftTopCrop, int leftBottomCrop, int rightTopCrop, int rightBottomCrop,
            float leftRotation, float rightRotation, int alignment,
            int leftZoom, int rightZoom,
            bool switchForParallel = false)
        {
            if (leftBitmap == null && rightBitmap == null) return;

            var canvasWidth = canvas.DeviceClipBounds.Width;
            var canvasHeight = canvas.DeviceClipBounds.Height;

            var innerBorderThickness = leftBitmap != null && rightBitmap != null && addBorder ? borderThickness : 0;
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
                SKBitmap rotatedAndZoomed = null;
                if (Math.Abs(innerLeftRotation) > 0.00001 ||
                    leftZoom > 0)
                {
                    var aspectRatio = leftBitmap.Height / (1f * leftBitmap.Width);
                    var leftVerticalZoom = aspectRatio * leftZoom;

                    rotatedAndZoomed = new SKBitmap(leftBitmap.Width, leftBitmap.Height);

                    using (var tempCanvas = new SKCanvas(rotatedAndZoomed))
                    {
                        var zoomedX = leftZoom / -2f;
                        var zoomedY = leftVerticalZoom / -2f;
                        var zoomedWidth = leftBitmap.Width + leftZoom;
                        var zoomedHeight = leftBitmap.Height + leftVerticalZoom;
                        tempCanvas.RotateDegrees(innerLeftRotation, leftBitmap.Width / 2f, leftBitmap.Height / 2f);
                        tempCanvas.DrawBitmap(
                            leftBitmap,
                            SKRect.Create(
                                0,
                                0,
                                leftBitmap.Width,
                                leftBitmap.Height),
                            SKRect.Create(
                                zoomedX,
                                zoomedY,
                                zoomedWidth,
                                zoomedHeight
                            ));
                        tempCanvas.RotateDegrees(innerLeftRotation, -1 * leftBitmap.Width / 2f, leftBitmap.Height / 2f);
                    }
                }
                
                canvas.DrawBitmap(
                    rotatedAndZoomed ?? leftBitmap,
                    SKRect.Create(
                        leftLeftCrop,
                        leftTopCrop + (alignment > 0 ? alignment : 0),
                        (rotatedAndZoomed?.Width ?? leftBitmap.Width) - leftLeftCrop - leftRightCrop,
                        (rotatedAndZoomed?.Height ?? leftBitmap.Height) - leftTopCrop - leftBottomCrop - Math.Abs(alignment)),
                    SKRect.Create(
                        leftPreviewX,
                        previewY,
                        leftPreviewWidth,
                        previewHeight));
            }

            if (rightBitmap != null)
            {
                SKBitmap rotatedAndZoomed = null;
                if (Math.Abs(innerRightRotation) > 0.00001 ||
                    rightZoom > 0)
                {
                    var aspectRatio = rightBitmap.Height / (1f * rightBitmap.Width);
                    var rightVerticalZoom = aspectRatio * rightZoom;

                    rotatedAndZoomed = new SKBitmap(rightBitmap.Width, rightBitmap.Height);

                    using (var tempCanvas = new SKCanvas(rotatedAndZoomed))
                    {
                        var zoomedX = rightVerticalZoom / -2f;
                        var zoomedY = rightVerticalZoom / -2f;
                        var zoomedWidth = rightBitmap.Width + rightVerticalZoom;
                        var zoomedHeight = rightBitmap.Height + rightVerticalZoom;
                        tempCanvas.RotateDegrees(innerRightRotation, rightBitmap.Width / 2f, rightBitmap.Height / 2f);
                        tempCanvas.DrawBitmap(
                            rightBitmap,
                            SKRect.Create(
                                0,
                                0,
                                rightBitmap.Width,
                                rightBitmap.Height),
                            SKRect.Create(
                                zoomedX,
                                zoomedY,
                                zoomedWidth,
                                zoomedHeight
                            ));
                        tempCanvas.RotateDegrees(innerRightRotation, -1 * rightBitmap.Width / 2f, rightBitmap.Height / 2f);
                    }
                }
                
                canvas.DrawBitmap(
                    rotatedAndZoomed ?? rightBitmap,
                    SKRect.Create(
                        rightLeftCrop,
                        rightTopCrop - (alignment < 0 ? alignment : 0),
                        (rotatedAndZoomed?.Width ?? rightBitmap.Width) - rightLeftCrop - rightRightCrop,
                        (rotatedAndZoomed?.Height ?? rightBitmap.Height) - rightTopCrop - rightBottomCrop - Math.Abs(alignment)),
                    SKRect.Create(
                        rightPreviewX,
                        previewY,
                        rightPreviewWidth,
                        previewHeight));
            }
        }

        public static int CalculateCanvasWidth(SKBitmap leftBitmap, SKBitmap rightBitmap, 
            int leftLeftCrop, int leftRightCrop, int rightLeftCrop, int rightRightCrop,
            int borderThickness)
        {
            if (leftBitmap == null && rightBitmap == null) return 0;

            if (leftBitmap == null || rightBitmap == null)
            {
                if (leftBitmap != null)
                {
                    return leftBitmap.Width * 2;
                }

                return rightBitmap.Width * 2;
            }

            return leftBitmap.Width + rightBitmap.Width -
                - leftLeftCrop - leftRightCrop - rightLeftCrop - rightRightCrop +
                4 * borderThickness;
        }

        public static int CalculateCanvasHeight(SKBitmap leftBitmap, SKBitmap rightBitmap, 
            int leftTopCrop, int leftBottomCrop, int rightTopCrop, int rightBottomCrop,
            int alignment, int borderThickness)
        {
            if (leftBitmap == null && rightBitmap == null) return 0;

            if (leftBitmap == null || rightBitmap == null)
            {
                return leftBitmap?.Height ?? rightBitmap.Height;
            }

            return leftBitmap.Height - leftTopCrop - leftBottomCrop - Math.Abs(alignment) +
                   2 * borderThickness;
            var rightHeight = rightBitmap.Height - rightTopCrop - rightBottomCrop +
                              2 * borderThickness; // not sure when this is not the same as left
        }
    }
}