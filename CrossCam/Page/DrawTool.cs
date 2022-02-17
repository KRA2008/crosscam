using System;
using System.Diagnostics;
using CrossCam.Model;
using CrossCam.ViewModel;
using CrossCam.Wrappers;
using SkiaSharp;
using Xamarin.Forms;

namespace CrossCam.Page
{
    public class DrawTool
    {
        public const double BORDER_CONVERSION_FACTOR = 0.001;
        public const float FLOATY_ZERO = 0.00001f;
        private const double FUSE_GUIDE_WIDTH_RATIO = 0.0127;
        private const int FUSE_GUIDE_MARGIN_HEIGHT_RATIO = 7;

        public static void DrawImagesOnCanvas(SKCanvas canvas, SKBitmap leftBitmap, SKBitmap rightBitmap,
            Settings settings, Edits edits, DrawMode drawMode, bool isFov = false, bool withSwap = false, SKFilterQuality quality = SKFilterQuality.High)
        {
            if (withSwap)
            {
                DrawImagesOnCanvasInternal(canvas, rightBitmap, leftBitmap,
                    settings.BorderWidthProportion, settings.AddBorder, settings.BorderColor,
                    edits.InsideCrop + edits.LeftCrop, edits.RightCrop + edits.OutsideCrop,
                    edits.LeftCrop + edits.OutsideCrop, edits.InsideCrop + edits.RightCrop,
                    edits.TopCrop, edits.BottomCrop,
                    edits.RightRotation, edits.LeftRotation,
                    edits.VerticalAlignment,
                    edits.RightZoom + (isFov ? edits.FovRightCorrection : 0), edits.LeftZoom + (isFov ? edits.FovLeftCorrection : 0),
                    edits.RightKeystone, edits.LeftKeystone,
                    drawMode, settings.SaveWithFuseGuide,
                    quality);
            }
            else
            {
                DrawImagesOnCanvasInternal(canvas, leftBitmap, rightBitmap,
                    settings.BorderWidthProportion, settings.AddBorder, settings.BorderColor,
                    edits.LeftCrop + edits.OutsideCrop, edits.InsideCrop + edits.RightCrop, edits.InsideCrop + edits.LeftCrop,
                    edits.RightCrop + edits.OutsideCrop,
                    edits.TopCrop, edits.BottomCrop,
                    edits.LeftRotation, edits.RightRotation,
                    edits.VerticalAlignment,
                    edits.LeftZoom + (isFov ? edits.FovLeftCorrection : 0), edits.RightZoom + (isFov ? edits.FovRightCorrection : 0),
                    edits.LeftKeystone, edits.RightKeystone,
                    drawMode, settings.SaveWithFuseGuide,
                    quality);
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
            DrawMode drawMode, bool fuseGuideRequested,
            SKFilterQuality quality)
        {
            if (leftBitmap == null && rightBitmap == null) return;
            
            var barrelCoeff = 2f;

            var canvasWidth = canvas.DeviceClipBounds.Width;
            var canvasHeight = canvas.DeviceClipBounds.Height;

            int leftBitmapWidthLessCrop;
            int baseHeight;

            if (leftBitmap != null)
            {
                baseHeight = leftBitmap.Height;
                leftBitmapWidthLessCrop = (int)(leftBitmap.Width * (1 - (leftLeftCrop + leftRightCrop)));
            }
            else
            {
                baseHeight = rightBitmap.Height;
                leftBitmapWidthLessCrop = (int)(rightBitmap.Width * (1 - (rightLeftCrop + rightRightCrop)));
            }

            var leftBitmapHeightLessCrop = (int)(baseHeight * (1 - (topCrop + bottomCrop + Math.Abs(alignment))));

            var innerBorderThicknessProportion = leftBitmap != null && 
                                                 rightBitmap != null && 
                                                 addBorder && 
                                                 drawMode != DrawMode.Cardboard &&
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

            var bitmapHeightWithEditsAndBorder = leftBitmapHeightLessCrop + leftBitmapWidthLessCrop * innerBorderThicknessProportion * 2;
            var drawFuseGuide = fuseGuideRequested &&
                                drawMode != DrawMode.Cardboard &&
                                drawMode != DrawMode.RedCyanAnaglyph &&
                                drawMode != DrawMode.GrayscaleRedCyanAnaglyph &&
                                leftBitmap != null && rightBitmap != null;

            float fuseGuideIconWidth = 0;
            float fuseGuideMarginHeight = 0;
            if (drawFuseGuide)
            {
                fuseGuideIconWidth = CalculateFuseGuideWidth((float)bitmapHeightWithEditsAndBorder);
                fuseGuideMarginHeight = CalculateFuseGuideMarginHeight((float)bitmapHeightWithEditsAndBorder);
                bitmapHeightWithEditsAndBorder += fuseGuideMarginHeight;
            }
            var heightRatio = bitmapHeightWithEditsAndBorder / (1f * canvasHeight);
            var scalingRatio = widthRatio > heightRatio ? widthRatio : heightRatio;

            fuseGuideIconWidth = (float)(fuseGuideIconWidth / scalingRatio);
            fuseGuideMarginHeight = (float)(fuseGuideMarginHeight / scalingRatio);

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
                    leftPreviewX = (float)(canvasWidth / 2f - (sidePreviewWidthLessCrop + innerBorderThicknessProportion * sidePreviewWidthLessCrop / 2f));
                    rightPreviewX = (float)(canvasWidth / 2f + innerBorderThicknessProportion * sidePreviewWidthLessCrop / 2f );
                    previewY = canvasHeight / 2f - previewHeightLessCrop / 2f;
                    break;
            }

            if (drawFuseGuide)
            {
                previewY += fuseGuideMarginHeight / 2f;
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
                    grayscale = FilterToGrayscale(leftBitmap, quality);
                }

                SKBitmap transformed = null;
                if (isLeftRotated ||
                    leftZoom > 0 ||
                    isLeftKeystoned)
                {
                    transformed = ZoomAndRotate(grayscale ?? leftBitmap, leftZoom, isLeftRotated, leftRotation, isLeftKeystoned, -leftKeystone, quality);
                }

                using var paint = new SKPaint
                {
                    FilterQuality = quality
                };
                
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

                var targetBitmap = transformed ?? grayscale ?? leftBitmap;
                var width = targetBitmap.Width;
                var height = targetBitmap.Height;

                var srcX = (float)(width * leftLeftCrop);
                var srcY = (float)(height * topCrop + (alignment > 0 ? alignment * height : 0));
                var srcWidth = (float)(width - width * (leftLeftCrop + leftRightCrop));
                var srcHeight = (float)(height - height * (topCrop + bottomCrop + Math.Abs(alignment)));
                
                if (drawMode == DrawMode.Cardboard)
                {
                    var openCv = DependencyService.Get<IOpenCv>();
                    if (openCv.IsOpenCvSupported())
                    {
                        var size = Math.Sqrt(Math.Pow(width, 2) + Math.Pow(height, 2));
                        var scaledCoeff = barrelCoeff / Math.Pow(size, 2);
                        targetBitmap = openCv.AddBarrelDistortion(targetBitmap, (float)scaledCoeff);
                    }
                }

                canvas.DrawBitmap(
                    targetBitmap,
                    SKRect.Create(
                        srcX,
                        srcY,
                        srcWidth,
                        srcHeight),
                    SKRect.Create(
                        leftPreviewX,
                        previewY,
                        sidePreviewWidthLessCrop,
                        previewHeightLessCrop),
                    paint);

                grayscale?.Dispose();
                transformed?.Dispose();
            }

            if (rightBitmap != null)
            {
                SKBitmap grayscale = null;
                if (drawMode == DrawMode.GrayscaleRedCyanAnaglyph)
                {
                    grayscale = FilterToGrayscale(rightBitmap, quality);
                }

                SKBitmap transformed = null;
                if (isRightRotated ||
                    rightZoom > 0 || 
                    isRightKeystoned)
                {
                    transformed = ZoomAndRotate(grayscale ?? rightBitmap, rightZoom, isRightRotated, rightRotation, isRightKeystoned, rightKeystone, quality);
                }

                using var paint = new SKPaint
                {
                    FilterQuality = quality
                };

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

                var targetBitmap = transformed ?? grayscale ?? rightBitmap;
                var width = targetBitmap.Width;
                var height = targetBitmap.Height;

                var srcX = (float) (width * rightLeftCrop);
                var srcY = (float) (height * topCrop - (alignment < 0 ? alignment * height : 0));
                var srcWidth = (float) (width - width * (rightLeftCrop + rightRightCrop));
                var srcHeight = (float) (height - height * (topCrop + bottomCrop + Math.Abs(alignment)));

                if (drawMode == DrawMode.Cardboard)
                {
                    var openCv = DependencyService.Get<IOpenCv>();
                    if (openCv.IsOpenCvSupported())
                    {
                        var size = Math.Sqrt(Math.Pow(width, 2) + Math.Pow(height, 2));
                        var scaledCoeff = barrelCoeff / Math.Pow(size, 2);
                        targetBitmap = openCv.AddBarrelDistortion(targetBitmap, (float)scaledCoeff);
                    }
                }

                canvas.DrawBitmap(
                    targetBitmap,
                    SKRect.Create(
                        srcX,
                        srcY,
                        srcWidth,
                        srcHeight),
                    SKRect.Create(
                        rightPreviewX,
                        previewY,
                        sidePreviewWidthLessCrop,
                        previewHeightLessCrop),
                    paint);

                grayscale?.Dispose();
                transformed?.Dispose();
            }

            if (innerBorderThicknessProportion > 0)
            {
                using var borderPaint = new SKPaint
                {
                    Color = borderColor == BorderColor.Black ? SKColor.Parse("000000") : SKColor.Parse("ffffff"),
                    Style = SKPaintStyle.StrokeAndFill,
                    FilterQuality = quality
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

            if (drawFuseGuide)
            {
                var previewBorderThickness = canvasWidth / 2f - (leftPreviewX + sidePreviewWidthLessCrop);
                var fuseGuideY = previewY - 2 * previewBorderThickness - fuseGuideMarginHeight / 2f; //why 2x border? why doesn't this have to account for icon height? i don't know.
                using var whitePaint = new SKPaint
                {
                    Color = new SKColor(byte.MaxValue, byte.MaxValue, byte.MaxValue),
                    FilterQuality = quality
                };
                canvas.DrawRect(
                    canvasWidth / 2f - previewBorderThickness - sidePreviewWidthLessCrop / 2f - fuseGuideIconWidth / 2f,
                    fuseGuideY, fuseGuideIconWidth, fuseGuideIconWidth, whitePaint);
                canvas.DrawRect(
                    canvasWidth / 2f + previewBorderThickness + sidePreviewWidthLessCrop / 2f + fuseGuideIconWidth / 2f,
                    fuseGuideY, fuseGuideIconWidth, fuseGuideIconWidth, whitePaint);
            }
        }

        private static SKBitmap FilterToGrayscale(SKBitmap originalBitmap, SKFilterQuality quality)
        {
            var grayed = new SKBitmap(originalBitmap.Width, originalBitmap.Height);

            using var graybrush = new SKPaint
            {
                FilterQuality = quality
            };
            graybrush.ColorFilter =
                SKColorFilter.CreateColorMatrix(new[]
                {
                    0.21f, 0.72f, 0.07f, 0.0f, 0.0f,
                    0.21f, 0.72f, 0.07f, 0.0f, 0.0f,
                    0.21f, 0.72f, 0.07f, 0.0f, 0.0f,
                    0.0f,  0.0f,  0.0f,  1.0f, 0.0f
                });
            using var tempCanvas = new SKCanvas(grayed);
            tempCanvas.DrawBitmap(
                originalBitmap,
                0,
                0,
                graybrush);

            return grayed;
        }

        private static SKBitmap ZoomAndRotate(SKBitmap originalBitmap, double zoom, bool isRotated, float rotation, bool isKeystoned, float keystone, SKFilterQuality quality)
        {
            var rotatedAndZoomed = new SKBitmap(originalBitmap.Width, originalBitmap.Height);

            using var skPaint = new SKPaint
            {
                FilterQuality = quality
            };
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
                    ), skPaint); // blows up the bitmap, which is cut off later
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
                using var tempCanvas = new SKCanvas(keystoned);
                tempCanvas.SetMatrix(TaperTransform.Make(new SKSize(originalBitmap.Width, originalBitmap.Height),
                    keystone > 0 ? TaperSide.Left : TaperSide.Right, TaperCorner.Both, 1 - Math.Abs(keystone)));
                tempCanvas.DrawBitmap(rotatedAndZoomed, 0, 0, skPaint);
                rotatedAndZoomed.Dispose();
            }

            return keystoned ?? rotatedAndZoomed;
        }

        public static int CalculateJoinedCanvasWidthWithEditsNoBorder(SKBitmap leftBitmap, SKBitmap rightBitmap,
            Edits edits)
        {
            return CalculateJoinedCanvasWidthWithEditsNoBorderInternal(leftBitmap, rightBitmap,
                edits.LeftCrop + edits.OutsideCrop, edits.InsideCrop + edits.RightCrop,
                edits.InsideCrop + edits.LeftCrop,
                edits.RightCrop + edits.OutsideCrop);
        }

        private static int CalculateJoinedCanvasWidthWithEditsNoBorderInternal(SKBitmap leftBitmap, SKBitmap rightBitmap, 
            double leftLeftCrop, double leftRightCrop, double rightLeftCrop, double rightRightCrop)
        {
            if (leftBitmap == null || rightBitmap == null)
            {
                return leftBitmap?.Width * 2 ?? rightBitmap?.Width * 2 ?? 0;
            }

            var baseWidth = Math.Min(leftBitmap.Width, rightBitmap.Width);
            return (int) (2 * baseWidth -
                          baseWidth * (leftLeftCrop + leftRightCrop + rightLeftCrop + rightRightCrop));
            }

        public static int CalculateCanvasHeightWithEditsNoBorder(SKBitmap leftBitmap, SKBitmap rightBitmap, Edits edits)
        {
            return CalculateCanvasHeightWithEditsNoBorderInternal(leftBitmap, rightBitmap,
                edits.TopCrop, edits.BottomCrop,
                edits.VerticalAlignment);
        }

        private static int CalculateCanvasHeightWithEditsNoBorderInternal(SKBitmap leftBitmap, SKBitmap rightBitmap,
            double topCrop, double bottomCrop, double alignment)
        {
            if (leftBitmap == null || rightBitmap == null)
            {
                return leftBitmap?.Height ?? rightBitmap?.Height ?? 0;
            }

            var baseHeight = Math.Min(leftBitmap.Height, rightBitmap.Height);
            return (int) (baseHeight - baseHeight * (topCrop + bottomCrop + Math.Abs(alignment)));
        }

        public static float CalculateFuseGuideMarginHeight(float baseHeight)
        {
            return CalculateFuseGuideWidth(baseHeight) * FUSE_GUIDE_MARGIN_HEIGHT_RATIO;
        }

        public static float CalculateFuseGuideWidth(float baseHeight)
        {
            return (float)(baseHeight * FUSE_GUIDE_WIDTH_RATIO);
        }
    }
}