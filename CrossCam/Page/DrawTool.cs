using System;
using CrossCam.Model;
using CrossCam.ViewModel;
using SkiaSharp;

namespace CrossCam.Page
{
    public class DrawTool
    {
        public const double BORDER_CONVERSION_FACTOR = 0.0005;
        private const float FLOATY_ZERO = 0.00001f;

        public static void DrawImagesOnCanvas(
            SKCanvas canvas, SKBitmap leftBitmap, SKBitmap rightBitmap,
            int borderThickness, bool addBorder, BorderColor borderColor,
            int leftLeftCrop, int leftRightCrop, int rightLeftCrop, int rightRightCrop,
            int topCrop, int bottomCrop,
            float leftRotation, float rightRotation, int alignment,
            int leftZoom, int rightZoom,
            float leftKeystone, float rightKeystone, 
            DrawMode drawMode)
        {
            if (leftBitmap == null && rightBitmap == null) return;

            var canvasWidth = canvas.DeviceClipBounds.Width;
            var canvasHeight = canvas.DeviceClipBounds.Height;

            var leftBitmapWidthLessCrop = 0;
            var rightBitmapWidthLessCrop = 0;
            var bitmapHeightLessCrop = 0;
            var aspectRatio = 0f;

            if (leftBitmap != null)
            {
                leftBitmapWidthLessCrop = leftBitmap.Width - leftLeftCrop - leftRightCrop;
                bitmapHeightLessCrop = leftBitmap.Height - topCrop - bottomCrop - Math.Abs(alignment);
                aspectRatio = leftBitmap.Height / (1f * leftBitmap.Width);
            }

            if (rightBitmap != null)
            {
                rightBitmapWidthLessCrop = rightBitmap.Width - rightLeftCrop - rightRightCrop;
                if (leftBitmap == null)
                {
                    bitmapHeightLessCrop = rightBitmap.Height - topCrop - bottomCrop - Math.Abs(alignment);
                    aspectRatio = rightBitmap.Height / (1f * rightBitmap.Width);
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

            var innerBorderThickness = leftBitmap != null && 
                                       rightBitmap != null && 
                                       addBorder && 
                                       drawMode != DrawMode.RedCyanAnaglyph &&
                                       drawMode != DrawMode.GrayscaleRedCyanAnaglyph ? 
                (int)(BORDER_CONVERSION_FACTOR * borderThickness * effectiveJoinedWidth) : 
                0;
            var effectiveJoinedHeight = bitmapHeightLessCrop;

            effectiveJoinedWidth += 3 * innerBorderThickness;
            effectiveJoinedHeight += 2 * innerBorderThickness;
            
            if (drawMode == DrawMode.RedCyanAnaglyph ||
                drawMode == DrawMode.GrayscaleRedCyanAnaglyph)
            {
                effectiveJoinedWidth /= 2;
            }

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
            float innerLeftKeystone;
            float innerRightKeystone;
            switch (drawMode)
            {
                case DrawMode.GrayscaleRedCyanAnaglyph:
                case DrawMode.RedCyanAnaglyph:
                    leftPreviewX = rightPreviewX = canvasWidth / 2f - leftBitmapWidthLessCrop / (2f * scalingRatio);
                    leftPreviewWidth = leftBitmapWidthLessCrop / scalingRatio;
                    rightPreviewWidth = rightBitmapWidthLessCrop / scalingRatio;
                    innerRightRotation = rightRotation;
                    innerLeftRotation = leftRotation;
                    innerRightKeystone = rightKeystone;
                    innerLeftKeystone = leftKeystone;
                    break;
                default:
                    leftPreviewX = canvasWidth / 2f - (leftBitmapWidthLessCrop + innerBorderThickness / 2f) / scalingRatio;
                    rightPreviewX = canvasWidth / 2f + innerBorderThickness / (2 * scalingRatio);
                    leftPreviewWidth = leftBitmapWidthLessCrop / scalingRatio;
                    rightPreviewWidth = rightBitmapWidthLessCrop / scalingRatio;
                    innerRightRotation = rightRotation;
                    innerLeftRotation = leftRotation;
                    innerRightKeystone = rightKeystone;
                    innerLeftKeystone = leftKeystone;
                    break;
            }
            var isRightRotated = Math.Abs(innerRightRotation) > FLOATY_ZERO;
            var isLeftRotated = Math.Abs(innerLeftRotation) > FLOATY_ZERO;
            var isRightKeystoned = Math.Abs(innerRightKeystone) > FLOATY_ZERO;
            var isLeftKeystoned = Math.Abs(innerLeftKeystone) > FLOATY_ZERO;

            if (leftBitmap != null)
            {
                SKBitmap grayscale = null;
                if (drawMode == DrawMode.GrayscaleRedCyanAnaglyph)
                {
                    grayscale = FilterToGrayscale(leftBitmap);
                }

                SKBitmap transformed = null;
                if (isLeftRotated ||
                    leftZoom > 0 ||
                    isLeftKeystoned)
                {
                    transformed = ZoomAndRotate(grayscale ?? leftBitmap, aspectRatio, leftZoom, isLeftRotated, innerLeftRotation, isLeftKeystoned, -innerLeftKeystone);
                }

                using (var paint = new SKPaint())
                {
                    if (drawMode == DrawMode.RedCyanAnaglyph ||
                        drawMode == DrawMode.GrayscaleRedCyanAnaglyph)
                    {
                        paint.ColorFilter =
                            SKColorFilter.CreateColorMatrix(new float[]
                            {
                                0, 0, 0, 0, 0,
                                0, 1, 0, 0, 0,
                                0, 0, 1, 0, 0,
                                0, 0, 0, 1, 0
                            });
                    }

                    canvas.DrawBitmap(
                        transformed ?? grayscale ?? leftBitmap,
                        SKRect.Create(
                            leftLeftCrop,
                            topCrop + (alignment > 0 ? alignment : 0),
                            (transformed?.Width ?? grayscale?.Width ?? leftBitmap.Width) - leftLeftCrop - leftRightCrop,
                            (transformed?.Height ?? grayscale?.Height ?? leftBitmap.Height) - topCrop - bottomCrop - Math.Abs(alignment)),
                        SKRect.Create(
                            leftPreviewX,
                            previewY,
                            leftPreviewWidth,
                            previewHeight),
                        paint);
                }

                grayscale?.Dispose();
                transformed?.Dispose();
            }

            if (rightBitmap != null)
            {
                SKBitmap grayscale = null;
                if (drawMode == DrawMode.GrayscaleRedCyanAnaglyph)
                {
                    grayscale = FilterToGrayscale(rightBitmap);
                }

                SKBitmap transformed = null;
                if (isRightRotated ||
                    rightZoom > 0 || 
                    isRightKeystoned)
                {
                    transformed = ZoomAndRotate(grayscale ?? rightBitmap, aspectRatio, rightZoom, isRightRotated, innerRightRotation, isRightKeystoned, innerRightKeystone);
                }

                using (var paint = new SKPaint())
                {
                    if (drawMode == DrawMode.RedCyanAnaglyph ||
                        drawMode == DrawMode.GrayscaleRedCyanAnaglyph)
                    {
                        paint.ColorFilter =
                            SKColorFilter.CreateColorMatrix(new float[]
                            {
                                1, 0, 0, 0, 0,
                                0, 0, 0, 0, 0,
                                0, 0, 0, 0, 0,
                                0, 0, 0, 1, 0
                            });
                        paint.BlendMode = SKBlendMode.Plus;
                    }

                    canvas.DrawBitmap(
                        transformed ?? grayscale ?? rightBitmap,
                        SKRect.Create(
                            rightLeftCrop,
                            topCrop - (alignment < 0 ? alignment : 0),
                            (transformed?.Width ?? grayscale?.Width ?? rightBitmap.Width) - rightLeftCrop - rightRightCrop,
                            (transformed?.Height ?? grayscale?.Height ?? rightBitmap.Height) - topCrop - bottomCrop - Math.Abs(alignment)),
                        SKRect.Create(
                            rightPreviewX,
                            previewY,
                            rightPreviewWidth,
                            previewHeight),
                        paint);
                }

                grayscale?.Dispose();
                transformed?.Dispose();
            }

            if (innerBorderThickness > 0)
            {
                var borderPaint = new SKPaint
                {
                    Color = borderColor == BorderColor.Black ? SKColor.Parse("000000") : SKColor.Parse("ffffff"),
                    Style = SKPaintStyle.StrokeAndFill
                };
                var originX = leftPreviewX - innerBorderThickness / scalingRatio;
                var originY = previewY - innerBorderThickness / scalingRatio;
                var fullPreviewWidth = leftPreviewWidth + rightPreviewWidth + 3 * innerBorderThickness / scalingRatio;
                var fullPreviewHeight = previewHeight + 2 * innerBorderThickness / scalingRatio;
                var scaledBorderThickness = innerBorderThickness / scalingRatio;
                var endX = rightPreviewX + rightPreviewWidth;
                var endY = previewY + previewHeight;
                canvas.DrawRect(originX, originY, fullPreviewWidth, scaledBorderThickness, borderPaint);
                canvas.DrawRect(originX, originY, scaledBorderThickness, fullPreviewHeight, borderPaint);
                canvas.DrawRect(canvasWidth / 2f - scaledBorderThickness / 2f, originY, scaledBorderThickness, fullPreviewHeight, borderPaint);
                canvas.DrawRect(endX, originY, scaledBorderThickness, fullPreviewHeight, borderPaint);
                canvas.DrawRect(originX, endY, fullPreviewWidth, scaledBorderThickness, borderPaint);
            }
        }

        private static SKBitmap FilterToGrayscale(SKBitmap originalBitmap)
        {
            var grayed = new SKBitmap(originalBitmap.Width, originalBitmap.Height);

            using (var graybrush = new SKPaint())
            {
                graybrush.ColorFilter =
                    SKColorFilter.CreateColorMatrix(new[]
                    {
                        0.21f, 0.72f, 0.07f, 0.0f, 0.0f,
                        0.21f, 0.72f, 0.07f, 0.0f, 0.0f,
                        0.21f, 0.72f, 0.07f, 0.0f, 0.0f,
                        0.0f,  0.0f,  0.0f,  1.0f, 0.0f
                    });
                using (var tempCanvas = new SKCanvas(grayed))
                {
                    tempCanvas.DrawBitmap(
                        originalBitmap,
                        0,
                        0,
                        graybrush);
                }
            }

            return grayed;
        }

        private static SKBitmap ZoomAndRotate(SKBitmap originalBitmap, float aspectRatio, int zoom, bool isRotated, float rotation, bool isKeystoned, float keystone)
        {
            var rotatedAndZoomed = new SKBitmap(originalBitmap.Width, originalBitmap.Height);

            using (var tempCanvas = new SKCanvas(rotatedAndZoomed))
            {
                var rightVerticalZoom = aspectRatio * zoom;
                var zoomedX = zoom / -2f;
                var zoomedY = rightVerticalZoom / -2f;
                var zoomedWidth = originalBitmap.Width + zoom;
                var zoomedHeight = originalBitmap.Height + rightVerticalZoom;
                if (isRotated)
                {
                    tempCanvas.RotateDegrees(rotation, originalBitmap.Width / 2f,
                        originalBitmap.Height / 2f);
                }
                tempCanvas.DrawBitmap(
                    originalBitmap,
                    SKRect.Create(
                        0,
                        0,
                        originalBitmap.Width,
                        originalBitmap.Height),
                    SKRect.Create(
                        zoomedX,
                        zoomedY,
                        zoomedWidth,
                        zoomedHeight
                    ));
                if (isRotated)
                {
                    tempCanvas.RotateDegrees(rotation, -1 * originalBitmap.Width / 2f,
                        originalBitmap.Height / 2f);
                }
            }

            SKBitmap keystoned = null;
            if (isKeystoned)
            {
                keystoned = new SKBitmap(originalBitmap.Width, originalBitmap.Height);
                using (var tempCanvas = new SKCanvas(keystoned))
                {
                    tempCanvas.SetMatrix(TaperTransform.Make(new SKSize(originalBitmap.Width, originalBitmap.Height),
                        keystone > 0 ? TaperSide.Left : TaperSide.Right, TaperCorner.Both, 1 - Math.Abs(keystone)));
                    tempCanvas.DrawBitmap(rotatedAndZoomed, 0, 0);
                    rotatedAndZoomed.Dispose();
                }
            }

            return keystoned ?? rotatedAndZoomed;
        }

        public static int CalculateJoinedCanvasWidthLessBorder(SKBitmap leftBitmap, SKBitmap rightBitmap, 
            int leftLeftCrop, int leftRightCrop, int rightLeftCrop, int rightRightCrop)
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
                leftLeftCrop - leftRightCrop - rightLeftCrop - rightRightCrop;
        }

        public static int CalculateCanvasHeightLessBorder(SKBitmap leftBitmap, SKBitmap rightBitmap, 
            int topCrop, int bottomCrop,
            int alignment)
        {
            if (leftBitmap == null && rightBitmap == null) return 0;

            if (leftBitmap == null || rightBitmap == null)
            {
                return leftBitmap?.Height ?? rightBitmap.Height;
            }

            return leftBitmap.Height - topCrop - bottomCrop - Math.Abs(alignment);
        }
    }
}