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
            int zoom,
            bool switchForParallel = false)
        {
            if (leftBitmap == null && rightBitmap == null) return;

            var screenWidth = canvas.DeviceClipBounds.Width;
            var screenHeight = canvas.DeviceClipBounds.Height;

            var innerBorderThickness = leftBitmap != null && rightBitmap != null && addBorder ? borderThickness : 0;

            var joinedBitmapWidthLessCropPlusBorder = CalculateCanvasWidth(leftBitmap, rightBitmap, leftLeftCrop, leftRightCrop,
                rightLeftCrop, rightRightCrop, innerBorderThickness);
            var joinedBitmapHeightLessCropPlusBorder = CalculateCanvasHeight(leftBitmap, rightBitmap, leftTopCrop, leftBottomCrop,
                rightTopCrop, rightBottomCrop, alignment, innerBorderThickness);

            var widthRatio = joinedBitmapWidthLessCropPlusBorder / (1f * screenWidth);
            var heightRatio = joinedBitmapHeightLessCropPlusBorder / (1f * screenHeight);

            var scalingRatio = widthRatio > heightRatio ? widthRatio : heightRatio;

            var previewY = screenHeight / 2f - joinedBitmapHeightLessCropPlusBorder / (2 * scalingRatio) + innerBorderThickness / scalingRatio;

            float leftPreviewX = 0;
            float rightPreviewX = 0;
            float innerRightRotation = 0;
            float innerLeftRotation = 0;
            float leftPreviewWidth = 0;
            float rightPreviewWidth = 0;
            float leftPreviewHeight = 0;
            float rightPreviewHeight = 0;
            if (leftBitmap != null && rightBitmap != null)
            {
                if (!switchForParallel)
                {
                    leftPreviewX = screenWidth / 2f - (leftBitmap.Width + innerBorderThickness - leftRightCrop) / scalingRatio;
                    rightPreviewX = screenWidth / 2f + (innerBorderThickness - rightLeftCrop) / scalingRatio;
                    leftPreviewWidth = leftBitmap.Width / scalingRatio;
                    rightPreviewWidth = rightBitmap.Width / scalingRatio;
                    leftPreviewHeight = leftBitmap.Height / scalingRatio;
                    rightPreviewHeight = rightBitmap.Height / scalingRatio;
                    innerLeftRotation = leftRotation;
                    innerRightRotation = rightRotation;
                }
                else
                {
                    leftPreviewX = screenWidth / 2f + (innerBorderThickness - rightLeftCrop) / scalingRatio;
                    rightPreviewX = screenWidth / 2f - (rightBitmap.Width + innerBorderThickness - rightLeftCrop) / scalingRatio;
                    leftPreviewWidth = rightBitmap.Width / scalingRatio;
                    rightPreviewWidth = leftBitmap.Width / scalingRatio;
                    leftPreviewHeight = rightBitmap.Height / scalingRatio;
                    rightPreviewHeight = leftBitmap.Height / scalingRatio;
                    innerLeftRotation = rightRotation;
                    innerRightRotation = leftRotation;
                }
            }

            if (leftBitmap != null)
            {
                SKBitmap rotatedAndZoomed = null;
                if (Math.Abs(innerLeftRotation) > 0.00001 ||
                    zoom > 0)
                {
                    var aspectRatio = leftBitmap.Height / (1f * leftBitmap.Width);
                    var leftHorizontalZoom = zoom > 0 ? zoom : 0;
                    var leftVerticalZoom = aspectRatio * leftHorizontalZoom;

                    rotatedAndZoomed = new SKBitmap(leftBitmap.Width, leftBitmap.Height);

                    using (var tempCanvas = new SKCanvas(rotatedAndZoomed))
                    {
                        var zoomedX = leftHorizontalZoom / -2f;
                        var zoomedY = leftVerticalZoom / -2f;
                        var zoomedWidth = leftBitmap.Width + leftHorizontalZoom;
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
                        0,
                        0,
                        rotatedAndZoomed?.Width ?? leftBitmap.Width,
                        rotatedAndZoomed?.Height ?? leftBitmap.Height),
                    SKRect.Create(
                        leftPreviewX,
                        previewY,
                        leftPreviewWidth,
                        leftPreviewHeight));
            }

            if (rightBitmap != null)
            {
                SKBitmap rotatedAndZoomed = null;
                if (Math.Abs(innerRightRotation) > 0.00001 ||
                    zoom < 0)
                {
                    var aspectRatio = rightBitmap.Height / (1f * rightBitmap.Width);
                    var rightHorizontalZoom = zoom < 0 ? Math.Abs(zoom) : 0;
                    var rightVerticalZoom = aspectRatio * rightHorizontalZoom;

                    rotatedAndZoomed = new SKBitmap(rightBitmap.Width, rightBitmap.Height);

                    using (var tempCanvas = new SKCanvas(rotatedAndZoomed))
                    {
                        var zoomedX = rightVerticalZoom / -2f;
                        var zoomedY = rightVerticalZoom / -2f;
                        var zoomedWidth = rightBitmap.Width + rightVerticalZoom;
                        var zoomedHeight = rightBitmap.Height + rightVerticalZoom;
                        tempCanvas.RotateDegrees(innerRightRotation, rightBitmap.Width / 2f, rightBitmap.Height / 2f);
                        tempCanvas.DrawBitmap(
                            leftBitmap,
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
                        0,
                        0,
                        rotatedAndZoomed?.Width ?? rightBitmap.Width,
                        rotatedAndZoomed?.Height ?? rightBitmap.Height),
                    SKRect.Create(
                        rightPreviewX,
                        previewY,
                        rightPreviewWidth,
                        rightPreviewHeight));
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