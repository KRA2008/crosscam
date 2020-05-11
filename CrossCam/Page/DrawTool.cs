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
            double leftLeftCrop, double leftRightCrop, double rightLeftCrop, double rightRightCrop,
            double topCrop, double bottomCrop,
            float leftRotation, float rightRotation, double alignment,
            double leftZoom, double rightZoom,
            float leftKeystone, float rightKeystone,
            double leftFov, double rightFov,
            DrawMode drawMode)
        {
            //TODO: deal with different aspect ratio pictures (for Android)
            if (leftBitmap == null && rightBitmap == null) return;

            var canvasWidth = canvas.DeviceClipBounds.Width;
            var canvasHeight = canvas.DeviceClipBounds.Height;

            var leftBitmapWidthLessCrop = 0;
            var rightBitmapWidthLessCrop = 0;
            var leftBitmapHeightLessCrop = 0;
            var rightBitmapHeightLessCrop = 0;

            if (leftBitmap != null)
            {
                leftBitmapWidthLessCrop = (int)(leftBitmap.Width - leftBitmap.Width * (leftLeftCrop + leftRightCrop));
                leftBitmapHeightLessCrop = (int)(leftBitmap.Height - leftBitmap.Height * (topCrop + bottomCrop + Math.Abs(alignment)));
            }

            if (rightBitmap != null)
            {
                rightBitmapWidthLessCrop = (int)(rightBitmap.Width - rightBitmap.Width * (rightLeftCrop + rightRightCrop));
                rightBitmapHeightLessCrop = (int)(rightBitmap.Height - rightBitmap.Height * (topCrop + bottomCrop + Math.Abs(alignment)));
            }

            if (rightBitmap == null)
            {
                rightBitmapWidthLessCrop = leftBitmapWidthLessCrop;
                rightBitmapHeightLessCrop = leftBitmapHeightLessCrop;
            }
            else if (leftBitmap == null)
            {
                leftBitmapWidthLessCrop = rightBitmapWidthLessCrop;
                leftBitmapHeightLessCrop = rightBitmapHeightLessCrop;
            }

            var innerBorderThicknessProportion = leftBitmap != null && 
                                       rightBitmap != null && 
                                       addBorder && 
                                       drawMode != DrawMode.RedCyanAnaglyph &&
                                       drawMode != DrawMode.GrayscaleRedCyanAnaglyph ? 
                BORDER_CONVERSION_FACTOR * borderThickness : 
                0;

            leftBitmapWidthLessCrop = (int)(leftBitmapWidthLessCrop + 1.5 * innerBorderThicknessProportion * leftBitmapWidthLessCrop);
            rightBitmapWidthLessCrop = (int)(rightBitmapWidthLessCrop + 1.5 * innerBorderThicknessProportion * rightBitmapWidthLessCrop);
            leftBitmapHeightLessCrop = (int)(leftBitmapHeightLessCrop + 2 * innerBorderThicknessProportion * leftBitmapHeightLessCrop);
            rightBitmapHeightLessCrop = (int)(rightBitmapHeightLessCrop + 2 * innerBorderThicknessProportion * rightBitmapHeightLessCrop);

            if (drawMode == DrawMode.RedCyanAnaglyph ||
                drawMode == DrawMode.GrayscaleRedCyanAnaglyph)
            {
                //effectiveJoinedWidth /= 2; // TODO: handle
            }

            var leftWidthRatio = leftBitmapWidthLessCrop / (canvasWidth / 2f);
            var leftHeightRatio = leftBitmapHeightLessCrop / (1f * canvasHeight);
            var leftScalingRatio = leftWidthRatio > leftHeightRatio ? leftWidthRatio : leftHeightRatio;

            var rightWidthRatio = rightBitmapWidthLessCrop / (canvasWidth / 2f);
            var rightHeightRatio = rightBitmapHeightLessCrop / (1f * canvasHeight);
            var rightScalingRatio = rightWidthRatio > rightHeightRatio ? rightWidthRatio : rightHeightRatio;

            var leftPreviewY = canvasHeight / 2f - leftBitmapHeightLessCrop / leftScalingRatio / 2f;
            var leftPreviewHeight = leftBitmapHeightLessCrop / leftScalingRatio;

            var rightPreviewY = canvasHeight / 2f - rightBitmapHeightLessCrop / rightScalingRatio / 2f;
            var rightPreviewHeight = rightBitmapHeightLessCrop / rightScalingRatio;

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
                case DrawMode.RedCyanAnaglyph: //TODO: handle
                    leftPreviewX = rightPreviewX = canvasWidth / 2f - leftBitmapWidthLessCrop / (2f * leftScalingRatio);
                    leftPreviewWidth = leftBitmapWidthLessCrop / leftScalingRatio;
                    rightPreviewWidth = rightBitmapWidthLessCrop / rightScalingRatio;
                    innerRightRotation = rightRotation;
                    innerLeftRotation = leftRotation;
                    innerRightKeystone = rightKeystone;
                    innerLeftKeystone = leftKeystone;
                    break;
                default:
                    leftPreviewX = (float)(canvasWidth / 2f - (leftBitmapWidthLessCrop + innerBorderThicknessProportion *leftBitmapWidthLessCrop / 2f) / leftScalingRatio);
                    rightPreviewX = (float)(canvasWidth / 2f + innerBorderThicknessProportion * rightBitmapWidthLessCrop / (2 * rightScalingRatio));
                    leftPreviewWidth = leftBitmapWidthLessCrop / leftScalingRatio;
                    rightPreviewWidth = rightBitmapWidthLessCrop / rightScalingRatio;
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

            var leftFovCorrectionProportion = 1d;
            var rightFovCorrectionProportion = 1d;
            if (leftFov > FLOATY_ZERO &&
                rightFov > FLOATY_ZERO)
            {
                if (leftFov > rightFov)
                {
                    leftFovCorrectionProportion = rightFov / leftFov;
                }
                else
                {
                    rightFovCorrectionProportion = leftFov / rightFov;
                }
            }

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
                    transformed = ZoomAndRotate(grayscale ?? leftBitmap, leftZoom, isLeftRotated, innerLeftRotation, isLeftKeystoned, -innerLeftKeystone);
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

                    var width = transformed?.Width ?? grayscale?.Width ?? leftBitmap.Width;
                    var height = transformed?.Height ?? grayscale?.Height ?? leftBitmap.Height;

                    var leftFovCorrection = (width - width * leftFovCorrectionProportion) / 2d;
                    var topFovCorrection = (height - height * leftFovCorrectionProportion) / 2d;

                    canvas.DrawBitmap(
                        transformed ?? grayscale ?? leftBitmap,
                        SKRect.Create(
                            (float)(width * leftLeftCrop + leftFovCorrection),
                            (float)(height * topCrop + (alignment > 0 ? alignment * height : 0) + topFovCorrection),
                            (float)(width - width * (leftLeftCrop + leftRightCrop) - leftFovCorrection),
                            (float)(height - height * (topCrop + bottomCrop + Math.Abs(alignment)) - topFovCorrection)),
                        SKRect.Create(
                            leftPreviewX,
                            leftPreviewY,
                            leftPreviewWidth,
                            leftPreviewHeight),
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
                    transformed = ZoomAndRotate(grayscale ?? rightBitmap, rightZoom, isRightRotated, innerRightRotation, isRightKeystoned, innerRightKeystone);
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

                    var width = transformed?.Width ?? grayscale?.Width ?? rightBitmap.Width;
                    var height = transformed?.Height ?? grayscale?.Height ?? rightBitmap.Height;

                    var rightFovCorrection = (width - width * rightFovCorrectionProportion) / 2d;
                    var topFovCorrection = (height - height * rightFovCorrectionProportion) / 2d;

                    canvas.DrawBitmap(
                        transformed ?? grayscale ?? rightBitmap,
                        SKRect.Create(
                            (float)(width * rightLeftCrop + rightFovCorrection),
                            (float)(height * topCrop - (alignment < 0 ? alignment * height : 0) + topFovCorrection),
                            (float)(width - width * (rightLeftCrop + rightRightCrop) - rightFovCorrection),
                            (float)(height - height * (topCrop + bottomCrop + Math.Abs(alignment)) - topFovCorrection)),
                        SKRect.Create(
                            rightPreviewX,
                            rightPreviewY,
                            rightPreviewWidth,
                            rightPreviewHeight),
                        paint);
                }

                grayscale?.Dispose();
                transformed?.Dispose();
            }

            if (innerBorderThicknessProportion > 0)
            {
                var borderPaint = new SKPaint
                {
                    Color = borderColor == BorderColor.Black ? SKColor.Parse("000000") : SKColor.Parse("ffffff"),
                    Style = SKPaintStyle.StrokeAndFill
                };
                //leftPreviewWidth and rightPreviewWidth should be the same
                var originX = (float)(leftPreviewX - innerBorderThicknessProportion * leftPreviewWidth / leftScalingRatio);
                var originY = (float)(leftPreviewY - innerBorderThicknessProportion * leftPreviewWidth / leftScalingRatio);
                var fullPreviewWidth = (float)(leftPreviewWidth + rightPreviewWidth + 3 * innerBorderThicknessProportion / leftScalingRatio);
                var fullPreviewHeight = (float)(leftPreviewHeight + 2 * innerBorderThicknessProportion / leftScalingRatio);
                var scaledBorderThickness = (float)(innerBorderThicknessProportion / leftScalingRatio);
                var endX = rightPreviewX + rightPreviewWidth;
                var endY = leftPreviewY + leftPreviewHeight;
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

        private static SKBitmap ZoomAndRotate(SKBitmap originalBitmap, double zoom, bool isRotated, float rotation, bool isKeystoned, float keystone)
        {
            var rotatedAndZoomed = new SKBitmap(originalBitmap.Width, originalBitmap.Height);

            using (var tempCanvas = new SKCanvas(rotatedAndZoomed))
            {
                var zoomedX = originalBitmap.Width * zoom / -2f;
                var zoomedY = originalBitmap.Height * zoom / -2f;
                var zoomedWidth = originalBitmap.Width * (1 + zoom);
                var zoomedHeight = originalBitmap.Height * (1 + zoom);
                if (isRotated)
                {
                    tempCanvas.RotateDegrees(rotation, originalBitmap.Width / 2f,
                        originalBitmap.Height / 2f);
                }
                tempCanvas.DrawBitmap(
                    originalBitmap,
                    SKRect.Create(
                        (float)zoomedX,
                        (float)zoomedY,
                        (float)zoomedWidth,
                        (float)zoomedHeight
                    )); // blows up the bitmap, which is cut off later
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
            double leftLeftCrop, double leftRightCrop, double rightLeftCrop, double rightRightCrop)
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

            var baseWidth = Math.Min(leftBitmap.Width, rightBitmap.Width);
            return (int)(2 * baseWidth -
                baseWidth * (leftLeftCrop + leftRightCrop + rightLeftCrop + rightRightCrop));
        }

        public static int CalculateCanvasHeightLessBorder(SKBitmap leftBitmap, SKBitmap rightBitmap,
            double topCrop, double bottomCrop,
            double alignment)
        {
            if (leftBitmap == null && rightBitmap == null) return 0;

            if (leftBitmap == null || rightBitmap == null)
            {
                return leftBitmap?.Height ?? rightBitmap.Height;
            }

            var baseHeight = Math.Min(leftBitmap.Height, rightBitmap.Height);
            return (int)(baseHeight - baseHeight * (topCrop + bottomCrop + Math.Abs(alignment)));
        }
    }
}