using System;
using CrossCam.Model;
using CrossCam.ViewModel;
using SkiaSharp;

namespace CrossCam.Page
{
    public class DrawTool
    {
        public const double BORDER_CONVERSION_FACTOR = 0.001;
        public const float FLOATY_ZERO = 0.00001f;

        public static void DrawImagesOnCanvas(SKCanvas canvas, SKBitmap leftBitmap, SKBitmap rightBitmap,
            Settings settings, Edits edits, DrawMode drawMode)
        {
            switch (drawMode)
            {
                case DrawMode.Cross:
                case DrawMode.GrayscaleRedCyanAnaglyph:
                case DrawMode.RedCyanAnaglyph:
                    DrawImagesOnCanvasInternal(canvas, leftBitmap, rightBitmap,
                        settings.BorderWidthProportion, settings.AddBorder, settings.BorderColor,
                        edits.LeftCrop + edits.OutsideCrop, edits.InsideCrop + edits.RightCrop, edits.InsideCrop + edits.LeftCrop,
                        edits.RightCrop + edits.OutsideCrop,
                        edits.TopCrop, edits.BottomCrop,
                        edits.LeftRotation, edits.RightRotation,
                        edits.VerticalAlignment,
                        edits.LeftZoom, edits.RightZoom,
                        edits.LeftKeystone, edits.RightKeystone,
                        drawMode);
                    break;
                case DrawMode.Parallel:
                    DrawImagesOnCanvasInternal(canvas, rightBitmap, leftBitmap,
                        settings.BorderWidthProportion, settings.AddBorder, settings.BorderColor,
                        edits.InsideCrop + edits.LeftCrop, edits.RightCrop + edits.OutsideCrop,
                        edits.LeftCrop + edits.OutsideCrop, edits.InsideCrop + edits.RightCrop,
                        edits.TopCrop, edits.BottomCrop,
                        edits.RightRotation, edits.LeftRotation,
                        edits.VerticalAlignment,
                        edits.RightZoom, edits.LeftZoom,
                        edits.RightKeystone, edits.LeftKeystone,
                        drawMode);
                    break;
            }
        }


        private static void DrawImagesOnCanvasInternal(
            SKCanvas canvas, SKBitmap leftBitmap, SKBitmap rightBitmap,
            int borderThickness, bool addBorder, BorderColor borderColor,
            double leftLeftCrop, double leftRightCrop, double rightLeftCrop, double rightRightCrop,
            double topCrop, double bottomCrop,
            float leftRotation, float rightRotation, double alignment,
            double leftZoom, double rightZoom,
            float leftKeystone, float rightKeystone,
            DrawMode drawMode)
        {
            //TODO: deal with different aspect ratio pictures (for Android)
            //TODO: deal with different fields of view (both of these started in CameraViewModel, need to move)
            //TODone? different resolutions?
            if (leftBitmap == null && rightBitmap == null) return;

            var canvasWidth = canvas.DeviceClipBounds.Width;
            var canvasHeight = canvas.DeviceClipBounds.Height;

            int leftBitmapWidthLessCrop;
            int leftBitmapHeightLessCrop;

            if (leftBitmap != null)
            {
                leftBitmapWidthLessCrop = (int)(leftBitmap.Width - leftBitmap.Width * (leftLeftCrop + leftRightCrop));
                leftBitmapHeightLessCrop = (int)(leftBitmap.Height - leftBitmap.Height * (topCrop + bottomCrop + Math.Abs(alignment)));
            }
            else
            {
                leftBitmapWidthLessCrop = (int)(rightBitmap.Width - rightBitmap.Width * (rightLeftCrop + rightRightCrop));
                leftBitmapHeightLessCrop = (int)(rightBitmap.Height - rightBitmap.Height * (topCrop + bottomCrop + Math.Abs(alignment)));
            }

            var innerBorderThicknessProportion = leftBitmap != null && 
                                       rightBitmap != null && 
                                       addBorder && 
                                       drawMode != DrawMode.RedCyanAnaglyph &&
                                       drawMode != DrawMode.GrayscaleRedCyanAnaglyph ? 
                BORDER_CONVERSION_FACTOR * borderThickness : 
                0;

            var widthRatio = (leftBitmapWidthLessCrop + leftBitmapWidthLessCrop * innerBorderThicknessProportion * 1.5) / (canvasWidth / 2f);
            if (drawMode == DrawMode.RedCyanAnaglyph ||
                drawMode == DrawMode.GrayscaleRedCyanAnaglyph)
            {
                widthRatio /= 2;
            }
            var heightRatio = (leftBitmapHeightLessCrop + leftBitmapWidthLessCrop * innerBorderThicknessProportion * 2) / (1f * canvasHeight);
            var scalingRatio = widthRatio > heightRatio ? widthRatio : heightRatio;

            float leftPreviewX;
            float rightPreviewX;
            float previewY;
            var sidePreviewWidthLessCrop = (float)(leftBitmapWidthLessCrop / scalingRatio);
            var previewHeightLessCrop = (float)(leftBitmapHeightLessCrop / scalingRatio);
            switch (drawMode)
            {
                case DrawMode.GrayscaleRedCyanAnaglyph:
                case DrawMode.RedCyanAnaglyph:
                    leftPreviewX = rightPreviewX = canvasWidth / 2f - sidePreviewWidthLessCrop / 2f; 
                    previewY = canvasHeight / 2f - previewHeightLessCrop / 2f;
                    break;
                default:
                    leftPreviewX = (float)(canvasWidth / 2f - sidePreviewWidthLessCrop - innerBorderThicknessProportion * sidePreviewWidthLessCrop / 2f);
                    rightPreviewX = (float)(canvasWidth / 2f + innerBorderThicknessProportion * sidePreviewWidthLessCrop / 2f );
                    previewY = canvasHeight / 2f - previewHeightLessCrop / 2f;
                    break;
            }
            var isRightRotated = Math.Abs(rightRotation) > FLOATY_ZERO;
            var isLeftRotated = Math.Abs(leftRotation) > FLOATY_ZERO;
            var isRightKeystoned = Math.Abs(rightKeystone) > FLOATY_ZERO;
            var isLeftKeystoned = Math.Abs(leftKeystone) > FLOATY_ZERO;

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
                    transformed = ZoomAndRotate(grayscale ?? leftBitmap, leftZoom, isLeftRotated, leftRotation, isLeftKeystoned, -leftKeystone);
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

                    canvas.DrawBitmap(
                        transformed ?? grayscale ?? leftBitmap,
                        SKRect.Create(
                            (float)(width * leftLeftCrop),
                            (float)(height * topCrop + (alignment > 0 ? alignment * height : 0)),
                            (float)(width - width * (leftLeftCrop + leftRightCrop)),
                            (float)(height - height * (topCrop + bottomCrop + Math.Abs(alignment)))),
                        SKRect.Create(
                            leftPreviewX,
                            previewY,
                            sidePreviewWidthLessCrop,
                            previewHeightLessCrop),
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
                    transformed = ZoomAndRotate(grayscale ?? rightBitmap, rightZoom, isRightRotated, rightRotation, isRightKeystoned, rightKeystone);
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

                    canvas.DrawBitmap(
                        transformed ?? grayscale ?? rightBitmap,
                        SKRect.Create(
                            (float)(width * rightLeftCrop),
                            (float)(height * topCrop - (alignment < 0 ? alignment * height : 0)),
                            (float)(width - width * (rightLeftCrop + rightRightCrop)),
                            (float)(height - height * (topCrop + bottomCrop + Math.Abs(alignment)))),
                        SKRect.Create(
                            rightPreviewX,
                            previewY,
                            sidePreviewWidthLessCrop,
                            previewHeightLessCrop),
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

                var originX = (float)(leftPreviewX - innerBorderThicknessProportion * sidePreviewWidthLessCrop);
                var originY = (float)(previewY - innerBorderThicknessProportion * sidePreviewWidthLessCrop);
                var fullPreviewWidth = (float)(2 * sidePreviewWidthLessCrop + 3 * innerBorderThicknessProportion * sidePreviewWidthLessCrop);
                var fullPreviewHeight = (float)(previewHeightLessCrop + 2 * innerBorderThicknessProportion * sidePreviewWidthLessCrop);
                var scaledBorderThickness = (float)(innerBorderThicknessProportion * sidePreviewWidthLessCrop);
                var endX = rightPreviewX + sidePreviewWidthLessCrop;
                var endY = previewY + previewHeightLessCrop;
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
            Edits edits)
        {
            return CalculateJoinedCanvasWidthLessBorderInternal(leftBitmap, rightBitmap,
                edits.LeftCrop + edits.OutsideCrop, edits.InsideCrop + edits.RightCrop,
                edits.InsideCrop + edits.LeftCrop,
                edits.RightCrop + edits.OutsideCrop);
        }

        private static int CalculateJoinedCanvasWidthLessBorderInternal(SKBitmap leftBitmap, SKBitmap rightBitmap, 
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

        public static int CalculateCanvasHeightLessBorder(SKBitmap leftBitmap, SKBitmap rightBitmap, Edits edits)
        {
            return CalculateCanvasHeightLessBorderInternal(leftBitmap, rightBitmap,
                edits.TopCrop, edits.BottomCrop,
                edits.VerticalAlignment);
        }

        private static int CalculateCanvasHeightLessBorderInternal(SKBitmap leftBitmap, SKBitmap rightBitmap,
            double topCrop, double bottomCrop, double alignment)
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